using LLVMSharp.Interop;
using Surab.Analysis;
using Surab.Compilation.LLVM;

namespace Surab.Compilation;

public sealed class CompilationContext
{
	internal CompilationContext(
		List<Reasoner> reasoners,
		Target target,
		LLVMCompilationContext llvmCompilationContext,
		CompilationOptions options)
	{
		Reasoners = reasoners;
		Target = target;
		LLVM = llvmCompilationContext;
		Options = options;

		LLVMTarget = LLVMTarget.Create(this);
	}

	public List<Reasoner> Reasoners { get; }

	public Target Target { get; }

	internal LLVMTarget LLVMTarget { get; }

	internal LLVMCompilationContext LLVM { get; }

	public CompilationOptions Options { get; }

	// TODO: Some of these might need to be moved to a reasoner context so that we can bust them as we rebuild reasoners on changes.
	// Also, some of these should not be here in the first place as they're tied to LLVM.

	internal Dictionary<Symbol, LLVMUnit> SymbolToLLVMUnit { get; } = [];

	internal Dictionary<TypeSymbol, LLVMTypeRef> TypeSymbolToLLVMType { get; } = [];
}
