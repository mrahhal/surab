namespace Surab.Analyzer.Core;

public readonly record struct LinePosition(
    int Line,
    int Char);

public readonly record struct FileSpan(
    int Start,
    int End)
{
    public Range GetRange()
    {
        return Start..End;
    }
}

public readonly record struct LineSpan(
    LinePosition Start,
    LinePosition End);

public readonly record struct Span(
    FileSpan File,
    LineSpan Line)
{
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
