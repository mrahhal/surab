using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Surab.Analysis;

namespace Surab.LSPServer.Handlers;

public class SemanticTokensFullHandler(
    ILogger<SemanticTokensFullHandler> logger,
    ILanguageServerConfiguration configuration,
    LanguageServerHost host) : SemanticTokensFullHandlerBase
{
    private readonly ILogger _logger = logger;
    private readonly ILanguageServerConfiguration _configuration = configuration;
    private readonly LanguageServerHost _host = host;

    public override Task<SemanticTokens?> Handle(SemanticTokensParams request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[SemanticTokensParams]");

        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        var sourceFile = _host.SurabProject.SourceFiles.First(x => x.Path == filePath);

        var document = GetSemanticTokensDocument();
        var builder = document.Create();
        Tokenize(builder, sourceFile, cancellationToken);
        var s = (SemanticTokens?)builder.Commit().GetSemanticTokens();
        return Task.FromResult(s);
    }

    private void Tokenize(
           SemanticTokensBuilder builder,
           SourceFile sourceFile,
           CancellationToken cancellationToken)
    {
        var tree = _host.SurabProject.GetSyntaxTree(sourceFile);
        var reasoner = _host.SurabProject.GetReasoner(sourceFile);

        foreach (var (line, ch, length, tokenType) in TokenizeRecursive(tree.Root, reasoner))
        {
            if (tokenType == null)
                continue;

            // TODO: Modifiers.
            builder.Push(line, ch, length, tokenType);
        }
    }

    private IEnumerable<(int, int, int, SemanticTokenType?)> TokenizeRecursive(SyntaxNode node, Reasoner reasoner)
    {
        foreach (var childItem in node.EnumerateBySpanStart())
        {
            if (childItem.Token != null)
            {
                var childToken = childItem.Token;

                foreach (var trivia in childToken.LeadingTrivia)
                {
                    yield return To(trivia.Span, GetTokenType(trivia, reasoner));
                }

                yield return To(childToken.Span, GetTokenType(childToken, reasoner));

                foreach (var trivia in childToken.TrailingTrivia)
                {
                    yield return To(trivia.Span, GetTokenType(trivia, reasoner));
                }
            }
            else if (childItem.Node != null)
            {
                foreach (var item in TokenizeRecursive(childItem.Node, reasoner))
                {
                    yield return item;
                }
            }
        }

        static (int, int, int, SemanticTokenType?) To(TextSpan span, SemanticTokenType? tokenType)
        {
            var line = span.Line.Start.Line;
            var ch = span.Line.Start.Char;
            var length = span.Size;

            return (line, ch, length, tokenType);
        }
    }

    private static SemanticTokenType? GetTokenType(SyntaxTrivia trivia, Reasoner reasoner)
    {
        return trivia.Kind switch
        {
            SyntaxTriviaKind.LineComment or SyntaxTriviaKind.BlockComment => (SemanticTokenType?)SemanticTokenType.Comment,
            _ => null,
        };
    }

    private static SemanticTokenType? GetTokenType(SyntaxToken token, Reasoner reasoner)
    {
        return token switch
        {
            SimpleToken simpleToken => simpleToken.SimpleTokenKind switch
            {
                SimpleTokenKind.At when simpleToken.Parent is AttrNode => SemanticTokenType.Decorator,
                _ when simpleToken.IsKeyword => (SemanticTokenType?)SemanticTokenType.Keyword,
                _ => null,
            },
            IdentifierToken identifierToken => identifierToken.Parent switch
            {
                FnSigNode sig => sig.Parent is FnDeclNode ? SemanticTokenType.Function : SemanticTokenType.Method,
                StructDeclNode or StructCreationExprNode => SemanticTokenType.Struct,
                FieldDeclNode => SemanticTokenType.Property,
                ParamNode => SemanticTokenType.Parameter,
                VarStmtNode => SemanticTokenType.Variable,
                NamedTypeNode => SemanticTokenType.Type,
                AttrNode => SemanticTokenType.Decorator,
                IdentifierExprNode identifierExpr => GetTokenType(reasoner.LookupSymbol(identifierExpr)),
                _ => null,
            },
            LiteralToken literalToken => literalToken.LiteralTokenKind switch
            {
                LiteralTokenKind.NumericInteger or LiteralTokenKind.NumericFloat => (SemanticTokenType?)SemanticTokenType.Number,
                _ => null,
            },
            _ => null,
        };
    }

    private static SemanticTokenType? GetTokenType(Symbol? symbol)
    {
        return symbol switch
        {
            FieldSymbol => (SemanticTokenType?)SemanticTokenType.Property,
            ParamSymbol => SemanticTokenType.Parameter,
            LocalSymbol => SemanticTokenType.Variable,
            FnSymbol => SemanticTokenType.Function,
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
            FnTypeSymbol => SemanticTokenType.Function,
            _ => null,
        };
    }

    private SemanticTokensDocument GetSemanticTokensDocument()
    {
        return new SemanticTokensDocument(RegistrationOptions.Legend);
    }

    protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities)
    {
        return new()
        {
            DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
            Legend = new SemanticTokensLegend
            {
                TokenModifiers = capability.TokenModifiers,
                TokenTypes = capability.TokenTypes,
            },
            Full = true,
        };
    }
}
