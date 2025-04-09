using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Surab.Analysis;

namespace Surab.LSPServer.Handlers;

public class CodeLensHandler(
	ILogger<CodeLensHandler> logger,
	SurabSolver solver) : CodeLensHandlerBase
{
	private readonly ILogger _logger = logger;
	private readonly SurabSolver _solver = solver;

	public override Task<CodeLensContainer?> Handle(CodeLensParams request, CancellationToken cancellationToken)
	{
		return _solver.Workspace.LockForRead(() =>
		{
			return Task.FromResult(GetCodeLens(request.TextDocument));
		});
	}

	public override Task<CodeLens> Handle(CodeLens request, CancellationToken cancellationToken)
	{
		return _solver.Workspace.LockForRead(() =>
		{
			return Task.FromResult(ResolveCodeLens(request));
		});
	}

	public CodeLensContainer? GetCodeLens(TextDocumentIdentifier textDocument)
	{
		var sourceFile = _solver.GetFile(textDocument);
		if (sourceFile == null)
			return null;
		var project = _solver.Workspace.GetFileProject(sourceFile.Path);
		var reasoner = project.GetReasoner(sourceFile);
		List<CodeLens> codeLenses = [];
		foreach (var symbol in reasoner.EnumerateAllSymbols().Where(x => x.Kind is Analysis.SymbolKind.Type))
		{
			var type = (TypeSymbol)symbol;
			if (type.TypeKind is TypeSymbolKind.Trait)
			{
				var data = new CodeLensData(sourceFile.Path.Value, symbol.SelectionSpan.Line.Start);
				codeLenses.Add(new CodeLens
				{
					Range = ConversionHelper.FromTo(symbol.SelectionSpan),
					Data = JObject.FromObject(data),
				});
			}
		}
		return new CodeLensContainer(codeLenses);
	}

	public CodeLens ResolveCodeLens(CodeLens codeLens)
	{
		var data = codeLens.Data?.ToObject<CodeLensData>();
		if (data == null)
			return codeLens;
		var sourceFile = _solver.GetFile(data.FilePath);
		if (sourceFile == null)
			return codeLens;
		var project = _solver.Workspace.GetFileProject(sourceFile.Path);
		var reasoner = project.GetReasoner(sourceFile);
		var node = _solver.GetNodeAt(sourceFile, data.Position);
		if (node == null)
			return codeLens;
		var symbol = reasoner.GetDeclaredSymbol(node);
		if (symbol == null || symbol is not TypeSymbol type)
			return codeLens;

		if (type.TypeKind is TypeSymbolKind.Trait)
		{
			var impls = _solver.Workspace.LookupImplementations(type).ToList();
			var implsCount = impls.Count;
			var text = implsCount == 1 ? "implementation" : "implementations";
			var command = new Command
			{
				Title = $"{implsCount} {text}",
				Name = "surab-lsp-client-vscode.showImplementations",
				Arguments = JArray.FromObject(new object[]
				{
					// Commented out is old way. Not needed but kept for reference.
					//data.Uri,
					ConversionHelper.FromTo(symbol.SelectionSpan.Line.Start),
					//impls.Select(x => new Location
					//{
					//	Uri = DocumentUri.FromFileSystemPath(symbol.DeclaringFile!.Path.Value!),
					//	Range = ConversionHelper.FromTo(x.SelectionSpan),
					//}),
				}, _solver.JsonSerializer),
			};
			return new CodeLens
			{
				Range = codeLens.Range,
				Data = codeLens.Data,
				Command = command,
			};
		}

		return codeLens;
	}

	private record class CodeLensData(string FilePath, LinePosition Position);

	protected override CodeLensRegistrationOptions CreateRegistrationOptions(CodeLensCapability capability, ClientCapabilities clientCapabilities)
	{
		return new()
		{
			DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
			ResolveProvider = true,
		};
	}
}
