using Surab.Analysis;
using Surab.Compilation.LLVM;

namespace Surab.Compilation;

public sealed class CompilationContext
{
    internal CompilationContext(
        SemanticModel model,
        LoweredTree tree,
        Target target,
        LLVMCompilationContext llvmCompilationContext,
        CompilationOptions options)
    {
        LoweredTree = tree;
        Model = model;
        Target = target;
        LLVM = llvmCompilationContext;
        Options = options;
    }

    internal LoweredTree LoweredTree { get; }

    public SemanticModel Model { get; }

    public Target Target { get; }

    internal LLVMCompilationContext LLVM { get; }

    public CompilationOptions Options { get; }

    internal Dictionary<Symbol, LLVMUnit> SymbolToValue { get; } = new();

    //internal Dictionary<Symbol>
}
