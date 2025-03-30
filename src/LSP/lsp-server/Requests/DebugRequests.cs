using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Surab.Analysis;

namespace Surab.LSPServer.Requests;

public static class GetSyntaxTreeRequest
{
	public class Response
	{
		public SyntaxTreeNode? Root { get; set; }
	}

	public class SyntaxTreeNode
	{
		public required string? Identifier { get; init; }
		public required string Kind { get; init; }
		public required OmniSharp.Extensions.LanguageServer.Protocol.Models.Range Span { get; init; }
		public required string SpanLabel { get; init; }
		public required SyntaxTreeNode[] Children { get; init; }
	}

	public static Task<Response> HandlerAsync(TextDocumentPositionParams @params, LanguageServer server)
	{
		var solver = server.Services.GetRequiredService<SurabSolver>();
		return solver.Workspace.LockForRead(() =>
		{
			server.Services.GetRequiredService<ILogger<SyntaxTreeNode>>().LogInformation("Handling GetSyntaxTreeRequest: {Path}", @params.TextDocument.ToString());
			var file = solver.GetFile(@params.TextDocument);
			if (file != null)
			{
				var project = solver.Workspace.GetFileProject(file.Path);
				var tree = project.GetSyntaxTree(file);
				var root = Convert(tree);
				return Task.FromResult(new Response { Root = root });
			}

			return Task.FromResult(new Response());
		});
	}

	private static SyntaxTreeNode? Convert(SyntaxTree tree)
	{
		return Convert(tree.Root);
	}

	private static SyntaxTreeNode Convert(SyntaxNode node)
	{
		var mainIdentifier = node.GetMainIdentifierToken()?.Value;
		return new SyntaxTreeNode
		{
			Identifier = mainIdentifier,
			Kind = node.Kind.ToString(),
			Span = ConversionHelper.FromTo(node.Span.Line),
			SpanLabel = node.Span.File.ToString(),
			Children = [.. node.ChildNodes.Select(Convert)],
		};
	}
}
