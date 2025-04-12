using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Surab.Analysis;

namespace Surab.LSPServer.Handlers;

public class PrepareRenameHandler(
	ILogger<PrepareRenameHandler> logger,
	SurabSolver solver) : PrepareRenameHandlerBase
{
	private readonly ILogger _logger = logger;
	private readonly SurabSolver _solver = solver;

	public override Task<RangeOrPlaceholderRange?> Handle(PrepareRenameParams request, CancellationToken cancellationToken)
	{
		return _solver.Workspace.LockForRead(() =>
		{
			return Task.FromResult(HandleCore(request));
		});
	}

	private RangeOrPlaceholderRange? HandleCore(PrepareRenameParams request)
	{
		var sourceFile = _solver.GetFile(request.TextDocument);
		if (sourceFile == null)
			return null;
		var project = _solver.Workspace.GetFileProject(sourceFile.Path);
		var reasoner = project.GetReasoner(sourceFile);

		var token = _solver.GetTouchingIdentifierTokenAt(sourceFile, ConversionHelper.FromTo(request.Position));
		if (token == null || token is not IdentifierToken identifier)
			return null;

		var symbol = reasoner.GetDeclaredSymbol(identifier) ?? reasoner.LookupSymbol(identifier.Parent);
		if (symbol == null)
			return null;

		if (!_solver.Workspace.IsSymbolInWorkspace(symbol))
			return null;

		return new (new PlaceholderRange
		{
			Range = ConversionHelper.FromTo(identifier.Span),
			Placeholder = identifier.Value,
		});
	}

	protected override RenameRegistrationOptions CreateRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities)
	{
		return new()
		{
			DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
			PrepareProvider = true,
		};
	}
}

public class RenameHandler(
	ILogger<RenameHandler> logger,
	SurabSolver solver) : RenameHandlerBase
{
	private readonly ILogger _logger = logger;
	private readonly SurabSolver _solver = solver;

	public override Task<WorkspaceEdit?> Handle(RenameParams request, CancellationToken cancellationToken)
	{
		return _solver.Workspace.LockForRead(() =>
		{
			return Task.FromResult(HandleCore(request));
		});
	}

	private WorkspaceEdit? HandleCore(RenameParams request)
	{
		var sourceFile = _solver.GetFile(request.TextDocument);
		if (sourceFile == null)
			return null;
		var project = _solver.Workspace.GetFileProject(sourceFile.Path);
		var reasoner = project.GetReasoner(sourceFile);

		var token = _solver.GetTouchingIdentifierTokenAt(sourceFile, ConversionHelper.FromTo(request.Position));
		if (token == null || token is not IdentifierToken identifier)
			return null;

		var symbol = reasoner.GetDeclaredSymbol(identifier) ?? reasoner.LookupSymbol(identifier.Parent);
		if (symbol == null)
			return null;

		if (!_solver.Workspace.IsSymbolInWorkspace(symbol))
			return null;

		List<WorkspaceEditDocumentChange> changes = [];

		foreach (var g in _solver.Workspace.LookupReferences(symbol).GroupBy(x => x.File))
		{
			var file = g.Key;
			changes.Add(new(new TextDocumentEdit
			{
				TextDocument = new OptionalVersionedTextDocumentIdentifier
				{
					Uri = OmniSharp.Extensions.LanguageServer.Protocol.DocumentUri.FromFileSystemPath(file.Path!.Value),
					//Version = ??
				},
				Edits = new(g.Select(x => new TextEdit
				{
					NewText = request.NewName,
					Range = ConversionHelper.FromTo(x.Span),
				}))
			}));
		}

		return new WorkspaceEdit
		{
			DocumentChanges = new(changes),
		};
	}

	protected override RenameRegistrationOptions CreateRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities)
	{
		return new()
		{
			DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
			PrepareProvider = true,
		};
	}
}
