using System.Diagnostics;

namespace Surab.Compilation;

[DebuggerDisplay("{ToString(),nq}")]
public readonly record struct Layout(BitSize size, BitSize alignment)
{
    public BitSize Size { get; } = size;

    public BitSize Alignment { get; } = alignment;

    public override string ToString()
    {
        return $"size={size}, alignment={alignment}";
    }
}

}
