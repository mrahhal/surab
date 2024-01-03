namespace Surab.Compilation.LLVM;

internal enum LLVMTypeLoweringKind
{
	/// <summary>
	/// Zero sized lowering.
	/// </summary>
	ZeroSized,
	/// <summary>
	/// By value lowering. This means
	/// </summary>
	ByValue,
	/// <summary>
	/// By copy ref. This means the original arg is copied but we need to lower it to be by ref.
	/// So mutations shouldn't be observed after the function we're going to call returns to preserve
	/// by copy semantics.
	/// </summary>
	ByCopyRef,
	AbiInt,
}

internal readonly struct LLVMTypeLowering
{
	public required LLVMTypeLoweringKind Kind { get; init; }

	public static readonly LLVMTypeLowering ZeroSized = new() { Kind = LLVMTypeLoweringKind.ZeroSized };

	public static readonly LLVMTypeLowering ByValue = new() { Kind = LLVMTypeLoweringKind.ByValue };

	public static readonly LLVMTypeLowering ByCopyRef = new() { Kind = LLVMTypeLoweringKind.ByCopyRef };

	public static readonly LLVMTypeLowering AbiInt = new() { Kind = LLVMTypeLoweringKind.AbiInt };
}
