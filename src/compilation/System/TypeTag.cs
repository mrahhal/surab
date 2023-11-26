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
        if (type == BuiltinTypeSystem.Char) return TypeTag.Char;
        if (type == BuiltinTypeSystem.Number) return TypeTag.Number;
        if (type == BuiltinTypeSystem.Bool) return TypeTag.Bool;
        if (type is StructTypeSymbol) return TypeTag.Struct;

        throw new UnreachableException();
    }
}
