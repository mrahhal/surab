namespace Surab.Analyzer;

public enum DiagnosticLevel
{
    Suggestion,
    Warning,
    Error,
}

public class Diagnostic
{
    public Diagnostic(TextSpan span, DiagnosticLevel level, string message)
    {
        Span = span;
        Level = level;
        Message = message;
    }

    public TextSpan Span { get; }

    public DiagnosticLevel Level { get; }

    public string Message { get; }

    public string Visualize(string src)
    {
        var start = Span.File.Start;
        var end = Span.File.End;

        var windowStart = BTill(start);
        var windowEnd = FTill(end);
        var lineNumber = Span.Line.Start.Line + 1;

        var leading = $"{lineNumber}: ";
        var v = leading + src[windowStart..windowEnd] + Environment.NewLine;
        v += string.Concat(Enumerable.Repeat(' ', leading.Length + start - windowStart));
        v += string.Concat(Enumerable.Repeat('^', end - start));

        return $"[{Level}] " + Message + Environment.NewLine + Environment.NewLine + v;

        int BTill(int p)
        {
            while (p > 0 && src[p - 1] != '\n')
            {
                p--;
            }
            return p;
        }
        int FTill(int p)
        {
            while (p < src.Length - 1 && src[p] != '\r' && src[p] != '\n')
            {
                p++;
            }
            return p;
        }
    }
}

public interface IDiagnosticsProvider
{
    IEnumerable<Diagnostic> GetDiagnostics();

    bool IsHealthy();
}

public class DiagnosticBag
{
    public List<Diagnostic> Diagnostics { get; } = new();

    public Diagnostic Add(TextSpan span, DiagnosticLevel level, string message)
    {
        var diagnostic = new Diagnostic(span, level, message);
        Diagnostics.Add(diagnostic);
        return diagnostic;
    }
}
