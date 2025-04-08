using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Surab.Analysis;

namespace Surab.LSPServer.Handlers;

public class CompletionHandler(
	ILogger<CompletionHandler> logger,
	SurabSolver solver) : CompletionHandlerBase
{
	private readonly ILogger _logger = logger;
	private readonly SurabSolver _solver = solver;

	public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
	{
		return _solver.Workspace.LockForRead(() =>
		{
			return Task.FromResult(GetCompletionListCore(request));
		});
	}

	private enum CaretPos
	{
		None,

		Before, // | token

		// Note: Touching == Inside or TouchingEnd
		Inside, // |token to toke|n
		TouchingEnd, // token|

		After, // token |
	}

	private enum TreatTokenAs
	{
		// One of the other two options depending on the target token kind.
		TargetTokenKind,

		// Completion applicable when touching. Replaces the whole token with new text.
		Identifier,

		// Completion applicable when touching end or after. Insert new text at caret pos.
		TriggerOp,
	}

	private CompletionList GetCompletionListCore(CompletionParams request)
	{
		var file = _solver.GetFile(request.TextDocument);
		if (file == null)
			return [];
		var project = _solver.Workspace.GetFileProject(file.Path);
		var reasoner = project.GetReasoner(file);

		var pos = ConversionHelper.FromTo(request.Position);

		// We're going to pick the target token around which we'll test patterns.
		// CaretPos will hold the info about the caret's actual pos relative to the picked token.
		//
		// Steps to pick target token:
		// 1. If caret is inside a comment trivia, exit.
		// 2. If caret touches an identifier, that will always be picked.
		// 3. Otherwise pick the first token preceding the pos.
		//
		// If target token is identifier and is not on same line as pos, exit. (possible because we use span with trivia to locate tokens)
		//
		// Some examples (`foo` represents an identifier token, `.` represents a simple token):
		// `.|foo` => `foo` // 2
		// `.foo|` => `foo` // 2
		// `.| ` => `.` // 3
		// `. | ` => `.` // 3
		// `foo|.` => `foo` // 2
		// `foo |` => `foo` // 3
		// `foo |.` => `foo` // 3
		// `= |;` => `foo` // 3

		var target = default(SyntaxToken?);
		var caretPos = default(CaretPos);

		{
			var tokenAtPos = reasoner.Tree.GetTokenAtWithTrivia(pos);
			if (tokenAtPos == null)
				return [];

			if (tokenAtPos.IsPositionInTrivia(pos, out var trivia, out var leading))
			{
				if (trivia.Value.Kind is SyntaxTriviaKind.LineComment or SyntaxTriviaKind.BlockComment)
				{
					// 1
					return [];
				}

				// 3
				if (leading)
				{
					// In leading whitespace, pick the preceding token.
					target = reasoner.Tree.GetTokenAtWithTrivia(tokenAtPos.SpanWithTrivia.Line.Start.GetCharJustBefore());
				}
				else
				{
					// In trailing whitespace, pick this token.
					target = tokenAtPos;
				}
			}
			else
			{
				if (tokenAtPos.Kind is SyntaxTokenKind.Identifier)
				{
					// 2
					target = tokenAtPos;
				}
				else
				{
					// 3
					target = reasoner.Tree.GetTokenAtWithTrivia(tokenAtPos.SpanWithTrivia.Line.Start.GetCharJustBefore());
				}
			}

			if (target == null)
				return [];

			if (target.Span.Line.Start > pos)
			{
				caretPos = CaretPos.Before;
			}
			else if (target.Span.Line.Contains(pos))
			{
				caretPos = CaretPos.Inside;
			}
			else if (target.Span.Line.End == pos)
			{
				caretPos = CaretPos.TouchingEnd;
			}
			else if (target.Span.Line.End < pos)
			{
				caretPos = CaretPos.After;
			}
		}

		Debug.Assert(target != null && caretPos != default);

		_logger.LogInformation(
			"{Position} {TokenKind} = {Token} ({CaretPos})",
			pos, target.Kind, target, caretPos);

		var textEditSpan = new LineSpan(pos, pos);
		var isApplicableCalled = false;

		{
			// TODO: Attributes.
			// @|
			// @foo|
		}

		{
			// foo.|
			// foo.bar|
			AccessExprNode? accessExpr = null;
			if (target.IsSimpleTokenOfKind(SimpleTokenKind.Dot, out _) && target.Parent is AccessExprNode n)
			{
				accessExpr = n;
			}
			else if (target is IdentifierToken && target.Parent is NameNode && target.Parent.Parent is AccessExprNode n2)
			{
				accessExpr = n2;
			}

			if (accessExpr != null)
			{
				if (!IsApplicable(TreatTokenAs.TargetTokenKind))
					return [];

				var targetType = reasoner.LookupType(accessExpr.Target);
				_logger.LogInformation("Matched: AccessExpr. Resolved type to {Type}.", targetType.Name);
				if (targetType != null)
				{
					return CreateCompletionFromSymbols(targetType.AccessibleMembers);
				}
			}
		}

		{
			// foo::|
			// foo::bar|
			ModuleAccessExprNode? moduleAccessExpr = null;
			if (target.IsSimpleTokenOfKind(SimpleTokenKind.ColonColon, out _) && target.Parent is ModuleAccessExprNode n)
			{
				moduleAccessExpr = n;
			}
			else if (target is IdentifierToken && target.Parent is NameNode && target.Parent.Parent is ModuleAccessExprNode n2)
			{
				moduleAccessExpr = n2;
			}

			if (moduleAccessExpr != null)
			{
				if (!IsApplicable(TreatTokenAs.TargetTokenKind))
					return [];

				var targetModule = reasoner.LookupSymbol(moduleAccessExpr.Target) as ModuleSymbol;
				_logger.LogInformation("Matched: ModuleAccessExpr. Resolved module to {Module}.", targetModule?.Name);
				if (targetModule != null)
				{
					return CreateCompletionFromSymbols(targetModule.ExportedSymbols);
				}
			}
		}

		{
			// a = |
			// a = ident|
			AssignmentExprNode? assignmentExpr = null;
			if (target.IsSimpleTokenOfKind(SimpleTokenKind.Equal, out _) && target.Parent is AssignmentExprNode n)
			{
				assignmentExpr = n;
			}
			else if (target is IdentifierToken && target.Parent is NameNode && target.Parent.Parent is NameExprNode && target.Parent.Parent.Parent is AssignmentExprNode n2)
			{
				assignmentExpr = n2;
			}

			if (assignmentExpr != null)
			{
				if (!IsApplicable(TreatTokenAs.TargetTokenKind))
					return [];

				var targetType = reasoner.LookupType(assignmentExpr.Lhs);
				_logger.LogInformation("Matched: AccessExpr. Resolved type to {Type}.", targetType.Name);
				if (targetType != null)
				{
					// TODO: If type is enum, we should offer the enum's variants (if identifier is empty or starts with .) + the usual local symbols.

					return CreateCompletion(assignmentExpr, ReferencableSymbolKind.Any);
				}
			}
		}

		{
			// EqualsExprClauseNode
			// var a = |
			// var a = ident|
			EqualsExprClauseNode? equalsExprClause = null;
			if (target.IsSimpleTokenOfKind(SimpleTokenKind.Equal, out _) && target.Parent is EqualsExprClauseNode n)
			{
				equalsExprClause = n;
			}
			else if (target is IdentifierToken && target.Parent is NameNode && target.Parent.Parent is NameExprNode && target.Parent.Parent.Parent is EqualsExprClauseNode n2)
			{
				equalsExprClause = n2;
			}

			if (equalsExprClause != null)
			{
				if (!IsApplicable(TreatTokenAs.TargetTokenKind))
					return [];

				return CreateCompletion(equalsExprClause, ReferencableSymbolKind.Any);
			}
		}

		{
			// ColonTypeNode or ExprTypeNode
			// : |
			// : ident|
			SyntaxNode? node = null;
			if (target.IsSimpleTokenOfKind(SimpleTokenKind.Colon, out _) && target.Parent is ColonTypeNode n)
			{
				node = n;
			}
			else if (target is IdentifierToken && target.Parent is NameNode && target.Parent.Parent is NameExprNode && target.Parent.Parent.Parent is ExprTypeNode n2)
			{
				node = n2;
			}

			if (node != null)
			{
				if (!IsApplicable(TreatTokenAs.TargetTokenKind))
					return [];

				return CreateCompletion(node, ReferencableSymbolKind.Type);
			}
		}

		{
			// TODO: Inside fn args
			// f(|)
		}

		{
			//// S.{ | }
			//// S.{ a| }
			//ObjectInitializerNode? objectInitializer = null;
			//ObjectInitializerItemNode? objectInitializerItem = null;
			//if (node is ObjectInitializerNode)
			//{
			//	objectInitializer = (ObjectInitializerNode)node;
			//}
			//else if (node is ObjectInitializerItemNode && node.Parent is ObjectInitializerNode)
			//{
			//	objectInitializer = (ObjectInitializerNode)node.Parent;
			//	objectInitializerItem = (ObjectInitializerItemNode)node;
			//}

			//if (objectInitializerItem != null)
			//{
			//	Debug.Assert(objectInitializer != null);
			//}
			//else if (objectInitializer != null)
			//{

			//}
		}

		{
			// S.{ a: | }

		}

		{
			// ident|
			NameNode? name = null;
			if (target is IdentifierToken && target.Parent is NameNode n)
			{
				name = n;
			}

			if (name != null)
			{
				if (!IsApplicable(TreatTokenAs.TargetTokenKind))
					return [];

				return CreateCompletion(name, ReferencableSymbolKind.Any);
			}
		}

		return [];

		// Should be called before processing the completion list.
		bool IsApplicable(TreatTokenAs treatAs)
		{
			isApplicableCalled = true;
			treatAs = treatAs switch
			{
				TreatTokenAs.TargetTokenKind => target.Kind is SyntaxTokenKind.Identifier ?
					TreatTokenAs.Identifier :
					TreatTokenAs.TriggerOp,
				_ => treatAs,
			};

			if (treatAs is TreatTokenAs.Identifier)
			{
				textEditSpan = target.Span.Line;
			}

			return treatAs switch
			{
				TreatTokenAs.Identifier => caretPos is CaretPos.Inside or CaretPos.TouchingEnd,
				TreatTokenAs.TriggerOp => caretPos is CaretPos.TouchingEnd or CaretPos.After,
				_ => throw new UnreachableException(),
			};
		}

		CompletionList CreateCompletion(SyntaxNode node, ReferencableSymbolKind kind)
		{
			Debug.Assert(isApplicableCalled);

			var referencable = reasoner
				.CollectAllReferencableSymbolsFromNode(node, kind)
				.Select(CreateUnresolvedCompletionItem);

			// TODO: Add more reserved/contextual keywords based on context. (else, impl, as, etc)

			IEnumerable<CompletionItem> keywords = [];
			if (kind == ReferencableSymbolKind.Any)
			{
				keywords = new[] { "var", "struct", "trait", "alias" }.Select(x => new CompletionItem
				{
					Label = x,
					Kind = CompletionItemKind.Keyword,
					TextEdit = new(new TextEdit { Range = ConversionHelper.FromTo(textEditSpan), NewText = x }),
				});
			}

			return Wrap([.. referencable, .. keywords]);
		}

		CompletionList CreateCompletionFromSymbols(IEnumerable<Symbol> symbols)
		{
			Debug.Assert(isApplicableCalled);

			return Wrap(symbols.Select(CreateUnresolvedCompletionItem));
		}

		CompletionItem CreateUnresolvedCompletionItem(Symbol symbol)
		{
			return new CompletionItem
			{
				Label = symbol.Name,
				Kind = GetCompletionItemKind(symbol),
				TextEdit = new(new TextEdit { Range = ConversionHelper.FromTo(textEditSpan), NewText = GetNewText() }),
				Command = GetCommand(),
				//Data = for resolve
			};

			string GetNewText()
			{
				return symbol switch
				{
					ModuleSymbol => $"{symbol.Name}::",
					_ => symbol.Name,
				};
			}

			Command? GetCommand()
			{
				return symbol switch
				{
					ModuleSymbol => new Command
					{
						Name = "editor.action.triggerSuggest"
					},
					_ => null,
				};
			}
		}

		static CompletionList Wrap(IEnumerable<CompletionItem> completionItems)
		{
			var list = new CompletionList(completionItems);
			return list;
		}
	}

	private static CompletionItemKind GetCompletionItemKind(Symbol symbol)
	{
		return symbol.Kind switch
		{
			Analysis.SymbolKind.Const => CompletionItemKind.Constant,
			Analysis.SymbolKind.Fn => ((FnSymbol)symbol).IsMethod ? CompletionItemKind.Method : CompletionItemKind.Function,
			Analysis.SymbolKind.Field => CompletionItemKind.Field,
			Analysis.SymbolKind.Local => CompletionItemKind.Variable,
			Analysis.SymbolKind.This => CompletionItemKind.Keyword,
			Analysis.SymbolKind.Param => CompletionItemKind.Variable,
			Analysis.SymbolKind.Module => CompletionItemKind.Module,
			Analysis.SymbolKind.Type => ((TypeSymbol)symbol).TypeKind switch
			{
				TypeSymbolKind.Struct => CompletionItemKind.Struct,
				//TypeSymbolKind.Class => CompletionItemKind.Class,
				TypeSymbolKind.Trait => CompletionItemKind.Interface,
				TypeSymbolKind.GenericParam => CompletionItemKind.TypeParameter,
				TypeSymbolKind.Fn => CompletionItemKind.Struct, // Is there no better one for an fn type?
				TypeSymbolKind.Alias => GetCompletionItemKind(((AliasTypeSymbol)symbol).UnwrapTypeAliases()),
				_ => CompletionItemKind.Text,
			},
			_ => CompletionItemKind.Text,
		};
	}

	public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
	{
		return _solver.Workspace.LockForRead(() =>
		{
			// Detail
			// LabelDetails
			// Documentation

			//Documentation = new MarkupContent
			//{
			//	Kind = MarkupKind.Markdown,
			//	Value = $"### This is just to test markup for {symbol.Name}\n\nYes that's how:\n- One\n- two",
			//},

			return Task.FromResult(request);
		});
	}

	protected override CompletionRegistrationOptions CreateRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
	{
		return new()
		{
			DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
			ResolveProvider = true,
			CompletionItem = new()
			{
				LabelDetailsSupport = true,
			},
			TriggerCharacters = new Container<string>(" ", "@", ".", ":", "("),
		};
	}
}
