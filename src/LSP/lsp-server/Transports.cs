using System.IO.Pipes;

namespace Surab.LSPServer;

public interface ITransport
{
	string Name { get; }

	Task ConnectAsync();

	Stream CreateInputStream();

	Stream CreateOutputStream();
}

public sealed class StudioTransport : ITransport
{
	public string Name => "stdio";

	public Task ConnectAsync()
	{
		return Task.CompletedTask;
	}

	public Stream CreateInputStream()
	{
		return Console.OpenStandardInput();
	}

	public Stream CreateOutputStream()
	{
		return Console.OpenStandardOutput();
	}
}

public sealed class PipeTransport : ITransport
{
	private readonly string _pipeName;
	private readonly NamedPipeClientStream _pipe;

	public PipeTransport(string pipeName)
	{
		_pipeName = pipeName;
		_pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
	}

	public string Name => "pipe";

	public Task ConnectAsync()
	{
		return _pipe.ConnectAsync();
	}

	public Stream CreateInputStream()
	{
		return _pipe;
	}

	public Stream CreateOutputStream()
	{
		return _pipe;
	}
}
