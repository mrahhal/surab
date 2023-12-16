using LLVMSharp.Interop;
using LLVMInterop = LLVMSharp.Interop.LLVM;

namespace Surab.Compilation.LLVM;

internal sealed class LLVMCompilationContext(
	LLVMModuleRef module,
	LLVMBuilderRef builder,
	LLVMTargetMachineRef targetMachine,
	LLVMTargetDataRef targetData,
	LLVMPassManagerRef passManager)
{
	public LLVMModuleRef Module { get; } = module;

	public LLVMContextRef Context => Module.Context;

	public LLVMBuilderRef Builder { get; } = builder;

	public LLVMTargetMachineRef TargetMachine { get; } = targetMachine;

	public LLVMTargetDataRef TargetData { get; } = targetData;

	public LLVMPassManagerRef PassManager { get; } = passManager;

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
			// Create a function pass manager for this engine
			var passManager = module.CreateFunctionPassManager();

			// Set up the optimizer pipeline.  Start with registering info about how the
			// target lays out data structures.
			// LLVM.DisposeTargetData(LLVM.GetExecutionEngineTargetData(engine));

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

			passManager.InitializeFunctionPassManager();

			return passManager;
		}
	}

	public void Verify()
	{
		Module.Verify(LLVMVerifierFailureAction.LLVMPrintMessageAction);
	}
}
