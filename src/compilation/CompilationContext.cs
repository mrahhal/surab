using Surab.Analysis;
using Surab.Compilation.LLVM;

namespace Surab.Compilation;

public sealed class CompilationContext
{
	internal CompilationContext(
		SurabProject project,
		Target target,
		LLVMCompilationContext llvmCompilationContext,
		CompilationOptions options)
	{
		Project = project;
		Target = target;
		LLVM = llvmCompilationContext;
		Options = options;

		LLVMTarget = LLVMTarget.Create(this);
	}

	public SurabProject Project { get; }

	public Target Target { get; }

	internal LLVMTarget LLVMTarget { get; }

	internal LLVMCompilationContext LLVM { get; }

	public CompilationOptions Options { get; }

	// TODO: Some of these might need to be moved to a reasoner context so that we can bust them as we rebuild reasoners on changes.
	// Also, some of these should not be here in the first place as they're tied to LLVM.

	internal Dictionary<HirValue, LLVMUnit> ValueToLLVMUnit { get; } = [];

	internal Dictionary<HirType, SCPPTypeRef> TypeToLLVMType { get; } = [];
}
