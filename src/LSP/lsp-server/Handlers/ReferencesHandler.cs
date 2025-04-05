using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Surab.LSPServer.Handlers;

public class ReferencesHandler(
	ILogger<ReferencesHandler> logger,
	SurabSolver solver) : ReferencesHandlerBase
{
	private readonly ILogger _logger = logger;
	private readonly SurabSolver _solver = solver;

	public override async Task<LocationContainer?> Handle(ReferenceParams request, CancellationToken cancellationToken)
	{
		return _solver.Workspace.LockForRead(() =>
		{
			return _solver.GetReferencesRef(request.TextDocument, request.Position);
		});
	}

	protected override ReferenceRegistrationOptions CreateRegistrationOptions(ReferenceCapability capability, ClientCapabilities clientCapabilities)
	{
		return new()
		{
			DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
		};
	}
}
