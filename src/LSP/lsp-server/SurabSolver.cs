using System;
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

	public SyntaxNode? GetNodeAt(ProjectFile file, LinePosition linePosition)
	{
		var project = Workspace.GetFileProject(file.Path);
		var tree = project.GetSyntaxTree(file);
		return tree.GetNodeAt(linePosition);
	}

	public (Symbol? symbol, TypeSymbol? type) GetSymbolAndTypeOfPosition(TextDocumentIdentifier textDocument, Position position)
	{
		var sourceFile = GetFile(textDocument);
		if (sourceFile == null)
			return (null, null);
		var project = Workspace.GetFileProject(sourceFile.Path);
		var reasoner = project.GetReasoner(sourceFile);
		var linePosition = ConversionHelper.FromTo(position);
		var node = GetNodeAt(sourceFile, linePosition);
		if (node == null)
			return (null, null);
		var token = node.GetTokenAt(linePosition);
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
		var sourceFile = GetFile(textDocument);
		if (sourceFile == null)
			return null;
		var project = Workspace.GetFileProject(sourceFile.Path);
		var reasoner = project.GetReasoner(sourceFile);
		var linePosition = ConversionHelper.FromTo(position);
		// TODO: Might need another method just for Go to definition like queries becase if the caret
		// is on the last index of the identifier it won't be treated as a "contain", but here
		// we actually want to treat it as a hit.
		var node = GetNodeAt(sourceFile, linePosition);
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
		var node = GetNodeAt(sourceFile, linePosition);
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
		var node = GetNodeAt(sourceFile, linePosition);
		if (node == null)
			return null;
		if (node is NameDefNode nameDef)
		{
			var result = default(IEnumerable<Symbol>?);
			if (nameDef.Parent is TraitDeclNode traitDecl)
			{
				var traitType = reasoner.GetDeclaredSymbol(traitDecl);
				result = project.LookupImplementations(traitType);
			}
			else if (nameDef.Parent is FnSigNode fnSig)
			{
				Debug.Assert(fnSig.Parent != null);
				var fnSymbol = reasoner.GetDeclaredSymbol(fnSig.Parent);
				result = project.LookupImplementations(fnSymbol);
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
}
