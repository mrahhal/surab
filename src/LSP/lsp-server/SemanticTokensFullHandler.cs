using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Surab.Analysis;

namespace Surab.LSPServer;

public class SemanticTokensFullHandler(
    ILogger<SemanticTokensFullHandler> logger,
    ILanguageServerConfiguration configuration,
    LanguageServerHost host) : SemanticTokensFullHandlerBase
{
    private readonly ILogger _logger = logger;
    private readonly ILanguageServerConfiguration _configuration = configuration;
    private readonly LanguageServerHost _host = host;

    private readonly TextDocumentSelector _textDocumentSelector = new(
        new TextDocumentFilter
        {
            Pattern = "**/*.surab",
        }
    );

    public override Task<SemanticTokens?> Handle(SemanticTokensParams request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handle semantics!");

        var filePath = request.TextDocument.Uri.GetFileSystemPath();
        var sourceFile = _host.SurabProject.SourceFiles.First(x => x.Path == filePath);

        var document = GetSemanticTokensDocument();
        var builder = document.Create();
        Tokenize(builder, sourceFile, cancellationToken);
        var s = (SemanticTokens?)builder.Commit().GetSemanticTokens();
        return Task.FromResult(s);
    }

    protected void Tokenize(
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
        foreach (var childItem in ConversionHelper.EnumerateBySpanStart(node))
        {
            if (childItem.token != null)
            {
                var childToken = childItem.token;
                var line = childToken.Span.Line.Start.Line;
                var ch = childToken.Span.Line.Start.Char;
                var length = childToken.Span.Size;
                var tokenType = ConversionHelper.GetTokenType(childToken, reasoner);

                yield return (line, ch, length, tokenType);
            }
            else if (childItem.node != null)
            {
                foreach (var item in TokenizeRecursive(childItem.node, reasoner))
                {
                    yield return item;
                }
            }
        }
    }

    protected SemanticTokensDocument GetSemanticTokensDocument()
    {
        return new SemanticTokensDocument(RegistrationOptions.Legend);
    }

    protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities)
    {
        return new()
        {
            DocumentSelector = _textDocumentSelector,
            Legend = new SemanticTokensLegend
            {
                TokenModifiers = capability.TokenModifiers,
                TokenTypes = capability.TokenTypes,
            },
            Full = true,
        };
    }
}
