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

	private SyntaxNode? GetNodeAtRecursive(SyntaxNode node, LinePosition linePosition)
	{
		foreach (var childNode in node.ChildNodes)
		{
			if (childNode.Span.Line.Contains(linePosition))
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
		var node = GetNodeAt(sourceFile, linePosition)!;
		var type = default(TypeSymbol?);
		var symbol = reasoner.GetDeclaredSymbol(node) ?? reasoner.LookupSymbol(node);
		if (symbol is TypeSymbol typeSymbol)
		{
			type = typeSymbol;
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

	public List<Symbol>? GetFirstLevelSymbols(TextDocumentIdentifier textDocument)
	{
		var sourceFile = GetSourceFile(textDocument);
		if (sourceFile == null)
			return null;
		var reasoner = Project.GetReasoner(sourceFile);
		var root = reasoner.Tree.Root;
		if (root is not ModuleNode moduleNode)
			return null;

		return moduleNode.DeclsList.Decls
			.Select(reasoner.GetDeclaredSymbol)
			.Where(s => s != null)
			.Select(s => s!)
			.ToList();
	}

	public LocationOrLocationLinks? GetDefinitionRef(TextDocumentIdentifier textDocument, Position position)
	{
		var sourceFile = GetSourceFile(textDocument)!;
		var reasoner = Project.GetReasoner(sourceFile);
		var linePosition = ConversionHelper.FromTo(position);
		var node = GetNodeAt(sourceFile, linePosition)!;
		var symbol = reasoner.LookupSymbol(node);
		if (symbol != null)
		{
			var span = symbol.DeclaringNode?.GetMainIdentifierToken()?.Span ?? symbol.DeclaringNode?.Span ?? new();
			var locations = new LocationOrLocationLinks(new LocationOrLocationLink(new Location
			{
				Uri = DocumentUri.FromFileSystemPath(symbol.DeclaringSourceFile!.Path!),
				Range = ConversionHelper.FromTo(span),
			}));
			return locations;
		}

		return null;
	}
}
