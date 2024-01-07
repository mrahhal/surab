using Serilog;
using Surab.LSPServer;

var logger = new LoggerConfiguration()
	.Enrich.FromLogContext()
	.WriteTo.File("surab-lsp-server.log")
	.CreateLogger();

logger.Information("[STARTED]");
var pipeArgName = "--pipe=";
var pipeArg = "";
foreach (var arg in args)
{
	logger.Information("arg: " + arg);

	if (arg.StartsWith(pipeArgName))
	{
		pipeArg = arg[pipeArgName.Length..];
		pipeArg = pipeArg["\\\\.\\pipe\\".Length..];
	}
}

if (string.IsNullOrEmpty(pipeArg))
{
	logger.Information("No pipe arg was provided. Server exiting...");
	return;
}
else
{
	logger.Information("Running server on pipe: " + pipeArg);
}

var host = LanguageServerHost.Create(logger, pipeArg);
await host.StartAsync();

logger.Information(messageTemplate: "[WAIT FOR EXIT]");
await host.Server.WaitForExit;
logger.Information("[SERVER EXITED]");
