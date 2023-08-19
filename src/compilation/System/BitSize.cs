namespace Surab.Compilation;

internal readonly struct BitSize
{
    private BitSize(int bits)
    {
        if (bits < 0) throw new ArgumentException("Size can't be negative.", nameof(bits));

        Bits = bits;
    }

    public int Bits { get; }

    public readonly int Bytes => Bits / 8;

    public readonly bool BytesSafe => Bits % 8 == 0;

    public readonly bool IsZeroSized => Bits == 0;

    public static BitSize FromBits(int bits)
    {
        return new BitSize(bits);
    }

    public static BitSize FromBytes(int bytes)
    {
        return new BitSize(bytes * 8);
    }

    public static BitSize operator +(BitSize x, BitSize y)
    {
        return FromBits(x.Bits + y.Bits);
    }

    public static BitSize operator -(BitSize x, BitSize y)
    {
        return FromBits(x.Bits - y.Bits);
    }

    public static BitSize operator *(BitSize x, BitSize y)
    {
        return FromBits(x.Bits * y.Bits);
    }

    public static BitSize operator /(BitSize x, BitSize y)
    {
        return FromBits(x.Bits / y.Bits);
    }
}

internal static class BitSizeExtensions
{
    public static BitSize AsBits(this int bits) => BitSize.FromBits(bits);

    public static BitSize AsBytes(this int bytes) => BitSize.FromBytes(bytes);
}
