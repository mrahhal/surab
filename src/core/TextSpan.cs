namespace Surab;

/// <summary>
/// Represents a span of text in source code.
/// Included are both the absolute file span (file offset) and the line span (line + line offset).
/// </summary>
public readonly record struct TextSpan(
    FileSpan File,
    LineSpan Line)
{
    public static TextSpan Empty { get; } = new();

    public static TextSpan FromTo(TextSpan from, TextSpan to) => new()
    {
        File = new()
        {
            Start = from.File.Start,
            End = to.File.End,
        },
        Line = new()
        {
            Start = from.Line.Start,
            End = to.Line.End,
        },
    };

    public TextSpan To(TextSpan to) => FromTo(this, to);

    public TextSpan ShrinkToStart() => this with
    {
        File = File with { End = File.Start },
        Line = Line with { End = Line.Start },
    };

    public TextSpan ShrinkToEnd() => this with
    {
        File = File with { Start = File.End },
        Line = Line with { Start = Line.End },
    };

    public Range GetRange() => File.GetRange();

    public override string ToString() => GetRange().ToString();
}

/// <summary>
/// A file span.
/// [Start..End)
/// </summary>
public readonly record struct FileSpan(
    int Start,
    int End) : IComparable<FileSpan>
{
    public Range GetRange() => Start..End;

    public int Length => End - Start;

    public bool IsEmpty => Length == 0;

    public bool Contains(int position) =>
        (position - Start) < Length;

    public bool Contains(FileSpan other) =>
        other.Start >= Start && other.End <= End;

    public int CompareTo(FileSpan other)
    {
        var diff = Start - other.Start;
        if (diff != 0)
        {
            return diff;
        }

        return Length - other.Length;
    }

    public override string ToString() => $"[{Start}..{End})";
}

public readonly record struct LineSpan(
    LinePosition Start,
    LinePosition End);

public readonly record struct LinePosition(
    int Line,
    int Char);
