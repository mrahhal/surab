using System.Diagnostics;
using System.Runtime.InteropServices;

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

		if (string.IsNullOrEmpty(targetTriple))
		{
			// TODO: temp
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				targetTriple = "x86_64-pc-windows-msvc";
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				targetTriple = "x86_64-pc-linux-gnu";
			}
			Console.WriteLine($"Setting target triple to: {targetTriple}");
		}

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
			if (!_context.LLVM.Module.Verify(out var messages))
			{
				throw new Exception("Verification failed: " + messages);
			}
		}

		// TODO:
		// - Use debug vs release libs depending on a compilation flag.
		// - Add -O3 for release?

		var wd = Directory.GetCurrentDirectory();

		if (!_context.LLVM.Module.PrintToFile(_context.Options.LLFileName, out var printError))
		{
			throw new Exception("Error while printing to file: " + printError);
		}

		if (!_context.LLVM.Module.EmitObjectFile(
			_context.LLVM.TargetMachine, SCPPOptLevel.O0,
			Path.Combine(wd, _context.Options.ObjectFileName), out var emitError))
		{
			throw new Exception("Error while compiling to object file: " + emitError);
		}

		return LinkerRunner.Run(_context.LLVM.Triple, [_context.Options.ObjectFileName]);
	}

	private static Target ParseTargetTriple(string targetTriple)
	{
		// TODO
		if (targetTriple == "x86_64-pc-windows-msvc")
		{
			return new x86_64WindowsMSVCTarget();
		}
		else if (targetTriple == "x86_64-pc-linux-gnu")
		{
			return new x86_64LinuxGNUTarget();
		}
		else
		{
			throw new UnreachableException();
		}
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
		Debug.Assert(_llvmFnUnit.Value.GetBasicBlocksCount() == 0);
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
					LLVM.Builder.BuildRet(SCPPValueRef.CreateConstInt(SCPPTypeRef.Int32, 0));
				}
				else
				{
					LLVM.Builder.BuildRetVoid();
				}
			}
		}
		else
		{
			if (!FnValue.AttrsList.TryGetIntrinsic(out var intrinsicAttr))
			{
				throw new Exception("Unexpected");
			}
			GenerateIntrinsic(intrinsicAttr.Name);
		}

		if (!SCPP.VerifyFunction(_llvmFnUnit.Value, out var error))
		{
			// TODO: Log? LLVM prints the problem on the console anyway.
			if (Compiler.VerificationEnabled)
				throw new Exception("LLVM function verification failed: " + error);
		}
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

		var gep = LLVM.Builder.BuildInBoundsGEP(
			ptrInnerType,
			ptrUnit.Value,
			[offsetUnit.Unwrap(LLVM).Value]);
		//[SCPPValueRef.CreateConstInt(SCPPTypeRef.Int64, 0), SCPPValueRef.CreateConstInt(SCPPTypeRef.Int64, 2)]);

		// For arrays
		//[SCPPValueRef.CreateConstInt(SCPPTypeRef.Int32, 0), offsetUnit.Unwrap(LLVM).Value]);

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
			var paramValue = _llvmFnUnit.Value.GetParam(llvmParamOffset);
			var paramType = t.Type;
			var paramName = t.Value.Name ?? string.Empty;
			var llvmType = LLVMTarget.LowerType(paramType);

			// Adaptations of params inside fn body
			// ---

			var info = LLVMTarget.ComputeInfo(t.Type, isReturnType: false);
			switch (info)
			{
				case DirectABIArgInfo:
					{
						var alignment = Target.GetAlignment(paramType);

						var ptr = LLVM.Builder.BuildAlloca(llvmType, paramName);
						ptr.SetAlignment(alignment.Bytes);
						LLVM.Builder.BuildStore(paramValue, ptr);
						var unit = LLVMUnit.Wrapped(llvmType, ptr);

						CreateParamUnit(t.Value, unit);
					}
					break;
				case IndirectABIArgInfo:
					{
						// paramValue is already a ptr. Treat it as the wrapper ptr directly.
						var alignment = Target.GetAlignment(t.Type);
						Debug.Assert(paramValue.TypeOf.Kind is SCPPTypeKind.pointer, "Param value should already be a ptr.");
						var unit = LLVMUnit.Wrapped(llvmType, paramValue);
						CreateParamUnit(t.Value, unit);
					}
					break;
				case AsIntegerABIArgInfo:
					{
						var layout = Target.GetLayout(t.Type);
						var llvmIntType = SCPPTypeRef.CreateInt(layout.Size.Bits);
						// TODO: We don't use GetAbiAlignmentOfType in other places, why do we have it here?
						var alignment = Math.Max(layout.Alignment.Bytes, LLVM.TargetMachine.GetAbiAlignmentOfType(llvmIntType)).AsBytes();

						var ptr = LLVM.Builder.BuildAlloca(llvmType, paramName);
						ptr.SetAlignment(alignment.Bytes);
						LLVM.Builder.BuildStore(paramValue, ptr);
						var unit = LLVMUnit.Wrapped(llvmType, ptr);

						CreateParamUnit(t.Value, unit);
					}
					break;
				case AsRealABIArgInfo:
					{
						var layout = Target.GetLayout(t.Type);
						// TODO: Refactor this mapping into a place.
						var llvmRealType = layout.Size.Bits switch
						{
							32 => SCPPTypeRef.Float,
							64 => SCPPTypeRef.Double,
							_ => throw new UnreachableException(),
						};

						// TODO: Same as in AsInteger, do we need GEP or memcpy instead here?
						var ptr = LLVM.Builder.BuildAlloca(llvmType, paramName);
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
			SCPPValueRef? sretPtr = null;
			if (_llvmFn.SRet.HasValue)
			{
				sretPtr = _llvmFnUnit.Value.GetParam(0);
			}
			CompileFnReturnStmt(FnValue.ReturnType, exprUnit, sretPtr);
		}
	}

	private void CompileFnReturnStmt(HirType returnType, LLVMUnit exprUnit, SCPPValueRef? sretOpt)
	{
		var info = LLVMTarget.ComputeInfo(returnType, isReturnType: true);

		if (info is IgnoreABIArgInfo)
		{
			return;
		}

		if (info is DirectABIArgInfo)
		{
			var (_, value) = exprUnit.Unwrap(LLVM);
			LLVM.Builder.BuildRet(value);
			return;
		}

		if (info is IndirectABIArgInfo)
		{
			// sret
			Debug.Assert(sretOpt.HasValue);
			var sretPtr = sretOpt.Value;
			Debug.Assert(sretPtr.TypeOf.Kind is SCPPTypeKind.pointer);
			var (_, exprValue) = exprUnit.Unwrap(LLVM);
			LLVM.Builder.BuildStore(exprValue, sretPtr);
			LLVM.Builder.BuildRetVoid();
			return;
		}

		if (info is AsIntegerABIArgInfo)
		{
			// abi int
			Debug.Assert(exprUnit.IsWrapped);
			var (_, exprValuePtr) = exprUnit;
			var layout = Target.GetLayout(returnType);
			var llvmIntType = SCPPTypeRef.CreateInt(layout.Size.Bits);
			var loaded = LLVM.Builder.BuildLoad(llvmIntType, exprValuePtr);
			loaded.SetAlignment(layout.Alignment.Bytes);
			LLVM.Builder.BuildRet(loaded);
			return;
		}

		if (info is AsRealABIArgInfo)
		{
			// abi real
			Debug.Assert(exprUnit.IsWrapped);
			var (_, exprValuePtr) = exprUnit;
			var layout = Target.GetLayout(returnType);
			var llvmRealType = layout.Size.Bits switch
			{
				32 => SCPPTypeRef.Float,
				64 => SCPPTypeRef.Double,
				_ => throw new UnreachableException(),
			};
			var loaded = LLVM.Builder.BuildLoad(llvmRealType, exprValuePtr);
			loaded.SetAlignment(layout.Alignment.Bytes);
			LLVM.Builder.BuildRet(loaded);
			return;
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
		var llvmArgCount = llvmFnType.CountParamTypes();
		var args = new List<SCPPValueRef>(llvmArgCount);

		if (sret.HasValue)
		{
			var (size, alignment) = Target.GetLayout(fnType.ReturnType);

			var sretAlloc = LLVM.Builder.BuildAlloca(sret.Value);
			sretAlloc.SetAlignment(alignment.Bytes);
			args.Add(sretAlloc);

			llvmIndex++;
		}

		while (llvmIndex < llvmArgCount)
		{
			var argUnit = _stack.Pop();

			// Adaptations of args before fn call
			// ---

			var paramType = fnType.ParamTypes[argOffset];
			var info = LLVMTarget.ComputeInfo(paramType, isReturnType: false);
			switch (info)
			{
				case DirectABIArgInfo:
					{
						var (_, argValue) = argUnit.Unwrap(LLVM);
						args.Add(argValue);
					}
					break;
				case IndirectABIArgInfo:
					{
						var llvmParamType = LLVMTarget.LowerType(paramType);
						// Copy the value into a new ptr to preserve *pass by copy* semantics.
						var copyPtr = LLVM.Builder.BuildAlloca(llvmParamType);
						var (_, argValue) = argUnit.Unwrap(LLVM);
						LLVM.Builder.BuildStore(argValue, copyPtr);
						args.Add(copyPtr);
					}
					break;
				case AsIntegerABIArgInfo:
					{
						// Might not be right, so this assertion is to detect a case where this isn't
						// true so we can look into it when it happens.
						Debug.Assert(argUnit.IsWrapped);
						var (_, argValuePtr) = argUnit;
						var layout = Target.GetLayout(paramType);
						var llvmIntType = SCPPTypeRef.CreateInt(layout.Size.Bits);
						var loaded = LLVM.Builder.BuildLoad(llvmIntType, argValuePtr);
						// TODO: In SetupParams, we're computing the max between this and the llvm type alignment.
						// Which one is right?
						loaded.SetAlignment(layout.Alignment.Bytes);
						args.Add(loaded);
					}
					break;
				case AsRealABIArgInfo:
					{
						Debug.Assert(argUnit.IsWrapped);
						var (_, argValuePtr) = argUnit;
						var layout = Target.GetLayout(paramType);
						var llvmRealType = layout.Size.Bits switch
						{
							32 => SCPPTypeRef.Float,
							64 => SCPPTypeRef.Double,
							_ => throw new UnreachableException(),
						};
						var loaded = LLVM.Builder.BuildLoad(llvmRealType, argValuePtr);
						loaded.SetAlignment(layout.Alignment.Bytes);
						args.Add(loaded);
					}
					break;
			}

			argOffset++;
			llvmIndex++;
		}

		var returnValue = LLVM.Builder.BuildCallWithType(llvmFnType, llvmFnValue, [.. args]);
		attrs.Apply(returnValue.AddCallSiteAttribute);

		if (sret.HasValue)
		{
			// Adaptation for sret. Load the sret ptr and push to the stack to simulate a return value.
			var sretPtr = args[0];
			_stack.Push(LLVMUnit.Wrapped(sret.Value, sretPtr));
		}
		else
		{
			// Always push a value, even if void, since this gets popped in VisitExprStmt.
			_stack.Push(LLVMUnit.Unwrapped(llvmFnType.GetFunctionReturnType(), returnValue));
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
				var cmpResult = LLVM.Builder.BuildICmp(ICmpPredicate.ICMP_EQ, conditionResult.Value, SCPPValueRef.CreateConstInt(SCPPTypeRef.Int1, 1));
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

	private SCPPBasicBlockRef EnterBasicBlock(string name)
	{
		var block = _llvmFnUnit.Value.AppendBasicBlock(name);
		LLVM.Builder.PositionAtEnd(block);
		return block;
	}

	private SCPPBasicBlockRef AddBasicBlock(string name)
	{
		return _llvmFnUnit.Value.AppendBasicBlock(name);
	}

	private void SeekToBasicBlock(SCPPBasicBlockRef block)
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
			UnaryOperatorTokenKind.Not => LLVM.Builder.BuildICmp(ICmpPredicate.ICMP_EQ, value, SCPPValueRef.CreateConstInt(SCPPTypeRef.Int1, 0)),
			_ => throw new NotImplementedException(),
		};
		// TODO:
		_stack.Push(LLVMUnit.Unwrapped(SCPPTypeRef.Double, newValue));

		SCPPValueRef BuildNeg()
		{
			if (node.Type.KnownTypeTag.IsNumericInteger())
			{
				return LLVM.Builder.BuildUnaryOp(SCPPUnaryOp.neg, value);
			}
			else
			{
				return LLVM.Builder.BuildUnaryOp(SCPPUnaryOp.fneg, value);
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
		_stack.Push(LLVMUnit.Unwrapped(SCPPTypeRef.Double, newValue));

		SCPPValueRef BuildAdd()
		{
			if (node.Type.KnownTypeTag.IsNumericInteger())
			{
				return LLVM.Builder.BuildBinaryOp(SCPPBinaryOp.add, leftValue, rightValue);
			}
			else
			{
				return LLVM.Builder.BuildBinaryOp(SCPPBinaryOp.fadd, leftValue, rightValue);
			}
		}

		SCPPValueRef BuildSub()
		{
			if (node.Type.KnownTypeTag.IsNumericInteger())
			{
				return LLVM.Builder.BuildBinaryOp(SCPPBinaryOp.sub, leftValue, rightValue);
			}
			else
			{
				return LLVM.Builder.BuildBinaryOp(SCPPBinaryOp.fsub, leftValue, rightValue);
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
			var size = Target.GetSize(toType);
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

		var ptrToField = LLVM.Builder.BuildStructGEP(llvmEnclosingType, targetPtr, fieldSection.SectionIndex);
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

		var ptrToField = LLVM.Builder.BuildStructGEP(targetType, targetPtr, fieldSection.SectionIndex);

		var leftType = node.FieldValue.Type;
		var rightType = node.Expr.Type;

		// Same as in VisitVariableStoreExpr.
		if (leftType is HirStructType && rightType is HirStructType && rhsUnit.IsWrapped)
		{
			var (_, rhs) = rhsUnit;
			var size = Target.GetSize(node.Expr.Type);
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
						var llvmValue = SCPPValueRef.CreateConstInt(llvmType, v, signExtend);
						_stack.Push(LLVMUnit.Unwrapped(llvmType, llvmValue));
					}
					else if (node.Type.KnownTypeTag.IsNumericFloat())
					{
						var v = node.Data.GetNumericValueAsFloat();
						var llvmType = LLVMTarget.LowerType(node.Type);
						var llvmValue = SCPPValueRef.CreateConstReal(llvmType, v);
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
					if (!node.Data.TryGetBoolValue(out var v))
					{
						throw new Exception("Unexpected");
					}
					var llvmType = SCPPTypeRef.Int1;
					var llvmValue = SCPPValueRef.CreateConstInt(llvmType, v ? (ulong)1 : 0);
					_stack.Push(LLVMUnit.Unwrapped(llvmType, llvmValue));
				}
				break;

			case LiteralDataKind.String:
				{
					if (!node.Data.TryGetStringValue(out var v))
					{
						throw new Exception("Unexpected");
					}
					var llvmType = SCPPTypeRef.CreatePointer(SCPPTypeRef.Void, 0);
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

		if (fnValue.IsMain && llvmFnType.GetFunctionReturnType() == SCPPTypeRef.Void)
		{
			// Rewrite to return i32.
			llvmFnType = SCPPTypeRef.CreateFunctionType(SCPPTypeRef.Int32, llvmFnType.GetParamTypes());
			// Do we need to rewrite llvmPtrType? It's an opaque ptr, so maybe not.
		}

		var name = fnValue.IsExtern || fnValue.IsMain ? fnValue.Name : fnValue.FullName;
		var llvmFnValue = LLVM.Module.AddFunction(name, llvmFnType);

		if (!fnValue.IsExtern && !fnValue.IsMain)
		{
			llvmFnValue.SetLinkage(SCPPLinkage.@internal);
		}

		// TODO: switch (cc) (C this is already the LLVM default if not set)
		// Unless overriden in a function attribute:
		// - If target platform is windows x64 => win64
		// - If target platform is linux x64 => c
		// Note: c == win64 if host platform is windows x64
		llvmFnValue.SetFunctionCallConv(CallConv.C);

		attrs.Apply(llvmFnValue.AddAttributeAtIndex);

		// Create an unwrapped unit out of direct fn values. This means direct calls to this fn value won't be loaded (in VisitCallExpr).
		return LLVMUnit.Unwrapped(llvmPtrType, llvmFnValue);
	}
}
