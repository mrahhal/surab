using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Serilog;
using Surab.Analysis;

namespace Surab.LSPServer;

public class LanguageServerHost
{
    private LanguageServerHost()
    {
    }

    public LanguageServer Server { get; private set; } = null!;

    public SurabProject SurabProject { get; private set; } = null!;

    public static LanguageServerHost Create()
    {
        return new LanguageServerHost();
    }

    public async Task StartAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton(this);

        var options = new LanguageServerOptions()
            .ConfigureLogging(b => b.AddSerilog(dispose: true))
            .WithInput(Console.OpenStandardInput())
            .WithOutput(Console.OpenStandardOutput())
            .WithConfigurationItem(new ConfigurationItem
            {
                Section = "surab",
            })
            .OnInitialize((_, @params, _) =>
            {
                var workspaceUri = @params.RootUri;
                if (workspaceUri is null) return Task.CompletedTask;

                Log.Logger.Information("Workspace: " + workspaceUri.GetFileSystemPath());

                InitializeProject(workspaceUri);

                return Task.CompletedTask;
            })
            .WithHandler<TextDocumentSyncHandler>()
            .WithHandler<SemanticTokensFullHandler>();

        Server = LanguageServer.Create(options, services.BuildServiceProvider());
        await Server.Initialize(default);
    }

    private void InitializeProject(DocumentUri workspaceUri)
    {
        var workspacePath = workspaceUri.GetFileSystemPath();
        var sourceFiles = Directory
            .GetFiles(workspacePath, "*.surab")
            .Select(x => SourceFile.Create(x, File.ReadAllText(x)))
            .ToList();
        SurabProject = SurabProject.Create(sourceFiles);
    }
}
