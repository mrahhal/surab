using System.Diagnostics;

namespace Surab.Compilation;

internal static class Memory
{
    /// <summary>
    /// Aligns a memory address forward to the given alignment. If the address is already aligned it'll be returned as is.
    /// </summary>
    /// <param name="address">The address to align.</param>
    /// <param name="alignment">The alignment.</param>
    /// <returns>The aligned address.</returns>
    public static BitSize AlignForward(BitSize address, BitSize alignment)
    {
        return AlignBackward((address.Bytes + alignment.Bytes - 1).AsBytes(), alignment);
    }

    /// <summary>
    /// Aligns a memory address backward to the given alignment. If the address is already aligned it'll be returned as is.
    /// </summary>
    /// <param name="address">The address to align.</param>
    /// <param name="alignment">The alignment.</param>
    /// <returns>The aligned address.</returns>
    public static BitSize AlignBackward(BitSize address, BitSize alignment)
    {
        AssertValidAlignment(alignment);
        var alignedInBytes = address.Bytes & ~(alignment.Bytes - 1);
        return alignedInBytes.AsBytes();
    }

    private static void AssertValidAlignment(BitSize alignment)
    {
        Debug.Assert(MathHelper.IsPowerOfTwo(alignment.Bytes), "Alignment should be a power of two.");
    }
}
