using Serilog;
using Surab.LSPServer;

var logger = new LoggerConfiguration()
	.Enrich.FromLogContext()
	.WriteTo.File("surab-lsp-server.log")
	.CreateLogger();

ITransport? transport = default;

logger.Information("[STARTED]");
var stdioArgName = "--stdio";
var pipeArgName = "--pipe=";
foreach (var arg in args)
{
	logger.Information("arg: " + arg);

	if (arg.StartsWith(pipeArgName))
	{
		var pipeName = arg[pipeArgName.Length..];
		pipeName = pipeName["\\\\.\\pipe\\".Length..];
		transport = new PipeTransport(pipeName);
	}
	else if (arg == stdioArgName)
	{
		transport = new StudioTransport();
	}
}

if (transport == null)
{
	logger.Warning(messageTemplate: "[NO TRANSPORT PROVIDED. EXITING]");
	return;
}
else
{
	logger.Information("Selected transport: " + transport.Name);
}

var host = LanguageServerHost.Create(logger, transport);
await host.StartAsync();

logger.Information(messageTemplate: "[WAIT FOR EXIT]");
await host.Server.WaitForExit;
logger.Information("[SERVER EXITED]");
