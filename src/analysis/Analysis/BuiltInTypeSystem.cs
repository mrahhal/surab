namespace Surab.Analysis;

// TODO: Shouldn't have a static type system here? We should have some kind of static type tag we can match on the symbol somehow?

/// <summary>
/// Contains builtin type system symbols.
/// </summary>
public static class BuiltinTypeSystem
{
	private static readonly HashSet<TypeSymbol> _builtinTypes = [];
	// TODO: Overkill. Do this better.
	private static readonly HashSet<StructTypeSymbol> _numericTypes = [];

	public static StructTypeSymbol None { get; private set; } = null!;

	public static StructTypeSymbol i32 { get; private set; } = null!;
	public static StructTypeSymbol i64 { get; private set; } = null!;
	public static StructTypeSymbol u32 { get; private set; } = null!;
	public static StructTypeSymbol u64 { get; private set; } = null!;
	public static StructTypeSymbol f32 { get; private set; } = null!;
	public static StructTypeSymbol f64 { get; private set; } = null!;

	public static StructTypeSymbol @bool { get; private set; } = null!;

	public static StructTypeSymbol @char { get; private set; } = null!;
	public static StructTypeSymbol @string { get; private set; } = null!;

	internal static void TryRegisterBuiltinSymbol(Symbol symbol)
	{
		if (symbol is StructTypeSymbol structTypeSymbol)
		{
			switch (structTypeSymbol.Name)
			{
				case "None":
					None = structTypeSymbol;
					_builtinTypes.Add(structTypeSymbol);
					break;

				case "i32":
					i32 = structTypeSymbol;
					_builtinTypes.Add(structTypeSymbol);
					_numericTypes.Add(structTypeSymbol);
					break;
				case "i64":
					i64 = structTypeSymbol;
					_builtinTypes.Add(structTypeSymbol);
					_numericTypes.Add(structTypeSymbol);
					break;
				case "u32":
					u32 = structTypeSymbol;
					_builtinTypes.Add(structTypeSymbol);
					_numericTypes.Add(structTypeSymbol);
					break;
				case "u64":
					u64 = structTypeSymbol;
					_builtinTypes.Add(structTypeSymbol);
					_numericTypes.Add(structTypeSymbol);
					break;
				case "f32":
					f32 = structTypeSymbol;
					_builtinTypes.Add(structTypeSymbol);
					_numericTypes.Add(structTypeSymbol);
					break;
				case "f64":
					f64 = structTypeSymbol;
					_builtinTypes.Add(structTypeSymbol);
					_numericTypes.Add(structTypeSymbol);
					break;

				case "bool":
					@bool = structTypeSymbol;
					_builtinTypes.Add(structTypeSymbol);
					break;

				case "char":
					@char = structTypeSymbol;
					_builtinTypes.Add(structTypeSymbol);
					break;
				case "string":
					@string = structTypeSymbol;
					_builtinTypes.Add(structTypeSymbol);
					break;

				default:
					break;
			}
		}
	}

	public static bool IsNumeric(TypeSymbol? type)
	{
		if (type == null) return false;
		return _numericTypes.Contains(type);
	}

	public static StructTypeSymbol? GetNumericTypeFromString(string? str)
	{
		if (str == null) return null;
		return _numericTypes.FirstOrDefault(t => t.Identifier?.Value == str);
	}

	public static bool IsNumericInteger(TypeSymbol? type)
	{
		if (type == null) return false;
		return type == i32 || type == i64 || type == u32 || type == u64;
	}

	public static bool IsNumericFloat(TypeSymbol? type)
	{
		if (type == null) return false;
		return type == f32 || type == f64;
	}

	public static bool IsBuiltin(TypeSymbol? type)
	{
		if (type == null) return false;
		return _builtinTypes.Contains(type);
	}
}
