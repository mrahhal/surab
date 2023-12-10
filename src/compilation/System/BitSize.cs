using System.Diagnostics;

namespace Surab.Compilation;

[DebuggerDisplay("{ToString(),nq}")]
public readonly record struct BitSize
{
    private BitSize(int bits)
    {
        if (bits < 0) throw new ArgumentException("Size can't be negative.", nameof(bits));

        Bits = bits;
    }

    public int Bits { get; }

    public readonly int Bytes => Bits / 8;

    public readonly bool IsBytesSafe => Bits % 8 == 0;

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

    public static bool operator >(BitSize x, BitSize y)
    {
        return x.Bits > y.Bits;
    }

    public static bool operator >=(BitSize x, BitSize y)
    {
        return Equals(x, y) || x > y;
    }

    public static bool operator <(BitSize x, BitSize y)
    {
        return x.Bits < y.Bits;
    }

    public static bool operator <=(BitSize x, BitSize y)
    {
        return Equals(x, y) || x < y;
    }

    public override string ToString()
    {
        if (IsBytesSafe)
        {
            return $"{Bytes} bytes";
        }
        else
        {
            return $"{Bits} bits (bytes unsafe)";
        }
    }
}

public static class BitSizeExtensions
{
    public static BitSize AsBits(this int bits) => BitSize.FromBits(bits);

    public static BitSize AsBytes(this int bytes) => BitSize.FromBytes(bytes);
}
