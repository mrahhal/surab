using MediatR;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using FileSystemWatcher = OmniSharp.Extensions.LanguageServer.Protocol.Models.FileSystemWatcher;

namespace Surab.LSPServer.Handlers;

public class DidChangeWatchedFilesHandler(
	ILogger<DidChangeWatchedFilesHandler> logger,
	SurabSolver solver) : DidChangeWatchedFilesHandlerBase
{
	private readonly ILogger _logger = logger;
	private readonly SurabSolver _solver = solver;

	public override Task<Unit> Handle(DidChangeWatchedFilesParams request, CancellationToken cancellationToken)
	{
		_logger.LogInformation("[DidChangeWatchedFilesParams]");

		_solver.Project.LockForWrite(vfs =>
		{
			foreach (var change in request.Changes)
			{
				switch (change.Type)
				{
					case FileChangeType.Created:
						{
							var path = change.Uri.GetFileSystemPath();
							vfs.AddFile(path, File.ReadAllText(path));
						}
						break;

					case FileChangeType.Changed:
						{
							// Nothing to do here. This is covered by DidChangeTextDocumentParams.
						}
						break;

					case FileChangeType.Deleted:
						{
							var file = vfs.GetFile(change.Uri);
							if (file == null)
							{
								_logger.LogInformation("Didn't find SourceFile with path {FilePath}.", change.Uri);
							}
							else
							{
								vfs.RemoveFile(file);
							}
						}
						break;

					default:
						break;
				}
			}
		});

		return Unit.Task;
	}

	protected override DidChangeWatchedFilesRegistrationOptions CreateRegistrationOptions(DidChangeWatchedFilesCapability capability, ClientCapabilities clientCapabilities)
	{
		// TODO: Track surab-project.json

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
