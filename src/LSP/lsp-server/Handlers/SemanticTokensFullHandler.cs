using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Surab.Analysis;

namespace Surab.LSPServer.Handlers;

public class SemanticTokensFullHandler(
	ILogger<SemanticTokensFullHandler> logger,
	SurabSolver solver) : SemanticTokensFullHandlerBase
{
	private readonly ILogger _logger = logger;
	private readonly SurabSolver _solver = solver;

	public override async Task<SemanticTokens?> Handle(SemanticTokensParams request, CancellationToken cancellationToken)
	{
		_logger.LogInformation("[SemanticTokensParams]");

		return _solver.Workspace.LockForRead(() =>
		{
			var sourceFile = _solver.GetFile(request.TextDocument.Uri);
			if (sourceFile == null)
				return null;

			var project = _solver.Workspace.GetFileProject(sourceFile.Path);
			var document = GetSemanticTokensDocument();
			var builder = document.Create();

			Tokenize(project, builder, sourceFile, cancellationToken);
			return builder.Commit().GetSemanticTokens();
		});
	}

	private void Tokenize(
		SurabProject project,
		SemanticTokensBuilder builder,
		ProjectFile file,
		CancellationToken ct)
	{
		var tree = project.GetSyntaxTree(file);
		var reasoner = project.GetReasoner(file);

		ct.ThrowIfCancellationRequested();

		foreach (var (line, ch, length, tokenType, modifiers) in TokenizeRecursive(tree.Root, reasoner))
		{
			if (tokenType == null)
				continue;

			ct.ThrowIfCancellationRequested();

			builder.Push(line, ch, length, tokenType, modifiers);
		}
	}

	private IEnumerable<(int, int, int, SemanticTokenType?, List<SemanticTokenModifier>)> TokenizeRecursive(SyntaxNode node, Reasoner reasoner)
	{
		var triviaTokenBuilder = new TokenBuilder<SyntaxTrivia>();
		var tokenTokenBuilder = new TokenBuilder<SyntaxToken>();

		foreach (var childItem in node.EnumerateBySpanStart())
		{
			if (childItem.Token != null)
			{
				var childToken = childItem.Token;

				foreach (var trivia in childToken.LeadingTrivia)
				{
					triviaTokenBuilder.Init(trivia, reasoner);
					Process(triviaTokenBuilder);
					yield return To(trivia.Span, triviaTokenBuilder);
				}

				tokenTokenBuilder.Init(childToken, reasoner);
				Process(tokenTokenBuilder);
				yield return To(childToken.Span, tokenTokenBuilder);

				foreach (var trivia in childToken.TrailingTrivia)
				{
					triviaTokenBuilder.Init(trivia, reasoner);
					Process(triviaTokenBuilder);
					yield return To(trivia.Span, triviaTokenBuilder);
				}
			}
			else if (childItem.Node != null)
			{
				foreach (var item in TokenizeRecursive(childItem.Node, reasoner))
				{
					yield return item;
				}
			}
		}

		static (int, int, int, SemanticTokenType?, List<SemanticTokenModifier>) To<T>(TextSpan span, TokenBuilder<T> builder)
		{
			var line = span.Line.Start.Line;
			var ch = span.Line.Start.Char;
			var length = span.Size;

			return (line, ch, length, builder.Type, builder.Modifiers);
		}
	}

	private static void Process(TokenBuilder<SyntaxTrivia> builder)
	{
		var trivia = builder.Item;
		var type = trivia.Kind switch
		{
			SyntaxTriviaKind.LineComment or SyntaxTriviaKind.BlockComment => (SemanticTokenType?)SemanticTokenType.Comment,
			_ => null,
		};
		// TODO: Add modifier for doc comment and the like.
		builder.SetType(type);
	}

	private static void Process(TokenBuilder<SyntaxToken> builder)
	{
		var token = builder.Item;
		var reasoner = builder.Reasoner;

		switch (token)
		{
			case SimpleToken simpleToken:
				{
					builder.SetType(simpleToken.SimpleTokenKind switch
					{
						SimpleTokenKind.At when simpleToken.Parent is AttrNode => SemanticTokenType.Decorator,
						_ when simpleToken.IsKeyword => (SemanticTokenType?)SemanticTokenType.Keyword,
						_ => null,
					});
				}
				break;

			case LiteralToken literalToken:
				{
					builder.SetType(literalToken.LiteralTokenKind switch
					{
						LiteralTokenKind.NumericInteger or LiteralTokenKind.NumericFloat => (SemanticTokenType?)SemanticTokenType.Number,
						LiteralTokenKind.Bool => SemanticTokenType.Keyword,
						LiteralTokenKind.Char => SemanticTokenType.String,
						LiteralTokenKind.String => SemanticTokenType.String,
						_ => null,
					});
				}
				break;

			case IdentifierToken identifierToken:
				{
					if (identifierToken.Parent is NameExprNode nameExpr)
					{
						var symbol = reasoner.LookupSymbol(nameExpr);
						builder.SetType(GetTokenType(symbol));
					}
					else if (identifierToken.Parent is ExprTypeNode exprType)
					{
						var symbol = reasoner.LookupSymbol(exprType);
						builder.SetType(GetTokenType(symbol));

						if (symbol is TypeSymbol typeSymbol && typeSymbol.IsBuiltin())
						{
							builder.AddModifier(SemanticTokenModifier.DefaultLibrary);
						}
					}
					else if (identifierToken.DeclaresSymbol())
					{
						var symbol = reasoner.GetDeclaredSymbol(identifierToken);
						builder.SetType(GetTokenType(symbol));

						builder.AddModifier(SemanticTokenModifier.Declaration);
					}
					else
					{
						builder.SetType(identifierToken.Parent switch
						{
							ConstDeclNode => SemanticTokenType.Variable,
							VarDeclNode => SemanticTokenType.Variable,
							FnSigNode sig => sig.Parent is FnDeclNode ? (SemanticTokenType?)SemanticTokenType.Function : SemanticTokenType.Method,
							StructDeclNode or TypeCreationExprNode => SemanticTokenType.Struct,
							FieldDeclNode => SemanticTokenType.Property,
							ParamNode => SemanticTokenType.Parameter,
							TypeNode => SemanticTokenType.Type,
							AttrNode => SemanticTokenType.Decorator,
							_ => null,
						});
					}
				}
				break;

			default:
				break;
		}
	}

	private static SemanticTokenType? GetTokenType(Symbol? symbol)
	{
		return symbol switch
		{
			AliasTypeSymbol aliasType => GetTokenType(aliasType.UnwrapTypeAliases()),
			ModuleSymbol => SemanticTokenType.Namespace,
			// TODO: An identifier could both be a variable and a function so can't just simply change the type to function.
			// Need a better way to represent this in the token. Maybe use custom modifiers (rust adds callable modifier)?
			//ISymbolHasType hasType when hasType.Type.UnwrapAliases().IsFn => SemanticTokenType.Function,
			FieldSymbol => (SemanticTokenType?)SemanticTokenType.Property,
			ParamSymbol => SemanticTokenType.Parameter,
			LocalSymbol => SemanticTokenType.Variable,
			FnSymbol => SemanticTokenType.Function,
			TypeSymbol typeSymbol => GetTokenType(typeSymbol),
			_ => null,
		};
	}

	private static SemanticTokenType? GetTokenType(TypeSymbol typeSymbol)
	{
		if (typeSymbol.IsUnknown()) return null;
		return typeSymbol switch
		{
			NamedTypeSymbol namedType when namedType.TypeKind is TypeSymbolKind.Struct => (SemanticTokenType?)SemanticTokenType.Struct,
			FnTypeSymbol => SemanticTokenType.Function,
			_ => null,
		};
	}

	private SemanticTokensDocument GetSemanticTokensDocument()
	{
		return new SemanticTokensDocument(RegistrationOptions.Legend);
	}

	protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities)
	{
		return new()
		{
			DocumentSelector = LanguageServerHelper.CreateSurabDocumentSelector(),
			Legend = new SemanticTokensLegend
			{
				TokenModifiers = capability.TokenModifiers,
				TokenTypes = capability.TokenTypes,
			},
			Full = true,
		};
	}

	private class TokenBuilder<T>
	{
		public T Item { get; private set; } = default!;
		public Reasoner Reasoner { get; private set; } = default!;

		public SemanticTokenType? Type { get; private set; }
		public List<SemanticTokenModifier> Modifiers { get; private set; } = default!;

		public void Init(T item, Reasoner reasoner)
		{
			Item = item;
			Reasoner = reasoner;

			Type = null;
			Modifiers ??= [];
			Modifiers.Clear();
		}

		public TokenBuilder<T> SetType(SemanticTokenType? type)
		{
			Type = type;
			return this;
		}

		public TokenBuilder<T> AddModifier(SemanticTokenModifier modifier)
		{
			Modifiers.Add(modifier);
			return this;
		}
	}
}
