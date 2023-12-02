using Surab.Analysis;
using Surab.Compilation.LLVM;

namespace Surab.Compilation;

public sealed class CompilationContext
{
    internal CompilationContext(
        List<SemanticModel> semanticModels,
        Target target,
        LLVMCompilationContext llvmCompilationContext,
        CompilationOptions options)
    {
        SemanticModels = semanticModels;
        Target = target;
        LLVM = llvmCompilationContext;
        Options = options;
    }

    public List<SemanticModel> SemanticModels { get; }

    public Target Target { get; }

    internal LLVMCompilationContext LLVM { get; }

    public CompilationOptions Options { get; }

    // TODO: Some of these might need to be moved to a semantic model context so that we can bust them.

    internal Dictionary<Symbol, LLVMUnit> SymbolToLLVMUnit { get; } = [];

    internal Dictionary<TypeSymbol, LLVMType> TypeSymbolToLLVMType { get; } = [];

    internal LLVMType GetOrCreateType(TypeSymbol typeSymbol)
    {
        // Cache LLVM lowered types.
        if (!TypeSymbolToLLVMType.TryGetValue(typeSymbol, out var llvmTypeUnit))
        {
            var llvmType = Target.LowerType(typeSymbol);
            llvmTypeUnit = new LLVMType(llvmType);
            TypeSymbolToLLVMType[typeSymbol] = llvmTypeUnit;
        }

        return llvmTypeUnit;
    }
}
