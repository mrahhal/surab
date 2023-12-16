using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Surab.Analysis;

namespace Surab.LSPServer.Handlers;

public class SemanticTokensFullHandler(
	ILogger<SemanticTokensFullHandler> logger,
	ILanguageServerConfiguration configuration,
	LanguageServerHost host) : SemanticTokensFullHandlerBase
{
	private readonly ILogger _logger = logger;
	private readonly ILanguageServerConfiguration _configuration = configuration;
	private readonly LanguageServerHost _host = host;

	public override Task<SemanticTokens?> Handle(SemanticTokensParams request, CancellationToken cancellationToken)
	{
		_logger.LogInformation("[SemanticTokensParams]");

		var filePath = request.TextDocument.Uri.GetFileSystemPath();
		var sourceFile = _host.SurabProject.SourceFiles.First(x => x.Path == filePath);

		var document = GetSemanticTokensDocument();
		var builder = document.Create();
		Tokenize(builder, sourceFile, cancellationToken);
		var s = (SemanticTokens?)builder.Commit().GetSemanticTokens();
		return Task.FromResult(s);
	}

	private void Tokenize(
		   SemanticTokensBuilder builder,
		   SourceFile sourceFile,
		   CancellationToken cancellationToken)
	{
		var tree = _host.SurabProject.GetSyntaxTree(sourceFile);
		var reasoner = _host.SurabProject.GetReasoner(sourceFile);

		foreach (var (line, ch, length, tokenType, modifiers) in TokenizeRecursive(tree.Root, reasoner))
		{
			if (tokenType == null)
				continue;

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
						// Strings need to be handled for interpolation.
						_ => null,
					});
				}
				break;

			case IdentifierToken identifierToken:
				{
					if (identifierToken.Parent is IdentifierExprNode identifierExpr)
					{
						var symbol = reasoner.LookupSymbol(identifierExpr);
						builder.SetType(GetTokenType(symbol));
					}
					else if (identifierToken.Parent is NamedTypeNode namedType)
					{
						var symbol = reasoner.LookupSymbol(namedType);
						builder.SetType(GetTokenType(symbol));

						if (symbol is TypeSymbol typeSymbol && typeSymbol.IsBuiltin)
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
							FnSigNode sig => sig.Parent is FnDeclNode ? (SemanticTokenType?)SemanticTokenType.Function : SemanticTokenType.Method,
							StructDeclNode or StructCreationExprNode => SemanticTokenType.Struct,
							FieldDeclNode => SemanticTokenType.Property,
							ParamNode => SemanticTokenType.Parameter,
							VarStmtNode => SemanticTokenType.Variable,
							NamedTypeNode => SemanticTokenType.Type,
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
		if (typeSymbol.IsError) return null;
		return typeSymbol switch
		{
			StructTypeSymbol => (SemanticTokenType?)SemanticTokenType.Struct,
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
