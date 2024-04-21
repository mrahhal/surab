using System.Diagnostics;
using LLVMSharp.Interop;
using Surab.Analysis;
using Surab.Compilation.LLVM;

namespace Surab.Compilation;

public sealed class Compiler
{
	private readonly CompilationContext _context;

	internal static readonly bool VerificationEnabled = true;

	private Compiler(
		SurabProject project,
		Target target,
		CompilationOptions options)
	{
		var llvm = LLVMCompilationContext.Create();

		_context = new CompilationContext(project, target, llvm, options);
	}

	/// <summary>
	/// Compiles the given project and returns the compiled executable path.
	///
	/// Will throw if the project contains error diagnostics. It's the responsibility of the consumer to check
	/// for those before calling compile.
	/// </summary>
	public static string Compile(
		SurabProject project,
		string targetTriple,
		CompilationOptions? options = null)
	{
		// Note: We're not taking any locks (particularly read) as the compilation doesn't run in an asynchronous environment right now.

		project.EnsureAnalyzedAsync().GetAwaiter().GetResult();
		var diags = project.GetProjectDiags();
		if (diags.Any(fd => fd.HighestLevel is DiagLevel.Error))
		{
			throw new InvalidOperationException("The project contains errors. You cannot call compile if there are errors.");
		}

		var target = ParseTargetTriple(targetTriple);
		var compiler = new Compiler(project, target, options ?? CompilationOptions.CreateDefault());
		return compiler.Compile();
	}

	private string Compile()
	{
		var rootModule = _context.Project.RootModule;
		var mainSymbol = rootModule.GetExportedSymbol("main");
		if (mainSymbol == null || mainSymbol is not FnSymbol)
		{
			throw new Exception("A main function is required in the root module.");
		}

		var mainFile = mainSymbol.DeclaringFile;
		Debug.Assert(mainFile != null);

		foreach (var reasoner in _context.Project.GetAllReasoners())
		{
			FileCompiler.Compile(_context, reasoner);
		}

		if (VerificationEnabled)
		{
			_context.LLVM.Module.Verify(LLVMVerifierFailureAction.LLVMPrintMessageAction);
		}

		//_context.LLVM.PassManager.FinalizeFunctionPassManager();
		//_context.LLVM.PassManager.Run(_context.LLVM.Module);

		_context.LLVM.Module.PrintToFile(_context.Options.LLFileName);
		return ClangRunner.Run([_context.Options.LLFileName]);
	}

	private static Target ParseTargetTriple(string targetTriple)
	{
		// TODO
		return new x86_64WindowsMSVCTarget();
	}
}

internal sealed class FileCompiler(CompilationContext context, LoweredTree loweredTree) : LoweredTreeVisitor
{
	private readonly CompilationContext _context = context;
	private readonly LoweredTree _loweredTree = loweredTree;

	public static void Compile(CompilationContext context, Reasoner reasoner)
	{
		var loweredTree = Lowerer.Lower(reasoner);
		var fileCompiler = new FileCompiler(context, loweredTree);
		fileCompiler.CompileFile();
	}

	private void CompileFile()
	{
		Visit(_loweredTree.Root);
	}

	public override void VisitFnDecl(LoweredFnDecl node)
	{
		CompileFn(node);
	}

	private void CompileFn(LoweredFnDecl fn)
	{
		FnCompiler.Compile(_context, fn);
	}
}

internal sealed class FnCompiler : LoweredTreeVisitor
{
	private readonly CompilationContext _context;
	private readonly LoweredFnDecl _fnDecl;
	private readonly LLVMUnit _llvmFnUnit;
	private readonly LLVMFn _llvmFn;

	private LLVMUnit? _llvmReceiverUnit;
	private readonly Stack<LLVMUnit> _stack = new();
	private bool _returnSeen = false;

