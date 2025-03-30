using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Surab.LSPServer.Handlers;

public class ImplementationHandler(
	ILogger<ImplementationHandler> logger,
	SurabSolver solver) : ImplementationHandlerBase
{
	private readonly ILogger _logger = logger;
	private readonly SurabSolver _solver = solver;

	public override async Task<LocationOrLocationLinks?> Handle(ImplementationParams request, CancellationToken cancellationToken)
	{
		return _solver.Workspace.LockForRead(() =>
		{
			return _solver.GetImplementationRef(request.TextDocument, request.Position);
		});
	}

	protected override ImplementationRegistrationOptions CreateRegistrationOptions(ImplementationCapability capability, ClientCapabilities clientCapabilities)
	{
		return new()
		{
			DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
		};
	}
}
