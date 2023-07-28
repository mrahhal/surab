using LLVMSharp.Interop;

namespace Surab.Compilation.LLVM;

internal sealed class LLVMCompilationContext
{
    public LLVMCompilationContext(
        LLVMModuleRef module,
        LLVMBuilderRef builder,
        LLVMPassManagerRef passManager)
    {
        Module = module;
        Builder = builder;
        PassManager = passManager;
    }

    public LLVMModuleRef Module { get; }

    public LLVMContextRef Context => Module.Context;

    public LLVMBuilderRef Builder { get; }

    public LLVMPassManagerRef PassManager { get; }

    public string LLFile => "main.ll";

    public static LLVMCompilationContext Create()
    {
        var module = LLVMModuleRef.CreateWithName("surabmodule");
        var builder = module.Context.CreateBuilder();

        // TODO:
        module.Target = "x86_64-pc-windows-msvc19.20.0";

        var passManager = CreatePassManager();

        return new LLVMCompilationContext (module, builder, passManager);

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
