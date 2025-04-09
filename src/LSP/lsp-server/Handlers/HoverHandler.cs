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
		return _solver.Workspace.LockForRead(() =>
		{
			return Task.FromResult(HandleCore(request));
		});
	}

	private Hover? HandleCore(HoverParams request)
	{
		var file = _solver.GetFile(request.TextDocument);
		if (file == null)
			return null;
		var project = _solver.Workspace.GetFileProject(file.Path);
		var reasoner = project.GetReasoner(file);
		var token = reasoner.Tree.GetTokenAt(ConversionHelper.FromTo(request.Position));
		var (symbol, type) = _solver.GetSymbolAndTypeOfPosition(request.TextDocument, request.Position);

		if (symbol == null)
			return null;

		var sig = $"{symbol.Name} | {type?.Name}";

		List<MarkedString> markedStrings = [sig];
		if (project.GetDocumentationOfSymbol(symbol) is { } str)
		{
			markedStrings.Add(str);
		}
		return new Hover
		{
			Contents = new(markedStrings),
			Range = token == null ? null : ConversionHelper.FromTo(token.Span),
		};
	}

	protected override HoverRegistrationOptions CreateRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities)
	{
		return new()
		{
			DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
		};
	}
}