	private FnCompiler(
		CompilationContext context,
		LoweredFnDecl fnDecl)
	{
		_context = context;
		_fnDecl = fnDecl;

		_llvmFnUnit = GetUnit(fnDecl.FnSymbol);
		_llvmFn = LLVMTarget.GetFnInfoFromFnType(fnDecl.FnSymbol.Type);
		Debug.Assert(_llvmFnUnit.Value.BasicBlocksCount == 0);
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
		if (FnSymbol.IsExtern && !FnSymbol.HasBody)
		{
			return;
		}

		// Entry has to exist before visiting the params in the signature.
		var entry = _llvmFnUnit.Value.AppendBasicBlock("entry");
		LLVM.Builder.PositionAtEnd(entry);

		SetupParams();

		// This is where we could check for intrinsic fns to generate bodies for if we need to.
		// ---

		if (_fnDecl.Body != null)
		{
			Visit(_fnDecl.Body);
		}

		if (!_returnSeen)
		{
			if (FnSymbol.IsMain && FnSymbol.ReturnType.KnownTypeTag == KnownTypeTag.None)
			{
				LLVM.Builder.BuildRet(LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0));
			}
			else
			{
				LLVM.Builder.BuildRetVoid();
			}
		}

		if (!_llvmFnUnit.Value.VerifyFunction(LLVMVerifierFailureAction.LLVMPrintMessageAction))
		{
			// TODO: Log? LLVM prints the problem on the console anyway.
			//if (Compiler.VerificationEnabled)
			//throw new Exception("LLVM function verification failed.");
		}

