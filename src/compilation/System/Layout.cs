using System.Diagnostics;

namespace Surab.Compilation;

[DebuggerDisplay("size={size}, alignment={alignment}")]
public readonly struct Layout(BitSize size, BitSize alignment)
{
    public BitSize Size { get; } = size;

    public BitSize Alignment { get; } = alignment;
}
