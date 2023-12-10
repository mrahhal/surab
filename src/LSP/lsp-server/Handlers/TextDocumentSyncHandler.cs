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
    LanguageServerHost host) : TextDocumentSyncHandlerBase
{
    private readonly ILogger _logger = logger;
    private readonly ILanguageServerConfiguration _configuration = configuration;
    private readonly LanguageServerHost _host = host;

    public TextDocumentSyncKind Change => TextDocumentSyncKind.Full;

    public override async Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
    {
        _logger.LogInformation("[DidOpenTextDocumentParams]");

        ReAnalyze(notification.TextDocument);

        await _configuration.GetScopedConfiguration(notification.TextDocument.Uri, token).ConfigureAwait(false);
        return Unit.Value;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
    {
        _logger.LogInformation("[DidChangeTextDocumentParams]");

        var filePath = notification.TextDocument.Uri.GetFileSystemPath();
        var sourceFile = _host.SurabProject.SourceFiles.First(x => x.Path == filePath);
        sourceFile.UpdateText(notification.ContentChanges.First().Text);

        ReAnalyze(notification.TextDocument);

        return Unit.Task;
    }

    private void ReAnalyze(TextDocumentIdentifier textDocument)
    {
        var filePath = textDocument.Uri.GetFileSystemPath();
        var sourceFile = _host.SurabProject.SourceFiles.First(x => x.Path == filePath);

        _host.SurabProject.EnsureAnalyzed();

        foreach (var fileDiagnostics in _host.SurabProject.GetProjectDiagnostics())
        {
            _host.Server.PublishDiagnostics(new PublishDiagnosticsParams
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
