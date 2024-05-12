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
	public static bool IsScalar(this HirType type)
	{
		return type.GetRuntimeKind() switch
		{
			RuntimeTypeKind.Numeric or RuntimeTypeKind.Char or RuntimeTypeKind.Bool or RuntimeTypeKind.Ptr => true,
			_ => false,
		};
	}

	public static RuntimeTypeKind GetRuntimeKind(this HirType type)
	{
		var tag = type.KnownTypeTag;

		if (tag == KnownTypeTag.None) return RuntimeTypeKind.ZeroSized;
		if (tag == KnownTypeTag.@char) return RuntimeTypeKind.Char;
		if (tag == KnownTypeTag.@string || type.Kind is HirTypeKind.Ptr or HirTypeKind.Fn) return RuntimeTypeKind.Ptr;
		if (type.KnownTypeTag.IsNumeric()) return RuntimeTypeKind.Numeric;
		if (tag == KnownTypeTag.@bool) return RuntimeTypeKind.Bool;
		if (type.Kind is HirTypeKind.Struct) return RuntimeTypeKind.Struct;

		throw new UnreachableException();
	}
}