		//LLVM.PassManager.RunFunctionPassManager(_llvmFnUnit.Value);
	}

	private void SetupParams()
	{
		// REVIEW:
		// FnSymbol.Params doesn't include receivers, FnSymbol.Type.ParamTypes includes receivers.
		// Document this or is there a better way?

		IEnumerable<(TypeSymbol Type, ParamSymbol? Symbol)> receiverParams = FnSymbol.MethodReceiverType == null ? [] : [(FnSymbol.MethodReceiverType, null)];
		IEnumerable<(TypeSymbol Type, ParamSymbol? Symbol)> normalParams = [.. FnSymbol.Params.Select(x => (x.Type, x))];
		IEnumerable<(TypeSymbol Type, ParamSymbol? Symbol)> overallParams = [.. receiverParams, .. normalParams];

		var llvmParamOffset = 0;
		if (_llvmFn.SRet.HasValue)
		{
			// Skip sret.
			llvmParamOffset++;
		}

		foreach (var t in overallParams)
		{
			var paramValue = _llvmFnUnit.Value.GetParam((uint)llvmParamOffset);
			var paramType = t.Type;
			var paramName = (t.Symbol == null ? "this" : t.Symbol.Name) ?? string.Empty;
			var llvmType = LLVMTarget.LowerType(paramType);

			// Adaptations of params inside fn body
			// ---

			var lowering = GetLLVMLowering(t.Type);
			switch (lowering.Kind)
			{
				case LLVMTypeLoweringKind.ByValue:
					{
						var alignment = Target.GetAbiAlignment(paramType);

						var ptr = LLVM.Builder.BuildAlloca(llvmType, paramName);
						ptr.SetAlignment((uint)alignment.Bytes);
						LLVM.Builder.BuildStore(paramValue, ptr);
						var unit = LLVMUnit.Wrapped(llvmType, ptr);

						CreateParamUnit(t.Symbol, unit);
					}
					break;
				case LLVMTypeLoweringKind.ByCopyRef:
					{
						// paramValue is already a ptr. Treat it as the wrapper ptr directly.
						var alignment = Target.GetAbiAlignment(t.Type);
						Debug.Assert(paramValue.TypeOf.Kind == LLVMTypeKind.LLVMPointerTypeKind, "Param value should already be a ptr.");
						var unit = LLVMUnit.Wrapped(llvmType, paramValue);
						CreateParamUnit(t.Symbol, unit);
					}
					break;
				case LLVMTypeLoweringKind.AsInt:
					{
						var layout = Target.GetLayout(t.Type);
						var llvmIntType = LLVMTypeRef.CreateInt((uint)layout.Size.Bits);
						var alignment = Math.Max(layout.Alignment.Bytes, (int)LLVM.TargetData.ABIAlignmentOfType(llvmIntType)).AsBytes();

						var ptr = LLVM.Builder.BuildAlloca(llvmType, paramName);
						ptr.SetAlignment((uint)alignment.Bytes);
						LLVM.Builder.BuildStore(paramValue, ptr);
						var unit = LLVMUnit.Wrapped(llvmType, ptr);

						CreateParamUnit(t.Symbol, unit);
					}
					break;
			}

			llvmParamOffset++;
		}
	}

	public override void VisitVariableDeclareStmt(LoweredVariableDeclareStmt node)
	{
		// These are usually hoisted to the top of the body by the lowerer.
		// Having these created in the entry basic block is important to enable certain LLVM optimizations.
		CreateLocalUnit(node.Symbol);
	}

	public override void VisitExprStmt(LoweredExprStmt node)
	{
		Visit(node.Expr);

		// The expr will always push a unit to the stack (even void fns).
		// An ExprStmt marks the end of an expr pipeline, so we simply always discard that last value here.
		_stack.Pop();
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
			var exprUnit = _stack.Pop();
			LLVMValueRef? sretPtr = null;
			if (_llvmFn.SRet.HasValue)
			{
				sretPtr = _llvmFnUnit.Value.GetParam(0);
			}
			LLVMTarget.CompileFnReturnStmt(FnSymbol.ReturnType, exprUnit, sretPtr);
		}
	}

	public override void VisitObjectAllocStmt(LoweredObjectAllocStmt node)
	{
		var llvmType = LLVMTarget.LowerType(node.Type);

		Debug.Assert(node.Type is NamedTypeSymbol); // temp

		var ptr = LLVM.Builder.BuildAlloca(llvmType);
		var unit = LLVMUnit.Wrapped(llvmType, ptr);

		// Tie unit to temp symbol.
		Debug.Assert(!_context.SymbolToLLVMUnit.ContainsKey(node.TempSymbol));
		_context.SymbolToLLVMUnit[node.TempSymbol] = unit;
	}

	public override void VisitCallExpr(LoweredCallExpr node)
	{
		Visit(node.Callee);
		var fnUnit = _stack.Pop();
		var fnType = node.FnType;

		// Unwrapping loads the actual fn ptr for indirect calls (direct calls won't be wrapped in the first place).
		var (_, llvmFnValue) = fnUnit.Unwrap(LLVM);
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
			var (size, alignment) = Target.GetLayout(fnType.ReturnType);

			var sretAlloc = LLVM.Builder.BuildAlloca(sret.Value);
			sretAlloc.SetAlignment((uint)alignment.Bytes);
			args.Add(sretAlloc);

			llvmIndex++;
		}

		while (llvmIndex < llvmArgCount)
		{
			var argUnit = _stack.Pop();

			// Adaptations of args before fn call
			// ---

			var paramType = fnType.ParamTypes[argOffset];
			var lowering = GetLLVMLowering(paramType);
			switch (lowering.Kind)
			{
				case LLVMTypeLoweringKind.ByValue:
					{
						var (_, argValue) = argUnit.Unwrap(LLVM);
						args.Add(argValue);
					}
					break;
				case LLVMTypeLoweringKind.ByCopyRef:
					{
						var llvmParamType = LLVMTarget.LowerType(paramType);
						// Copy the value into a new ptr to preserve *pass by copy* semantics.
						var copyPtr = LLVM.Builder.BuildAlloca(llvmParamType);
						var (_, argValue) = argUnit.Unwrap(LLVM);
						LLVM.Builder.BuildStore(argValue, copyPtr);
						args.Add(copyPtr);
					}
					break;
				case LLVMTypeLoweringKind.AsInt:
					{
						// Might not be right, so this assertion is to detect a case where this isn't
						// true so we can look into it when it happens.
						Debug.Assert(argUnit.IsWrapped);
						var (_, argValuePtr) = argUnit;
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

		if (sret.HasValue)
		{
			// Adaptation for sret. Load the sret ptr and push to the stack to simulate a return value.
			var sretPtr = args[0];
			_stack.Push(LLVMUnit.Wrapped(sret.Value, sretPtr));
		}
		else
		{
			// Always push a value, even if void, since this gets popped in VisitExprStmt.
			_stack.Push(LLVMUnit.Unwrapped(llvmFnType.ReturnType, returnValue));
		}
	}

	public override void VisitUnaryExpr(LoweredUnaryExpr node)
	{
		Visit(node.Expr);
		var unit = _stack.Pop();

		switch (node.Op)
		{
			case UnaryOperatorTokenKind.Address:
				DoAddressOf(node.Type, unit);
				return;
			case UnaryOperatorTokenKind.Deref:
				DoDeref(node.Type, unit);
				return;
		}

		var (_, value) = unit.Unwrap(LLVM);

		var newValue = node.Op switch
		{
			UnaryOperatorTokenKind.Neg => BuildNeg(),
			UnaryOperatorTokenKind.Not => LLVM.Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, value, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 0)),
			_ => throw new NotImplementedException(),
		};
		// TODO:
		_stack.Push(LLVMUnit.Unwrapped(LLVMTypeRef.Double, newValue));

		LLVMValueRef BuildNeg()
		{
			if (node.Type.IsNumericInteger())
			{
				return LLVM.Builder.BuildNeg(value);
			}
			else
			{
				return LLVM.Builder.BuildFNeg(value);
			}
		}
	}

	private void DoAddressOf(TypeSymbol type, LLVMUnit unit)
	{
		var llvmType = LLVMTarget.LowerType(type);
		// Taking an address of something is only valid with lvalues. This means the unit we're taking an address of is always wrapped.
		Debug.Assert(unit.IsWrapped);
		// Address results are always unwrapped (i.e. literal units).
		_stack.Push(LLVMUnit.Unwrapped(llvmType, unit.Value));
	}

	public void DoDeref(TypeSymbol type, LLVMUnit unit)
	{
		var llvmType = LLVMTarget.LowerType(type);
		// Derefing something is only valid on wrapped units.
		Debug.Assert(unit.IsWrapped);
		// Deref results are always wrapped (i.e. not literal units).
		_stack.Push(LLVMUnit.Wrapped(llvmType, unit.Unwrap(LLVM).Value));
		//var load = LLVM.Builder.BuildLoad2(unit.Type, unit.Value);
		//_stack.Push(LLVMUnit.Wrapped(llvmType, load));
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
			if (node.Type.IsNumericInteger())
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
			if (node.Type.IsNumericInteger())
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
		var unit = GetUnit(node.Variable);
		// We don't load it, but give that option to the consumer of this unit.
		// Some consumers will want to simply load it and deal with the value, but others will need access to the ptr.
		_stack.Push(unit);
	}

	public override void VisitVariableStoreExpr(LoweredVariableStoreStmt node)
	{
		var (_, value) = GetUnit(node.Variable);
		Visit(node.Expr);
		var rhsUnit = _stack.Pop();

		var leftType = ((ISymbolHasType)node.Variable).Type;
		var rightType = node.Expr.Type;

		// REVIEW: We want to call memcpy instead of store when it's a struct and rhs is wrapped.
		// This is better than loading into a struct value and then storing it back (effectively emulating memcpy). But even more importantly, this
		// is necessary for sret to be compiled properly.
		// Is this logic here good enough?
		// NOTE: This doesn't mean we'll memcpy the struct data for an actual struct ptr. An actual struct ptr will not have matched StructTypeSymbol. (it'll be a ptr symbol)
		if (leftType is NamedTypeSymbol && rightType is NamedTypeSymbol && rhsUnit.IsWrapped)
		{
			var (_, rhs) = rhsUnit;
			var size = Target.GetAbiSize(node.Expr.Type);
			LLVM.CallIntrinsic_memcpy_i64(value, rhs, size.Bytes);
		}
		else
		{
			var (_, rhs) = rhsUnit.Unwrap(LLVM);
			LLVM.Builder.BuildStore(rhs, value);
		}
	}

	public override void VisitFieldLoadExpr(LoweredFieldLoadExpr node)
	{
		Visit(node.Target);
		var (_, targetPtr) = _stack.Pop();
		var enclosingType = node.Field.EnclosingType;
		var llvmEnclosingType = LLVMTarget.LowerType(enclosingType);

		var structLayout = Target.GetStructLayout((NamedTypeSymbol)node.Field.Parent!);
		var fieldSection = structLayout.Sections.First(s => s.FieldIndex == node.Field.FieldIndex);

		var ptrToField = LLVM.Builder.BuildStructGEP2(llvmEnclosingType, targetPtr, (uint)fieldSection.SectionIndex);
		var fieldLLVMType = LLVMTarget.LowerType(node.Field.Type);
		// We don't load it, for the same reason outlined in VisitVariableLoadExpr.
		_stack.Push(LLVMUnit.Wrapped(fieldLLVMType, ptrToField));
	}

	public override void VisitFieldStoreExpr(LoweredFieldStoreStmt node)
	{
		Visit(node.Target);
		var (_, targetPtr) = _stack.Pop();
		var targetType = LLVMTarget.LowerType(node.Field.EnclosingType);

		Visit(node.Expr);
		var rhsUnit = _stack.Pop();

		var structLayout = Target.GetStructLayout((NamedTypeSymbol)node.Field.Parent!);
		var fieldSection = structLayout.Sections.First(s => s.FieldIndex == node.Field.FieldIndex);
		var fieldLLVMType = LLVMTarget.LowerType(node.Field.Type);

		var ptrToField = LLVM.Builder.BuildStructGEP2(targetType, targetPtr, (uint)fieldSection.SectionIndex);

		var leftType = node.Field.Type;
		var rightType = node.Expr.Type;

		// Same as in VisitVariableStoreExpr.
		if (leftType is NamedTypeSymbol && rightType is NamedTypeSymbol && rhsUnit.IsWrapped)
		{
			var (_, rhs) = rhsUnit;
			var size = Target.GetAbiSize(node.Expr.Type);
			LLVM.CallIntrinsic_memcpy_i64(ptrToField, rhs, size.Bytes);
		}
		else
		{
			var (_, rhs) = rhsUnit.Unwrap(LLVM);
			LLVM.Builder.BuildStore(rhs, ptrToField);
		}
	}

	public override void VisitThisLoadExpr(LoweredThisLoadExpr node)
	{
		Debug.Assert(_llvmReceiverUnit != null);
		_stack.Push(_llvmReceiverUnit);
	}

	public override void VisitSymbolLoadExpr(LoweredSymbolLoadExpr node)
	{
		// TODO: Merge with VariableLoad?
		var unit = GetUnit(node.Symbol);
		_stack.Push(unit);
	}

	public override void VisitLiteralExpr(LoweredLiteralExpr node)
	{
		switch (node.Literal.LiteralTokenKind)
		{
			case LiteralTokenKind.NumericInteger:
			case LiteralTokenKind.NumericFloat:
				{
					if (node.Type.IsNumericInteger())
					{
						var v = node.Literal.GetNumericValueAsInteger();
						var llvmType = LLVMTarget.LowerType(node.Type);
						// TODO: Do sign and zero extension more properly?
						var signExtend = Target.GetIntegerSign(node.Type) == IntegerSign.Signed;
						var llvmValue = LLVMValueRef.CreateConstInt(llvmType, v, signExtend);
						_stack.Push(LLVMUnit.Unwrapped(llvmType, llvmValue));
					}
					else if (node.Type.IsNumericFloat())
					{
						var v = node.Literal.GetNumericValueAsFloat();
						var llvmType = LLVMTarget.LowerType(node.Type);
						var llvmValue = LLVMValueRef.CreateConstReal(llvmType, v);
						_stack.Push(LLVMUnit.Unwrapped(llvmType, llvmValue));
					}
					else
					{
						throw new UnreachableException();
					}
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

			case LiteralTokenKind.String:
				{
					Debug.Assert(node.Literal.TryGetStringValue(out var v));
					var llvmType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Void, 0);
					var llvmValue = LLVM.Builder.BuildGlobalStringPtr(v!);
					_stack.Push(LLVMUnit.Unwrapped(llvmType, llvmValue));
				}
				break;

			default:
				// TODO: Add to SemanticAnalyzer.
				throw new NotImplementedException("Should not be here.");
		}
	}

	private LLVMUnit GetUnit(Symbol symbol)
	{
		Debug.Assert(symbol is not TypeSymbol, "Type symbols don't have units.");

		// FnSymbol's unit is lazily created below, whereas all other symbols should have been already created by the time this is called.

		if (_context.SymbolToLLVMUnit.TryGetValue(symbol, out var llvmUnit))
		{
			return llvmUnit;
		}

		if (symbol is FnSymbol fnSymbol)
		{
			llvmUnit = CreateFnUnit(fnSymbol);
			_context.SymbolToLLVMUnit[fnSymbol] = llvmUnit;
			return llvmUnit;
		}

		throw new UnreachableException();
	}

	private LLVMUnit CreateLocalUnit(LocalSymbol localSymbol)
	{
		var typeRef = LLVMTarget.LowerType(localSymbol.Type);
		var ptr = LLVM.Builder.BuildAlloca(typeRef, localSymbol.Name ?? string.Empty);
		var unit = LLVMUnit.Wrapped(typeRef, ptr);
		_context.SymbolToLLVMUnit[localSymbol] = unit;
		return unit;
	}

	// paramSymbol is null for the receiver param.
	private LLVMUnit CreateParamUnit(ParamSymbol? paramSymbol, LLVMUnit unit)
	{
		if (paramSymbol != null)
		{
			_context.SymbolToLLVMUnit[paramSymbol] = unit;
		}
		else
		{
			_llvmReceiverUnit = unit;
		}
		return unit;
	}

	private LLVMUnit CreateFnUnit(FnSymbol fnSymbol)
	{
		var (sret, attrs) = LLVMTarget.GetFnInfoFromFnType(fnSymbol.Type);
		var (llvmPtrType, llvmFnType) = LLVMTarget.LowerType(fnSymbol.Type);

		if (fnSymbol.IsMain && llvmFnType.ReturnType == LLVMTypeRef.Void)
		{
			// Rewrite to return i32.
			llvmFnType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32, llvmFnType.ParamTypes);
			// Do we need to rewrite llvmPtrType? It's an opaque ptr, so maybe not.
		}

		var name = fnSymbol.IsExtern || fnSymbol.IsMain ? fnSymbol.Name : fnSymbol.FullName;
		var llvmFnValue = LLVM.Module.AddFunction(name, llvmFnType);

		if (!fnSymbol.IsExtern && !fnSymbol.IsMain)
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
	public LLVMTypeRef LowerFnReturnType(FnTypeSymbol fnType)
	{
		if (fnType.ReturnType.KnownTypeTag == KnownTypeTag.None)
			return LLVMTypeRef.Void;

		Debug.Assert(!Target.GetAbiSize(fnType.ReturnType).IsZeroSized);

		return LowerFnReturnTypeCore(fnType);
	}

	protected abstract LLVMTypeRef LowerFnReturnTypeCore(FnTypeSymbol fnType);

	public void CompileFnReturnStmt(TypeSymbol returnType, LLVMUnit exprUnit, LLVMValueRef? sretOpt)
	{
		CompileFnReturnStmtCore(returnType, exprUnit, sretOpt);
	}

	protected abstract void CompileFnReturnStmtCore(TypeSymbol returnType, LLVMUnit exprUnit, LLVMValueRef? sretOpt);

	/// <summary>
	/// Lowers a type from Surab to LLVM.
	/// </summary>
	/// <param name="type">The Surab type to lower.</param>
	/// <returns>The lowered LLVM type.</returns>
	public LLVMTypeRef LowerType(TypeSymbol type)
	{
		type = type.UnwrapTypeAliases();

		if (Context.TypeSymbolToLLVMType.TryGetValue(type, out var llvmType))
		{
			return llvmType;
		}

		llvmType = LowerTypeCore(type);
		Context.TypeSymbolToLLVMType[type] = llvmType;
		return llvmType;
	}

	private LLVMTypeRef LowerTypeCore(TypeSymbol type)
	{
		var tag = type.KnownTypeTag;

		if (tag is KnownTypeTag.None)
			return LLVMTypeRef.Void;
		if (tag is KnownTypeTag.@char or KnownTypeTag.@bool)
			return LLVMTypeRef.Int1;
		if (tag is KnownTypeTag.@string || type is PtrTypeSymbol)
			return LLVMTypeRef.CreatePointer(LLVMTypeRef.Void, 0);

		if (tag is KnownTypeTag.i32 or KnownTypeTag.u32)
			return LLVMTypeRef.Int32;
		if (tag is KnownTypeTag.i64 or KnownTypeTag.u64)
			return LLVMTypeRef.Int64;
		if (tag is KnownTypeTag.f32)
			return LLVMTypeRef.Float;
		if (tag is KnownTypeTag.f64)
			return LLVMTypeRef.Double;

		if (type is NamedTypeSymbol structType)
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
			// TODO: Optionally mangled name instead.
			var llvmType = LLVM.Context.CreateNamedStruct(structType.FullName);
			llvmType.StructSetBody(elements, Packed: false);
			return llvmType;
		}

		if (type is FnTypeSymbol fnType)
		{
			var (llvmType, _) = LowerType(fnType);
			return llvmType;
		}

		throw new UnreachableException();
	}

	/// <summary>
	/// Lowers an fn type from Surab to LLVM.
	/// </summary>
	/// <param name="fnType">The Surab fn type to lower.</param>
	/// <returns>The lowered LLVM type (which is an opaque ptr in the case of fns here) and the actual LLVM type (the actual fn type).</returns>
	public (LLVMTypeRef llvmType, LLVMTypeRef llvmActualType) LowerType(FnTypeSymbol fnType)
	{
		var (sret, _) = GetFnInfoFromFnType(fnType);
		var returnType = LowerFnReturnType(fnType);

		// TODO: Better way?
		var llvmIndex = 0;
		var capacity = fnType.ParamTypes.Length + 1; // Max length of llvm params.
		var @params = new List<LLVMTypeRef>(capacity);

		if (sret.HasValue)
		{
			@params.Add(LLVMTypeRef.CreatePointer(LowerType(fnType.ReturnType), 0));
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
				case LLVMTypeLoweringKind.ByCopyRef:
					{
						@params.Add(LLVMTypeRef.CreatePointer(paramLLVMType, 0));
					}
					break;
				case LLVMTypeLoweringKind.AsInt:
					{
						var layout = Target.GetLayout(paramType);
						var llvmIntType = LLVMTypeRef.CreateInt((uint)layout.Size.Bits);
						@params.Add(llvmIntType);
					}
					break;
			}

			llvmIndex++;
		}

		var llvmFnType = LLVMTypeRef.CreateFunction(returnType, @params.ToArray(), false);

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
			attrs.AddParam(llvmIndex, LLVM.Context.CreateTypeAttribute(AttributeKind.StructRet, returnType));
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
				case LLVMTypeLoweringKind.ByCopyRef:
					{
					}
					break;
				case LLVMTypeLoweringKind.AsInt:
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

