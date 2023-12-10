using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Surab.Analysis;

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

    internal static IEnumerable<(SyntaxNode? node, SyntaxToken? token)> EnumerateBySpanStart(SyntaxNode node)
    {
        List<(SyntaxNode? node, SyntaxToken? token, TextSpan span)> list = [
            .. node.ChildTokens.Select<SyntaxToken, (SyntaxNode? node, SyntaxToken? token, TextSpan span)>(t => (null, t, t.Span)),
            .. node.ChildNodes.Select<SyntaxNode, (SyntaxNode? node, SyntaxToken? token, TextSpan span)>(n => (n, null, n.Span)),
        ];

        return list.OrderBy(x => x.span.File.Start).Select(x => (x.node, x.token));
    }
}
