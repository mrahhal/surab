using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
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

    public SurabProject SurabProject { get; private set; } = null!;

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
            .WithHandler<TextDocumentSyncHandler>()
            .WithHandler<SemanticTokensFullHandler>()
            .WithHandler<DefinitionHandler>()
            .WithHandler<DeclarationHandler>();

        Server = LanguageServer.Create(options, services.BuildServiceProvider());
        _logger = Server.Services.GetRequiredService<ILogger<LanguageServerHost>>();

        await Server.Initialize(default);
    }

    private async Task InitializeProjectAsync(DocumentUri workspaceUri)
    {
        var progress = await Server.WorkDoneManager.Create(
            new WorkDoneProgressBegin
            {
                Title = "Initializing project...",
            });

        var workspacePath = workspaceUri.GetFileSystemPath();
        var sourceFiles = Directory
            .GetFiles(workspacePath, "*.surab")
            .Select(x => SourceFile.Create(x, File.ReadAllText(x)))
            .ToList();
        SurabProject = SurabProject.Create(sourceFiles);
    }
}
