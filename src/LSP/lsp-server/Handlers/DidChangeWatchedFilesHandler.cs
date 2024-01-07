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

		_solver.Project.LockForWrite(() =>
		{
			foreach (var change in request.Changes)
			{
				switch (change.Type)
				{
					case FileChangeType.Created:
						{
							var path = change.Uri.GetFileSystemPath();
							var sourceFile = SourceFile.Create(path, File.ReadAllText(path));
							_solver.Project.AddSourceFile(sourceFile);
						}
						break;

					case FileChangeType.Changed:
						{
							// Nothing to do here. This is covered by DidChangeTextDocumentParams.
						}
						break;

					case FileChangeType.Deleted:
						{
							var sourceFile = _solver.GetSourceFile(change.Uri);
							if (sourceFile != null)
							{
								_solver.Project.RemoveSourceFile(sourceFile);
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
