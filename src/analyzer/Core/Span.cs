namespace Surab.Analyzer.Core;

public readonly struct LinePosition
{
    public required int Line { get; init; }

    public required int Char { get; init; }
}

public readonly struct FileSpan
{
    public required int Start { get; init; }

    public required int End { get; init; }

    public Range GetRange()
    {
        return Start..End;
    }
}

public readonly struct LineSpan
{
    public required LinePosition Start { get; init; }

    public required LinePosition End { get; init; }
}

public readonly struct Span
{
    public required FileSpan File { get; init; }

    public required LineSpan Line { get; init; }

    public static Span FromTo(Span from, Span to)
    {
        return new()
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
    }

    public Span To(Span to)
    {
        return FromTo(this, to);
    }

    public Span ShrinkToStart()
    {
        return this with
        {
            File = File with { End = File.Start },
            Line = Line with { End = Line.Start },
        };
    }

    public Span ShrinkToEnd()
    {
        return this with
        {
            File = File with { Start = File.End },
            Line = Line with { Start = Line.End },
        };
    }

    public Range GetRange()
    {
        return File.GetRange();
    }

    public override string ToString()
    {
        return GetRange().ToString();
    }
}
