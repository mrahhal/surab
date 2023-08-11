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

    public override void VisitFunctionDeclaration(LoweredFnDeclaration node)
    {
        CompileFunction(node);

        base.VisitFunctionDeclaration(node);
    }

    private void CompileFunction(LoweredFnDeclaration fn)
    {
        FunctionCompiler.Compile(_context, fn);
    }
}

internal sealed class FunctionCompiler : LoweredVisitor
{
    private readonly CompilationContext _context;
    private readonly LoweredFnDeclaration _fnDecl;
    private readonly LLVMTypeRef _fnType;
    private readonly LLVMValueRef _fnValue;

    private readonly Stack<LLVMUnit> _stack = new();
    private bool _returnSeen = false;

    private FunctionCompiler(
        CompilationContext context,
        LoweredFnDeclaration fnDecl,
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
        LoweredFnDeclaration fnDecl)
    {
        var (fnType, fnValue) = context.GetOrCreate(fnDecl.FnSymbol);
        Debug.Assert(fnValue.BasicBlocksCount == 0);

        var c = new FunctionCompiler(context, fnDecl, fnType, fnValue);
        c.Compile();
    }

    private void Compile()
    {
        // Entry has to exist before visiting the params in the signature.
        var entry = _fnValue.AppendBasicBlock("entry");
        LLVM.Builder.PositionAtEnd(entry);

        SetupParameters(_fnDecl.FnSymbol.Parameters);
        SetupLocals();

        if (_fnDecl.FnSymbol.Identifier?.Value == "print")
        {
            CompileSpecialPrint();
        }
        else
        {
            Visit(_fnDecl.Body);

            //if (_functionType.ReturnType.Kind != LLVMTypeKind.LLVMVoidTypeKind)
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
        //LLVM.PassManager.RunFunctionPassManager(_functionValue);
    }

    private void SetupLocals()
    {
        foreach (var variableStatement in LoweringUtils.CollectLocalVariableStatements(_fnDecl.Body))
        {
            _context.Create((LocalSymbol)variableStatement.Symbol);
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

    private void SetupParameters(IEnumerable<ParameterSymbol> parameters)
    {
        var i = (uint)0;
        foreach (var p in parameters)
        {
            var paramValueRef = _fnValue.GetParam(i);
            _context.Create(p, paramValueRef);

            i++;
        }
    }

    public override void VisitExpressionStatement(LoweredExpressionStatement node)
    {
        base.VisitExpressionStatement(node);

        // Discard the value? For non void functions..? Or always push a value in an expression to discard here?
        _stack.Pop();
    }

    public override void VisitVariableStatement(LoweredVariableStatement node)
    {
        // Nothing todo here since these variable statements are collected and processed at the start of a function
        // to setup alloca instructions in the entry block.
    }

    public override void VisitReturnStatement(LoweredReturnStatement node)
    {
        _returnSeen = true;
        if (node.Expression == null)
        {
            LLVM.Builder.BuildRetVoid();
        }
        else
        {
            Visit(node.Expression);
            var (_, value) = _stack.Pop();
            LLVM.Builder.BuildRet(value);
        }
    }

    public override void VisitCallExpression(LoweredCallExpression node)
    {
        var (fnType, fnValue) = _context.GetOrCreate(node.Callee);

        // TODO: node.Callee is not always an IdentifierExpressionNode.
        // After desugaring this will always be the identifier (or what's equivalent)?

        var fnSymbol = node.Callee;

        VisitList(node.Arguments);

        var args = new List<LLVMValueRef>(capacity: fnSymbol.Parameters.Length);

        for (var i = 0; i < fnSymbol.Parameters.Length; i++)
        {
            var (_, argValue) = _stack.Pop();
            args.Add(argValue);
        }

        var returnValue = LLVM.Builder.BuildCall2(fnType, fnValue, args.ToArray());
        // Always push a value, even if void, since this gets popped in VisitExpressionStatement.
        _stack.Push((fnType.ReturnType, returnValue));
    }

    public override void VisitBinaryExpression(LoweredBinaryExpression node)
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

    public override void VisitVariableLoadExpression(LoweredVariableLoadExpression node)
    {
        var (type, value) = _context.GetOrCreate(node.Variable);
        value = LLVM.Builder.BuildLoad2(type, value);
        _stack.Push((type, value));
    }

    public override void VisitVariableStoreExpression(LoweredVariableStoreStatement node)
    {
        var (_, value) = _context.GetOrCreate(node.Variable);
        Visit(node.Expression);
        var (_, rhs) = _stack.Pop();

        LLVM.Builder.BuildStore(rhs, value);
    }

    public override void VisitLiteralExpression(LoweredLiteralExpression node)
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
