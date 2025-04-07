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
		Inside, // |token or tok|en
		TouchingEnd, // token|
		After, // token |
	}

	private record struct TokenCaretData(SyntaxToken Token, CaretPos Caret);

	private CompletionList GetCompletionListCore(CompletionParams request)
	{
		var file = _solver.GetFile(request.TextDocument);
		if (file == null)
			return [];
		var project = _solver.Workspace.GetFileProject(file.Path);
		var reasoner = project.GetReasoner(file);

		var pos = ConversionHelper.FromTo(request.Position);
		var posMinusOne = pos.GetCharJustBefore();
		var token = reasoner.Tree.GetTokenAtWithTrivia(posMinusOne);
		if (token == null)
			return [];

		CaretPos caretPos;

		if (token.IsPositionInTrivia(pos, out var trivia, out var leading))
		{
			// Inside comment trivia.
			// // |
			// /* | */
			if (trivia.Value.Kind is SyntaxTriviaKind.LineComment or SyntaxTriviaKind.BlockComment)
			{
				return [];
			}
			if (leading)
			{
				// Adjust to preceeding token and set caretPos.
				// adjustedToken | token
				token = reasoner.Tree.GetTokenAt(token.SpanWithTrivia.Line.Start.GetCharJustBefore());
				if (token == null)
					return [];
				caretPos = CaretPos.After;
			}
			else
			{
				// token |
				caretPos = CaretPos.After;
			}
		}
		else
		{
			caretPos = token.Span.Line.End == posMinusOne ?
				CaretPos.TouchingEnd :
				CaretPos.Inside;
		}

		Debug.Assert(caretPos != default);

		_logger.LogInformation(
			"[CompletionHandler] {Position} {TokenKind} = {Token} ({CaretPos})",
			request.Position, token?.Kind, token, caretPos);

		var identifier = default(IdentifierToken?);
		var textEditSpan = new LineSpan(pos, pos);

		{
			// foo.|
			// foo.bar|
			AccessExprNode? accessExpr = null;
			if (token.IsSimpleTokenOfKind(SimpleTokenKind.Dot, out _) && token.Parent is AccessExprNode n)
			{
				accessExpr = n;
			}
			else if (token is IdentifierToken t && token.Parent is NameNode && token.Parent.Parent is AccessExprNode n2)
			{
				accessExpr = n2;
				SetIdentifier(t);
			}

			if (accessExpr != null)
			{
				if (identifier != null && caretPos is CaretPos.After)
					// foo.bar |
					return [];

				var targetType = reasoner.LookupType(accessExpr.Target);
				_logger.LogInformation("[CompletionHandler] AccessExpr ;; Incomplete Text: {Text} ;; Resolved type to {Type}.", identifier?.Value, targetType.Name);
				if (targetType != null)
				{
					return CreateCompletion(targetType.AccessibleMembers);
				}
			}
		}

		{
			// foo::|
			// foo::bar|
			ModuleAccessExprNode? moduleAccessExpr = null;
			if (token.IsSimpleTokenOfKind(SimpleTokenKind.ColonColon, out _) && token.Parent is ModuleAccessExprNode n)
			{
				moduleAccessExpr = n;
			}
			else if (token is IdentifierToken t && token.Parent is NameNode && token.Parent.Parent is ModuleAccessExprNode n2)
			{
				moduleAccessExpr = n2;
				SetIdentifier(t);
			}

			if (moduleAccessExpr != null)
			{
				if (identifier != null && caretPos is CaretPos.After)
					// foo::bar |
					return [];

				var targetModule = reasoner.LookupSymbol(moduleAccessExpr.Target) as ModuleSymbol;
				_logger.LogInformation("[CompletionHandler] ModuleAccessExpr ;; Incomplete Text: {Text} ;; Resolved module to {Module}.", identifier?.Value, targetModule?.Name);
				if (targetModule != null)
				{
					return CreateCompletion(targetModule.ExportedSymbols);
				}
			}
		}

		{
			// a = |
			// a = ident|
			AssignmentExprNode? assignmentExpr = null;
			if (token.IsSimpleTokenOfKind(SimpleTokenKind.Equal, out _) && token.Parent is AssignmentExprNode n)
			{
				assignmentExpr = n;
			}
			else if (token is IdentifierToken t && token.Parent is NameNode && token.Parent.Parent is NameExprNode && token.Parent.Parent.Parent is AssignmentExprNode n2)
			{
				assignmentExpr = n2;
				SetIdentifier(t);
			}

			if (assignmentExpr != null)
			{
				if (identifier != null && caretPos is CaretPos.After)
					// a = ident |
					return [];

				var targetType = reasoner.LookupType(assignmentExpr.Lhs);
				_logger.LogInformation("[CompletionHandler] AccessExpr ;; Incomplete Text: {Text} ;; Resolved type to {Type}.", identifier?.Value, targetType.Name);
				if (targetType != null)
				{
					// TODO: If type is enum, we should offer the enum's variants (if identifier is empty or starts with .) + the usual local symbols.

					return CreateCompletion(GetReferencableSymbolsFromNode(assignmentExpr));
				}
			}
		}

		{
			// var a = |
			// var a = ident|
			// Very similar to above? Factor out and share.
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
			//// ident|
			//if (node is NameNode name)
			//{
			//	Debug.Assert(name.Parent?.Kind is not SyntaxNodeKind.AccessExpr and not SyntaxNodeKind.ModuleAccessExpr);
			//	var identifier = name.Identifier;
			//}
		}

		// keywords (var, struct, trait, etc)
		// Some of these need to be processed as part of the above sections.

		return [];

		IEnumerable<Symbol> GetReferencableSymbolsFromNode(SyntaxNode node)
		{
			return reasoner.CollectAllReferencableSymbolsFromNode(node);
		}

		void SetIdentifier(IdentifierToken it)
		{
			identifier = it;
			textEditSpan = it.Span.Line;
		}

		CompletionList CreateCompletion(IEnumerable<Symbol> symbols)
		{
			return new(symbols.Select(CreateUnresolvedCompletionItem));
		}

		CompletionItem CreateUnresolvedCompletionItem(Symbol symbol)
		{
			return new CompletionItem
			{
				Label = symbol.Name,
				Kind = GetCompletionItemKind(symbol),
				TextEdit = new(new TextEdit { Range = ConversionHelper.FromTo(textEditSpan), NewText = GetNewText() }),
				Documentation = new MarkupContent
				{
					Kind = MarkupKind.Markdown,
					Value = $"### This is just to test markup for {symbol.Name}\n\nYes that's how:\n- One\n- two",
				},
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
		}
	}

	private static CompletionItemKind GetCompletionItemKind(Symbol symbol)
	{
		return symbol.Kind switch
		{
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
				TypeSymbolKind.Fn => CompletionItemKind.Function,
				TypeSymbolKind.Alias => CompletionItemKind.Value,
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
			TriggerCharacters = new Container<string>(".", ":"),
		};
	}
}
