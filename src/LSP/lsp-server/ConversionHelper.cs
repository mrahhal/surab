using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Surab.LSPServer;

public static class ConversionHelper
{
    public static OmniSharp.Extensions.LanguageServer.Protocol.Models.Range FromTo(TextSpan span)
    {
        return new(new(span.Line.Start.Line, span.Line.Start.Char), new(span.Line.End.Line, span.Line.End.Char));
    }

    public static DiagnosticSeverity? FromTo(DiagnosticLevel level)
    {
        return level switch
        {
            DiagnosticLevel.Error => DiagnosticSeverity.Error,
            DiagnosticLevel.Warning => DiagnosticSeverity.Warning,
            DiagnosticLevel.Suggestion => DiagnosticSeverity.Hint,
            _ => null,
        };
    }
}
