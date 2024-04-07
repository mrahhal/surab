using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using Surab.Analysis;
using Surab.LSPServer.Handlers;

namespace Surab.LSPServer;

public class LanguageServerHost
{
	private readonly Serilog.Core.Logger _serilogLogger;
	private ILogger<LanguageServerHost> _logger = null!;

	private LanguageServerHost(Serilog.Core.Logger logger)
	{
		_serilogLogger = logger;
	}

	public LanguageServer Server { get; private set; } = null!;

	public SurabProject Project { get; private set; } = null!;

	public static LanguageServerHost Create(Serilog.Core.Logger logger)
	{
		return new LanguageServerHost(logger);
	}

	public async Task StartAsync()
	{
		var services = new ServiceCollection();
		services.AddSingleton(this);

		var options = new LanguageServerOptions()
			.ConfigureLogging(b => b.AddSerilog(_serilogLogger, dispose: true))
			.WithServices(services =>
			{
				services.AddSingleton<SurabSolver>();
			})
			.WithInput(Console.OpenStandardInput())
			.WithOutput(Console.OpenStandardOutput())
			.WithConfigurationItem(new ConfigurationItem
			{
				Section = "surab",
			})
			.OnInitialize(async (_, @params, _) =>
			{
				var workspaceUri = @params.RootUri;
				if (workspaceUri is null)
					return;

				_logger.LogInformation("Workspace: {Workspace}", workspaceUri.GetFileSystemPath());

				await InitializeProjectAsync(workspaceUri);
			})
			.OnInitialized((_, _, _, _) =>
			{
				// Launch a preliminary analysis when a workspace is opened and initialized.
				// We don't need to await for the task before replying to the client because this can be a long running operation.
				// The project in this state is already ready to handle change requests.

				// Shouldn't pass the cancellation token from OnInitialized as that will cancel the analysis right away.
				_ = Project.EnsureAnalyzedAsync(CancellationToken.None);
				return Task.CompletedTask;
			})
			.WithHandler<TextDocumentSyncHandler>()
			.WithHandler<DidChangeWatchedFilesHandler>()
			.WithHandler<SemanticTokensFullHandler>()
			.WithHandler<DefinitionHandler>()
			.WithHandler<DeclarationHandler>()
			.WithHandler<HoverHandler>()
			.WithHandler<DocumentSymbolHandler>()
			.WithHandler<SignatureHelpHandler>();

		Server = LanguageServer.Create(options, services.BuildServiceProvider());
		_logger = Server.Services.GetRequiredService<ILogger<LanguageServerHost>>();

		await Server.Initialize(default);
	}

	private Task InitializeProjectAsync(DocumentUri workspaceUri)
	{
		var workspacePath = workspaceUri.GetFileSystemPath();
		var projectPaths = SurabProjectFSLoader.FindProjects(workspacePath).ToList();
		// TODO: Report this to the client properly, and have the ability to reload.
		if (projectPaths.Count == 0)
		{
			throw new Exception("No Surab projects.");
		}
		if (projectPaths.Count > 1)
		{
			throw new Exception("More than one Surab project is not supported.");
		}
		var projectPath = projectPaths.Single();
		Project = SurabProjectFSLoader.CreateProjectAt(projectPath);
		Project.SubscribeToFullDiagnosticsReady(HandleFullDiagnosticsReady);

		// TODO:
		//Project.SubscribeToAnalysis...?
		//var progress = await _solver.Server.WorkDoneManager.Create(
		//	new WorkDoneProgressBegin
		//	{
		//		Title = "Starting analysis...",
		//		Percentage = 10,
		//	});
		//progress.OnNext("Collecting diags...", 70, null);
		//_solver.Server.ShowMessage(new ShowMessageParams
		//{
		//	Message = "Failed while analyzing.",
		//	Type = MessageType.Error,
		//});
		//progress.OnCompleted();

		return Task.CompletedTask;
	}

	private void HandleFullDiagnosticsReady(IEnumerable<FileDiagnostics> diags)
	{
		_logger.LogInformation("Publishing diagnostics");
		foreach (var fileDiagnostics in diags)
		{
			Server.PublishDiagnostics(new PublishDiagnosticsParams
			{
				Uri = DocumentUri.FromFileSystemPath(fileDiagnostics.FilePath.Value),
				Diagnostics = Container.From(fileDiagnostics.Diagnostics.Select(d => new OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic
				{
					Range = ConversionHelper.FromTo(d.Span),
					Severity = ConversionHelper.FromTo(d.Level),
					Message = d.Message,
				})),
			});
		}
	}
}
