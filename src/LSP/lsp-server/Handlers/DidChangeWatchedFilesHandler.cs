using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using FileSystemWatcher = OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher;

namespace Surab.LSPServer.Handlers;

public class DidChangeWatchedFilesHandler(
	ILogger<DidChangeWatchedFilesHandler> logger,
	LanguageServerHost host) : DidChangeWatchedFilesHandlerBase
{
	private readonly ILogger _logger = logger;
	private readonly LanguageServerHost _host = host;

	public override Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
	{
		_logger.LogInformation("[DidChangeWatchedFilesParams]");

		// TODO

		return Unit.Task;
	}

	protected override DidChangeWatchedFilesRegistrationOptions CreateRegistrationOptions(DidChangeWatchedFilesCapability capability, ClientCapabilities clientCapabilities)
	{
		return new()
		{
			Watchers = new Container<FileSystemWatcher>(
				new FileSystemWatcher()
				{
					Kind = WatchKind.Create | WatchKind.Change | WatchKind.Delete,
					GlobPattern = new(LanguageServerHelper.GetSurabFilesGlob()),
				}),
		};
	}
}
