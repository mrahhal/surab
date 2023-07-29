using System.Diagnostics;
using LLVMSharp.Interop;
using Surab.Analysis;
using Surab.Compilation.LLVM;

namespace Surab.Compilation;

public sealed class Compiler
{
    private readonly CompilationContext _context;

    private Compiler(SemanticModel model)
    {
        var llvm = LLVMCompilationContext.Create();

        _context = new CompilationContext(model, llvm);
    }

    public static string Compile(SemanticModel model)
    {
        var compiler = new Compiler(model);
        return compiler.Compile();
    }

    private string Compile()
    {
        return RootCompiler.Compile(_context);
    }
}

internal sealed class RootCompiler : Visitor
{
    private readonly CompilationContext _context;

    public RootCompiler(CompilationContext context)
    {
        _context = context;
    }

    private SemanticModel Model => _context.Model;

    private LLVMCompilationContext LLVM => _context.LLVM;

    public static string Compile(CompilationContext context)
    {
        var rootCompiler = new RootCompiler(context);
        rootCompiler.CompileModule();

        rootCompiler.LLVM.Module.PrintToFile(context.LLVM.LLFile);

        return ClangRunner.Run(new[] { context.LLVM.LLFile });
    }

    private void CompileModule()
    {
        Visit(Model.Tree.Root);

        LLVM.Module.Verify(LLVMVerifierFailureAction.LLVMPrintMessageAction);
    }

    public override void VisitFunctionDeclaration(FunctionDeclarationNode node)
    {
        CompileFunction(node.Signature, node.Body);

        base.VisitFunctionDeclaration(node);
    }

    private void CompileFunction(
        FunctionSignatureNode signature,
        BlockExpressionNode body)
    {
        FunctionCompiler.Compile(_context, signature, body);
    }
}

internal sealed class FunctionCompiler : Visitor
{
    private readonly CompilationContext _context;
    private readonly FunctionSignatureNode _signature;
    private readonly BlockExpressionNode _body;
    private readonly FunctionSymbol _functionSymbol;
    private readonly LLVMTypeRef _functionType;
    private readonly LLVMValueRef _functionValue;

    private readonly Stack<LLVMUnit> _stack = new();

    private FunctionCompiler(
        CompilationContext context,
        FunctionSignatureNode signature,
        BlockExpressionNode body,
        FunctionSymbol function,
        LLVMTypeRef functionType,
        LLVMValueRef functionValue)
    {
        _context = context;
        _signature = signature;
        _body = body;
        _functionSymbol = function;
        _functionType = functionType;
        _functionValue = functionValue;
    }

    private SemanticModel Model => _context.Model;

    private LLVMCompilationContext LLVM => _context.LLVM;

    public static void Compile(
        CompilationContext context,
        FunctionSignatureNode signature,
        BlockExpressionNode body)
    {
        context.Model.TryGetDeclaredSymbol(signature.Parent!, out var functionSymbol);
        var function = (FunctionSymbol)functionSymbol!;
        var (functionType, functionValue) = context.GetOrCreate(function);
        Debug.Assert(functionValue.BasicBlocksCount == 0);

        var c = new FunctionCompiler(context, signature, body, function, functionType, functionValue);
        c.Compile();
    }

    private void Compile()
    {
        // Entry has to exist before visiting the params in the signature.
        var entry = _functionValue.AppendBasicBlock("entry");
        LLVM.Builder.PositionAtEnd(entry);

        Visit(_signature);

        SetupLocals();

        if (_signature.Identifier.Identifier == "print")
        {
            CompileSpecialPrint();
        }
        else
        {
            Visit(_body);

            if (_functionType.ReturnType.Kind != LLVMTypeKind.LLVMVoidTypeKind)
            {
                var (_, value) = _stack.Pop();
                LLVM.Builder.BuildRet(value);
            }
            else
            {
                LLVM.Builder.BuildRetVoid();
            }
        }

        _functionValue.VerifyFunction(LLVMVerifierFailureAction.LLVMPrintMessageAction);
        //LLVM.PassManager.RunFunctionPassManager(_functionValue);
    }

