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

    internal Dictionary<Symbol, LLVMUnit> SymbolToLLVMUnit { get; } = [];

    internal Dictionary<FnSymbol, LLVMFn> FnSymbolToLLVMFn { get; } = [];

    internal Dictionary<FnSymbol, LLVMFnAttrsDef> FnToAttrs { get; } = [];

    internal Dictionary<Symbol, LLVMType> TypeToLLVMType { get; } = [];

    internal LLVMType GetOrCreateType(TypeSymbol typeSymbol)
    {
        // Cache LLVM lowered types.
        if (!TypeToLLVMType.TryGetValue(typeSymbol, out var llvmTypeUnit))
        {
            var llvmType = Target.LowerType(typeSymbol);
            llvmTypeUnit = new LLVMType(llvmType);
            TypeToLLVMType[typeSymbol] = llvmTypeUnit;
        }

        return llvmTypeUnit;
    }
}
