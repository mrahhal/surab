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
        Target target,
        CompilationOptions options)
    {
        var llvm = LLVMCompilationContext.Create();
        // Need to compile the prelude too.
        var models = new List<SemanticModel> { Prelude.GetSemanticModel(), model };

        _context = new CompilationContext(models, target, llvm, options);
    }

    public static string Compile(
        SemanticModel model,
        string targetTriple,
        CompilationOptions? options = null)
    {
        var target = ParseTargetTriple(targetTriple);
        var compiler = new Compiler(model, target, options ?? CompilationOptions.CreateDefault());
        return compiler.Compile();
    }

    private string Compile()
    {
        foreach (var model in _context.SemanticModels)
        {
            ModuleCompiler.Compile(_context, model);
        }

        _context.LLVM.Module.PrintToFile(_context.Options.LLFileName);
        return ClangRunner.Run(new[] { _context.Options.LLFileName });
    }

    private static Target ParseTargetTriple(string targetTriple)
    {
        // TODO
        return new x86_64WindowsMSVCTarget();
    }
}

internal sealed class ModuleCompiler(CompilationContext context, LoweredTree loweredTree) : LoweredVisitor
{
    private readonly CompilationContext _context = context;
    private readonly LoweredTree _loweredTree = loweredTree;

    private LLVMCompilationContext LLVM => _context.LLVM;

    public static void Compile(CompilationContext context, SemanticModel semanticModel)
    {
        var loweredTree = Lowerer.Lower(semanticModel);
        var rootCompiler = new ModuleCompiler(context, loweredTree);
        rootCompiler.CompileModule();
    }

