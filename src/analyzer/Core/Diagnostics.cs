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
