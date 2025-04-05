using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Surab.Analysis;

namespace Surab.LSPServer.Handlers;

public class FoldingRangeHandler(
	ILogger<FoldingRangeHandler> logger,
	SurabSolver solver) : FoldingRangeHandlerBase
{
	private readonly ILogger _logger = logger;
	private readonly SurabSolver _solver = solver;

	public override async Task<Container<FoldingRange>?> Handle(FoldingRangeRequestParam request, CancellationToken cancellationToken)
	{
		return _solver.Workspace.LockForRead(() =>
		{
			var file = _solver.GetFile(request.TextDocument);
			if (file == null)
				return null;
			var project = _solver.Workspace.GetFileProject(file.Path);
			var reasoner = project.GetReasoner(file);

			List<FoldingRange> ranges = [];
			Visitor.Visit(reasoner.Tree, ranges);

			return new Container<FoldingRange>(ranges);
		});
	}

	protected override FoldingRangeRegistrationOptions CreateRegistrationOptions(FoldingRangeCapability capability, ClientCapabilities clientCapabilities)
	{
		return new()
		{
			DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
		};
	}

	private class Visitor(List<FoldingRange> ranges) : SyntaxTreeVisitor
	{
		public static void Visit(SyntaxTree tree, List<FoldingRange> ranges)
		{
			var visitor = new Visitor(ranges);
			visitor.Visit(tree.Root);
		}

		public override void Visit(SyntaxNode node)
		{
			// TODO: Currently a decl fold range starts from the attrs list, which when collapsed
			// obscures the decl type and name. vscode also doesn't support collapsed text.
			// Maybe try and calculate a starting span that skips the attrs list for visibility.

			var span = node.Span.Line;
			if (!IgnoreFold(node.Kind) && span.Start.Line != span.End.Line)
			{
				var collapsedText = ComputeCollapsedText(node);
				Fold(span, collapsedText: collapsedText);
			}

			base.Visit(node);
		}

		public override void Visit(SyntaxToken token)
		{
			foreach (var trivia in token.LeadingTrivia)
			{
				ProcessTrivia(trivia);
			}
			foreach (var trivia in token.TrailingTrivia)
			{
				ProcessTrivia(trivia);
			}

			base.Visit(token);
		}

		public override void VisitImportsList(ImportsListNode node)
		{
			Fold(node, FoldingRangeKind.Imports);

			base.VisitImportsList(node);
		}

		private void ProcessTrivia(SyntaxTrivia trivia)
		{
			if (trivia.Kind is SyntaxTriviaKind.BlockComment)
			{
				Fold(trivia.Span.Line, FoldingRangeKind.Comment);
			}
		}

		private void Fold(SyntaxNode node, FoldingRangeKind? kind = null, string? collapsedText = null)
		{
			Fold(node.Span.Line, kind, collapsedText);
		}

		private void Fold(LineSpan span, FoldingRangeKind? kind = null, string? collapsedText = null)
		{
			ranges.Add(new FoldingRange
			{
				StartLine = span.Start.Line,
				StartCharacter = span.Start.Char,
				EndLine = span.End.Line,
				EndCharacter = span.End.Char,
				Kind = kind ?? new FoldingRangeKind("node"),
				CollapsedText = collapsedText,
			});
		}

		private bool IgnoreFold(SyntaxNodeKind kind) => kind is
			SyntaxNodeKind.File or
			SyntaxNodeKind.DeclsList;

		private string? ComputeCollapsedText(SyntaxNode node)
		{
			// vscode currently doesn't support this capability. It's safe to set but will be ignored.
			var mainIdentifier = node.GetMainIdentifierToken();
			if (mainIdentifier != null)
			{
				var tag = node.GetFriendlyDeclTag();
				return $"{tag} {mainIdentifier.Value}";
			}
			return mainIdentifier?.Value;
		}
	}
}
