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
		return _solver.Project.LockForRead(() =>
		{
			var sourceFile = _solver.GetSourceFile(request.TextDocument);
			if (sourceFile == null)
				return null;

			var firstLevelSymbols = GetFirstLevelSymbols(sourceFile);
			if (firstLevelSymbols == null)
				return null;

			List<SymbolInformationOrDocumentSymbol> documentSymbols = [.. Cleanup(firstLevelSymbols.Cast<InFileSymbol>().Select(Convert))];
			return new SymbolInformationOrDocumentSymbolContainer(documentSymbols);
		});
	}

	private List<Symbol>? GetFirstLevelSymbols(SourceFile sourceFile)
	{
		var reasoner = _solver.Project.GetReasoner(sourceFile);
		var root = reasoner.Tree.Root;
		if (root is not FileNode fileNode)
			return null;

		return fileNode.DeclsList.Decls
			.Select(reasoner.GetDeclaredSymbol)
			.Where(s => s != null)
			.Select(s => s!)
			.ToList();
	}

	private IEnumerable<DocumentSymbol> Cleanup(IEnumerable<DocumentSymbol?> symbols)
	{
		return symbols.Where(s => s != null).Select(s => s!);
	}

	private DocumentSymbol? Convert(InFileSymbol symbol)
	{
		var kind = GetSymbolKind(symbol);
		if (kind == null)
			return null;

		return new DocumentSymbol
		{
			Children = new([.. Cleanup(symbol.Children.Cast<InFileSymbol>().Select(Convert))]),
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
			return fn.FnSigNode.ParamsList.Span.Line;
		}
		return symbol.SelectionSpan.Line;
	}

	private string? GetDetail(Symbol symbol)
	{
		return symbol switch
		{
			FnSymbol fn => SyntaxTreeUtils.GetSigStringWithoutIdentifier(fn.FnSigNode),
			ISymbolHasType hasType => hasType.Type.Name,
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
		return symbol switch
		{
			ConstSymbol => LSPSymbolKind.Constant,
			FnSymbol fn => fn.IsMethod ? LSPSymbolKind.Method : LSPSymbolKind.Function,
			FieldSymbol => LSPSymbolKind.Field,
			LocalSymbol => null,
			StructTypeSymbol => LSPSymbolKind.Struct,
			ClassTypeSymbol => LSPSymbolKind.Class,
			AliasTypeSymbol => LSPSymbolKind.Null, // ?
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
