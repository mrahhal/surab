using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Surab.LSPServer.Handlers;

public class CodeLensHandler(
	ILogger<CodeLensHandler> logger,
	SurabSolver solver) : CodeLensHandlerBase
{
	private readonly ILogger _logger = logger;
	private readonly SurabSolver _solver = solver;

	public override Task<CodeLensContainer?> Handle(CodeLensParams request, CancellationToken cancellationToken)
	{
		return _solver.Workspace.LockForRead(() =>
		{
			return Task.FromResult(_solver.GetCodeLens(request.TextDocument));
		});
	}

	public override Task<CodeLens> Handle(CodeLens request, CancellationToken cancellationToken)
	{
		return _solver.Workspace.LockForRead(() =>
		{
			return Task.FromResult(_solver.ResolveCodeLens(request));
		});
	}

	protected override CodeLensRegistrationOptions CreateRegistrationOptions(CodeLensCapability capability, ClientCapabilities clientCapabilities)
	{
		return new()
		{
			DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
			ResolveProvider = true,
		};
	}
}
