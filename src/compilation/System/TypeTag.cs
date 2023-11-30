using System.Diagnostics;
using Surab.Analysis;

namespace Surab.Compilation;

public enum TypeTag
{
    Number,
    Char,
    Bool,
    Struct,
}

public static class TypeTagExtensions
{
    public static TypeTag GetTag(this TypeSymbol type)
    {
        if (type == BuiltinTypeSystem.@char) return TypeTag.Char;
        if (BuiltinTypeSystem.IsNumber(type)) return TypeTag.Number;
        if (type == BuiltinTypeSystem.@bool) return TypeTag.Bool;
        if (type is StructTypeSymbol) return TypeTag.Struct;

        throw new UnreachableException();
    }
}
