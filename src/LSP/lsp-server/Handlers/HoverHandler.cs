using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Surab.LSPServer.Handlers;

public class HoverHandler(
    ILogger<HoverHandler> logger,
    SurabSolver solver) : HoverHandlerBase
{
    private readonly ILogger _logger = logger;
    private readonly SurabSolver _solver = solver;

    public override Task<Hover?> Handle(HoverParams request, CancellationToken cancellationToken)
    {
        var (symbol, type) = _solver.GetSymbolAndTypeOfPosition(request.TextDocument, request.Position);
        if (symbol == null)
            return Task.FromResult((Hover?)null);

        return Task.FromResult((Hover?)new Hover
        {
            Contents = new(new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = $"{symbol.Name} | {type?.Name}",
            }),
        });
    }

    protected override HoverRegistrationOptions CreateRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities)
    {
        return new()
        {
            DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
        };
    }
}
