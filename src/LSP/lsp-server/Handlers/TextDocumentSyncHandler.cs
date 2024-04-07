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

	public override Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token) => Unit.Task;

	public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
	{
		_logger.LogInformation("[DidChangeTextDocumentParams]");

		_solver.Project.LockForWrite(vfs =>
		{
			var file = vfs.GetFile(notification.TextDocument.Uri);
			if (file == null)
			{
				_logger.LogInformation("Didn't find SourceFile with path {FilePath}.", notification.TextDocument.Uri);
			}
			else
			{
				vfs.UpdateFile(file, notification.ContentChanges.First().Text);
			}
		});

		return Unit.Task;
	}

	public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token) => Unit.Task;

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