    private void CompileModule()
    {
        Visit(_loweredTree.Root);

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
    private readonly LLVMUnit _llvmUnit;
    private readonly LLVMFn _llvmFn;

    private readonly Stack<LLVMUnit> _stack = new();
    private bool _returnSeen = false;

    private FnCompiler(
        CompilationContext context,
        LoweredFnDecl fnDecl)
    {
        _context = context;
        _fnDecl = fnDecl;

        _llvmUnit = GetOrCreateFn(fnDecl.FnSymbol);
        _llvmFn = LLVMTarget.GetFnInfoFromFnType(fnDecl.FnSymbol.Type);
        Debug.Assert(_llvmUnit.Value.BasicBlocksCount == 0);
    }

    private Target Target => _context.Target;

    private LLVMTarget LLVMTarget => _context.LLVMTarget;

    private LLVMCompilationContext LLVM => _context.LLVM;

    private FnSymbol FnSymbol => _fnDecl.FnSymbol;

    public static void Compile(
        CompilationContext context,
        LoweredFnDecl fnDecl)
    {
        var c = new FnCompiler(context, fnDecl);
        c.Compile();
    }

    private void Compile()
    {
        // TODO: Is extern fns always without body or will the same keyword be used to export our fns to C?
        if (FnSymbol.IsExtern)
        {
            return;
        }

        // Entry has to exist before visiting the params in the signature.
        var entry = _llvmUnit.Value.AppendBasicBlock("entry");
        LLVM.Builder.PositionAtEnd(entry);

        SetupParams(_fnDecl.FnSymbol.Params);
        SetupLocals();

        // This is where we could check for intrinsic fns to generate bodies for if we need to.
        // ---

        if (_fnDecl.Body != null)
        {
            Visit(_fnDecl.Body);
        }

        if (!_returnSeen)
        {
            if (FnSymbol.IsMain && FnSymbol.ReturnType == BuiltinTypeSystem.None)
            {
                LLVM.Builder.BuildRet(LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0));
            }
            else
            {
                LLVM.Builder.BuildRetVoid();
            }
        }

        if (!_llvmUnit.Value.VerifyFunction(LLVMVerifierFailureAction.LLVMPrintMessageAction))
        {
            throw new Exception("LLVM function verification failed.");
        }
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
            var paramValue = _llvmUnit.Value.GetParam((uint)llvmParamOffset);

            // Adaptations of params inside fn body
            // ---

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
        var fnType = node.FnType;

        var fnUnit = GetOrCreateFn(node.Callee);
        // Unwrapping loads the actual fn ptr for indirect calls (direct calls won't be wrapped in the first place).
        var (llvmFnPtrType, llvmFnValue) = fnUnit.Unwrap(LLVM);
        var (_, llvmFnType) = LLVMTarget.LowerType(fnType);
        var (sret, attrs) = LLVMTarget.GetFnInfoFromFnType(fnType);

        // Process in reverse args order because values are pushed to a LIFO stack.
        VisitList(node.Args.Reverse());

        var argOffset = 0;
        var llvmIndex = 0;
        var llvmArgCount = (int)llvmFnType.ParamTypesCount;
        var args = new List<LLVMValueRef>(llvmArgCount);

        if (sret.HasValue)
        {
            var size = Target.GetAbiSize(fnType.ReturnType);

            var sretAlloc = LLVM.Builder.BuildAlloca(sret.Value);
            sretAlloc.SetAlignment((uint)size.Bytes);
            args.Add(sretAlloc);

            llvmIndex++;
        }

        while (llvmIndex < llvmArgCount)
        {
            var unit = _stack.Pop();

            // Adaptations of args before fn call
            // ---

            var paramType = fnType.ParamTypes[argOffset];
            var lowering = GetLLVMLowering(paramType);
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
                        var llvmParamType = LLVMTarget.LowerType(paramType);
                        var (_, argValuePtr) = unit;
                        // TODO: This might be wrong. This is giving the same ptr to the fn (should be in ByRef), we might want to give a copy here in ByMutRef?
                        args.Add(argValuePtr);
                    }
                    break;
                case LLVMTypeLoweringKind.AbiInt:
                    {
                        var (_, argValuePtr) = unit;
                        var layout = Target.GetLayout(paramType);
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

        var returnValue = LLVM.Builder.BuildCall2(llvmFnType, llvmFnValue, [.. args]);
        attrs.Apply((index, attr) => returnValue.AddCallSiteAttributeAtIndex(index, attr));

        // Always push a value, even if void, since this gets popped in VisitExprStmt.
        _stack.Push(LLVMUnit.Unwrapped(llvmFnType.ReturnType, returnValue));
    }

    public override void VisitUnaryExpr(LoweredUnaryExpr node)
    {
        Visit(node.Expr);
        var (_, value) = _stack.Pop().Unwrap(LLVM);

        var newValue = node.Op switch
        {
            UnaryOperatorTokenKind.Neg => BuildNeg(),
            UnaryOperatorTokenKind.Not => LLVM.Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, value, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 0)),
            _ => throw new NotImplementedException(),
        };
        _stack.Push(LLVMUnit.Unwrapped(LLVMTypeRef.Double, newValue));

