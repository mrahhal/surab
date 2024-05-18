namespace Surab.Analysis;

public static class KnownTypes
{
	internal static KnownTypeTag? GetTag(string? name)
	{
		return name switch
		{
			"None" => (KnownTypeTag?)KnownTypeTag.None,
			"i8" => KnownTypeTag.i8,
			"i16" => KnownTypeTag.i16,
			"i32" => KnownTypeTag.i32,
			"i64" => KnownTypeTag.i64,
			"isize" => KnownTypeTag.isize,
			"u8" => KnownTypeTag.u8,
			"u16" => KnownTypeTag.u16,
			"u32" => KnownTypeTag.u32,
			"u64" => KnownTypeTag.u64,
			"usize" => KnownTypeTag.usize,
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
			KnownTypeTag.i8 or
			KnownTypeTag.i16 or
			KnownTypeTag.i32 or
			KnownTypeTag.i64 or
			KnownTypeTag.isize or
			KnownTypeTag.u8 or
			KnownTypeTag.u16 or
			KnownTypeTag.u32 or
			KnownTypeTag.u64 or
			KnownTypeTag.usize or
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
			KnownTypeTag.i8 or
			KnownTypeTag.i16 or
			KnownTypeTag.i32 or
			KnownTypeTag.i64 or
			KnownTypeTag.isize or
			KnownTypeTag.u8 or
			KnownTypeTag.u16 or
			KnownTypeTag.u32 or
			KnownTypeTag.u64 or
			KnownTypeTag.usize;
	}

	public static bool IsNumericFloat(this TypeSymbol? type) => type?.KnownTypeTag.IsNumericFloat() ?? false;
	public static bool IsNumericFloat(this KnownTypeTag? tag)
	{
		return tag is
			KnownTypeTag.f32 or
			KnownTypeTag.f64;
	}
}
