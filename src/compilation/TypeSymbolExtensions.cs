using System.Diagnostics;
using Surab.Analysis;

namespace Surab.Compilation;

public enum RuntimeTypeKind
{
	Invalid,
	ZeroSized,
	Numeric,
	Char,
	Bool,
	Struct,
	Ptr,
}

public static class TypeSymbolExtensions
{
	public static bool IsScalar(this TypeSymbol type)
	{
		return type.GetRuntimeKind() switch
		{
			RuntimeTypeKind.Numeric or RuntimeTypeKind.Char or RuntimeTypeKind.Bool or RuntimeTypeKind.Ptr => true,
			_ => false,
		};
	}

	public static RuntimeTypeKind GetRuntimeKind(this TypeSymbol type)
	{
		type = type.UnwrapAliases();
		var tag = type.KnownTypeTag;

		if (tag == KnownTypeTag.None) return RuntimeTypeKind.ZeroSized;
		if (tag == KnownTypeTag.@char) return RuntimeTypeKind.Char;
		if (tag == KnownTypeTag.@string || type is PtrTypeSymbol) return RuntimeTypeKind.Ptr;
		if (type.IsNumeric()) return RuntimeTypeKind.Numeric;
		if (tag == KnownTypeTag.@bool) return RuntimeTypeKind.Bool;
		if (type is StructTypeSymbol) return RuntimeTypeKind.Struct;
		if (type is FnTypeSymbol) return RuntimeTypeKind.Ptr;

		throw new UnreachableException();
	}
}
