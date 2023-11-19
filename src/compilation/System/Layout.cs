using System.Diagnostics;

namespace Surab.Compilation;

[DebuggerDisplay("{ToString(),nq}")]
public readonly record struct Layout(BitSize Size, BitSize Alignment)
{
    public override string ToString()
    {
        return $"size={Size}, alignment={Alignment}";
    }
}

public class StructLayout(
    Layout layout,
    List<StructLayoutSection> sections)
{
    public Layout Layout { get; } = layout;

    public IReadOnlyList<StructLayoutSection> Sections { get; } = sections;
}

public enum StructLayoutSectionKind
{
    Data,
    Padding,
}

/// <summary>
/// Represents either data or padding sections of a struct.
/// </summary>
public class StructLayoutSection
{
    private StructLayoutSection(StructLayoutSectionKind kind)
    {
        Kind = kind;
    }

    public StructLayoutSectionKind Kind { get; init; }

    public BitSize Offset { get; init; }

    public BitSize Size { get; init; }

    /// <summary>
    /// Points to the index of the field in the struct this data member is associated with.
    /// This is -1 when Kind is Padding.
    /// </summary>
    public int FieldIndex { get; init; }

    public static StructLayoutSection Data(BitSize offset, BitSize size, int fieldIndex)
    {
        return new(StructLayoutSectionKind.Data) { Offset = offset, Size = size, FieldIndex = fieldIndex };
    }

    public static StructLayoutSection Padding(BitSize offset, BitSize size)
    {
        return new(StructLayoutSectionKind.Padding) { Offset = offset, Size = size, FieldIndex = -1 };
    }
}