        LLVMValueRef BuildNeg()
        {
            if (BuiltinTypeSystem.IsNumericInteger(node.Type))
            {
                return LLVM.Builder.BuildNeg(value);
            }
            else
            {
                return LLVM.Builder.BuildFNeg(value);
            }
        }
    }

    public override void VisitBinaryExpr(LoweredBinaryExpr node)
    {
        Visit(node.Left);
        var (_, leftValue) = _stack.Pop().Unwrap(LLVM);

        Visit(node.Right);
        var (_, rightValue) = _stack.Pop().Unwrap(LLVM);

        var newValue = node.Op switch
        {
            BinaryOperatorTokenKind.Add => BuildAdd(),
            BinaryOperatorTokenKind.Sub => BuildSub(),
            // TODO: Missing
            _ => throw new NotImplementedException(),
        };
        _stack.Push(LLVMUnit.Unwrapped(LLVMTypeRef.Double, newValue));

        LLVMValueRef BuildAdd()
        {
            if (BuiltinTypeSystem.IsNumericInteger(node.Type))
            {
                return LLVM.Builder.BuildAdd(leftValue, rightValue);
            }
            else
            {
                return LLVM.Builder.BuildFAdd(leftValue, rightValue);
            }
        }

        LLVMValueRef BuildSub()
        {
            if (BuiltinTypeSystem.IsNumericInteger(node.Type))
            {
                return LLVM.Builder.BuildSub(leftValue, rightValue);
            }
            else
            {
                return LLVM.Builder.BuildFSub(leftValue, rightValue);
            }
        }
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
        var fieldLLVMType = LLVMTarget.LowerType(node.Field.Type);
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
        var fieldLLVMType = LLVMTarget.LowerType(node.Field.Type);

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
        switch (node.Literal.LiteralTokenKind)
        {
            case LiteralTokenKind.NumericInteger:
                {
                    Debug.Assert(node.Literal.TryGetNumericIntegerValue(out var v));
                    var llvmType = LLVMTarget.LowerType(node.Type);
                    // TODO: Do sign and zero extension more properly?
                    var signExtend = Target.GetIntegerSign(node.Type) == IntegerSign.Signed;
                    var llvmValue = LLVMValueRef.CreateConstInt(llvmType, v, signExtend);
                    _stack.Push(LLVMUnit.Unwrapped(llvmType, llvmValue));
                }
                break;

            case LiteralTokenKind.NumericFloat:
                {
                    Debug.Assert(node.Literal.TryGetNumericFloatValue(out var v));
                    var llvmType = LLVMTarget.LowerType(node.Type);
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

    private LLVMUnit GetOrCreateFn(Symbol symbol)
    {
        if (symbol is FnSymbol fnSymbol)
        {
            if (_context.SymbolToLLVMUnit.TryGetValue(fnSymbol, out var llvmUnit))
            {
                return llvmUnit;
            }
            llvmUnit = CreateFn(fnSymbol);
            _context.SymbolToLLVMUnit[fnSymbol] = llvmUnit;
            return llvmUnit;
        }
        else
        {
            return Get(symbol);
        }
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
        var llvmType = LLVMTarget.LowerType(paramSymbol.Type);

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
        var typeRef = LLVMTarget.LowerType(localSymbol.Type);
        var ptr = LLVM.Builder.BuildAlloca(typeRef, localSymbol.Identifier?.Value ?? string.Empty);
        var unit = LLVMUnit.Wrapped(typeRef, ptr);

        _context.SymbolToLLVMUnit[localSymbol] = unit;
        return unit;
    }

    private LLVMUnit CreateFn(FnSymbol fnSymbol)
    {
        // TODO: Optimize? This is being called multiple times in sequence when we compile an fn.
        var (sret, attrs) = LLVMTarget.GetFnInfoFromFnType(fnSymbol.Type);
        var (llvmPtrType, llvmFnType) = LLVMTarget.LowerType(fnSymbol.Type);

        if (fnSymbol.IsMain && llvmFnType.ReturnType == LLVMTypeRef.Void)
        {
            // Rewrite to return i32.
            llvmFnType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32, llvmFnType.ParamTypes);
            // Do we need to rewrite llvmPtrType? It's an opaque ptr, so maybe not.
        }

        var llvmFnValue = LLVM.Module.AddFunction(fnSymbol.Identifier!.Value, llvmFnType);

        if (!fnSymbol.IsExport && !fnSymbol.IsExtern && !fnSymbol.IsMain)
        {
            llvmFnValue.Linkage = LLVMLinkage.LLVMInternalLinkage;
        }

        // TODO: switch (cc) (C this is already the LLVM default if not set)
        llvmFnValue.SetFunctionCallConv(CallConv.C);

        attrs.Apply((index, attr) => llvmFnValue.AddAttributeAtIndex(index, attr));

        // Create an unwrapped unit out of direct fn values. This means direct calls to this fn value won't be loaded (in VisitCallExpr).
        return LLVMUnit.Unwrapped(llvmPtrType, llvmFnValue);
    }

    public LLVMTypeLowering GetLLVMLowering(TypeSymbol type)
    {
        return LLVMTarget.GetLLVMLowering(type);
    }
}

/// <summary>
/// LLVM compilation related logic that changes with target.
/// </summary>
internal abstract class LLVMTarget
{
    protected LLVMTarget(CompilationContext context)
    {
        Context = context;
    }

    protected CompilationContext Context { get; }

    protected LLVMCompilationContext LLVM => Context.LLVM;

    protected Target Target => Context.Target;

    public static LLVMTarget Create(CompilationContext context)
    {
        var target = context.Target;

        return target switch
        {
            x86_64WindowsMSVCTarget => new x86_64WindowsMSVCLLVMTarget(context),
            _ => throw new UnreachableException(),
        };
    }

    public bool IsReturnSRet(FnTypeSymbol fnType)
    {
        if (Target.GetAbiSize(fnType.ReturnType).IsZeroSized)
            return false;

        return IsReturnSRetCore(fnType);
    }

    public abstract bool IsReturnSRetCore(FnTypeSymbol fnType);

    // TODO: Support different calling conventions. Right now it's always Win64 cc.
    public abstract LLVMTypeRef LowerFnReturnType(FnSymbol fn);

    /// <summary>
    /// Lowers a type from Surab to LLVM.
    /// </summary>
    /// <param name="type">The Surab type to lower.</param>
    /// <returns>The lowered LLVM type.</returns>
    public LLVMTypeRef LowerType(TypeSymbol type)
    {
        if (type == BuiltinTypeSystem.None)
            return LLVMTypeRef.Void;
        if (type == BuiltinTypeSystem.@char || type == BuiltinTypeSystem.@bool)
            return LLVMTypeRef.Int1;

        if (type == BuiltinTypeSystem.i32 || type == BuiltinTypeSystem.u32)
            return LLVMTypeRef.Int32;
        if (type == BuiltinTypeSystem.i64 || type == BuiltinTypeSystem.u64)
            return LLVMTypeRef.Int64;
        if (type == BuiltinTypeSystem.f32)
            return LLVMTypeRef.Float;
        if (type == BuiltinTypeSystem.f64)
            return LLVMTypeRef.Double;

        if (type is StructTypeSymbol structType)
        {
            var fields = structType.Members.Fields().ToList();
            var structLayout = Target.GetStructLayout(structType);
            var elements = structLayout.Sections.Select(m =>
            {
                return m.Kind switch
                {
                    StructLayoutSectionKind.Padding => LLVMTypeRef.CreateArray(LLVMTypeRef.Int1, (uint)m.Size.Bytes),
                    StructLayoutSectionKind.Data => LowerType(fields[m.FieldIndex].Type),
                    _ => throw new UnreachableException(),
                };
            }).ToArray();
            return LLVMTypeRef.CreateStruct(elements, Packed: false);
        }

        if (type is FnTypeSymbol fnType)
        {
            var (llvmType, _) = LowerType(fnType);
            return llvmType;
        }

        throw new UnreachableException();
    }

    /// <summary>
    /// Lowers a type from Surab to LLVM.
    /// </summary>
    /// <param name="type">The Surab type to lower.</param>
    /// <param name="llvmActualType">An out variable that contains the actual LLVM type of in certain cases (such as <paramref name="type"/> being <see cref="FnTypeSymbol"/>). This is helpful in cases the returned LLVM type is an opaque ptr.</param>
    /// <returns>The lowered LLVM type.</returns>
    public (LLVMTypeRef llvmType, LLVMTypeRef llvmActualType) LowerType(FnTypeSymbol fnType)
    {
        // TODO: Optimize? This is being called multiple times in sequence when we compile an fn.
        var (sret, _) = GetFnInfoFromFnType(fnType);
        var returnType = LowerType(fnType.ReturnType);

        // TODO: Better way?
        var llvmIndex = 0;
        var capacity = fnType.ParamTypes.Length + 1; // Max length of llvm params.
        var @params = new List<LLVMTypeRef>(capacity);

        if (sret.HasValue)
        {
            @params.Add(returnType);
            llvmIndex++;
        }

        foreach (var paramType in fnType.ParamTypes)
        {
            // Create fn param types
            // ---

            var lowering = GetLLVMLowering(paramType);
            var paramLLVMType = LowerType(paramType);
            switch (lowering.Kind)
            {
                case LLVMTypeLoweringKind.ByValue:
                    {
                        @params.Add(paramLLVMType);
                    }
                    break;
                case LLVMTypeLoweringKind.ByMutRef:
                    {
                        @params.Add(LLVMTypeRef.CreatePointer(paramLLVMType, 0));
                    }
                    break;
                case LLVMTypeLoweringKind.AbiInt:
                    {
                        var layout = Target.GetLayout(paramType);
                        var llvmIntType = LLVMTypeRef.CreateInt((uint)layout.Size.Bits);
                        @params.Add(llvmIntType);
                    }
                    break;
            }

            llvmIndex++;
        }

        var llvmFnType = LLVMTypeRef.CreateFunction(sret.HasValue ? LLVMTypeRef.Void : returnType, @params.ToArray(), false);

        // TODO: AddressSpace?
        return (LLVMTypeRef.CreatePointer(llvmFnType, 0), llvmFnType);
    }

    // TODO: Optimize calls to this method? This is being called multiple times in sequence when we compile an fn. We could cache the result.
    public LLVMFn GetFnInfoFromFnType(FnTypeSymbol fnType)
    {
        var returnType = LowerType(fnType.ReturnType);

        // TODO: Better way?
        var llvmIndex = 0;
        var attrs = new LLVMFnAttrsDef();

        var sret = IsReturnSRet(fnType);
        if (sret)
        {
            attrs.AddParam(llvmIndex, LLVM.Context.CreateEnumAttribute(AttributeKind.StructRet, default));
            llvmIndex++;
        }

        foreach (var paramType in fnType.ParamTypes)
        {
            // Add attrs
            // ---

            var lowering = GetLLVMLowering(paramType);
            //var llvmType = Target.LowerType(paramType);
            switch (lowering.Kind)
            {
                case LLVMTypeLoweringKind.ByValue:
                    {
                    }
                    break;
                case LLVMTypeLoweringKind.ByMutRef:
                    {
                    }
                    break;
                case LLVMTypeLoweringKind.AbiInt:
                    {
                    }
                    break;
            }

            llvmIndex++;
        }

        return new LLVMFn(sret ? returnType : (LLVMTypeRef?)null, attrs);
    }

    public LLVMTypeLowering GetLLVMLowering(TypeSymbol type)
    {
        if (Target.GetAbiSize(type).IsZeroSized)
            return LLVMTypeLowering.ZeroSized;

        return GetLLVMLoweringCore(type);
    }

    protected abstract LLVMTypeLowering GetLLVMLoweringCore(TypeSymbol type);
}

internal class x86_64WindowsLLVMTarget(CompilationContext context) : LLVMTarget(context)
{
    protected new x86_64WindowsTarget Target => (x86_64WindowsTarget)Context.Target;

    public override bool IsReturnSRetCore(FnTypeSymbol fnType)
    {
        return Target.Classify(fnType.ReturnType) == Classification.Memory;
    }

    public override LLVMTypeRef LowerFnReturnType(FnSymbol fn)
    {
        var returnType = fn.ReturnType;
        var classification = Target.Classify(returnType);
        return classification switch
        {
            Classification.Integer => returnType.IsScalar ? LowerType(returnType) : LLVMTypeRef.CreateInt((uint)Target.GetAbiSize(returnType).Bits),
            //win_i128 =>
            Classification.Memory => LLVMTypeRef.Void,
            Classification.SSE => LowerType(returnType),
            _ => throw new UnreachableException(),
        };
    }

    protected override LLVMTypeLowering GetLLVMLoweringCore(TypeSymbol type)
    {
        Debug.Assert(!Target.GetAbiSize(type).IsZeroSized);

        return Target.Classify(type) switch
        {
            Classification.Integer => type.IsScalar ? LLVMTypeLowering.ByValue : LLVMTypeLowering.AbiInt,
            //win_i128 =>
            Classification.Memory => LLVMTypeLowering.ByMutRef,
            Classification.SSE => LLVMTypeLowering.ByValue,
            _ => throw new UnreachableException(),
        };
    }
}

internal sealed class x86_64WindowsMSVCLLVMTarget(CompilationContext context) : x86_64WindowsLLVMTarget(context)
{
    private new x86_64WindowsMSVCTarget Target => (x86_64WindowsMSVCTarget)Context.Target;
}
