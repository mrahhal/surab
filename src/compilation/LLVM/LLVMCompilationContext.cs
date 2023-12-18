using System.Diagnostics;
using LLVMSharp.Interop;
using LLVMInterop = LLVMSharp.Interop.LLVM;

namespace Surab.Compilation.LLVM;

internal enum LLVMIntrinsic
{
	memcpy_i64,
}

internal sealed class LLVMCompilationContext
{
	private readonly Dictionary<LLVMIntrinsic, LLVMUnit> _intrinsicToValue = [];

	private LLVMCompilationContext(
		LLVMModuleRef module,
		LLVMBuilderRef builder,
		LLVMTargetMachineRef targetMachine,
		LLVMTargetDataRef targetData,
		LLVMPassManagerRef passManager)
	{
		Module = module;
		Builder = builder;
		TargetMachine = targetMachine;
		TargetData = targetData;
		PassManager = passManager;

		DeclareIntrinsics();
	}

	public LLVMModuleRef Module { get; }

	public LLVMContextRef Context => Module.Context;

	public LLVMBuilderRef Builder { get; }

	public LLVMTargetMachineRef TargetMachine { get; }

	public LLVMTargetDataRef TargetData { get; }

	public LLVMPassManagerRef PassManager { get; }

	public static LLVMCompilationContext Create()
	{
		var module = LLVMModuleRef.CreateWithName("surabmodule");
		var builder = module.Context.CreateBuilder();

		LLVMInterop.InitializeX86Target();
		LLVMInterop.InitializeX86TargetInfo();
		LLVMInterop.InitializeX86TargetMC();
		LLVMInterop.InitializeX86AsmPrinter();
		LLVMInterop.InitializeX86AsmParser();

		// TODO:
		module.Target = "x86_64-pc-windows-msvc19.39.33218";
		var succeeded = LLVMTargetRef.TryGetTargetFromTriple(module.Target, out var targetRef, out var error);
		if (!succeeded)
		{
			throw new Exception(error);
		}

		var targetMachine = targetRef.CreateTargetMachine(
			module.Target, null!, null!,
			LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault, LLVMRelocMode.LLVMRelocDefault, LLVMCodeModel.LLVMCodeModelDefault);
		var targetData = targetMachine.CreateTargetDataLayout();

		var passManager = CreatePassManager();

		return new(module, builder, targetMachine, targetData, passManager);

		// ---

		LLVMPassManagerRef CreatePassManager()
		{
			var passManager = module.CreateFunctionPassManager();

			// Provide basic AliasAnalysis support for GVN.
			passManager.AddBasicAliasAnalysisPass();

			// Promote allocas to registers.
			passManager.AddPromoteMemoryToRegisterPass();

			// Do simple "peephole" optimizations and bit-twiddling optzns.
			passManager.AddInstructionCombiningPass();

			// Reassociate expressions.
			passManager.AddReassociatePass();

			// Eliminate Common SubExpressions.
			passManager.AddGVNPass();

			// Simplify the control flow graph (deleting unreachable blocks, etc).
			passManager.AddCFGSimplificationPass();

			//passManager.AddConstantMergePass();

			//passManager.AddCorrelatedValuePropagationPass();

			//passManager.AddDCEPass();

			//passManager.AddDeadArgEliminationPass();

			//passManager.AddDeadStoreEliminationPass();

			//passManager.AddDemoteMemoryToRegisterPass();

			//passManager.AddFunctionAttrsPass();

			//passManager.AddFunctionInliningPass();

			passManager.InitializeFunctionPassManager();

			return passManager;
		}
	}

	public void Verify()
	{
		Module.Verify(LLVMVerifierFailureAction.LLVMPrintMessageAction);
	}

	public void CallIntrinsic_memcpy_i64(LLVMValueRef dest, LLVMValueRef src, int length)
	{
		var (fnType, fnValue) = GetIntrinsic(LLVMIntrinsic.memcpy_i64);
		Builder.BuildCall2(
			fnType, fnValue,
			[dest, src, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, (ulong)length), LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 0)]);
	}

	private LLVMUnit GetIntrinsic(LLVMIntrinsic intrinsic)
	{
		return _intrinsicToValue[intrinsic];
	}

	private void DeclareIntrinsics()
	{
		Debug.Assert(_intrinsicToValue.Count == 0);

		var fnType = LLVMTypeRef.CreateFunction(
			LLVMTypeRef.Void,
			[LLVMTypeRef.CreatePointer(LLVMTypeRef.Void, 0), LLVMTypeRef.CreatePointer(LLVMTypeRef.Void, 0), LLVMTypeRef.Int64, LLVMTypeRef.Int1]);
		var fnValue = Module.AddFunction(
			"llvm.memcpy.p0.p0.i64",
			fnType);
		_intrinsicToValue[LLVMIntrinsic.memcpy_i64] = LLVMUnit.Unwrapped(fnType, fnValue);
	}
}
