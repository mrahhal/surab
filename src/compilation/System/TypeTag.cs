using System.Diagnostics;
using Surab.Analysis;

namespace Surab.Compilation;

public enum TypeTag
{
    Numeric,
    Char,
    Bool,
    Struct,
    Ptr,
}

public static class TypeTagExtensions
{
    public static TypeTag GetTag(this TypeSymbol type)
    {
        if (type == BuiltinTypeSystem.@char) return TypeTag.Char;
        if (BuiltinTypeSystem.IsNumeric(type)) return TypeTag.Numeric;
        if (type == BuiltinTypeSystem.@bool) return TypeTag.Bool;
        if (type is StructTypeSymbol) return TypeTag.Struct;
        if (type is FnTypeSymbol) return TypeTag.Ptr;

        throw new UnreachableException();
    }
}