    private void SetupLocals()
    {
        foreach (var localSymbol in SemanticUtils.CollectLocals(Model, _body))
        {
            _context.Create(localSymbol);
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

        var value = _functionValue.GetParam(0);

        var strF = LLVM.Builder.BuildGlobalStringPtr("%f\n", "strF");
        LLVM.Builder.BuildCall2(printfType, printf, new[] { strF, value });
        LLVM.Builder.BuildRetVoid();
    }

    public override void VisitParametersList(ParametersListNode node)
    {
        var i = (uint)0;
        foreach (var p in node.Parameters)
        {
            var paramValueRef = _functionValue.GetParam(i);

            if (Model.TryGetDeclaredSymbol(p, out var symbol))
            {
                var parameterSymbol = (ParameterSymbol)symbol;
                _context.Create(parameterSymbol, paramValueRef);
            }
            else
            {
                throw new Exception();
            }

            i++;
        }
    }

    public override void VisitExpressionStatement(ExpressionStatementNode node)
    {
        Walk(node);

        // Discard the value? For non void functions..? Or always push a value in an expression to discard here?
        //_valueStack.Pop();
    }

    public override void VisitLetStatement(LetStatementNode node)
    {
        if (Model.TryGetDeclaredSymbol(node, out var symbol))
        {
            var localSymbol = (LocalSymbol)symbol;
            var (_, value) = _context.GetOrCreate(localSymbol);

            Visit(node.Initializer);
            var (_, initializerValue) = _stack.Pop();
            LLVM.Builder.BuildStore(initializerValue, value);

            // Wrong, will set the size of the allocated stack.
            //value.Initializer = _valueStack.Pop();
        }
        else
        {
            throw new Exception();
        }
    }

    public override void VisitCallExpression(CallExpressionNode node)
    {
        Visit(node.Callee);
        var (functionType, functionValue) = _stack.Pop();

        // TODO: node.Callee is not always an IdentifierExpressionNode.
        // After desugaring this will always be the identifier (or what's equivalent)?
        var functionSymbol = (FunctionSymbol?)Model.LookupSymbol((IdentifierExpressionNode)node.Callee);
        Debug.Assert(functionSymbol != null);

        Visit(node.Arguments);

        var args = new List<LLVMValueRef>(capacity: functionSymbol.Parameters.Length);

        for (var i = 0; i < functionSymbol.Parameters.Length; i++)
        {
            var (_, argValue) = _stack.Pop();
            args.Add(argValue);
        }

        var returnValue = LLVM.Builder.BuildCall2(functionType, functionValue, args.ToArray());
        _stack.Push((functionType.ReturnType, returnValue));
    }

    public override void VisitBinaryExpression(BinaryExpressionNode node)
    {
        Visit(node.Left);
        var (_, leftValue) = _stack.Pop();

        Visit(node.Right);
        var (_, rightValue) = _stack.Pop();

        var value = node.Op.BinaryOperatorTokenKind switch
        {
            BinaryOperatorTokenKind.Add => LLVM.Builder.BuildFAdd(leftValue, rightValue),
            BinaryOperatorTokenKind.Sub => LLVM.Builder.BuildFSub(leftValue, rightValue),
            // TODO: Missing
            _ => throw new NotImplementedException(),
        };

        _stack.Push((LLVMTypeRef.Double, value));
    }

    //public override void VisitAssignmentExpression(AssignmentExpressionNode node)
    //{
    //    // TODO: Walk instead of Visit if you don't want VisitIdentifierExpression to be called for assignment?

    //    Visit(node.Lhs);
    //    var toStore = _valueStack.Pop();

    //    Visit(node.Rhs);
    //    var rhs = _valueStack.Pop();

    //    //_functionValue.EntryBasicBlock.
    //    //LLVM.Builder.InsertBlock.

    //    LLVM.Builder.BuildStore(toStore, rhs);
    //}

    public override void VisitIdentifierExpression(IdentifierExpressionNode node)
    {
        // TODO: We'll only be here if this is a definite load??

        // Later, this will be also called in places such as `let a = Number.max;` for `Number` type identifier.
        // Should a desugaring stage remove this possibility and change this to bind to the direct method/field/prop? Yes I think.

        var target = Model.LookupSymbol(node);
        Debug.Assert(target != null);
        var (type, value) = _context.GetOrCreate(target);

        if (value.TypeOf.Kind == LLVMTypeKind.LLVMPointerTypeKind && value.Kind != LLVMValueKind.LLVMFunctionValueKind)
        {
            // Load from an alloca if this isn't a function.
            value = LLVM.Builder.BuildLoad2(type, value);
        }

        _stack.Push((type, value));
    }

    public override void VisitLiteralExpression(LiteralExpressionNode node)
    {
        // Similar to CompilationContext.GetForType?
        switch (node.Literal.LiteralTokenKind)
        {
            case LiteralTokenKind.Number:
                Debug.Assert(node.Literal.TryGetNumberValue(out var v));
                var llvmType = LLVMTypeRef.Double;
                var llvmValue = LLVMValueRef.CreateConstReal(llvmType, v);
                _stack.Push((llvmType, llvmValue));
                break;

            default:
                // TODO: Add to SemanticAnalyzer.
                Debug.Assert(false, "Should not be here.");
                throw new NotImplementedException();
        }
    }
}
