namespace Surab.Compilation.LLVM;

internal enum LLVMTypeLoweringKind
{
    no_bits,
    byval,
    byref,
    byref_mut,
    abi_sized_int,
}

internal readonly struct LLVMTypeLowering
{
    public required LLVMTypeLoweringKind Kind { get; init; }

    public static readonly LLVMTypeLowering no_bits = new() { Kind = LLVMTypeLoweringKind.no_bits };

    public static readonly LLVMTypeLowering byval = new() { Kind = LLVMTypeLoweringKind.byval };

    public static readonly LLVMTypeLowering byref = new() { Kind = LLVMTypeLoweringKind.byref };

    public static readonly LLVMTypeLowering byref_mut = new() { Kind = LLVMTypeLoweringKind.byref_mut };

    public static readonly LLVMTypeLowering abi_sized_int = new() { Kind = LLVMTypeLoweringKind.abi_sized_int };
}
