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
        Target target,
        CompilationOptions options)
    {
        var llvm = LLVMCompilationContext.Create();

        _context = new CompilationContext(model, loweredTree, target, llvm, options);
    }

    public static string Compile(
        SemanticModel model,
        string targetTriple,
        CompilationOptions? options = null)
    {
        var loweredTree = Lowerer.Lower(model);
        var target = ParseTargetTriple(targetTriple);
        var compiler = new Compiler(model, loweredTree, target, options ?? CompilationOptions.CreateDefault());
        return compiler.Compile();
    }

    private string Compile()
    {
        return RootCompiler.Compile(_context);
    }

    private static Target ParseTargetTriple(string targetTriple)
    {
        // TODO
        return new x86_64WindowsMSVCTarget();
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
    private readonly LLVMFn _llvmFn;

    private readonly Stack<LLVMUnit> _stack = new();
    private bool _returnSeen = false;

    private FnCompiler(
        CompilationContext context,
        LoweredFnDecl fnDecl)
    {
        _context = context;
        _fnDecl = fnDecl;

        _llvmFn = GetOrCreateFn(fnDecl.FnSymbol);
        Debug.Assert(_llvmFn.Value.BasicBlocksCount == 0);
    }

    private Target Target => _context.Target;

    private LLVMCompilationContext LLVM => _context.LLVM;

    public static void Compile(
        CompilationContext context,
        LoweredFnDecl fnDecl)
    {
        var c = new FnCompiler(context, fnDecl);
        c.Compile();
    }

    private void Compile()
    {
        // Entry has to exist before visiting the params in the signature.
        var entry = _llvmFn.Value.AppendBasicBlock("entry");
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

        _llvmFn.Value.VerifyFunction(LLVMVerifierFailureAction.LLVMPrintMessageAction);
        //LLVM.PassManager.RunFunctionPassManager(_fnValue);
    }

    private void SetupParams(IEnumerable<ParamSymbol> @params)
    {
        var llvmParamOffset = 0;
        if (_llvmFn.SRet.HasValue)
        {
            // Skip sret.
            llvmParamOffset++;
        }

        foreach (var param in @params)
        {
            var paramValue = _llvmFn.Value.GetParam((uint)llvmParamOffset);
            CreateParam(param, paramValue);

            // TODO: From updateFunc.
            var lowering = GetLLVMLowering(param.Type);
            switch (lowering.Kind)
            {
                case LLVMTypeLoweringKind.byval:
                    {

                    }
                    break;
                case LLVMTypeLoweringKind.byref:
                    {

                    }
                    break;
                case LLVMTypeLoweringKind.byref_mut:
                    {

                    }
                    break;
                    //case LLVMTypeLoweringKind.abi_sized_int:
                    //    {
                    //    }
                    //    break;
            }

            llvmParamOffset++;
        }
    }

    private void SetupLocals()
    {
        foreach (var variableStmt in LoweringUtils.CollectLocalVariableStmts(_fnDecl.Body))
        {
            CreateLocal(variableStmt.Symbol);
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

        var value = _llvmFn.Value.GetParam(0);

        var strF = LLVM.Builder.BuildGlobalStringPtr("%g\n", "strF");
        LLVM.Builder.BuildCall2(printfType, printf, new[] { strF, value });
        LLVM.Builder.BuildRetVoid();
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
        var (fnType, fnValue, sret) = GetOrCreateFn(node.Callee);

        // TODO: node.Callee is not always an IdentifierExprNode.
        // After desugaring this will always be the identifier (or what's equivalent)?

        VisitList(node.Args);

        var fnSymbol = node.Callee;

        var argOffset = 0;
        var llvmIndex = 0;
        var llvmArgCount = (int)fnType.ParamTypesCount;
        var args = new List<LLVMValueRef>(llvmArgCount);
        var attrs = new LLVMFnAttrsDef();

        if (sret.HasValue)
        {
            var size = Target.GetAbiSize(fnSymbol.ReturnType);

            var sretAlloc = LLVM.Builder.BuildAlloca(sret.Value);
            sretAlloc.SetAlignment((uint)size.Bytes);
            args.Add(sretAlloc);

            attrs.AddParam(llvmIndex, LLVM.Context.CreateEnumAttribute(AttributeKind.StructRet, default));
            llvmIndex++;
        }

        while (llvmIndex < llvmArgCount)
        {
            var (_, argValue) = _stack.Pop();

            // TODO: Remove, and do it in lowering.
            args.Add(argValue);
            //attrs[llvmIndex] = Array.Empty<LLVMAttributeRef>();

            var param = fnSymbol.Params[argOffset];
            var lowering = GetLLVMLowering(param.Type);
            switch (lowering.Kind)
            {
                case LLVMTypeLoweringKind.byval:
                    {

                    }
                    break;
                case LLVMTypeLoweringKind.byref:
                    {

                    }
                    break;
                case LLVMTypeLoweringKind.byref_mut:
                    {

                    }
                    break;
            }

            argOffset++;
            llvmIndex++;
        }

        var returnValue = LLVM.Builder.BuildCall2(fnType, fnValue, args.ToArray());
        attrs.Apply((index, attr) => fnValue.AddCallSiteAttributeAtIndex(index, attr));

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
        // Isn't this only working right now because all of our variables are always pass by value?
        // If they're pass by ref, isn't there a case where we want to just pass the ptr itself without loading it?

        var (type, value) = Get(node.Variable);
        value = LLVM.Builder.BuildLoad2(type, value);
        _stack.Push((type, value));
    }

    public override void VisitVariableStoreExpr(LoweredVariableStoreStmt node)
    {
        var (_, value) = Get(node.Variable);
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

    private LLVMFn GetOrCreateFn(FnSymbol symbol)
    {
        if (_context.SymbolToValue.TryGetValue(symbol, out var unit))
        {
            return (LLVMFn)unit;
        }
        var llvmFn = CreateFn(symbol);
        _context.SymbolToValue[symbol] = llvmFn;
        return llvmFn;
    }

    private LLVMUnit Get(Symbol symbol)
    {
        if (_context.SymbolToValue.TryGetValue(symbol, out var unit))
        {
            return unit;
        }

        throw new Exception("Unexpected?");
    }

    private LLVMUnit CreateParam(ParamSymbol paramSymbol, LLVMValueRef paramValue)
    {
        var typeRef = Target.LowerType(paramSymbol.Type, _context);

        var ptr = LLVM.Builder.BuildAlloca(typeRef, paramSymbol.Identifier?.Value ?? string.Empty);
        var unit = new LLVMUnit(typeRef, ptr);
        LLVM.Builder.BuildStore(paramValue, ptr);

        _context.SymbolToValue[paramSymbol] = unit;
        return unit;
    }

    private LLVMUnit CreateLocal(LocalSymbol localSymbol)
    {
        var typeRef = Target.LowerType(localSymbol.Type, _context);

        var ptr = LLVM.Builder.BuildAlloca(typeRef, localSymbol.Identifier?.Value ?? string.Empty);
        var unit = new LLVMUnit(typeRef, ptr);

        _context.SymbolToValue[localSymbol] = unit;
        return unit;
    }

    private LLVMFn CreateFn(FnSymbol fnSymbol)
    {
        var returnType = Target.LowerType(fnSymbol.ReturnType, _context);

        // TODO: Better way?
        var llvmIndex = 0;
        var capacity = fnSymbol.Params.Length + 1; // Max length of llvm params.
        var @params = new List<LLVMTypeRef>(capacity);
        var attrs = new LLVMFnAttrsDef();

        var sret = Target.IsReturnSRet(fnSymbol, _context);
        if (sret)
        {
            @params.Add(returnType);
            attrs.AddParam(llvmIndex, LLVM.Context.CreateEnumAttribute(AttributeKind.StructRet, default));
            llvmIndex++;
        }

        foreach (var param in fnSymbol.Params)
        {
            // TODO: Remove, add to lowering.
            @params.Add(Target.LowerType(param.Type, _context));

            // TODO: if (isExtern) ?

            // This is for attributes.
            var lowering = GetLLVMLowering(param.Type);
            switch (lowering.Kind)
            {
                case LLVMTypeLoweringKind.byval:
                    {
                        if (!IsByRef(param.Type))
                        {

                        }
                    }
                    break;
                case LLVMTypeLoweringKind.byref:
                    {

                    }
                    break;
                case LLVMTypeLoweringKind.byref_mut:
                    {

                    }
                    break;
            }

            llvmIndex++;
        }

        var fnType = LLVMTypeRef.CreateFunction(sret ? LLVMTypeRef.Void : returnType, @params.ToArray(), false);
        var fnValue = LLVM.Module.AddFunction(fnSymbol.Identifier!.Value, fnType);

        // TODO: switch (cc)
        fnValue.SetFunctionCallConv(CallConv.C);

        attrs.Apply((index, attr) => fnValue.AddAttributeAtIndex(index, attr));

        return new LLVMFn(fnType, fnValue, sret ? returnType : (LLVMTypeRef?)null);
    }

    public bool IsByRef(TypeSymbol type)
    {
        // Not sure if will be needed, but should return true for things like load a (since it'll be ptr so we know to load),
        // and false for when the variable is stuff like 1 + 2 (since it won't be a ptr so we know). ?

        // Or maybe we don't need it because of VisitVariableLoadExpr? But what about when there's a fn call with a param
        // that takes a ptr, in that case we wouldn't want to load the variable ptr (which is done in VisitVariableLoadExpr). ?

        // TODO: Classes, Pointers, etc

        return false;
    }

    public LLVMTypeLowering GetLLVMLowering(TypeSymbol type)
    {
        return Target.GetLLVMLowering(type);
    }
}
