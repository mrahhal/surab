using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

namespace Surab.LSPServer.Handlers;

public class TextDocumentSyncHandler(
	ILogger<TextDocumentSyncHandler> logger,
	ILanguageServerConfiguration configuration,
	SurabSolver solver) : TextDocumentSyncHandlerBase
{
	private readonly ILogger _logger = logger;
	private readonly ILanguageServerConfiguration _configuration = configuration;
	private readonly SurabSolver _solver = solver;

	public TextDocumentSyncKind Change => TextDocumentSyncKind.Full;

	public override async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
	{
		_logger.LogInformation("[DidOpenTextDocumentParams]");

		await ReAnalyzeAsync(notification.TextDocument);

		await _configuration.GetScopedConfiguration(notification.TextDocument.Uri, token).ConfigureAwait(false);
		return Unit.Value;
	}

	public override async Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
	{
		_logger.LogInformation("[DidChangeTextDocumentParams]");

		var sourceFile = _solver.GetSourceFile(notification.TextDocument)!;
		sourceFile.UpdateText(notification.ContentChanges.First().Text);

		await ReAnalyzeAsync(notification.TextDocument);

		return Unit.Value;
	}

	private async Task ReAnalyzeAsync(TextDocumentIdentifier textDocument)
	{
		var progress = await _solver.Server.WorkDoneManager.Create(
			new WorkDoneProgressBegin
			{
				Title = "Starting analysis...",
				Percentage = 10,
			});

		var sourceFile = _solver.GetSourceFile(textDocument)!;

		_solver.Project.EnsureAnalyzed();

		progress.OnNext("Collecting diags...", 70, null);

		foreach (var fileDiagnostics in _solver.Project.GetProjectDiagnostics())
		{
			_solver.Server.PublishDiagnostics(new PublishDiagnosticsParams
			{
				Uri = DocumentUri.FromFileSystemPath(fileDiagnostics.SourceFile.Path!),
				Diagnostics = Container.From(fileDiagnostics.Diagnostics.Select(d => new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
				{
					Range = ConversionHelper.FromTo(d.Span),
					Severity = ConversionHelper.FromTo(d.Level),
					Message = d.Message,
				})),
			});
		}

		progress.OnCompleted();
	}

	public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
	{
		_logger.LogInformation("[DidCloseTextDocumentParams]");

		if (_configuration.TryGetScopedConfiguration(notification.TextDocument.Uri, out var disposable))
		{
			disposable.Dispose();
		}
		return Unit.Task;
	}

	public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token) => Unit.Task;

	protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
	{
		return new()
		{
			DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
			Change = Change,
			Save = new SaveOptions() { IncludeText = true },
		};
	}

	public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) => new(uri, "surab");
}