internal abstract class x86_64WindowsLLVMTarget(CompilationContext context) : LLVMTarget(context)
{
	protected new x86_64WindowsTarget Target => (x86_64WindowsTarget)base.Target;

	public override bool IsReturnSRetCore(FnTypeSymbol fnType)
	{
		return Target.Classify(fnType.ReturnType) == Classification.Memory;
	}

	protected override LLVMTypeRef LowerFnReturnTypeCore(FnTypeSymbol fnType)
	{
		var returnType = fnType.ReturnType;
		var classification = Target.Classify(returnType);
		return classification switch
		{
			Classification.Integer => returnType.IsScalar() ? LowerType(returnType) : LLVMTypeRef.CreateInt((uint)Target.GetAbiSize(returnType).Bits),
			//win_i128 =>
			Classification.Memory => LLVMTypeRef.Void,
			Classification.SSE => LowerType(returnType),
			_ => throw new UnreachableException(),
		};
	}

	// Logic sync'd with LowerFnReturnTypeCore above.
	protected override void CompileFnReturnStmtCore(TypeSymbol returnType, LLVMUnit exprUnit, LLVMValueRef? sretOpt)
	{
		Debug.Assert(!returnType.IsUnknown() && !Target.GetAbiSize(returnType).IsZeroSized);

		var classification = Target.Classify(returnType);

		var passthroughCase =
			(classification is Classification.Integer && returnType.IsScalar()) ||
			classification is Classification.SSE;
		if (passthroughCase)
		{
			var (_, value) = exprUnit.Unwrap(LLVM);
			LLVM.Builder.BuildRet(value);
			return;
		}

		if (classification is Classification.Memory)
		{
			// sret
			Debug.Assert(sretOpt.HasValue);
			var sretPtr = sretOpt.Value;
			Debug.Assert(sretPtr.TypeOf.Kind is LLVMTypeKind.LLVMPointerTypeKind);
			var (_, exprValue) = exprUnit.Unwrap(LLVM);
			LLVM.Builder.BuildStore(exprValue, sretPtr);
			return;
		}

		if (classification is Classification.Integer)
		{
			// abi int

			Debug.Assert(exprUnit.IsWrapped);
			var (_, exprValuePtr) = exprUnit;
			var layout = Target.GetLayout(returnType);
			var llvmIntType = LLVMTypeRef.CreateInt((uint)layout.Size.Bits);
			var loaded = LLVM.Builder.BuildLoad2(llvmIntType, exprValuePtr);
			loaded.SetAlignment((uint)layout.Alignment.Bytes);
			LLVM.Builder.BuildRet(loaded);
			return;
		}
	}

	protected override LLVMTypeLowering GetLLVMLoweringCore(TypeSymbol type)
	{
		Debug.Assert(!Target.GetAbiSize(type).IsZeroSized);

		return Target.Classify(type) switch
		{
			Classification.Integer => type.IsScalar() ? LLVMTypeLowering.ByValue : LLVMTypeLowering.AsInt,
			//win_i128 =>
			Classification.Memory => LLVMTypeLowering.ByCopyRef,
			Classification.SSE => LLVMTypeLowering.ByValue,
			_ => throw new UnreachableException(),
		};
	}
}

internal sealed class x86_64WindowsMSVCLLVMTarget(CompilationContext context) : x86_64WindowsLLVMTarget(context)
{
	private new x86_64WindowsMSVCTarget Target => (x86_64WindowsMSVCTarget)Context.Target;
}
