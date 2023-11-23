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

internal sealed class RootCompiler(CompilationContext context) : LoweredVisitor
{
    private readonly CompilationContext _context = context;

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
        else if (_fnDecl.FnSymbol.Identifier?.Value == "printb")
        {
            CompileSpecialPrintB();
        }
        else
        {
            if (_fnDecl.Body != null)
            {
                Visit(_fnDecl.Body);
            }

            if (!_returnSeen)
            {
                LLVM.Builder.BuildRetVoid();
            }
        }

        _llvmFn.Value.VerifyFunction(LLVMVerifierFailureAction.LLVMPrintMessageAction);
        //LLVM.PassManager.RunFunctionPassManager(_llvmFn.Value);
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

            var lowering = GetLLVMLowering(param.Type);
            switch (lowering.Kind)
            {
                case LLVMTypeLoweringKind.ByValue:
                    {
                        var alignment = Target.GetAbiAlignment(param.Type);
                        CreateParam(param, paramValue, alignment);
                    }
                    break;
                case LLVMTypeLoweringKind.ByMutRef:
                    {
                        // paramValue is already a ptr. Treat it as the wrapper ptr directly.
                        var alignment = Target.GetAbiAlignment(param.Type);
                        CreateParam(param, paramValue, alignment, createWrapperPtr: false);
                    }
                    break;
                case LLVMTypeLoweringKind.AbiInt:
                    {
                        var layout = Target.GetLayout(param.Type);
                        var llvmIntType = LLVMTypeRef.CreateInt((uint)layout.Size.Bits);
                        var alignment = Math.Max(layout.Alignment.Bytes, (int)LLVM.TargetData.ABIAlignmentOfType(llvmIntType)).AsBytes();
                        CreateParam(param, paramValue, alignment);
                    }
                    break;
            }

