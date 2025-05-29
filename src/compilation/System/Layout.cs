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
public sealed class StructLayoutSection
{
	private StructLayoutSection(StructLayoutSectionKind kind)
	{
		Kind = kind;
	}

	public StructLayoutSectionKind Kind { get; }

	public required BitSize Offset { get; init; }

	public required BitSize Size { get; init; }

	/// <summary>
	/// Gets the index of the <see cref="HirStructType.Fields"/> in the struct this data section is associated with.
	/// This is -1 when Kind is Padding.
	/// </summary>
	public required int FieldIndex { get; init; }

	/// <summary>
	/// Gets the index of this section in the sections array of the struct.
	/// </summary>
	public required int SectionIndex { get; init; }

	public static StructLayoutSection Data(BitSize offset, BitSize size, int fieldIndex, int sectionIndex)
	{
		return new(StructLayoutSectionKind.Data) { Offset = offset, Size = size, FieldIndex = fieldIndex, SectionIndex = sectionIndex };
	}

	public static StructLayoutSection Padding(BitSize offset, BitSize size, int sectionIndex)
	{
		return new(StructLayoutSectionKind.Padding) { Offset = offset, Size = size, FieldIndex = -1, SectionIndex = sectionIndex };
	}
}
