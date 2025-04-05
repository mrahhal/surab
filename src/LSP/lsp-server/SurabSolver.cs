using System.Diagnostics;
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

	public SurabWorkspace Workspace => Host.Workspace;

	public ProjectFile? GetFile(TextDocumentIdentifier identifier) => GetFile(identifier.Uri);

	public ProjectFile? GetFile(DocumentUri uri)
	{
		return Host.Workspace.GetFile(uri.GetFileSystemPath());
	}

	// This prefers a name like node if the caret is at the boundary between a name and non name node.
	// e.g. foo|(args) => will return the name node for foo.
	public SyntaxNode? GetTouchingNodeAt(ProjectFile file, LinePosition linePosition)
	{
		var project = Workspace.GetFileProject(file.Path);
		var tree = project.GetSyntaxTree(file);
		// TODO: This can be optimized by doing this in a single pass somehow.
		var node = tree.GetNodeAt(linePosition);
		if (linePosition.Char != 0)
		{
			// Check one char before the position.
			var token = tree.GetTokenAt(new(linePosition.Line, linePosition.Char - 1));
			if (token != null && token.Kind is SyntaxTokenKind.Identifier)
			{
				// Prefer the parent node of this identifier token.
				return token.Parent;
			}
		}

		return node;
	}

	public (Symbol? symbol, TypeSymbol? type) GetSymbolAndTypeOfPosition(TextDocumentIdentifier textDocument, Position position)
	{
		var sourceFile = GetFile(textDocument);
		if (sourceFile == null)
			return (null, null);
		var project = Workspace.GetFileProject(sourceFile.Path);
		var reasoner = project.GetReasoner(sourceFile);
		var linePosition = ConversionHelper.FromTo(position);
		var node = GetTouchingNodeAt(sourceFile, linePosition);
		if (node == null)
			return (null, null);
		//var token = node.GetTokenAt(linePosition);
		//if (token == null)
		//	return (null, null);
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
		var sourceFile = GetFile(textDocument);
		if (sourceFile == null)
			return null;
		var project = Workspace.GetFileProject(sourceFile.Path);
		var reasoner = project.GetReasoner(sourceFile);
		var linePosition = ConversionHelper.FromTo(position);
		var node = GetTouchingNodeAt(sourceFile, linePosition);
		if (node == null)
			return null;
		//var token = node.GetTokenAt(linePosition);
		//if (token == null)
		//	return null;
		var symbol = reasoner.LookupSymbol(node);
		if (symbol is ThisSymbol thisSymbol)
		{
			symbol = thisSymbol.Type;
		}
		if (symbol is TypeSymbol typeSymbol)
		{
			symbol = typeSymbol.UnwrapPointers();
		}
		if (symbol != null)
		{
			var span = symbol.DeclaringNode?.GetMainIdentifierToken()?.Span ?? symbol.DeclaringNode?.Span ?? new();
			var locations = new LocationOrLocationLinks(new LocationOrLocationLink(new Location
			{
				Uri = DocumentUri.FromFileSystemPath(symbol.DeclaringFile!.Path.Value!),
				Range = ConversionHelper.FromTo(span),
			}));
			return locations;
		}

		return null;
	}

	public LocationOrLocationLinks? GetDeclarationRef(TextDocumentIdentifier textDocument, Position position)
	{
		var sourceFile = GetFile(textDocument);
		if (sourceFile == null)
			return null;
		var project = Workspace.GetFileProject(sourceFile.Path);
		var reasoner = project.GetReasoner(sourceFile);
		var linePosition = ConversionHelper.FromTo(position);
		var node = GetTouchingNodeAt(sourceFile, linePosition);
		if (node == null)
			return null;
		if (node is NameDefNode nameDef && nameDef.Parent is FnSigNode fnSig)
		{
			node = fnSig;
		}
		var symbol = reasoner.GetDeclaredSymbol(node) ?? reasoner.LookupSymbol(node);
		symbol = project.LookupDeclaration(symbol);
		if (symbol != null)
		{
			var span = symbol.DeclaringNode?.GetMainIdentifierToken()?.Span ?? symbol.DeclaringNode?.Span ?? new();
			var locations = new LocationOrLocationLinks(new LocationOrLocationLink(new Location
			{
				Uri = DocumentUri.FromFileSystemPath(symbol.DeclaringFile!.Path.Value!),
				Range = ConversionHelper.FromTo(span),
			}));
			return locations;
		}

		return null;
	}

	internal LocationOrLocationLinks? GetImplementationRef(TextDocumentIdentifier textDocument, Position position)
	{
		var sourceFile = GetFile(textDocument);
		if (sourceFile == null)
			return null;
		var project = Workspace.GetFileProject(sourceFile.Path);
		var reasoner = project.GetReasoner(sourceFile);
		var linePosition = ConversionHelper.FromTo(position);
		var node = GetTouchingNodeAt(sourceFile, linePosition);
		if (node == null)
			return null;
		if (node is NameDefNode nameDef)
		{
			var result = default(IEnumerable<Symbol>?);
			if (nameDef.Parent is TraitDeclNode traitDecl)
			{
				var traitType = reasoner.GetDeclaredSymbol(traitDecl);
				result = Workspace.LookupImplementations(traitType);
			}
			else if (nameDef.Parent is FnSigNode fnSig)
			{
				Debug.Assert(fnSig.Parent != null);
				var fnSymbol = reasoner.GetDeclaredSymbol(fnSig.Parent);
				result = Workspace.LookupImplementations(fnSymbol);
			}
			if (result != null)
			{
				var locations = new LocationOrLocationLinks(result.Select(x =>
				{
					return new LocationOrLocationLink(new Location
					{
						Uri = DocumentUri.FromFileSystemPath(x.DeclaringFile!.Path.Value!),
						Range = ConversionHelper.FromTo(x.SelectionSpan),
					});
				}));
				return locations;
			}
		}
		return null;
	}

	internal LocationContainer? GetReferencesRef(TextDocumentIdentifier textDocument, Position position)
	{
		var symbol = GetTouchingSymbol(textDocument, position);
		if (symbol == null)
			return null;
		var references = Workspace.LookupReferences(symbol);
#pragma warning disable IDE0306 // Simplify collection initialization
		return new LocationContainer(references.Select(x =>
		{
			return new Location
			{
				Uri = DocumentUri.FromFileSystemPath(x.Tree.File.Path.Value!),
				Range = ConversionHelper.FromTo(x.Span),
			};
		}));
#pragma warning restore IDE0306 // Simplify collection initialization
	}

	private Symbol? GetTouchingSymbol(TextDocumentIdentifier textDocument, Position position)
	{
		var sourceFile = GetFile(textDocument);
		if (sourceFile == null)
			return null;
		var project = Workspace.GetFileProject(sourceFile.Path);
		var reasoner = project.GetReasoner(sourceFile);
		var linePosition = ConversionHelper.FromTo(position);
		var node = GetTouchingNodeAt(sourceFile, linePosition);
		if (node == null)
			return null;

		if (node is NameDefNode nameDef && nameDef.Parent != null)
		{
			return reasoner.GetDeclaredSymbol(nameDef.Parent);
		}

		if (node is NameNode name)
		{
			return reasoner.LookupSymbol(name);
		}

		if (node is ThisExprNode && reasoner.LookupSymbol(node) is ThisSymbol thisSymbol)
		{
			return thisSymbol.Type;
		}

		return reasoner.LookupSymbol(node);
	}
}
