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
			if (node is ImportsListNode)
			{
				Fold(node, FoldingRangeKind.Imports);
			}
			else if (node is StructDeclNode structDecl)
			{
				Fold(structDecl.OpenBraceToken.Span.Line.Start, structDecl.ClosedBraceToken.Span.Line.End);
			}
			else if (node is TraitDeclNode traitDecl)
			{
				Fold(traitDecl.OpenBraceToken.Span.Line.Start, traitDecl.ClosedBraceToken.Span.Line.End);
			}
			else if (node is ImplDeclNode implDecl)
			{
				Fold(implDecl.OpenBraceToken.Span.Line.Start, implDecl.ClosedBraceToken.Span.Line.End);
			}
			else if (!IgnoreFold(node.Kind))
			{
				Fold(node.Span.Line);
			}

			base.Visit(node);
		}

		public override void Visit(SyntaxToken token)
		{
			ProcessTriviaList(token.LeadingTrivia);
			ProcessTriviaList(token.TrailingTrivia);
		}

		private void ProcessTriviaList(IEnumerable<SyntaxTrivia> triviaList)
		{
			// Fold block comments.
			// Fold continuous list of line comments only separated by whitespace.

			// TODO: The logic here sucks. Redo later.
			var lineCommentStartSpan = default(LinePosition?);
			var lineCommentEndSpanTentative = default(LinePosition?);

			foreach (var trivia in triviaList)
			{
				if (trivia.Kind is SyntaxTriviaKind.LineComment)
				{
					if (lineCommentStartSpan == null)
					{
						lineCommentStartSpan = lineCommentEndSpanTentative = trivia.Span.Line.Start;
					}
					else if (IsContinuous(lineCommentEndSpanTentative, trivia.Span.Line.Start))
					{
						lineCommentEndSpanTentative = trivia.Span.Line.Start;
					}
					else
					{
						TryFoldLineTriviaAndReset();
						lineCommentStartSpan = lineCommentEndSpanTentative = trivia.Span.Line.Start;
					}
				}

				if (trivia.Kind is SyntaxTriviaKind.BlockComment)
				{
					TryFoldLineTriviaAndReset();
					Fold(trivia.Span.Line, FoldingRangeKind.Comment);
				}
			}

			TryFoldLineTriviaAndReset();

			void TryFoldLineTriviaAndReset()
			{
				if (lineCommentStartSpan != null && lineCommentEndSpanTentative != null)
				{
					Fold(lineCommentStartSpan.Value, lineCommentEndSpanTentative.Value, FoldingRangeKind.Comment);
					lineCommentStartSpan = lineCommentEndSpanTentative = null;
				}
			}

			static bool IsContinuous(LinePosition? pos1, LinePosition? pos2)
			{
				if (pos1 == null || pos2 == null)
					return false;
				return pos1.Value.Line == pos2.Value.Line || pos1.Value.Line + 1 == pos2.Value.Line;
			}
		}

		private void Fold(SyntaxNode node, FoldingRangeKind? kind = null, string? collapsedText = null)
		{
			Fold(node.Span.Line, kind, collapsedText);
		}

		private void Fold(LineSpan span, FoldingRangeKind? kind = null, string? collapsedText = null)
		{
			Fold(span.Start, span.End, kind, collapsedText);
		}

		private void Fold(LinePosition start, LinePosition end, FoldingRangeKind? kind = null, string? collapsedText = null)
		{
			if (start.Line == end.Line)
			{
				return;
			}

			ranges.Add(new FoldingRange
			{
				StartLine = start.Line,
				StartCharacter = start.Char,
				EndLine = end.Line,
				EndCharacter = end.Char,
				Kind = kind ?? new FoldingRangeKind("node"),
				CollapsedText = collapsedText,
			});
		}

		private bool IgnoreFold(SyntaxNodeKind kind) => kind is
			SyntaxNodeKind.File or
			SyntaxNodeKind.DeclsList or
			SyntaxNodeKind.ConstDecl or
			SyntaxNodeKind.VarDecl or
			SyntaxNodeKind.MethodDecl or
			SyntaxNodeKind.FnDecl or
			SyntaxNodeKind.AliasDecl or
			SyntaxNodeKind.FieldDecl or
			SyntaxNodeKind.Param or
			SyntaxNodeKind.Arg or
			SyntaxNodeKind.NameDef or
			SyntaxNodeKind.Name or
			SyntaxNodeKind.GenericParam or
			SyntaxNodeKind.GenericArg or
			SyntaxNodeKind.ExprType or
			SyntaxNodeKind.PtrType;
	}
}