            llvmParamOffset++;
        }
    }

    private void SetupLocals()
    {
        if (_fnDecl.Body == null) return;

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
        LLVM.Builder.BuildCall2(printfType, printf, [strF, value]);
        LLVM.Builder.BuildRetVoid();
    }

    // Only prints a bool for now.
    private void CompileSpecialPrintB()
    {
        var printfType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32, new[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }, true);
        var printf = LLVM.Module.GetNamedFunction("printf");
        if (printf == default)
        {
            printf = LLVM.Module.AddFunction("printf", printfType);
        }

        var value = _llvmFn.Value.GetParam(0);

        var strF = LLVM.Builder.BuildGlobalStringPtr("%d\n", "strF");
        LLVM.Builder.BuildCall2(printfType, printf, [strF, value]);
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
            var (_, value) = _stack.Pop().Unwrap(LLVM);
            LLVM.Builder.BuildRet(value);
        }
    }

    public override void VisitCallExpr(LoweredCallExpr node)
    {
        var (fnType, fnValue, sret) = GetOrCreateFn(node.Callee);

        // TODO: node.Callee is not always an IdentifierExprNode.
        // After desugaring this will always be the identifier (or what's equivalent)?

        // Process in reverse args order because values are pushed to a LIFO stack.
        VisitList(node.Args.Reverse());

        var fnSymbol = node.Callee;

        var argOffset = 0;
        var llvmIndex = 0;
        var llvmArgCount = (int)fnType.ParamTypesCount;
        var args = new List<LLVMValueRef>(llvmArgCount);
        var attrs = _context.FnToAttrs[fnSymbol];

        if (sret.HasValue)
        {
            var size = Target.GetAbiSize(fnSymbol.ReturnType);

            var sretAlloc = LLVM.Builder.BuildAlloca(sret.Value);
            sretAlloc.SetAlignment((uint)size.Bytes);
            args.Add(sretAlloc);

            llvmIndex++;
        }

        while (llvmIndex < llvmArgCount)
        {
            var unit = _stack.Pop();

            var param = fnSymbol.Params[argOffset];
            var lowering = GetLLVMLowering(param.Type);
            switch (lowering.Kind)
            {
                case LLVMTypeLoweringKind.ByValue:
                    {
                        var (_, argValue) = unit.Unwrap(LLVM);
                        args.Add(argValue);
                    }
                    break;
                case LLVMTypeLoweringKind.ByMutRef:
                    {
                        var llvmType = Target.LowerType(param.Type);
                        var (_, argValuePtr) = unit;
                        // TODO: This might be wrong. This is giving the same ptr to the fn (should be in ByRef), we might want to give a copy here in ByMutRef?
                        args.Add(argValuePtr);
                    }
                    break;
                case LLVMTypeLoweringKind.AbiInt:
                    {
                        var (_, argValuePtr) = unit;
                        var layout = Target.GetLayout(param.Type);
                        var llvmIntType = LLVMTypeRef.CreateInt((uint)layout.Size.Bits);
                        var loaded = LLVM.Builder.BuildLoad2(llvmIntType, argValuePtr);
                        loaded.SetAlignment((uint)layout.Alignment.Bytes);
                        args.Add(loaded);
                    }
                    break;
            }

            argOffset++;
            llvmIndex++;
        }

        var returnValue = LLVM.Builder.BuildCall2(fnType, fnValue, [.. args]);
        attrs.Apply((index, attr) => fnValue.AddCallSiteAttributeAtIndex(index, attr));

        // Always push a value, even if void, since this gets popped in VisitExprStmt.
        _stack.Push(LLVMUnit.Unwrapped(fnType.ReturnType, returnValue));
    }

    public override void VisitBinaryExpr(LoweredBinaryExpr node)
    {
        Visit(node.Left);
        var (_, leftValue) = _stack.Pop().Unwrap(LLVM);

        Visit(node.Right);
        var (_, rightValue) = _stack.Pop().Unwrap(LLVM);

        var value = node.Op switch
        {
            BinaryOperatorTokenKind.Add => LLVM.Builder.BuildFAdd(leftValue, rightValue),
            BinaryOperatorTokenKind.Sub => LLVM.Builder.BuildFSub(leftValue, rightValue),
            // TODO: Missing
            _ => throw new NotImplementedException(),
        };

        _stack.Push(LLVMUnit.Unwrapped(LLVMTypeRef.Double, value));
    }

    public override void VisitVariableLoadExpr(LoweredVariableLoadExpr node)
    {
        // Units tied to a variable are always wrapped.
        var unit = Get(node.Variable);
        // We don't load it, but give that option to the consumer of this unit.
        // Some consumers will want to simply load it and deal with the value, but others will need access to the ptr.
        _stack.Push(unit);
    }

    public override void VisitVariableStoreExpr(LoweredVariableStoreStmt node)
    {
        var (_, value) = Get(node.Variable);
        Visit(node.Expr);
        var (_, rhs) = _stack.Pop().Unwrap(LLVM);

        LLVM.Builder.BuildStore(rhs, value);
    }

    public override void VisitFieldLoadExpr(LoweredFieldLoadExpr node)
    {
        Visit(node.Target);
        var (targetType, targetPtr) = _stack.Pop();

        var structLayout = Target.GetStructLayout((StructTypeSymbol)node.Field.Parent!);
        var fieldSection = structLayout.Sections.First(s => s.FieldIndex == node.Field.FieldIndex);

        var ptrToField = LLVM.Builder.BuildStructGEP2(targetType, targetPtr, (uint)fieldSection.SectionIndex);
        var fieldLLVMType = Target.LowerType(node.Field.Type);
        // We don't load it, for the same reason outlined in VisitVariableLoadExpr.
        _stack.Push(LLVMUnit.Wrapped(fieldLLVMType, ptrToField));
    }

    public override void VisitFieldStoreExpr(LoweredFieldStoreStmt node)
    {
        Visit(node.Target);
        var (targetType, targetPtr) = _stack.Pop();

        Visit(node.Expr);
        var (_, rhs) = _stack.Pop().Unwrap(LLVM);

        // TODO: Optimize somehow to remove the need to compute the struct layout again here.
        var structLayout = Target.GetStructLayout((StructTypeSymbol)node.Field.Parent!);
        var fieldSection = structLayout.Sections.First(s => s.FieldIndex == node.Field.FieldIndex);
        var fieldLLVMType = Target.LowerType(node.Field.Type);

        var ptrToField = LLVM.Builder.BuildStructGEP2(targetType, targetPtr, (uint)fieldSection.SectionIndex);
        LLVM.Builder.BuildStore(rhs, ptrToField);
    }

    public override void VisitStructCreationExpr(LoweredStructCreationExpr node)
    {
        var llvmType = _context.GetOrCreateType(node.Symbol);

        // TODO: Optimize somehow to remove the need to compute the struct layout again here.
        var structLayout = Target.GetStructLayout(node.Symbol);

        var ptr = LLVM.Builder.BuildAlloca(llvmType.Type);

        var fieldsSet = new HashSet<FieldSymbol>();
        foreach (var item in node.MapInitializer.Items)
        {
            // Assumed to be only an identifier to a field for now.
            var field = (FieldSymbol)node.Symbol.GetMember(item.Left)!;
            var fieldSection = structLayout.Sections.First(s => s.FieldIndex == field.FieldIndex);

            Visit(item.Right);
            var (_, rhs) = _stack.Pop().Unwrap(LLVM);

            // Duplicated a bit from VisitFieldStoreExpr?
            var ptrToField = LLVM.Builder.BuildStructGEP2(llvmType.Type, ptr, (uint)fieldSection.SectionIndex);
            LLVM.Builder.BuildStore(rhs, ptrToField);

            fieldsSet.Add(field);
        }

        // TODO: Set fields with default values.

        _stack.Push(LLVMUnit.Wrapped(llvmType.Type, ptr));
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
                    _stack.Push(LLVMUnit.Unwrapped(llvmType, llvmValue));
                }
                break;

            case LiteralTokenKind.Bool:
                {
                    Debug.Assert(node.Literal.TryGetBoolValue(out var v));
                    var llvmType = LLVMTypeRef.Int1;
                    var llvmValue = LLVMValueRef.CreateConstInt(llvmType, v ? (ulong)1 : 0);
                    _stack.Push(LLVMUnit.Unwrapped(llvmType, llvmValue));
                }
                break;

            default:
                // TODO: Add to SemanticAnalyzer.
                throw new NotImplementedException("Should not be here.");
        }
    }

    private LLVMFn GetOrCreateFn(FnSymbol symbol)
    {
        if (_context.FnSymbolToLLVMFn.TryGetValue(symbol, out var llvmFn))
        {
            return llvmFn;
        }
        llvmFn = CreateFn(symbol);
        _context.FnSymbolToLLVMFn[symbol] = llvmFn;
        return llvmFn;
    }

    private LLVMUnit Get(Symbol symbol)
    {
        if (_context.SymbolToLLVMUnit.TryGetValue(symbol, out var unit))
        {
            return unit;
        }

        throw new Exception("Unexpected?");
    }

    private LLVMUnit CreateParam(ParamSymbol paramSymbol, LLVMValueRef paramValue, BitSize alignment, bool createWrapperPtr = true)
    {
        var llvmType = Target.LowerType(paramSymbol.Type);

        LLVMUnit unit;
        if (createWrapperPtr)
        {
            var ptr = LLVM.Builder.BuildAlloca(llvmType, paramSymbol.Identifier?.Value ?? string.Empty);
            ptr.SetAlignment((uint)alignment.Bytes);
            LLVM.Builder.BuildStore(paramValue, ptr);
            unit = LLVMUnit.Wrapped(llvmType, ptr);
        }
        else
        {
            Debug.Assert(paramValue.TypeOf.Kind == LLVMTypeKind.LLVMPointerTypeKind, "Param value should already be a ptr.");
            unit = LLVMUnit.Wrapped(llvmType, paramValue);
        }

        _context.SymbolToLLVMUnit[paramSymbol] = unit;
        return unit;
    }

    private LLVMUnit CreateLocal(LocalSymbol localSymbol)
    {
        var typeRef = Target.LowerType(localSymbol.Type);

        var ptr = LLVM.Builder.BuildAlloca(typeRef, localSymbol.Identifier?.Value ?? string.Empty);
        var unit = LLVMUnit.Wrapped(typeRef, ptr);

        _context.SymbolToLLVMUnit[localSymbol] = unit;
        return unit;
    }

    private LLVMFn CreateFn(FnSymbol fnSymbol)
    {
        var returnType = Target.LowerType(fnSymbol.ReturnType);

        // TODO: Better way?
        var llvmIndex = 0;
        var capacity = fnSymbol.Params.Length + 1; // Max length of llvm params.
        var @params = new List<LLVMTypeRef>(capacity);
        var attrs = new LLVMFnAttrsDef();
        _context.FnToAttrs[fnSymbol] = attrs;

        var sret = Target.IsReturnSRet(fnSymbol);
        if (sret)
        {
            @params.Add(returnType);
            attrs.AddParam(llvmIndex, LLVM.Context.CreateEnumAttribute(AttributeKind.StructRet, default));
            llvmIndex++;
        }

        foreach (var param in fnSymbol.Params)
        {
            // TODO: if (isExtern) ?

            var lowering = GetLLVMLowering(param.Type);
            var llvmType = Target.LowerType(param.Type);
            switch (lowering.Kind)
            {
                case LLVMTypeLoweringKind.ByValue:
                    {
                        @params.Add(llvmType);
                    }
                    break;
                case LLVMTypeLoweringKind.ByMutRef:
                    {
                        @params.Add(LLVMTypeRef.CreatePointer(llvmType, 0));
                    }
                    break;
                case LLVMTypeLoweringKind.AbiInt:
                    {
                        var layout = Target.GetLayout(param.Type);
                        var llvmIntType = LLVMTypeRef.CreateInt((uint)layout.Size.Bits);
                        @params.Add(llvmIntType);
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

        return new LLVMFn(LLVMUnit.Wrapped(fnType, fnValue), sret ? returnType : (LLVMTypeRef?)null);
    }

    public LLVMTypeLowering GetLLVMLowering(TypeSymbol type)
    {
        return Target.GetLLVMLowering(type);
    }
}
