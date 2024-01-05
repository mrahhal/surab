using System.Diagnostics;

namespace Surab;

/// <summary>
/// A span of text in a source file. All offsets use UTF-16 encoding.
/// Included are both the absolute offset based file span (file offset) and the line based span (line + char offset).
/// </summary>
public readonly record struct TextSpan(
	FileSpan File,
	LineSpan Line)
{
	public static TextSpan Empty { get; } = new();

	/// <summary>
	/// The size of the text span.
	/// </summary>
	public int Size => File.End - File.Start;

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
/// An offset based file span. [Start..End)
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

/// <summary>
/// A line based span. [Start..End)
/// </summary>
/// <param name="Start">The span's start position.</param>
/// <param name="End">The span's end position.</param>
[DebuggerDisplay("LineSpan: [{Start,nq}..{End,nq})")]
public readonly record struct LineSpan(
	LinePosition Start,
	LinePosition End)
{
	public bool Contains(LinePosition otherPosition)
	{
		return
			Start <= otherPosition &&
			End > otherPosition;
	}

	public bool Contains(LineSpan other)
	{
		return
			Start <= other.Start &&
			End >= other.End;
	}
}

/// <summary>
/// A line based position.
/// </summary>
/// <param name="Line">Line position (zero-based).</param>
/// <param name="Char">Char position (zer-based).</param>
[DebuggerDisplay("LinePosition: {ToString(),nq}")]
public readonly record struct LinePosition(
	int Line,
	int Char)
{
	public static bool operator >(LinePosition p1, LinePosition p2)
	{
		return
			p1.Line > p2.Line ||
			(p1.Line == p2.Line && p1.Char > p2.Char);
	}

	public static bool operator >=(LinePosition p1, LinePosition p2)
	{
		return Equals(p1, p2) || p1 > p2;
	}

	public static bool operator <(LinePosition p1, LinePosition p2)
	{
		return
			p1.Line < p2.Line ||
			(p1.Line == p2.Line && p1.Char < p2.Char);
	}

	public static bool operator <=(LinePosition p1, LinePosition p2)
	{
		return Equals(p1, p2) || p1 < p2;
	}

	public override string ToString()
	{
		return $"{{{Line}:{Char}}}";
	}
}
