namespace Surab.Analysis;

/// <summary>
/// Describes additional characteristics on a resolved type.
/// Some characteristics pertain more to the value that the type corresponds to.
/// </summary>
[Flags]
public enum TypeCharacteristics
{
	Default = 0b0,

	/// <summary>
	/// Type is inferred as opposed to typed.
	/// </summary>
	Inferred = 0b_0001,
}

/// <summary>
/// Represents a resolved type.
/// </summary>
public struct ResolvedType
{
	private TypeCharacteristics _characteristics = TypeCharacteristics.Default;

	internal ResolvedType(TypeSymbol type)
	{
		Type = type;
	}

	/// <summary>
	/// Gets the resolved type symbol.
	/// </summary>
	public TypeSymbol Type { get; }

	public readonly bool IsInferred => HasCharacteristics(TypeCharacteristics.Inferred);

	internal void AddCharacteristics(TypeCharacteristics characteristics)
	{
		_characteristics |= characteristics;
	}

	public readonly bool HasCharacteristics(TypeCharacteristics characteristics)
	{
		return (_characteristics & characteristics) != 0;
	}

	public static ResolvedType Typed(TypeSymbol type)
	{
		return new ResolvedType(type);
	}

	public static ResolvedType Inferred(TypeSymbol type)
	{
		var r = new ResolvedType(type);
		r.AddCharacteristics(TypeCharacteristics.Inferred);
		return r;
	}
}
