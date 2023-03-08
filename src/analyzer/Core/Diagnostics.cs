namespace Surab.Analyzer.Core;

public enum DiagnosticLevel
{
    Suggestion,
    Warning,
    Error,
}

public class Diagnostic
{
    public required TextSpan Span { get; init; }

    public required DiagnosticLevel Level { get; init; }

    public required string Message { get; init; }
}

public interface IDiagnosticsProvider
{
    IEnumerable<Diagnostic> GetDiagnostics();

    bool IsHealthy();
}

public class DiagnosticsBag
{
    public List<Diagnostic> Diagnostics { get; } = new();

    public Diagnostic Add(TextSpan span, DiagnosticLevel level, string message)
    {
        var diagnostic = new Diagnostic { Span = span, Level = level, Message = message };
        Diagnostics.Add(diagnostic);
        return diagnostic;
    }
}
