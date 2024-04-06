using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Surab.Analysis;

namespace Surab.LSPServer;

public class SurabSolver(
	ILogger<SurabSolver> logger,
	LanguageServerHost host)
{
	private readonly ILogger _logger = logger;
	private readonly LanguageServerHost _host = host;

	public LanguageServerHost Host => _host;

	public LanguageServer Server => Host.Server;

	public SurabProject Project => Host.Project;

	public SourceFile? GetSourceFile(TextDocumentIdentifier identifier) => GetSourceFile(identifier.Uri);

	public SourceFile? GetSourceFile(DocumentUri uri)
	{
		var filePath = uri.GetFileSystemPath();
		var sourceFile = Host.Project.GetSourceFile(filePath);
		if (sourceFile == null)
		{
			_logger.LogInformation("Didn't find SourceFile with path {FilePath}.", filePath);
		}
		return sourceFile;
	}

	/// <summary>
	/// Gets the deepest node at the given position.
	/// </summary>
	public SyntaxNode? GetNodeAt(SourceFile sourceFile, LinePosition linePosition)
	{
		var tree = Project.GetSyntaxTree(sourceFile);
		return GetNodeAtRecursive(tree.Root, linePosition);
	}

	/// <summary>
	/// Gets the token at the position without taking into account trivia.
	/// </summary>
	public SyntaxToken? GetTokenAt(SyntaxNode node, LinePosition linePosition)
	{
		foreach (var token in node.ChildTokens)
		{
			if (token.Span.Line.Contains(linePosition))
			{
				return token;
			}
			else if (token.Span.Line.Start > linePosition)
			{
				break;
			}
		}
		return null;
	}

	private SyntaxNode? GetNodeAtRecursive(SyntaxNode node, LinePosition linePosition)
	{
		foreach (var childNode in node.ChildNodes)
		{
			if (childNode.SpanWithTrivia.Line.Contains(linePosition))
			{
				return GetNodeAtRecursive(childNode, linePosition);
			}
		}
		return node;
	}

	public (Symbol? symbol, TypeSymbol? type) GetSymbolAndTypeOfPosition(TextDocumentIdentifier textDocument, Position position)
	{
		var sourceFile = GetSourceFile(textDocument)!;
		var reasoner = Project.GetReasoner(sourceFile);
		var linePosition = ConversionHelper.FromTo(position);
		var node = GetNodeAt(sourceFile, linePosition);
		if (node == null)
			return (null, null);
		var token = GetTokenAt(node, linePosition);
		if (token == null)
			return (null, null);
		var type = default(TypeSymbol?);
		var symbol = reasoner.GetDeclaredSymbol(node) ?? reasoner.LookupSymbol(node);
		if (symbol is TypeSymbol typeSymbol)
		{
			type = typeSymbol;
		}
		else if (symbol is ModuleSymbol moduleSymbol)
		{
			return (moduleSymbol, null);
		}
		else if (symbol is FnSymbol && reasoner.GetDeclaredSymbol(node) == null)
		{
			// LookupType can perform updates to the receiver arg.
			type = reasoner.LookupType(node);
		}
		else if (symbol is ISymbolHasType hasType)
		{
			type = hasType.Type;
		}
		else if (symbol != null)
		{
			type = reasoner.LookupType(node);
		}
		return (symbol, type);
	}

	public LocationOrLocationLinks? GetDefinitionRef(TextDocumentIdentifier textDocument, Position position)
	{
		var sourceFile = GetSourceFile(textDocument)!;
		var reasoner = Project.GetReasoner(sourceFile);
		var linePosition = ConversionHelper.FromTo(position);
		var node = GetNodeAt(sourceFile, linePosition);
		if (node == null)
			return null;
		var token = GetTokenAt(node, linePosition);
		if (token == null)
			return null;
		var symbol = reasoner.LookupSymbol(node);
		if (symbol != null && symbol is InFileSymbol inFileSymbol)
		{
			var span = inFileSymbol.DeclaringNode?.GetMainIdentifierToken()?.Span ?? inFileSymbol.DeclaringNode?.Span ?? new();
			var locations = new LocationOrLocationLinks(new LocationOrLocationLink(new Location
			{
				Uri = DocumentUri.FromFileSystemPath(inFileSymbol.DeclaringSourceFile!.Path.Value!),
				Range = ConversionHelper.FromTo(span),
			}));
			return locations;
		}

		return null;
	}
}
