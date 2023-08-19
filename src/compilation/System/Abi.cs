using System.Diagnostics;
using Surab.Analysis;

namespace Surab.Compilation;

internal enum Classification
{
    Integer,
    SSE,
    SSEUP,
    Memory,
}

internal static class Abi
{
    // https://learn.microsoft.com/en-us/cpp/build/x64-software-conventions
    public static Classification ClassifyTypeForWindows(TypeSymbol type, Target target)
    {
        var size = GetAbiSize(type, target);
        Debug.Assert(!size.IsZeroSized, "Classify shouldn't be called with zero sized types.");

        return type.GetTag() switch
        {
            TypeTag.Bool or
            TypeTag.Char
            => size.Bytes switch
            {
                1 or 2 or 4 or 8 => Classification.Integer,
                _ => Classification.Memory,
            },

            TypeTag.Number
            => Classification.SSE,

            _ => throw new UnreachableException(),
        };
    }

    public static BitSize GetAbiSize(TypeSymbol type, Target target)
    {
        return target.GetSize(type);
    }
}
