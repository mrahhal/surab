namespace Surab.Compilation.LLVM;

/// <summary>
/// The kind of an <see cref="LLVMTypeLowering"/>.
/// </summary>
internal enum LLVMTypeLoweringKind
{
	None,

	/// <summary>
	/// Zero sized.
	/// </summary>
	ZeroSized,
	/// <summary>
	/// By value. The arg should be passed as is by its value without any conversion.
	/// </summary>
	ByValue,
	/// <summary>
	/// By copy ref. The arg is originally passed as a copy but the abi is telling us to pass it by ref.
	/// Mutations shouldn't be observed after the function we're going to call returns to preserve by copy semantics.
	/// </summary>
	ByCopyRef,
	/// <summary>
	/// As an integer. The arg should be passed as a bitcasted integer of the same bit size.
	/// </summary>
	AsInt,
}

/// <summary>
/// Represents a certain conversion imposed by an ABI by which an arg should be passed.
/// </summary>
internal readonly struct LLVMTypeLowering
{
	public required LLVMTypeLoweringKind Kind { get; init; }

	public static readonly LLVMTypeLowering ZeroSized = new() { Kind = LLVMTypeLoweringKind.ZeroSized };

	public static readonly LLVMTypeLowering ByValue = new() { Kind = LLVMTypeLoweringKind.ByValue };

	public static readonly LLVMTypeLowering ByCopyRef = new() { Kind = LLVMTypeLoweringKind.ByCopyRef };

	public static readonly LLVMTypeLowering AsInt = new() { Kind = LLVMTypeLoweringKind.AsInt };
}
