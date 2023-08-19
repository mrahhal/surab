using System.Diagnostics;
using Surab.Analysis;

namespace Surab.Compilation;

internal enum TypeTag
{
    Number,
    Char,
    Bool,
}

internal static class TypeTagExtensions
{
    public static TypeTag GetTag(this TypeSymbol type)
    {
        if (type == BuiltInTypeSystem.Char) return TypeTag.Char;
        if (type == BuiltInTypeSystem.Number) return TypeTag.Number;
        if (type == BuiltInTypeSystem.Bool) return TypeTag.Bool;

        throw new UnreachableException();
    }
}
