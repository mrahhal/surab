using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Server;
using Surab.Analysis;

namespace Surab.LSPServer;

#pragma warning disable IDE0306 // Simplify collection initialization
public class SurabSolver(
	ILogger<SurabSolver> logger,
	LanguageServerHost host)
{
	private readonly ILogger _logger = logger;
	private readonly LanguageServerHost _host = host;
	private readonly JsonSerializer _jsonSerializer = new()
	{
		ContractResolver = new DefaultContractResolver
		{
			NamingStrategy = new CamelCaseNamingStrategy()
		},
	};

	public LanguageServerHost Host => _host;

	public LanguageServer Server => Host.Server;

	public SurabWorkspace Workspace => Host.Workspace;

	public JsonSerializer JsonSerializer => _jsonSerializer;

	public ProjectFile? GetFile(TextDocumentIdentifier identifier) => GetFile(identifier.Uri);

	public ProjectFile? GetFile(DocumentUri uri)
	{
		return Host.Workspace.GetFile(uri.GetFileSystemPath());
	}

	public ProjectFile? GetFile(string uri)
	{
		return Host.Workspace.GetFile(uri);
	}

	public SyntaxNode? GetNodeAt(ProjectFile file, LinePosition linePosition)
	{
		var project = Workspace.GetFileProject(file.Path);
		var tree = project.GetSyntaxTree(file);
		return tree.GetNodeAt(linePosition);
	}

	// This prefers an identifier token in the prev char if the caret is at the boundary between an
	// identifier and non identifier.
	// e.g. foo|(args) => will return the `foo` token instead of the `(` token.
	public SyntaxToken? GetTouchingIdentifierTokenAt(ProjectFile file, LinePosition linePosition)
	{
		var project = Workspace.GetFileProject(file.Path);
		var tree = project.GetSyntaxTree(file);
		var firstToken = tree.GetTokenAt(linePosition);
		if (firstToken != null && firstToken.Kind is SyntaxTokenKind.Identifier)
		{
			return firstToken;
		}
		if (linePosition.Char != 0)
		{
			// Check one char before the position.
			var token = tree.GetTokenAt(linePosition.GetCharJustBefore());
			if (token != null && token.Kind is SyntaxTokenKind.Identifier)
			{
				return token;
			}
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
		var token = GetTouchingIdentifierTokenAt(sourceFile, linePosition);
		if (token == null)
			return null;
		var node = token.Parent;
		if (node is NameDefNode nameDef && nameDef.Parent is FnSigNode fnSig)
		{
			node = fnSig;
		}
		var symbol = reasoner.GetDeclaredSymbol(node) ?? reasoner.LookupSymbol(node);
		symbol = project.LookupDeclaration(symbol);
		if (symbol != null)
		{
			return GetLocation(symbol);
		}
		return null;
	}

	public LocationOrLocationLinks? GetDefinitionRef(TextDocumentIdentifier textDocument, Position position)
	{
		var sourceFile = GetFile(textDocument);
		if (sourceFile == null)
			return null;
		var project = Workspace.GetFileProject(sourceFile.Path);
		var reasoner = project.GetReasoner(sourceFile);
		var linePosition = ConversionHelper.FromTo(position);
		var token = GetTouchingIdentifierTokenAt(sourceFile, linePosition);
		if (token == null)
			return null;
		var node = token.Parent;
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
			return GetLocation(symbol);
		}
		return null;
	}

	public LocationOrLocationLinks? GetTypeDefinitionRef(TextDocumentIdentifier textDocument, Position position)
	{
		var symbol = GetTouchingSymbol(textDocument, position);
		var typeSymbol = symbol switch
		{
			TypeSymbol t => t,
			ISymbolHasType hasType => hasType.Type,
			_ => null,
		};
		if (typeSymbol != null)
		{
			return GetLocation(typeSymbol);
		}
		return null;
	}

	public (Symbol? symbol, TypeSymbol? type) GetSymbolAndTypeOfPosition(TextDocumentIdentifier textDocument, Position position)
	{
		var sourceFile = GetFile(textDocument);
		if (sourceFile == null)
			return (null, null);
		var project = Workspace.GetFileProject(sourceFile.Path);
		var reasoner = project.GetReasoner(sourceFile);
		var linePosition = ConversionHelper.FromTo(position);
		var token = GetTouchingIdentifierTokenAt(sourceFile, linePosition);
		if (token == null)
			return (null, null);
		var node = token.Parent;
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

	public LocationOrLocationLinks? GetImplementationRef(TextDocumentIdentifier textDocument, Position position)
	{
		var sourceFile = GetFile(textDocument);
		if (sourceFile == null)
			return null;
		var project = Workspace.GetFileProject(sourceFile.Path);
		var reasoner = project.GetReasoner(sourceFile);
		var linePosition = ConversionHelper.FromTo(position);
		var token = GetTouchingIdentifierTokenAt(sourceFile, linePosition);
		if (token == null)
			return null;
		var node = token.Parent;
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
				return GetLocation(result);
			}
		}
		return null;
	}

	public LocationContainer? GetReferencesRef(TextDocumentIdentifier textDocument, Position position)
	{
		var symbol = GetTouchingSymbol(textDocument, position);
		if (symbol == null)
			return null;
		var refs = GetAllReferences(symbol);
		return new LocationContainer(refs.Select(r => new Location
		{
			Uri = DocumentUri.FromFileSystemPath(r.File.Path.Value),
			Range = ConversionHelper.FromTo(r.Span),
		}));
	}

	public DocumentHighlightContainer? GetDocumentHighlightRef(TextDocumentIdentifier textDocument, Position position)
	{
		var sourceFile = GetFile(textDocument);
		var symbol = GetTouchingSymbol(textDocument, position);
		if (symbol == null)
			return null;
		var refs = GetAllReferences(symbol);
		return new DocumentHighlightContainer(refs.Where(r => r.File == sourceFile).Select(r => new DocumentHighlight
		{
			Kind = r.Kind,
			Range = ConversionHelper.FromTo(r.Span),
		}));
	}

	public List<(ProjectFile File, TextSpan Span, DocumentHighlightKind Kind)> GetAllReferences(Symbol symbol)
	{
		var usages = Workspace.WorkspaceProjects.SelectMany(p => p.GetUsagesOfSymbolInProject(symbol));
		var refs = new List<(ProjectFile, TextSpan, DocumentHighlightKind)>();
		if (symbol.DeclaringNode != null)
		{
			var declaringSpan = symbol.DeclaringNode.GetMainIdentifierToken()?.Span ?? symbol.DeclaringNode.Span;
			refs.Add((symbol.DeclaringFile!, declaringSpan, DocumentHighlightKind.Text));
		}
		refs.AddRange(usages.Select(u =>
		{
			var kind = u.Parent?.Kind switch
			{
				SyntaxNodeKind.AssignmentExpr => DocumentHighlightKind.Write,
				null => DocumentHighlightKind.Text,
				_ => DocumentHighlightKind.Read,
			};
			return (u.Tree.File, u.Span, kind);
		}));
		return refs;
	}

	private Symbol? GetTouchingSymbol(TextDocumentIdentifier textDocument, Position position)
	{
		var sourceFile = GetFile(textDocument);
		if (sourceFile == null)
			return null;
		var project = Workspace.GetFileProject(sourceFile.Path);
		var reasoner = project.GetReasoner(sourceFile);
		var linePosition = ConversionHelper.FromTo(position);
		var token = GetTouchingIdentifierTokenAt(sourceFile, linePosition);
		if (token == null)
			return null;
		var node = token.Parent;

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

		return reasoner.GetDeclaredSymbol(node) ?? reasoner.LookupSymbol(node);
	}

	private LocationOrLocationLinks? GetLocation(params IEnumerable<Symbol>? symbols)
	{
		if (symbols == null)
			return null;

		return new LocationOrLocationLinks(symbols.Select(symbol =>
		{
			//var span = symbol.DeclaringNode?.GetMainIdentifierToken()?.Span ?? symbol.DeclaringNode?.Span ?? new();
			var span = symbol.SelectionSpan;
			return new LocationOrLocationLink(new Location
			{
				Uri = DocumentUri.FromFileSystemPath(symbol.DeclaringFile!.Path.Value!),
				Range = ConversionHelper.FromTo(span),
			});
		}));
	}
}

#pragma warning restore IDE0306 // Simplify collection initialization
