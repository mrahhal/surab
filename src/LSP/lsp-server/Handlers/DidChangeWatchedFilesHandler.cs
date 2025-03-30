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

		_solver.Workspace.LockForWrite(() =>
		{
			foreach (var change in request.Changes)
			{
				var path = change.Uri.GetFileSystemPath();
				var project = _solver.Workspace.GetFileProject(path);

				switch (change.Type)
				{
					case FileChangeType.Created:
						{
							project.VFS.AddFile(path, File.ReadAllText(path));
						}
						break;

					case FileChangeType.Changed:
						{
							// Nothing to do here. This is covered by DidChangeTextDocumentParams.
						}
						break;

					case FileChangeType.Deleted:
						{
							var file = project.VFS.GetFile(path);
							if (file == null)
							{
								_logger.LogInformation("Did not find SourceFile with path {FilePath}.", change.Uri);
							}
							else
							{
								project.VFS.RemoveFile(file);
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
