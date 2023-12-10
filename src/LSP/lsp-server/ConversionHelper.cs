using System.Linq;
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

    internal static SemanticTokenType? GetTokenType(SyntaxToken token, Reasoner reasoner)
    {
        return token switch
        {
            SimpleToken simpleToken => simpleToken.SimpleTokenKind switch
            {
                SimpleTokenKind.Fn or SimpleTokenKind.Return or SimpleTokenKind.Struct or SimpleTokenKind.Var or SimpleTokenKind.True or SimpleTokenKind.False
                    => (SemanticTokenType?)SemanticTokenType.Keyword,
                _ => null,
            },
            // TODO: If identifier, lookup symbol, etc
            IdentifierToken identifierToken => identifierToken.Parent switch
            {
                FnSigNode => (SemanticTokenType?)SemanticTokenType.Function, // TODO: Method
                StructDeclNode => SemanticTokenType.Struct,
                FieldDeclNode => SemanticTokenType.Property,
                ParamNode => SemanticTokenType.Parameter,
                VarStmtNode => SemanticTokenType.Variable,
                NamedTypeNode => SemanticTokenType.Type,
                IdentifierExprNode identifierExpr => GetTokenType(reasoner.LookupSymbol(identifierExpr)),
                _ => null,
            },
            LiteralToken literalToken => literalToken.LiteralTokenKind switch
            {
                LiteralTokenKind.NumericInteger or LiteralTokenKind.NumericFloat => (SemanticTokenType?)SemanticTokenType.Number,
                _ => null,
            },
            // TODO: Comments
            _ => null,
        };
    }

    private static SemanticTokenType? GetTokenType(Symbol? symbol)
    {
        return symbol switch
        {
            FieldSymbol => (SemanticTokenType?)SemanticTokenType.Property,
            ParamSymbol => (SemanticTokenType?)SemanticTokenType.Parameter,
            LocalSymbol => (SemanticTokenType?)SemanticTokenType.Variable,
            FnSymbol => (SemanticTokenType?)SemanticTokenType.Function,
            TypeSymbol typeSymbol => GetTokenType(typeSymbol),
            _ => null,
        };
    }

    private static SemanticTokenType? GetTokenType(TypeSymbol typeSymbol)
    {
        if (typeSymbol.IsError) return null;
        return typeSymbol switch
        {
            StructTypeSymbol => (SemanticTokenType?)SemanticTokenType.Struct,
            FnTypeSymbol => (SemanticTokenType?)SemanticTokenType.Function,
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
