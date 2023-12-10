using Serilog;
using Surab.LSPServer;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.File("surab-lsp-server.log")
    .CreateLogger();

Log.Logger.Information("[STARTED]");
foreach (var arg in args)
{
    Log.Logger.Information("arg: " + arg);
}

var host = LanguageServerHost.Create();
await host.StartAsync();

Log.Logger.Information(messageTemplate: "[WAIT FOR EXIT]");
await host.Server.WaitForExit;
Log.Logger.Information("[SERVER EXITED]");
