using System.Diagnostics;
using Surab.Analysis;

namespace Surab.Compilation;

public enum TypeTag
{
    Number,
    Char,
    Bool,
}

public static class TypeTagExtensions
{
    public static TypeTag GetTag(this TypeSymbol type)
    {
        if (type == BuiltInTypeSystem.Char) return TypeTag.Char;
        if (type == BuiltInTypeSystem.Number) return TypeTag.Number;
        if (type == BuiltInTypeSystem.Bool) return TypeTag.Bool;

        throw new UnreachableException();
    }
}
