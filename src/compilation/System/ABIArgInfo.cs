namespace Surab.Compilation;

/// <summary>
/// Represents ABI info for a return type or arg type on how to lower it to llvm
/// while respecting the target ABI.
/// </summary>
public abstract class ABIArgInfo
{
}

/// <summary>
/// Zero sized.
/// </summary>
public sealed class IgnoreABIArgInfo : ABIArgInfo
{
}

/// <summary>
/// Direct, by value. The arg should be passed as is by its value without any conversion.
/// </summary>
public sealed class DirectABIArgInfo : ABIArgInfo
{
}

/// <summary>
/// Indirect, by copy ref. The arg is originally passed as a copy but the abi is telling us to pass it by ref.
/// Mutations shouldn't be observed after the function we're going to call returns to preserve by copy semantics.
/// </summary>
public sealed class IndirectABIArgInfo : ABIArgInfo
{
}

/// <summary>
/// As an integer. The arg should be passed as a bitcasted integer of the same bit size.
/// </summary>
public sealed class AsIntegerABIArgInfo : ABIArgInfo
{
}

/// <summary>
/// As a float. The arg should be passed as a float (usually a transparent struct of a float or double).
/// </summary>
public sealed class AsRealABIArgInfo : ABIArgInfo
{
}
