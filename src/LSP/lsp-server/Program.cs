using Serilog;
using Surab.LSPServer;

var logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.File("surab-lsp-server.log")
    .CreateLogger();

logger.Information("[STARTED]");
foreach (var arg in args)
{
    logger.Information("arg: " + arg);
}

var host = LanguageServerHost.Create(logger);
await host.StartAsync();

logger.Information(messageTemplate: "[WAIT FOR EXIT]");
await host.Server.WaitForExit;
logger.Information("[SERVER EXITED]");
