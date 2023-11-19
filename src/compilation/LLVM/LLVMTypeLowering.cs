namespace Surab.Compilation.LLVM;

internal enum LLVMTypeLoweringKind
{
    ZeroSized,
    ByValue,
    //ByRef,
    ByMutRef,
    AbiInt,
}

internal readonly struct LLVMTypeLowering
{
    public required LLVMTypeLoweringKind Kind { get; init; }

    public static readonly LLVMTypeLowering ZeroSized = new() { Kind = LLVMTypeLoweringKind.ZeroSized };

    public static readonly LLVMTypeLowering ByValue = new() { Kind = LLVMTypeLoweringKind.ByValue };

    //public static readonly LLVMTypeLowering ByRef = new() { Kind = LLVMTypeLoweringKind.ByRef };

    public static readonly LLVMTypeLowering ByMutRef = new() { Kind = LLVMTypeLoweringKind.ByMutRef };

    public static readonly LLVMTypeLowering AbiInt = new() { Kind = LLVMTypeLoweringKind.AbiInt };
}
