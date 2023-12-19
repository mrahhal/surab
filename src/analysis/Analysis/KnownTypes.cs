namespace Surab.Analysis;

public static class KnownTypes
{
	internal static KnownTypeTag? GetTag(string? name)
	{
		return name switch
		{
			"None" => (KnownTypeTag?)KnownTypeTag.None,
			"i32" => KnownTypeTag.i32,
			"i64" => KnownTypeTag.i64,
			"u32" => KnownTypeTag.u32,
			"u64" => KnownTypeTag.u64,
			"f32" => KnownTypeTag.f32,
			"f64" => KnownTypeTag.f64,
			"bool" => KnownTypeTag.@bool,
			"char" => KnownTypeTag.@char,
			"string" => KnownTypeTag.@string,
			_ => null,
		};
	}

	public static bool IsBuiltin(this TypeSymbol? type) => type?.KnownTypeTag.IsBuiltin() ?? false;
	public static bool IsBuiltin(this KnownTypeTag? tag)
	{
		return tag is
			KnownTypeTag.None or
			KnownTypeTag.i32 or
			KnownTypeTag.i64 or
			KnownTypeTag.u32 or
			KnownTypeTag.u64 or
			KnownTypeTag.f32 or
			KnownTypeTag.f64 or
			KnownTypeTag.@bool or
			KnownTypeTag.@char or
			KnownTypeTag.@string;
	}

	public static bool IsNumeric(this TypeSymbol? type) => type?.KnownTypeTag.IsNumeric() ?? false;
	public static bool IsNumeric(this KnownTypeTag? tag)
	{
		return tag.IsNumericInteger() || tag.IsNumericFloat();
	}

	public static bool IsNumericInteger(this TypeSymbol? type) => type?.KnownTypeTag.IsNumericInteger() ?? false;
	public static bool IsNumericInteger(this KnownTypeTag? tag)
	{
		return tag is
			KnownTypeTag.i32 or
			KnownTypeTag.i64 or
			KnownTypeTag.u32 or
			KnownTypeTag.u64;
	}

	public static bool IsNumericFloat(this TypeSymbol? type) => type?.KnownTypeTag.IsNumericFloat() ?? false;
	public static bool IsNumericFloat(this KnownTypeTag? tag)
	{
		return tag is
			KnownTypeTag.f32 or
			KnownTypeTag.f64;
	}
}
