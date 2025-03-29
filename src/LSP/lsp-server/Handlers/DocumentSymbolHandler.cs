using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Surab.Analysis;
using LSPSymbolKind = OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind;

namespace Surab.LSPServer.Handlers;

public class DocumentSymbolHandler(
	ILogger<DocumentSymbolHandler> logger,
	SurabSolver solver) : DocumentSymbolHandlerBase
{
	private readonly ILogger _logger = logger;
	private readonly SurabSolver _solver = solver;

	public override async Task<SymbolInformationOrDocumentSymbolContainer?> Handle(DocumentSymbolParams request, CancellationToken cancellationToken)
	{
		return _solver.Workspace.LockForRead(() =>
		{
			var file = _solver.GetFile(request.TextDocument);
			if (file == null)
				return null;

			var project = _solver.Workspace.GetFileProject(file.Path);
			var firstLevelSymbols = GetFirstLevelSymbols(project, file);
			if (firstLevelSymbols == null)
				return null;

			List<SymbolInformationOrDocumentSymbol> documentSymbols = [.. Process(firstLevelSymbols)];
			return new SymbolInformationOrDocumentSymbolContainer(documentSymbols);
		});
	}

	private IEnumerable<DocumentSymbol> Process(IEnumerable<Symbol> symbols)
	{
		return Cleanup(symbols.Select(Convert));
	}

	private List<Symbol>? GetFirstLevelSymbols(SurabProject project, ProjectFile file)
	{
		var reasoner = project.GetReasoner(file);
		var root = reasoner.Tree.Root;
		if (root is not FileNode fileNode)
			return null;

		return fileNode.Decls
			.Select(reasoner.GetDeclaredSymbol)
			.Where(s => s != null)
			.Select(s => s!)
			.ToList();
	}

	private IEnumerable<DocumentSymbol> Cleanup(IEnumerable<DocumentSymbol?> symbols)
	{
		return symbols.Where(s => s != null).Select(s => s!);
	}

	private DocumentSymbol? Convert(Symbol symbol)
	{
		var kind = GetSymbolKind(symbol);
		if (kind == null)
			return null;

		return new DocumentSymbol
		{
			Children = new([.. Process(symbol.Children)]),
			Name = string.IsNullOrEmpty(symbol.Name) ? FillEmptyName(symbol) : symbol.Name,
			Kind = kind.Value,
			Range = ConversionHelper.FromTo(symbol.Span),
			SelectionRange = ConversionHelper.FromTo(GetSelectionSpan(symbol)),
			Detail = GetDetail(symbol),
		};
	}

	private LineSpan GetSelectionSpan(Symbol symbol)
	{
		// Special case for initializer method because it has an empty identifier (so empty selection span).
		if (symbol is FnSymbol fn && fn.IsInitializerMethod)
		{
			// Return the param list as the span instead.
			return fn.SigNode.ParamsList.Span.Line;
		}
		// TODO: For module symbols, the selection should be the import node.
		return symbol.SelectionSpan.Line;
	}

	private string? GetDetail(Symbol symbol)
	{
		return symbol switch
		{
			FnSymbol fn => SyntaxTreeUtils.GetSigStringWithoutIdentifier(fn.SigNode),
			ISymbolHasType hasType => hasType.Type.Name,
			ImplSymbol => "impl",
			_ => null,
		};
	}

	private string FillEmptyName(Symbol symbol)
	{
		return symbol switch
		{
			FnSymbol fn => fn.IsInitializerMethod ? "[init]" : "UNKNOWN FN NAME",
			_ => "UNKNOWN",
		};
	}

	private LSPSymbolKind? GetSymbolKind(Symbol symbol)
	{
		// Returning a null means we don't want to show the symbol in the document symbol viewer.
		return symbol switch
		{
			ConstSymbol => LSPSymbolKind.Constant,
			FnSymbol fn => fn.IsMethod ? LSPSymbolKind.Method : LSPSymbolKind.Function,
			FieldSymbol => LSPSymbolKind.Field,
			LocalSymbol => null,
			NamedTypeSymbol namedType => namedType.TypeKind switch
			{
				TypeSymbolKind.Struct => LSPSymbolKind.Struct,
				TypeSymbolKind.Trait => LSPSymbolKind.Interface,
				_ => LSPSymbolKind.Null, // TODO
			},
			AliasTypeSymbol => LSPSymbolKind.Null, // TODO
			ImplSymbol => LSPSymbolKind.Interface,
			_ => null,
		};
	}

	protected override DocumentSymbolRegistrationOptions CreateRegistrationOptions(DocumentSymbolCapability capability, ClientCapabilities clientCapabilities)
	{
		return new()
		{
			DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
		};
	}
}
