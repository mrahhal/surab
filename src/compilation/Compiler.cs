using System.Diagnostics;
using LLVMSharp.Interop;
using Surab.Analysis;
using Surab.Compilation.LLVM;

namespace Surab.Compilation;

public sealed class Compiler
{
    private readonly CompilationContext _context;

    private Compiler(
        SemanticModel model,
        LoweredTree loweredTree,
        CompilationOptions options)
    {
        var llvm = LLVMCompilationContext.Create();

        _context = new CompilationContext(loweredTree, model, llvm, options);
    }

    public static string Compile(
        SemanticModel model,
        CompilationOptions? options = null)
    {
        var loweredTree = Lowerer.Lower(model);
        var compiler = new Compiler(model, loweredTree, options ?? CompilationOptions.CreateDefault());
        return compiler.Compile();
    }

    private string Compile()
    {
        return RootCompiler.Compile(_context);
    }
}

internal sealed class RootCompiler : LoweredVisitor
{
    private readonly CompilationContext _context;

    public RootCompiler(CompilationContext context)
    {
        _context = context;
    }

    private LLVMCompilationContext LLVM => _context.LLVM;

    public static string Compile(CompilationContext context)
    {
        var rootCompiler = new RootCompiler(context);
        rootCompiler.CompileModule();

        rootCompiler.LLVM.Module.PrintToFile(context.Options.LLFileName);

        return ClangRunner.Run(new[] { context.Options.LLFileName });
    }

    private void CompileModule()
    {
        Visit(_context.LoweredTree.Root);

        LLVM.Module.Verify(LLVMVerifierFailureAction.LLVMPrintMessageAction);
    }

    public override void VisitFnDecl(LoweredFnDecl node)
    {
        CompileFn(node);

        base.VisitFnDecl(node);
    }

    private void CompileFn(LoweredFnDecl fn)
    {
        FnCompiler.Compile(_context, fn);
    }
}

internal sealed class FnCompiler : LoweredVisitor
{
    private readonly CompilationContext _context;
    private readonly LoweredFnDecl _fnDecl;
    private readonly LLVMTypeRef _fnType;
    private readonly LLVMValueRef _fnValue;

    private readonly Stack<LLVMUnit> _stack = new();
    private bool _returnSeen = false;

    private FnCompiler(
        CompilationContext context,
        LoweredFnDecl fnDecl,
        LLVMTypeRef fnType,
        LLVMValueRef fnValue)
    {
        _context = context;
        _fnDecl = fnDecl;
        _fnType = fnType;
        _fnValue = fnValue;
    }

    private LLVMCompilationContext LLVM => _context.LLVM;

    public static void Compile(
        CompilationContext context,
        LoweredFnDecl fnDecl)
    {
        var (fnType, fnValue) = context.GetOrCreate(fnDecl.FnSymbol);
        Debug.Assert(fnValue.BasicBlocksCount == 0);

        var c = new FnCompiler(context, fnDecl, fnType, fnValue);
        c.Compile();
    }

    private void Compile()
    {
        // Entry has to exist before visiting the params in the signature.
        var entry = _fnValue.AppendBasicBlock("entry");
        LLVM.Builder.PositionAtEnd(entry);

        SetupParams(_fnDecl.FnSymbol.Params);
        SetupLocals();

        if (_fnDecl.FnSymbol.Identifier?.Value == "print")
        {
            CompileSpecialPrint();
        }
        else
        {
            Visit(_fnDecl.Body);

            //if (_fnType.ReturnType.Kind != LLVMTypeKind.LLVMVoidTypeKind)
            //{
            //    var (_, value) = _stack.Pop();
            //    LLVM.Builder.BuildRet(value);
            //}
            //else
            //{
            //    LLVM.Builder.BuildRetVoid();
            //}
            if (!_returnSeen)
            {
                LLVM.Builder.BuildRetVoid();
            }
        }

        _fnValue.VerifyFunction(LLVMVerifierFailureAction.LLVMPrintMessageAction);
        //LLVM.PassManager.RunFunctionPassManager(_fnValue);
    }

    private void SetupLocals()
    {
        foreach (var variableStmt in LoweringUtils.CollectLocalVariableStmts(_fnDecl.Body))
        {
            _context.Create((LocalSymbol)variableStmt.Symbol);
        }
    }

