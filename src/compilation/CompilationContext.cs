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

    // TODO: Some of these might need to be moved to a reasoner context so that we can bust them.

    internal Dictionary<Symbol, LLVMUnit> SymbolToLLVMUnit { get; } = [];

    internal Dictionary<TypeSymbol, LLVMType> TypeSymbolToLLVMType { get; } = [];

    internal LLVMType GetOrCreateType(TypeSymbol typeSymbol)
    {
        // Cache LLVM lowered types.
        if (!TypeSymbolToLLVMType.TryGetValue(typeSymbol, out var llvmTypeUnit))
        {
            var llvmType = LLVMTarget.LowerType(typeSymbol);
            llvmTypeUnit = new LLVMType(llvmType);
            TypeSymbolToLLVMType[typeSymbol] = llvmTypeUnit;
        }

        return llvmTypeUnit;
    }
}
