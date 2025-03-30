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

		// TODO: std project diags are not being reported right now
		var involvedProjects = project.GetInvolvedProjects();
		var tasks = involvedProjects.Select(p => p.EnsureAnalyzedAsync());
		Task.WhenAll(tasks).GetAwaiter().GetResult();

		foreach (var p in involvedProjects)
		{
			var diags = p.GetProjectDiags();
			if (diags.Any(fd => fd.HighestLevel is DiagLevel.Error))
			{
				throw new InvalidOperationException("The project contains errors. You cannot call compile if there are errors.");
			}
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

		var interner = new LoweringInterner();
		var loweredProject = HirLowerer.LowerProject(_context.Project, interner);
		var loweredTrees = Monomorphization.Monomorph(loweredProject, interner);

		foreach (var tree in loweredTrees)
		{
			TreeCompiler.Compile(_context, tree);
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

internal sealed class TreeCompiler(
	CompilationContext context,
	HirTree tree) : HirTreeVisitor
{
	private readonly CompilationContext _context = context;
	private readonly HirTree _tree = tree;

	public static void Compile(CompilationContext context, HirTree tree)
	{
		var treeCompiler = new TreeCompiler(context, tree);
		treeCompiler.Compile();
	}

	private void Compile()
	{
		Visit(_tree.Root);
	}

	public override void VisitFnDecl(HirFnDecl node)
	{
		CompileFn(node);
	}

	private void CompileFn(HirFnDecl fn)
	{
		FnCompiler.Compile(_context, fn, fn.FnValue);
	}
}

// We will create key structs for FnClosed Value and StructClosed Type. These can be formed from
// the HirValue/Type after it gets EnsureClosed (using flat smap) when compiling an fn.
// The value of the key is the LLVM value/type that was created.

internal sealed class FnCompiler : HirTreeVisitor
{
	private readonly CompilationContext _context;
	private readonly HirFnDecl _fnDecl;
	private readonly LLVMUnit _llvmFnUnit;
	private readonly LLVMFn _llvmFn;

	private readonly Stack<LLVMUnit> _stack = new();
	private bool _returnSeen = false;
	private readonly List<LLVMUnit> _paramUnits = [];

	private FnCompiler(
		CompilationContext context,
		HirFnDecl fnDecl,
		HirFnValue fnValue)
	{
		Debug.Assert(fnValue is not HirGenericFnValue && fnValue.GetIsFullyConcrete());

		_context = context;
		_fnDecl = fnDecl;
		FnValue = fnValue;

		_llvmFnUnit = GetUnit(fnValue);
		_llvmFn = LLVMTarget.GetFnInfoFromFnType(fnValue.Type);
		Debug.Assert(_llvmFnUnit.Value.BasicBlocksCount == 0);
	}

	private Target Target => _context.Target;

	private LLVMTarget LLVMTarget => _context.LLVMTarget;

	private LLVMCompilationContext LLVM => _context.LLVM;

	private HirFnValue FnValue { get; }

	public static void Compile(
		CompilationContext context,
		HirFnDecl fnDecl,
		HirFnValue fnValue)
	{
		var c = new FnCompiler(context, fnDecl, fnValue);
		c.Compile();
	}

	private void Compile()
	{
		if (FnValue.IsExtern && _fnDecl.Body == null)
		{
			return;
		}

		// Entry has to exist before visiting the params in the signature.
		EnterBasicBlock("entry");

		SetupParams();

		// This is where we could check for intrinsic fns to generate bodies for if we need to.
		// ---

		if (_fnDecl.Body != null)
		{
			Visit(_fnDecl.Body);

			if (!_returnSeen)
			{
				if (FnValue.IsMain && FnValue.ReturnType.KnownTypeTag == KnownTypeTag.@void)
				{
					LLVM.Builder.BuildRet(LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0));
				}
				else
				{
					LLVM.Builder.BuildRetVoid();
				}
			}
		}
		else
		{
			Debug.Assert(
				FnValue.AttrsList.TryGetIntrinsic(out var intrinsicAttr));
			GenerateIntrinsic(intrinsicAttr.Name);
		}

		if (!_llvmFnUnit.Value.VerifyFunction(LLVMVerifierFailureAction.LLVMPrintMessageAction))
		{
			// TODO: Log? LLVM prints the problem on the console anyway.
			//if (Compiler.VerificationEnabled)
			//throw new Exception("LLVM function verification failed.");
		}

		//LLVM.PassManager.RunFunctionPassManager(_llvmFnUnit.Value);
	}

	private void GenerateIntrinsic(string name)
	{
		switch (name)
		{
			case "memory_ptr_add_offset":
				GenerateIntrinsic_memory_ptr_add_offset();
				break;
			default:
				throw new UnreachableException();
		}
	}

	private void GenerateIntrinsic_memory_ptr_add_offset()
	{
		var ptrUnit = _paramUnits[0];
		var ptrType = (HirPtrType)FnValue.Params[0].Type;
		Debug.Assert(ptrType.InnerType != null);
		var ptrInnerType = LLVMTarget.LowerType(ptrType.InnerType);
		var offsetUnit = _paramUnits[1];

		var gep = LLVM.Builder.BuildInBoundsGEP2(
			ptrInnerType,
			ptrUnit.Value,
			[offsetUnit.Unwrap(LLVM).Value]);
		//[LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, 0), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, 2)]);

		// For arrays
		//[LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0), offsetUnit.Unwrap(LLVM).Value]);

		LLVM.Builder.BuildRet(gep);
	}

	private void SetupParams()
	{
		// REVIEW:
		// FnSymbol.Params doesn't include receivers, FnSymbol.Type.ParamTypes includes receivers.
		// Document this or is there a better way?

		IEnumerable<(HirType Type, HirValue Value)> normalParams = [.. FnValue.Params.Select(x => (x.Type, x))];
		var overallParams = normalParams;

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
			var paramName = t.Value.Name ?? string.Empty;
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

						CreateParamUnit(t.Value, unit);
					}
					break;
				case LLVMTypeLoweringKind.ByCopyRef:
					{
						// paramValue is already a ptr. Treat it as the wrapper ptr directly.
						var alignment = Target.GetAbiAlignment(t.Type);
						Debug.Assert(paramValue.TypeOf.Kind == LLVMTypeKind.LLVMPointerTypeKind, "Param value should already be a ptr.");
						var unit = LLVMUnit.Wrapped(llvmType, paramValue);
						CreateParamUnit(t.Value, unit);
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

						CreateParamUnit(t.Value, unit);
					}
					break;
			}

			llvmParamOffset++;
		}
	}

	public override void VisitLocalDeclareStmt(HirLocalDeclareStmt node)
	{
		// These are usually hoisted to the top of the body by the lowerer.
		// Having these created in the entry basic block is important to enable certain LLVM optimizations.
		CreateLocalUnit(node.Value);
	}

	public override void VisitExprStmt(HirExprStmt node)
	{
		Visit(node.Expr);

		// The expr will always push a unit to the stack (even void fns).
		// An ExprStmt marks the end of an expr pipeline, so we simply always discard that last value here.
		_stack.Pop();
	}

	public override void VisitReturnStmt(HirReturnStmt node)
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
			LLVMTarget.CompileFnReturnStmt(FnValue.ReturnType, exprUnit, sretPtr);
		}
	}

	public override void VisitCallExpr(HirCallExpr node)
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

	// The reason we don't need to implement block expr to avoid pushing a value when there's no
	// block return is because HirBlockExpr only enumerates the expr if it exists and it's a valid
	// return. If it didn't have one no unit is pushed. If it had an fn call returning void it
	// wouldn't pass type check.

	public override void VisitMatchExpr(HirMatchExpr node)
	{
		var loweredType = LLVMTarget.LowerType(node.Type);
		// If matchResultUnit is null, match has no block return.
		var matchResultUnit = node.Type.KnownTypeTag is KnownTypeTag.@void ?
			null :
			LLVMUnit.Wrapped(loweredType, LLVM.Builder.BuildAlloca(loweredType, "match_result"));

		var matchEndBlock = AddBasicBlock("match_end");
		var thenBlockName = "brt";
		var elseBlockName = "bre";

		// Compile each arm, putting result in matchResult if needed.
		for (var i = 0; i < node.Arms.Length; i++)
		{
			var arm = node.Arms[i];
			var nextArm = i + 1 >= node.Arms.Length ? null : node.Arms[i + 1];
			var condition = arm.Condition;
			var block = arm.Block;

			if (condition != null)
			{
				Visit(condition);
				var conditionType = condition.Type;
				Debug.Assert(conditionType.KnownTypeTag is KnownTypeTag.@bool);
				var conditionResult = _stack.Pop();
				var cmpResult = LLVM.Builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, conditionResult.Value, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 1));
				var brThen = AddBasicBlock(thenBlockName);
				var brElse = nextArm == null ? matchEndBlock : AddBasicBlock(elseBlockName);
				LLVM.Builder.BuildCondBr(cmpResult, brThen, brElse);

				// Generate then block.
				SeekToBasicBlock(brThen);
				GenerateThen();
				LLVM.Builder.BuildBr(matchEndBlock);

				// Seek to else, will be filled in the next iteration.
				SeekToBasicBlock(brElse);
			}
			else
			{
				GenerateThen();
				LLVM.Builder.BuildBr(matchEndBlock);
				SeekToBasicBlock(matchEndBlock);
			}

			void GenerateThen()
			{
				Visit(block);
				// block.Type could still be void in case of terminators.
				if (matchResultUnit != null && block.Type.KnownTypeTag is not KnownTypeTag.@void)
				{
					var rhsUnit = _stack.Pop();
					GenerateValueStore(matchResultUnit, node.Type, rhsUnit, node.Type);
				}
			}
		}

		if (matchResultUnit != null)
		{
			_stack.Push(matchResultUnit);
		}
	}

	private LLVMBasicBlockRef EnterBasicBlock(string name)
	{
		var block = _llvmFnUnit.Value.AppendBasicBlock(name);
		LLVM.Builder.PositionAtEnd(block);
		return block;
	}

	private LLVMBasicBlockRef AddBasicBlock(string name)
	{
		return _llvmFnUnit.Value.AppendBasicBlock(name);
	}

	private void SeekToBasicBlock(LLVMBasicBlockRef block)
	{
		LLVM.Builder.PositionAtEnd(block);
	}

	public override void VisitUnaryExpr(HirUnaryExpr node)
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
			if (node.Type.KnownTypeTag.IsNumericInteger())
			{
				return LLVM.Builder.BuildNeg(value);
			}
			else
			{
				return LLVM.Builder.BuildFNeg(value);
			}
		}
	}

	private void DoAddressOf(HirType type, LLVMUnit unit)
	{
		var llvmType = LLVMTarget.LowerType(type);
		// Taking an address of something is only valid with lvalues. This means the unit we're taking an address of is always wrapped.
		Debug.Assert(unit.IsWrapped);
		// Address results are always unwrapped (i.e. literal units).
		_stack.Push(LLVMUnit.Unwrapped(llvmType, unit.Value));
	}

	public void DoDeref(HirType type, LLVMUnit unit)
	{
		var llvmType = LLVMTarget.LowerType(type);
		// Derefing something is only valid on wrapped units.
		Debug.Assert(unit.IsWrapped);
		// Deref results are always wrapped (i.e. not literal units).
		_stack.Push(LLVMUnit.Wrapped(llvmType, unit.Unwrap(LLVM).Value));
		//var load = LLVM.Builder.BuildLoad2(unit.Type, unit.Value);
		//_stack.Push(LLVMUnit.Wrapped(llvmType, load));
	}

	public override void VisitBinaryExpr(HirBinaryExpr node)
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
			if (node.Type.KnownTypeTag.IsNumericInteger())
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
			if (node.Type.KnownTypeTag.IsNumericInteger())
			{
				return LLVM.Builder.BuildSub(leftValue, rightValue);
			}
			else
			{
				return LLVM.Builder.BuildFSub(leftValue, rightValue);
			}
		}
	}

	public override void VisitValueLoadExpr(HirValueLoadExpr node)
	{
		// Units tied to a value are always wrapped.
		var unit = GetUnit(node.Value);
		// We don't load it, but give that option to the consumer of this unit.
		// Some consumers will want to simply load it and deal with the value, but others will need access to the ptr.
		_stack.Push(unit);
	}

	public override void VisitValueStoreExpr(HirValueStoreStmt node)
	{
		var destUnit = GetUnit(node.Value);
		Visit(node.Expr);
		var rhsUnit = _stack.Pop();

		var leftType = node.Value.Type;
		var rightType = node.Expr.Type;

		GenerateValueStore(destUnit, leftType, rhsUnit, rightType);
	}

	private void GenerateValueStore(LLVMUnit destUnit, HirType destType, LLVMUnit toUnit, HirType toType)
	{
		var (_, value) = destUnit;

		// REVIEW: We want to call memcpy instead of store when it's a struct and rhs is wrapped.
		// This is better than loading into a struct value and then storing it back (effectively emulating memcpy). But even more importantly, this
		// is necessary for sret to be compiled properly.
		// Is this logic here good enough?
		// NOTE: This doesn't mean we'll memcpy the struct data for an actual struct ptr. An actual struct ptr will not have matched StructTypeSymbol. (it'll be a ptr symbol)
		if (destType is HirStructType && toType is HirStructType && toUnit.IsWrapped)
		{
			var (_, rhs) = toUnit;
			var size = Target.GetAbiSize(toType);
			LLVM.CallIntrinsic_memcpy_i64(value, rhs, size.Bytes);
		}
		else
		{
			var (_, rhs) = toUnit.Unwrap(LLVM);
			LLVM.Builder.BuildStore(rhs, value);
		}
	}

	public override void VisitFieldLoadExpr(HirFieldLoadExpr node)
	{
		Visit(node.Target);
		var (_, targetPtr) = _stack.Pop();
		var enclosingType = node.FieldValue.StructType;
		var llvmEnclosingType = LLVMTarget.LowerType(enclosingType);

		var structLayout = Target.GetStructLayout(node.FieldValue.StructType);
		var fieldSection = structLayout.Sections.First(s => s.FieldIndex == node.FieldValue.FieldIndex);

		var ptrToField = LLVM.Builder.BuildStructGEP2(llvmEnclosingType, targetPtr, (uint)fieldSection.SectionIndex);
		var fieldLLVMType = LLVMTarget.LowerType(node.FieldValue.Type);
		// We don't load it, for the same reason outlined in VisitVariableLoadExpr.
		_stack.Push(LLVMUnit.Wrapped(fieldLLVMType, ptrToField));
	}

	public override void VisitFieldStoreExpr(HirFieldStoreStmt node)
	{
		Visit(node.Target);
		var (_, targetPtr) = _stack.Pop();
		var targetType = LLVMTarget.LowerType(node.FieldValue.StructType);

		Visit(node.Expr);
		var rhsUnit = _stack.Pop();

		var structLayout = Target.GetStructLayout(node.FieldValue.StructType);
		var fieldSection = structLayout.Sections.First(s => s.FieldIndex == node.FieldValue.FieldIndex);
		var fieldLLVMType = LLVMTarget.LowerType(node.FieldValue.Type);

		var ptrToField = LLVM.Builder.BuildStructGEP2(targetType, targetPtr, (uint)fieldSection.SectionIndex);

		var leftType = node.FieldValue.Type;
		var rightType = node.Expr.Type;

		// Same as in VisitVariableStoreExpr.
		if (leftType is HirStructType && rightType is HirStructType && rhsUnit.IsWrapped)
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

	public override void VisitLiteralExpr(HirLiteralExpr node)
	{
		switch (node.Data.Kind)
		{
			case LiteralDataKind.NumericInteger:
			case LiteralDataKind.NumericFloat:
				{
					if (node.Type.KnownTypeTag.IsNumericInteger())
					{
						var v = node.Data.GetNumericValueAsInteger();
						var llvmType = LLVMTarget.LowerType(node.Type);
						// TODO: Do sign and zero extension more properly?
						var signExtend = Target.GetIntegerSign(node.Type) == IntegerSign.Signed;
						var llvmValue = LLVMValueRef.CreateConstInt(llvmType, v, signExtend);
						_stack.Push(LLVMUnit.Unwrapped(llvmType, llvmValue));
					}
					else if (node.Type.KnownTypeTag.IsNumericFloat())
					{
						var v = node.Data.GetNumericValueAsFloat();
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

			case LiteralDataKind.Bool:
				{
					Debug.Assert(node.Data.TryGetBoolValue(out var v));
					var llvmType = LLVMTypeRef.Int1;
					var llvmValue = LLVMValueRef.CreateConstInt(llvmType, v ? (ulong)1 : 0);
					_stack.Push(LLVMUnit.Unwrapped(llvmType, llvmValue));
				}
				break;

			case LiteralDataKind.String:
				{
					Debug.Assert(node.Data.TryGetStringValue(out var v));
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

	private LLVMUnit GetUnit(HirValue value)
	{
		// FnSymbol's unit is lazily created below, whereas all other symbols should have been already created by the time this is called.

		if (_context.ValueToLLVMUnit.TryGetValue(value, out var llvmUnit))
		{
			return llvmUnit;
		}

		if (value is HirFnValue fnValue)
		{
			llvmUnit = CreateFnUnit(fnValue);
			_context.ValueToLLVMUnit[value] = llvmUnit;
			return llvmUnit;
		}

		throw new UnreachableException();
	}

	private LLVMUnit CreateLocalUnit(HirValue localValue)
	{
		var typeRef = LLVMTarget.LowerType(localValue.Type);
		var ptr = LLVM.Builder.BuildAlloca(typeRef, localValue.Name ?? string.Empty);
		var unit = LLVMUnit.Wrapped(typeRef, ptr);
		_context.ValueToLLVMUnit[localValue] = unit;
		return unit;
	}

	private void CreateParamUnit(HirValue paramValue, LLVMUnit unit)
	{
		_paramUnits.Add(unit);
		_context.ValueToLLVMUnit[paramValue] = unit;
	}

	private LLVMUnit CreateFnUnit(HirFnValue fnValue)
	{
		var (sret, attrs) = LLVMTarget.GetFnInfoFromFnType(fnValue.Type);
		var (llvmPtrType, llvmFnType) = LLVMTarget.LowerType(fnValue.Type);

		if (fnValue.IsMain && llvmFnType.ReturnType == LLVMTypeRef.Void)
		{
			// Rewrite to return i32.
			llvmFnType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32, llvmFnType.ParamTypes);
			// Do we need to rewrite llvmPtrType? It's an opaque ptr, so maybe not.
		}

		var name = fnValue.IsExtern || fnValue.IsMain ? fnValue.Name : fnValue.FullName;
		var llvmFnValue = LLVM.Module.AddFunction(name, llvmFnType);

		if (!fnValue.IsExtern && !fnValue.IsMain)
		{
			llvmFnValue.Linkage = LLVMLinkage.LLVMInternalLinkage;
		}

		// TODO: switch (cc) (C this is already the LLVM default if not set)
		llvmFnValue.SetFunctionCallConv(CallConv.C);

		attrs.Apply((index, attr) => llvmFnValue.AddAttributeAtIndex(index, attr));

		// Create an unwrapped unit out of direct fn values. This means direct calls to this fn value won't be loaded (in VisitCallExpr).
		return LLVMUnit.Unwrapped(llvmPtrType, llvmFnValue);
	}

	public LLVMTypeLowering GetLLVMLowering(HirType type)
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

	public bool IsReturnSRet(HirFnType fnType)
	{
		if (Target.GetAbiSize(fnType.ReturnType).IsZeroSized)
			return false;

		return IsReturnSRetCore(fnType);
	}

	public abstract bool IsReturnSRetCore(HirFnType fnType);

	// TODO: Support different calling conventions. Right now it's always Win64 cc.
	public LLVMTypeRef LowerFnReturnType(HirFnType fnType)
	{
		if (fnType.ReturnType.KnownTypeTag == KnownTypeTag.@void)
			return LLVMTypeRef.Void;

		Debug.Assert(!Target.GetAbiSize(fnType.ReturnType).IsZeroSized);

		return LowerFnReturnTypeCore(fnType);
	}

	protected abstract LLVMTypeRef LowerFnReturnTypeCore(HirFnType fnType);

	public void CompileFnReturnStmt(HirType returnType, LLVMUnit exprUnit, LLVMValueRef? sretOpt)
	{
		CompileFnReturnStmtCore(returnType, exprUnit, sretOpt);
	}

	protected abstract void CompileFnReturnStmtCore(HirType returnType, LLVMUnit exprUnit, LLVMValueRef? sretOpt);

	/// <summary>
	/// Lowers a type from Surab to LLVM.
	/// </summary>
	/// <param name="type">The Surab type to lower.</param>
	/// <returns>The lowered LLVM type.</returns>
	public LLVMTypeRef LowerType(HirType type)
	{
		if (Context.TypeToLLVMType.TryGetValue(type, out var llvmType))
		{
			return llvmType;
		}

		llvmType = LowerTypeCore(type);
		Context.TypeToLLVMType[type] = llvmType;
		return llvmType;
	}

	private LLVMTypeRef LowerTypeCore(HirType type)
	{
		if (type.Kind is HirTypeKind.GenericParam)
		{
			throw new Exception("Unexpected generic param type still present when lowering type to LLVM.");
		}

		var tag = type.KnownTypeTag;

		if (tag.IsNumericInteger())
		{
			var size = Target.GetAbiSize(type);
			return LLVMTypeRef.CreateInt((uint)size.Bits);
		}

		if (tag is KnownTypeTag.@void)
			return LLVMTypeRef.Void;
		if (tag is KnownTypeTag.@char or KnownTypeTag.@bool)
			return LLVMTypeRef.Int1;

		if (tag is KnownTypeTag.f32)
			return LLVMTypeRef.Float;
		if (tag is KnownTypeTag.f64)
			return LLVMTypeRef.Double;

		if (tag is KnownTypeTag.@string || type.Kind is HirTypeKind.Ptr)
			return LLVMTypeRef.CreatePointer(LLVMTypeRef.Void, 0);

		if (type is HirStructType structType)
		{
			var fields = structType.Fields;
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

		if (type is HirFnType fnType)
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
	public (LLVMTypeRef llvmType, LLVMTypeRef llvmActualType) LowerType(HirFnType fnType)
	{
		var (sret, _) = GetFnInfoFromFnType(fnType);
		var returnType = LowerFnReturnType(fnType);

		// TODO: Better way?
		var llvmIndex = 0;
		var capacity = fnType.ParamTypes.Count + 1; // Max length of llvm params.
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
	public LLVMFn GetFnInfoFromFnType(HirFnType fnType)
	{
		//Debug.Assert(fnType is HirFnType fnType);
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

	public LLVMTypeLowering GetLLVMLowering(HirType type)
	{
		if (Target.GetAbiSize(type).IsZeroSized)
			return LLVMTypeLowering.ZeroSized;

		return GetLLVMLoweringCore(type);
	}

	protected abstract LLVMTypeLowering GetLLVMLoweringCore(HirType type);
}

internal abstract class x86_64WindowsLLVMTarget(CompilationContext context) : LLVMTarget(context)
{
	protected new x86_64WindowsTarget Target => (x86_64WindowsTarget)base.Target;

	public override bool IsReturnSRetCore(HirFnType fnType)
	{
		return Target.Classify(fnType.ReturnType) == Classification.Memory;
	}

	protected override LLVMTypeRef LowerFnReturnTypeCore(HirFnType fnType)
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
	protected override void CompileFnReturnStmtCore(HirType returnType, LLVMUnit exprUnit, LLVMValueRef? sretOpt)
	{
		Debug.Assert(!Target.GetAbiSize(returnType).IsZeroSized);

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

	protected override LLVMTypeLowering GetLLVMLoweringCore(HirType type)
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