    // Only prints a number for now.
    private void CompileSpecialPrint()
    {
        var printfType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32, new[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }, true);
        var printf = LLVM.Module.GetNamedFunction("printf");
        if (printf == default)
        {
            printf = LLVM.Module.AddFunction("printf", printfType);
        }

        var value = _fnValue.GetParam(0);

        var strF = LLVM.Builder.BuildGlobalStringPtr("%g\n", "strF");
        LLVM.Builder.BuildCall2(printfType, printf, new[] { strF, value });
        LLVM.Builder.BuildRetVoid();
    }

    private void SetupParams(IEnumerable<ParamSymbol> @params)
    {
        var i = (uint)0;
        foreach (var p in @params)
        {
            var paramValueRef = _fnValue.GetParam(i);
            _context.Create(p, paramValueRef);

            i++;
        }
    }

    public override void VisitExprStmt(LoweredExprStmt node)
    {
        base.VisitExprStmt(node);

        // Discard the value? For non void functions..? Or always push a value in an expr to discard here?
        _stack.Pop();
    }

    public override void VisitVariableStmt(LoweredVariableStmt node)
    {
        // Nothing todo here since these variable stmts are collected and processed at the start of a function
        // to setup alloca instructions in the entry block.
    }

    public override void VisitReturnStmt(LoweredReturnStmt node)
    {
        _returnSeen = true;
        if (node.Expr == null)
        {
            LLVM.Builder.BuildRetVoid();
        }
        else
        {
            Visit(node.Expr);
            var (_, value) = _stack.Pop();
            LLVM.Builder.BuildRet(value);
        }
    }

    public override void VisitCallExpr(LoweredCallExpr node)
    {
        var (fnType, fnValue) = _context.GetOrCreate(node.Callee);

        // TODO: node.Callee is not always an IdentifierExprNode.
        // After desugaring this will always be the identifier (or what's equivalent)?

        var fnSymbol = node.Callee;

        VisitList(node.Args);

        var args = new List<LLVMValueRef>(capacity: fnSymbol.Params.Length);

        for (var i = 0; i < fnSymbol.Params.Length; i++)
        {
            var (_, argValue) = _stack.Pop();
            args.Add(argValue);
        }

        var returnValue = LLVM.Builder.BuildCall2(fnType, fnValue, args.ToArray());
        // Always push a value, even if void, since this gets popped in VisitExprStmt.
        _stack.Push((fnType.ReturnType, returnValue));
    }

    public override void VisitBinaryExpr(LoweredBinaryExpr node)
    {
        Visit(node.Left);
        var (_, leftValue) = _stack.Pop();

        Visit(node.Right);
        var (_, rightValue) = _stack.Pop();

        var value = node.Op switch
        {
            BinaryOperatorTokenKind.Add => LLVM.Builder.BuildFAdd(leftValue, rightValue),
            BinaryOperatorTokenKind.Sub => LLVM.Builder.BuildFSub(leftValue, rightValue),
            // TODO: Missing
            _ => throw new NotImplementedException(),
        };

        _stack.Push((LLVMTypeRef.Double, value));
    }

    public override void VisitVariableLoadExpr(LoweredVariableLoadExpr node)
    {
        var (type, value) = _context.GetOrCreate(node.Variable);
        value = LLVM.Builder.BuildLoad2(type, value);
        _stack.Push((type, value));
    }

    public override void VisitVariableStoreExpr(LoweredVariableStoreStmt node)
    {
        var (_, value) = _context.GetOrCreate(node.Variable);
        Visit(node.Expr);
        var (_, rhs) = _stack.Pop();

        LLVM.Builder.BuildStore(rhs, value);
    }

    public override void VisitLiteralExpr(LoweredLiteralExpr node)
    {
        // Similar to CompilationContext.GetForType?
        switch (node.Literal.LiteralTokenKind)
        {
            case LiteralTokenKind.Number:
                {
                    Debug.Assert(node.Literal.TryGetNumberValue(out var v));
                    var llvmType = LLVMTypeRef.Double;
                    var llvmValue = LLVMValueRef.CreateConstReal(llvmType, v);
                    _stack.Push((llvmType, llvmValue));
                }
                break;

            default:
                // TODO: Add to SemanticAnalyzer.
                throw new NotImplementedException("Should not be here.");
        }
    }
}
