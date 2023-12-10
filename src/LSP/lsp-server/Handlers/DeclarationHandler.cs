using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Surab.LSPServer.Handlers;

public class DeclarationHandler(
    ILogger<DeclarationHandler> logger,
    SurabSolver solver) : DeclarationHandlerBase
{
    private readonly ILogger _logger = logger;
    private readonly SurabSolver _solver = solver;

    public override Task<LocationOrLocationLinks?> Handle(DeclarationParams request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_solver.GetDefinitionRef(request.TextDocument, request.Position));
    }

    protected override DeclarationRegistrationOptions CreateRegistrationOptions(DeclarationCapability capability, ClientCapabilities clientCapabilities)
    {
        return new()
        {
            DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
        };
    }
}
