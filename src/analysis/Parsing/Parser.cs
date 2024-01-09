using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Surab.Analysis;

public static class Parser
{
	/// <summary>
	/// Parses the src into a file.
	/// </summary>
	public static SyntaxTree ParseFile(string src) => ParseFile(SourceFile.FromText(src));

	/// <summary>
	/// Parses the src into a file.
	/// </summary>
	public static SyntaxTree ParseFile(SourceFile sourceFile)
	{
		var tokens = Lexer.Lex(sourceFile.Text);
		var parser = new ParserImpl(sourceFile, tokens);
		return parser.ParseAsFile();
	}

	/// <summary>
	/// Parses the src into a declaration.
	/// </summary>
	public static SyntaxTree ParseDecl(string src) => ParseDecl(SourceFile.FromText(src));

	/// <summary>
	/// Parses the src into a declaration.
	/// </summary>
	public static SyntaxTree ParseDecl(SourceFile sourceFile)
	{
		var tokens = Lexer.Lex(sourceFile.Text);
		var parser = new ParserImpl(sourceFile, tokens);
		return parser.ParseAsDecl();
	}

	/// <summary>
	/// Parses the src into a statement.
	/// </summary>
	public static SyntaxTree ParseStmt(string src) => ParseStmt(SourceFile.FromText(src));

	/// <summary>
	/// Parses the src into a statement.
	/// </summary>
	public static SyntaxTree ParseStmt(SourceFile sourceFile)
	{
		var tokens = Lexer.Lex(sourceFile.Text);
		var parser = new ParserImpl(sourceFile, tokens);
		return parser.ParseAsStmt();
	}

	/// <summary>
	/// Parses the src into a expression.
	/// </summary>
	public static SyntaxTree ParseExpr(string src) => ParseExpr(SourceFile.FromText(src));

	/// <summary>
	/// Parses the src into an expression.
	/// </summary>
	public static SyntaxTree ParseExpr(SourceFile sourceFile)
	{
		var tokens = Lexer.Lex(sourceFile.Text);
		var parser = new ParserImpl(sourceFile, tokens);
		return parser.ParseAsExpr();
	}
}

internal class ParserImpl
{
	private readonly string _src;
	private readonly SourceFile _sourceFile;
	private readonly Peeker<Token> _tokens;
	private Token? _current;
	private Token? _prev;
	private DiagnosticBag _diagnostics = new();
	private readonly SyntaxTree _tree;

	// To manage assigning trivia to tokens, we keep track of the trivia collected in this map for ease of consumption by tokens.
	// Each Advance will collect and fill leading list before, and collect and fill trailing list after moving to next non trivia token,
	// and then setting these in this map for the token to consume on creation using `ConsumeTrivia`.
	private readonly Dictionary<int, (List<Token>? Leading, List<Token>? Trailing)> _tokenOffsetToTriviaMap = [];

	public ParserImpl(
		SourceFile sourceFile,
		IEnumerable<Token> tokens)
	{
		_src = sourceFile.Text;
		_sourceFile = sourceFile;
		_tokens = new(tokens);
		_tree = new(sourceFile);

		// So that the parser starts with the first token in current.
		Advance();
	}

	public SyntaxTree MakeSyntaxTree(SyntaxNode root)
	{
		Debug.Assert(IsAtEnd() && _tokenOffsetToTriviaMap.Count == 0);

		var diagnostics = _diagnostics;
		_diagnostics = new();

		_tree.Root = root;
		_tree.Diagnostics = diagnostics;

		return _tree;
	}

	/// <summary>
	/// Advances current to point to the next token. Collects any trivia before and tracks consumed tokens.
	/// </summary>
	private void Advance()
	{
		List<Token>? leadingTriviaTokens;

		if (_current == null && _tokens.TryPeek(0, out _))
		{
			// First call to Advance. CollectAllIncomingTriviaTokens depends on _current being filled.
			_tokens.TryMoveNext(out var token);
			_current = token;

			leadingTriviaTokens = CollectAllIncomingTriviaTokens();
		}
		else
		{
			_tokens.TryMoveNext(out var token);
			_prev = _current;
			_current = token;

			leadingTriviaTokens = CollectAllIncomingTriviaTokens();
		}

		var trailingTriviaTokens = default(List<Token>?);

		// this.Peek() skips trivia.
		if (Peek(0) == null)
		{
			// We're at end. Try to collect any remaining trivia. We'll make it the trailing trivia of the current token.

			//_tokens.TryMoveNext(out var token);
			//_current = token;

			trailingTriviaTokens = CollectRestOfTriviaTokensAfterCurrentWithoutMoving();
		}

		if (_current != null)
		{
			_tokenOffsetToTriviaMap.Add(_current.Span.File.Start, (leadingTriviaTokens, trailingTriviaTokens));
		}
	}

	/// <summary>
	/// Only called when a current token is expected to exist. Throws if it doesn't.
	/// </summary>
	[DebuggerStepThrough]
	private Token AssertedCurrent() =>
		_current ?? throw new Exception("Expected current token to exist.");

	[DebuggerStepThrough]
	private Token AssertedPrev() =>
		_prev ?? throw new Exception("Expected prev token to exist.");

	[DebuggerStepThrough]
	[MemberNotNullWhen(false, nameof(_current))]
	private bool IsAtEnd() => _current == null;

	/// <summary>
	/// Peek ahead by n tokens (skipping trivia). n = 0 => next token.
	/// </summary>
	[DebuggerStepThrough]
	private Token? Peek(int n)
	{
		var nonTriviaTokensSkipped = 0; // Counts non trivia tokens until we reach n.
		var i = 0;

		while (true)
		{
			if (_tokens.TryPeek(i, out var result))
			{
				if (!result.Kind.IsTrivia())
				{
					nonTriviaTokensSkipped++;
					if (nonTriviaTokensSkipped - 1 == n)
					{
						return result;
					}
				}
			}
			else
			{
				return null;
			}

			i++;
		}
	}

	[DebuggerStepThrough]
	private Token? P1() => Peek(0);

	// ---

	public SyntaxTree ParseAsFile()
	{
		var file = ParseFile();
		return MakeSyntaxTree(file);
	}

	public SyntaxTree ParseAsDecl()
	{
		var decl = ParseDecl();
		return MakeSyntaxTree(decl);
	}

	public SyntaxTree ParseAsStmt()
	{
		var stmt = ParseStmt();
		return MakeSyntaxTree(stmt);
	}

	public SyntaxTree ParseAsExpr()
	{
		var expr = ParseExpr();
		return MakeSyntaxTree(expr);
	}

	private DeclNode RecoverDecl()
	{
		var skipped = RecoverMain();
		var skippedNode = new SkippedTokensDeclNode(_tree, skipped);
		ReportSkippedNode(skippedNode);
		return skippedNode;
	}

	private DeclNode RecoverMemberDecl()
	{
		var skipped = RecoverMain();
		var skippedNode = new SkippedTokensDeclNode(_tree, skipped);
		ReportSkippedNode(skippedNode);
		return skippedNode;
	}

	private StmtNode RecoverStmt()
	{
		var skipped = RecoverMain();
		var skippedNode = new SkippedTokensStmtNode(_tree, skipped);
		ReportSkippedNode(skippedNode);
		return skippedNode;
	}

	private void ReportSkippedNode(SyntaxNode node)
	{
		_diagnostics.Add(node.Span, DiagnosticLevel.Error, $"skipped unparsed tokens");
	}

	private List<SkippedToken> RecoverMain()
	{
		if (IsAtEnd())
			return [];

		var list = new List<SkippedToken>();

		// Keep advancing till we find a good place to stop.
		// TODO: Split recovery mode between decl, stmt, etc?

		while (true)
		{
			Advance();
			var prev = AssertedPrev();
			var (leading, trailing) = ConsumeTrivia(prev);
			list.Add(new SkippedToken(_tree, leading, trailing, prev));

			if (IsAtEnd()) break;

			if (prev.Kind is TokenKind.Semicolon ||
				_current.Kind.CanStartDecl() ||
				_current.Kind.CanStartStmt())
			{
				break;
			}
		}

		return list;
	}

	/// <summary>
	/// Keeps moving to next as long as _current is a trivia token. Returns collected trivia tokens.
	/// </summary>
	private List<Token>? CollectAllIncomingTriviaTokens()
	{
		var list = default(List<Token>?);
		while (!IsAtEnd() && _current.Kind.IsTrivia())
		{
			_tokens.TryMoveNext(out var token);
			// _prev and _current should always point to a non trivia outside of CollectAnyTrivia.
			// So we don't update _prev at all, but we update _current here as we keep going until the next token is not trivia.
			var triviaToken = _current;
			_current = token;

			(list ??= []).Add(triviaToken);
		}
		return list;
	}

	private List<Token>? CollectRestOfTriviaTokensAfterCurrentWithoutMoving()
	{
		Debug.Assert(Peek(0) == null);

		var list = default(List<Token>?);
		var i = 0;
		while (_tokens.TryPeek(i, out var triviaToken))
		{
			Debug.Assert(triviaToken.Kind.IsTrivia());
			(list ??= []).Add(triviaToken);
			i++;
		}
		return list;
	}

	/// <summary>
	/// Consume the trivia associated to the given token. Every token must call this method.
	/// </summary>
	private (List<Token> LeadingTriviaTokens, List<Token> TrailingTriviaTokens) ConsumeTrivia(Token token)
	{
		var offset = token.Span.File.Start;
		Debug.Assert(_tokenOffsetToTriviaMap.ContainsKey(offset));

		var (leadingTriviaTokens, trailingTriviaTokens) = _tokenOffsetToTriviaMap[offset];
		_tokenOffsetToTriviaMap.Remove(offset);

		return (leadingTriviaTokens ?? [], trailingTriviaTokens ?? []);
	}

	// ---

	/// <summary>
	/// Returns `true` if the current token matches the given <see cref="TokenKind"/>.
	/// </summary>
	[DebuggerStepThrough]
	[MemberNotNullWhen(true, [nameof(_current)])]
	private bool CheckToken(TokenKind tokenKind) =>
		_current?.Kind == tokenKind;

	/// <summary>
	/// If the current token matches the given [TokenKind], advances and returns `true`.
	/// </summary>
	private bool TryEatToken(TokenKind tokenKind)
	{
		if (CheckToken(tokenKind))
		{
			Advance();
			return true;
		}
		return false;
	}

	private SimpleToken? TryEatSimpleToken(TokenKind tokenKind)
	{
		if (TryEatToken(tokenKind))
		{
			var prev = AssertedPrev();
			var (leading, trailing) = ConsumeTrivia(prev);
			return SimpleToken.Create(_tree, leading, trailing, prev);
		}
		return null;
	}

	private Token EatAssertedToken(TokenKind tokenKind)
	{
		if (!TryEatToken(tokenKind))
		{
			throw new Exception($"Expected an asserted token {tokenKind}.");
		}
		return AssertedPrev();
	}

	private SimpleToken EatAssertedSimpleToken(TokenKind tokenKind)
	{
		var token = EatAssertedToken(tokenKind);
		var (leading, trailing) = ConsumeTrivia(token);
		return SimpleToken.Create(_tree, leading, trailing, token);
	}

	private SimpleToken EatExpectedSimpleToken(TokenKind tokenKind)
	{
		if (TryEatToken(tokenKind))
		{
			var prev = AssertedPrev();
			var (leading, trailing) = ConsumeTrivia(prev);
			return SimpleToken.Create(_tree, leading, trailing, prev);
		}
		else
		{
			var token = SimpleToken.CreateMissing(_tree, tokenKind, GetProperSpanForMissingThingAtCurrent());
			_diagnostics.Add(token.Span, DiagnosticLevel.Error, $"expected '{token}'");
			return token;
		}
	}

	private bool CheckContextualKeyword(ContextualKeyword contextualKeyword)
	{
		return CheckToken(TokenKind.Identifier) && TokenHelper.IsContextualKeyword(contextualKeyword, GetLexeme(_current));
	}

	private SimpleToken EatAssertedContextualKeywordToken(ContextualKeyword contextualKeyword)
	{
		Debug.Assert(CheckContextualKeyword(contextualKeyword));
		Advance();
		var prev = AssertedPrev();
		var (leading, trailing) = ConsumeTrivia(prev);
		return SimpleToken.Create(_tree, leading, trailing, contextualKeyword, prev);
	}

	private SimpleToken EatExpectedContextualKeywordToken(ContextualKeyword contextualKeyword)
	{
		if (CheckContextualKeyword(contextualKeyword))
		{
			return EatAssertedContextualKeywordToken(contextualKeyword);
		}
		else
		{
			var token = SimpleToken.CreateMissing(_tree, contextualKeyword, GetProperSpanForMissingThingAtCurrent());
			_diagnostics.Add(token.Span, DiagnosticLevel.Error, $"expected '{contextualKeyword}'");
			return token;
		}
	}

	private MissingTypeNode CreateMissingTypeNode(AttrsListNode? attrs)
	{
		// TODO: Is the span shrinking here correct?
		var node = new MissingTypeNode(_tree, attrs == null ? GetProperSpanForMissingThingAtCurrent() : attrs.Span.ShrinkToEnd(), attrs);
		_diagnostics.Add(node.Span, DiagnosticLevel.Error, $"expected type");
		return node;
	}

	private MissingExprNode CreateMissingExprNode()
	{
		var node = new MissingExprNode(_tree, GetProperSpanForMissingThingAtCurrent());
		_diagnostics.Add(node.Span, DiagnosticLevel.Error, $"expected expression");
		return node;
	}

	private TextSpan GetProperSpanForMissingThingAtCurrent()
	{
		if (_current == null)
			return new();

		// Try to get a span at the start of the first leading trivia of the current token.
		// e.g. This gives us a span at the newline to report a missing semicolon, instead of at the next token
		// after it (which is probably on another line, more confusing).
		if (_tokenOffsetToTriviaMap.TryGetValue(_current.Span.File.Start, out var t) && t.Leading != null)
		{
			return t.Leading[0].Span.ShrinkToStart();
		}

		return _current.Span.ShrinkToStart();
	}

	// TODO: Find usages of this to intern strings.
	private ReadOnlySpan<char> GetLexeme(Token token) => GetLexeme(token.Span);

	private ReadOnlySpan<char> GetLexeme(TextSpan span)
	{
		var (start, end) = span.File;
		return _src.AsSpan()[start..end];
	}

	// ---

	private FileNode ParseFile()
	{
		var importsList = ParseImportsList();
		var declsList = ParseDeclsList();

		return new FileNode(_tree, importsList, declsList);
	}

	private ImportsListNode ParseImportsList()
	{
		var imports = new List<ImportNode>();

		while (!IsAtEnd() && CheckContextualKeyword(ContextualKeyword.Import))
		{
			imports.Add(ParseImport());
		}

		return new ImportsListNode(_tree, imports);
	}

	/// <summary>
	/// Parses `import "..."`
	/// Requirements: current is Identifier (import)
	/// </summary>
	private ImportNode ParseImport()
	{
		var importToken = EatAssertedContextualKeywordToken(ContextualKeyword.Import);
		var expr = ParseExpr();
		var semicolonToken = EatExpectedSimpleToken(TokenKind.Semicolon);

		return new ImportNode(_tree, importToken, expr, semicolonToken);
	}

	private DeclsListNode ParseDeclsList()
	{
		var decls = new List<DeclNode>();

		while (!IsAtEnd())
		{
			var delcaration = ParseDecl();
			decls.Add(delcaration);
		}

		return new DeclsListNode(_tree, decls);
	}

	// Decls
	// -----

	/// <summary>
	/// Parses any non member decl.
	/// </summary>
	private DeclNode ParseDecl(AttrsListNode? attrs = null)
	{
		// Parse decl, and if we don't know what kind at all, do recovery.

		attrs ??= ParseOptionalAttrsList();

		return _current?.Kind switch
		{
			TokenKind.Const => ParseConstDecl(attrs),
			TokenKind.Var => ParseVarDecl(attrs),
			TokenKind.Fn => ParseFnDecl(attrs),
			TokenKind.Struct => ParseStructDecl(attrs),
			TokenKind.Class => ParseClassDecl(attrs),
			TokenKind.Alias => ParseAliasDecl(attrs),

			_ => RecoverDecl(),
		};
	}

	private DeclNode ParseMemberDecl()
	{
		var attrs = ParseOptionalAttrsList();

		return _current?.Kind switch
		{
			// `identifier: Type = 2;` or `identifier = 2;`
			TokenKind.Identifier when P1()?.Kind is TokenKind.Colon or TokenKind.Equal => ParseFieldDecl(attrs),

			// `identifier(`
			TokenKind.Identifier when P1()?.Kind is TokenKind.LeftParen => ParseMethodDecl(attrs),
			// `(` (initializer)
			TokenKind.LeftParen => ParseMethodDecl(attrs),
			// `+(`
			_ when _current != null && _current.Kind.IsMethodOp() && P1()?.Kind is TokenKind.LeftParen => ParseMethodDecl(attrs),

			// Local decl
			_ when _current != null && _current.Kind.CanStartDecl() => ParseDecl(attrs),

			_ => RecoverMemberDecl(),
		};
	}

	/// <summary>
	/// Parses `const identifier = expr;`
	/// </summary>
	private DeclNode ParseConstDecl(AttrsListNode? attrs)
	{
		var constToken = EatAssertedSimpleToken(TokenKind.Const);
		var identifier = ParseIdentifier();
		var type = ParseOptionalColonType();
		var initializer = ParseOptionalEqualsExprClause();
		var semicolonToken = EatExpectedSimpleToken(TokenKind.Semicolon);

		return new ConstDeclNode(_tree, attrs, constToken, identifier, type, initializer, semicolonToken);
	}

	/// <summary>
	/// Parses `var a: Type = expr;`
	/// </summary>
	private DeclNode ParseVarDecl(AttrsListNode? attrs)
	{
		var varToken = EatAssertedSimpleToken(TokenKind.Var);
		var identifier = ParseIdentifier();
		var type = ParseOptionalColonType();
		var initializer = ParseOptionalEqualsExprClause();
		var semicolonToken = EatExpectedSimpleToken(TokenKind.Semicolon);

		return new VarDeclNode(_tree, attrs, varToken, identifier, type, initializer, semicolonToken);
	}

	/// <summary>
	/// Parses:
	/// - `fn identifier(param1, param2) : Type { ... }`
	/// - `fn identifier(param1, param2) : Type;`
	/// </summary>
	private DeclNode ParseFnDecl(AttrsListNode? attrs)
	{
		var fnToken = EatAssertedSimpleToken(TokenKind.Fn);
		var sig = ParseFnSig(FnSigParseMode.Fn);
		var (body, semicolonToken) = ParseFnBlockOrSemicolon();

		return new FnDeclNode(_tree, attrs, fnToken, sig, body, semicolonToken);
	}

	/// <summary>
	/// Parses `identifier(param1, param2)`
	/// </summary>
	private DeclNode ParseMethodDecl(AttrsListNode? attrs)
	{
		var sig = ParseFnSig(FnSigParseMode.Method);
		var (body, semicolonToken) = ParseFnBlockOrSemicolon();

		return new MethodDeclNode(_tree, attrs, sig, body, semicolonToken);
	}

	private (BlockExprNode?, SimpleToken?) ParseFnBlockOrSemicolon()
	{
		var body = default(BlockExprNode?);
		var semicolonToken = default(SimpleToken?);
		if (CheckToken(TokenKind.LeftBrace))
		{
			body = ParseBlockExpr();
		}
		else
		{
			semicolonToken = TryEatSimpleToken(TokenKind.Semicolon);
		}
		return (body, semicolonToken);
	}

	/// <summary>
	/// Parses `struct Name { ... }`
	/// </summary>
	private DeclNode ParseStructDecl(AttrsListNode? attrs)
	{
		var structToken = EatAssertedSimpleToken(TokenKind.Struct);
		var identifier = ParseIdentifier();
		var openBraceToken = EatExpectedSimpleToken(TokenKind.LeftBrace);
		var members = ParseDeclMembers();
		var closedBraceToken = EatExpectedSimpleToken(TokenKind.RightBrace);

		return new StructDeclNode(_tree, attrs, structToken, identifier, openBraceToken, members, closedBraceToken);
	}

	/// <summary>
	/// Parses `class Name { ... }`
	/// </summary>
	private DeclNode ParseClassDecl(AttrsListNode? attrs)
	{
		var classToken = EatAssertedSimpleToken(TokenKind.Class);
		var identifier = ParseIdentifier();
		var openBraceToken = EatExpectedSimpleToken(TokenKind.LeftBrace);
		var members = ParseDeclMembers();
		var closedBraceToken = EatExpectedSimpleToken(TokenKind.RightBrace);

		return new ClassDeclNode(_tree, attrs, classToken, identifier, openBraceToken, members, closedBraceToken);
	}

	private DeclsListNode ParseDeclMembers()
	{
		var members = new List<DeclNode>();

		while (!IsAtEnd() && !CheckToken(TokenKind.RightBrace))
		{
			var decl = ParseMemberDecl();
			members.Add(decl);
		}

		return new DeclsListNode(_tree, members);
	}

	/// <summary>
	/// Parses `alias Foo as Bar;`
	/// </summary>
	private DeclNode ParseAliasDecl(AttrsListNode? attrs)
	{
		var aliasToken = EatAssertedSimpleToken(TokenKind.Alias);
		var identifier = ParseIdentifier();
		var asToken = EatExpectedContextualKeywordToken(ContextualKeyword.As);
		var type = ParseType();
		var semicolonToken = EatExpectedSimpleToken(TokenKind.Semicolon);

		return new AliasDeclNode(_tree, attrs, aliasToken, identifier, asToken, type, semicolonToken);
	}

	/// <summary>
	/// Parses `identifier: Type = expr;`
	/// Requirements: current is Identifier
	/// </summary>
	private DeclNode ParseFieldDecl(AttrsListNode? attrs)
	{
		var identifier = ParseAssertedIdentifier();
		var type = ParseOptionalColonType();
		var initializer = ParseOptionalEqualsExprClause();
		var semicolonToken = EatExpectedSimpleToken(TokenKind.Semicolon);

		return new FieldDeclNode(_tree, attrs, identifier, type, initializer, semicolonToken);
	}

	// Stmts
	// -----

	/// <summary>
	/// Parses any stmt.
	/// Requirements: not at end.
	/// </summary>
	private StmtNode ParseStmt()
	{
		// Parse stmt, and if we don't know what kind at all, do recovery.

		return _current?.Kind switch
		{
			TokenKind.Return => ParseReturnStmt(),
			_ when _current != null && _current.Kind.CanStartDecl() => ParseDeclStmt(),
			_ when _current != null && _current.Kind.CanStartExpr() => ParseExprStmt(),

			// We have no idea what the shape is, so do stmt level recovery.
			_ => RecoverStmt(),
		};
	}

	/// <summary>
	/// Parses `return expr;`
	/// </summary>
	private StmtNode ParseReturnStmt()
	{
		var returnToken = EatAssertedSimpleToken(TokenKind.Return);
		ExprNode? expr;
		SimpleToken semicolonToken;
		if (CheckToken(TokenKind.Semicolon))
		{
			expr = null;
			semicolonToken = EatAssertedSimpleToken(TokenKind.Semicolon);
		}
		else
		{
			expr = ParseExpr();
			semicolonToken = EatExpectedSimpleToken(TokenKind.Semicolon);
		}

		return new ReturnStmtNode(_tree, returnToken, expr, semicolonToken);
	}

	/// <summary>
	/// Parses a local decl stmt.
	/// Requirements: current can start decl.
	/// </summary>
	private StmtNode ParseDeclStmt()
	{
		var decl = ParseDecl();

		return new DeclStmtNode(_tree, decl);
	}

	/// <summary>
	/// Parses either a BareExprStmtNode or ExprStmtNode.
	/// Requirements: _current.Kind.CanStartExpr() is true
	/// </summary>
	private StmtNode ParseExprStmt()
	{
		var expr = ParseExpr();
		var semicolonToken = TryEatSimpleToken(TokenKind.Semicolon);

		//Debug.Assert(!expr.IsMissing || semicolonToken != null);

		return semicolonToken is null ?
			new BareExprStmtNode(_tree, expr) :
			new ExprStmtNode(_tree, expr, semicolonToken);
	}

	// Exprs
	// -----

	/// <summary>
	/// Parses any expr.
	/// </summary>
	private ExprNode ParseExpr()
	{
		return ParseExprForPrecedence(Precedence.None);
	}

	/// <summary>
	/// Keeps parsing right exprs as long as the next precedence is greater than the given `precedence`.
	/// </summary>
	private ExprNode ParseExprForPrecedence(Precedence precedence)
	{
		if (IsAtEnd()) return CreateMissingExprNode();

		// Struct creation takes priority.
		if (_current.Kind == TokenKind.Identifier && P1()?.Kind == TokenKind.LeftBrace)
		{
			return ParseStructCreationExpr();
		}

		if (!Pratt.IsPrefixRule(_current.Kind))
		{
			// TODO: Should we try to perform a recovery of some kind?
			return CreateMissingExprNode();
		}

		var expr = ParsePrefixExpr();

		while (!IsAtEnd())
		{
			var rule = Pratt.GetInfixRule(_current.Kind);
			if (rule == null) break;

			if (precedence >= rule.Precedence)
			{
				break;
			}

			expr = ParseInfixExpr(rule, expr);
		}

		return expr;
	}

	private ExprNode ParsePrefixExpr()
	{
		var token = AssertedCurrent();
		return token.Kind switch
		{
			TokenKind.LeftParen => ParseGroupingExpr(),
			TokenKind.LeftBrace => ParseBlockExpr(),
			TokenKind.Identifier => ParseIdentifierExpr(),
			TokenKind.This => ParseThisExpr(),
			_ when token.Kind.IsUnary() => ParseUnaryExpr(),
			_ when token.Kind.IsLiteral() => ParseLiteralExpr(),
			_ => CreateMissingExprNode(),
		};
	}

	private ExprNode ParseGroupingExpr()
	{
		var openParenToken = EatAssertedSimpleToken(TokenKind.LeftParen);
		var expr = ParseExpr();
		var closedParenToken = EatExpectedSimpleToken(TokenKind.RightParen);

		return new GroupingExprNode(_tree, openParenToken, expr, closedParenToken);
	}

	private ExprNode ParseIdentifierExpr()
	{
		var identifier = ParseAssertedIdentifier();

		return new IdentifierExprNode(_tree, identifier);
	}

	private ExprNode ParseThisExpr()
	{
		var thisToken = EatAssertedSimpleToken(TokenKind.This);

		return new ThisExprNode(_tree, thisToken);
	}

	/// <summary>
	/// Parses `!a`
	/// </summary>
	private ExprNode ParseUnaryExpr()
	{
		Advance();
		var prev = AssertedPrev();
		var (leading, trailing) = ConsumeTrivia(prev);
		var op = UnaryOperatorToken.Create(_tree, leading, trailing, prev);
		var expr = ParseExprForPrecedence(Precedence.Prefix);

		return new UnaryExprNode(_tree, op, expr);
	}

	private ExprNode ParseLiteralExpr()
	{
		var literal = ParseLiteralToken();

		return new LiteralExprNode(_tree, literal);
	}

	private LiteralToken ParseLiteralToken()
	{
		return AssertedCurrent().Kind switch
		{
			TokenKind.False or TokenKind.True => ParseLiteralBool(),
			TokenKind.NumericLiteral => ParseLiteralNumeric(),
			TokenKind.CharLiteral => ParseLiteralChar(),
			TokenKind.StringLiteral => ParseLiteralString(),
			_ => throw new UnreachableException("unexpected literal"),
		};
	}

	private LiteralToken ParseLiteralBool()
	{
		Advance();
		var token = AssertedPrev();
		var (leading, trailing) = ConsumeTrivia(token);
		return token.Kind switch
		{
			TokenKind.False => LiteralToken.FromBool(_tree, leading, trailing, token, false),
			TokenKind.True => LiteralToken.FromBool(_tree, leading, trailing, token, true),
			_ => throw new UnreachableException(),
		};
	}

	private LiteralToken ParseLiteralNumeric()
	{
		Advance();
		var token = AssertedPrev();
		var lexeme = GetLexeme(token);
		var (leading, trailing) = ConsumeTrivia(token);

		var type = default(string?);
		if (_current?.Kind == TokenKind.NumericLiteralType)
		{
			Advance();
			var prev = AssertedPrev();
			(_, trailing) = ConsumeTrivia(prev);
			type = GetLexeme(prev).ToString();
		}

		LiteralToken literalToken;
		if (ulong.TryParse(lexeme, out var ulongValue))
		{
			literalToken = LiteralToken.FromNumericInteger(_tree, leading, trailing, token, ulongValue);
		}
		else if (double.TryParse(lexeme, out var doubleValue))
		{
			literalToken = LiteralToken.FromNumericFloat(_tree, leading, trailing, token, doubleValue);
		}
		else
		{
			throw new UnreachableException();
		}

		literalToken.NumericType = type;
		return literalToken;
	}

	private LiteralToken ParseLiteralChar()
	{
		Advance();
		var token = AssertedPrev();
		var (leading, trailing) = ConsumeTrivia(token);
		var data = (CharLiteralTokenData)token.Data!;
		if (data.IsTerminated)
		{
			var lexeme = GetLexeme(token);
			var ch = lexeme[1];
			return LiteralToken.FromChar(_tree, leading, trailing, token, ch);
		}
		else
		{
			return LiteralToken.FromChar(_tree, leading, trailing, token, null);
		}
	}

	private LiteralToken ParseLiteralString()
	{
		Advance();
		var token = AssertedPrev();
		var (leading, trailing) = ConsumeTrivia(token);
		var data = (StringLiteralTokenData)token.Data!;
		if (data.IsTerminated)
		{
			var lexeme = GetLexeme(token);
			return LiteralToken.FromString(_tree, leading, trailing, token, lexeme[1..^1].ToString());
		}
		else
		{
			return LiteralToken.FromString(_tree, leading, trailing, token, null);
		}
	}

	private ExprNode ParseInfixExpr(InfixRule rule, ExprNode left)
	{
		var token = AssertedCurrent();
		var targetPrecedence = rule.AdjustedPreference();

		return token.Kind switch
		{
			TokenKind.LeftParen => ParseCallExpr(left),
			TokenKind.Dot => ParseAccessExpr(left),
			TokenKind.Equal => ParseAssignmentExpr(targetPrecedence, left),
			_ when token.Kind.IsBinary() => ParseBinaryExpr(targetPrecedence, left),
			_ => CreateMissingExprNode(),
		};
	}

	private ExprNode ParseCallExpr(ExprNode left)
	{
		// Inside the args list of a call expr, parsing will continue until we hit the closing paren,
		// so we don't pass over the precedence here, otherwise parsing will prematurely stop since a call has the strongest precedence.
		var args = ParseArgsList();

		return new CallExprNode(_tree, left, args);
	}

	private ExprNode ParseAccessExpr(ExprNode left)
	{
		// In an access expr, we always parse one identifier, so we don't pass the precedence.
		var dotToken = EatAssertedSimpleToken(TokenKind.Dot);
		var identifier = ParseIdentifier();

		return new AccessExprNode(_tree, left, dotToken, identifier);
	}

	/// <summary>
	/// Parses `left = right`
	/// </summary>
	private ExprNode ParseAssignmentExpr(Precedence precedence, ExprNode left)
	{
		var equalToken = EatAssertedSimpleToken(TokenKind.Equal);
		var rhs = ParseExprForPrecedence(precedence);

		return new AssignmentExprNode(_tree, left, equalToken, rhs);
	}

	private ExprNode ParseBinaryExpr(Precedence precedence, ExprNode left)
	{
		Advance();
		var prev = AssertedPrev();
		var (leading, trailing) = ConsumeTrivia(prev);
		var op = BinaryOperatorToken.Create(_tree, leading, trailing, prev);
		var right = ParseExprForPrecedence(precedence);

		return new BinaryExprNode(_tree, left, op, right);
	}

	/// <summary>
	/// Parses `{ ... }`
	/// </summary>
	private BlockExprNode ParseBlockExpr()
	{
		var openBraceToken = EatAssertedSimpleToken(TokenKind.LeftBrace);
		var stmts = new List<StmtNode>();

		while (true)
		{
			// REVIEW: Is CanStartStmt() right here?
			if (IsAtEnd() || CheckToken(TokenKind.RightBrace) || !_current.Kind.CanStartStmt())
				break;

			stmts.Add(ParseStmt());
		}

		var closedBraceToken = EatExpectedSimpleToken(TokenKind.RightBrace);

		return new BlockExprNode(_tree, openBraceToken, stmts, closedBraceToken);
	}

	/// <summary>
	/// Parses `Struct1 { a = b, c = d }`
	/// Requirements: current is Identifier and next is LeftParen.
	/// </summary>
	private StructCreationExprNode ParseStructCreationExpr()
	{
		var identifier = ParseAssertedIdentifier();
		var mapInitializer = ParseMapInitializer();

		return new StructCreationExprNode(_tree, identifier, mapInitializer);
	}

	/// <summary>
	/// Parses `{ a = b, c = d }`
	/// Requirements: current is LeftParen.
	private MapInitializerNode ParseMapInitializer()
	{
		var openBraceToken = EatAssertedSimpleToken(TokenKind.LeftBrace);

		var items = new List<MapInitializerItemNode>();

		while (true)
		{
			VerifyLoopIsNotPossiblyInfinite(items, threshold: 1_000_000);

			if (IsAtEnd() || CheckToken(TokenKind.RightBrace) || (!_current.Kind.CanStartExpr() && !CheckToken(TokenKind.Comma)))
				break;

			// Will parse a whole single assignment expr.
			var expr = ParseExpr();
			var commaToken = TryEatSimpleToken(TokenKind.Comma);

			items.Add(new MapInitializerItemNode(_tree, expr, commaToken));
		}

		var closedBraceToken = EatExpectedSimpleToken(TokenKind.RightBrace);

		return new MapInitializerNode(_tree, openBraceToken, items, closedBraceToken);
	}

	// ---

	private EqualsExprClauseNode? ParseOptionalEqualsExprClause()
	{
		if (_current == null || (_current.Kind is not TokenKind.Equal && !_current.Kind.CanStartExpr()))
		{
			return null;
		}

		var equalToken = EatExpectedSimpleToken(TokenKind.Equal);
		var expr = ParseExpr();

		return new EqualsExprClauseNode(_tree, equalToken, expr);
	}

	/// <summary>
	/// Parses `: Type`
	/// </summary>
	private ColonTypeNode? ParseOptionalColonType()
	{
		if (!CheckToken(TokenKind.Colon))
		{
			return null;
		}

		var colonToken = EatAssertedSimpleToken(TokenKind.Colon);
		var type = ParseType();

		return new ColonTypeNode(_tree, colonToken, type);
	}

	private TypeNode? ParseOptionalType()
	{
		var type = default(TypeNode?);
		if (_current?.Kind.CanStartType() ?? false)
		{
			type = ParseType();
		}
		return type;
	}

	/// <summary>
	/// Parses the various type syntax.
	/// - `@extern *NamedType`
	/// - `NamedType`
	/// - `@extern fn (i32) i32`
	/// </summary>
	private TypeNode ParseType()
	{
		var attrs = ParseOptionalAttrsList();

		return _current?.Kind switch
		{
			TokenKind.Asterisk => ParsePtrType(attrs),
			TokenKind.Identifier => ParseNamedType(attrs),
			// Note that in the faulty: `fn some() /* not complete */ fn main() {}`
			// The parser will think the next fn keyword (for main) the start of the fn return type of some.
			// Currently, can't figure out a good way to recover this better without doing a lot of lookaheads.
			TokenKind.Fn => ParseFnType(attrs),
			// IsAtEnd check is included here.
			_ => CreateMissingTypeNode(attrs),
		};
	}

	/// <summary>
	/// Parses `*Type`
	/// Requirements: current is Asterisk.
	/// </summary>
	private PtrTypeNode ParsePtrType(AttrsListNode? attrs)
	{
		var asteriskToken = EatAssertedSimpleToken(TokenKind.Asterisk);
		var innerType = default(TypeNode?);
		if (!IsAtEnd() && _current.Kind.CanStartType())
		{
			innerType = ParseType();
		}

		return new PtrTypeNode(_tree, attrs, asteriskToken, innerType);
	}

	/// <summary>
	/// Parses `NamedType`
	/// </summary>
	private NamedTypeNode ParseNamedType(AttrsListNode? attrs)
	{
		var identifier = ParseAssertedIdentifier();

		return new NamedTypeNode(_tree, attrs, identifier);
	}

	/// <summary>
	/// Parses `@extern fn (Type1, Type2) Type3`
	/// Requirements: current is Fn.
	/// </summary>
	private FnTypeNode ParseFnType(AttrsListNode? attrs)
	{
		var fnToken = EatAssertedSimpleToken(TokenKind.Fn);
		var paramTypes = ParseParamTypesList();
		var returnType = ParseOptionalType();

		return new FnTypeNode(_tree, attrs, fnToken, paramTypes, returnType);
	}

	/// <summary>
	/// Parses `(ident1: Type, ident2: Type)`
	/// </summary>
	private ParamsListNode ParseParamsList()
	{
		var openParenToken = EatExpectedSimpleToken(TokenKind.LeftParen);

		var @params = new List<ParamNode>();

		while (true)
		{
			VerifyLoopIsNotPossiblyInfinite(@params);

			if (IsAtEnd() || CheckToken(TokenKind.RightParen) || (!_current.Kind.CanStartParam() && !CheckToken(TokenKind.Comma)))
				break;

			@params.Add(ParseParam());
		}

		var closedParenToken = EatExpectedSimpleToken(TokenKind.RightParen);

		return new ParamsListNode(_tree, openParenToken, @params, closedParenToken);
	}

	/// <summary>
	/// Parses `identifier: Type,`
	/// </summary>
	private ParamNode ParseParam()
	{
		Debug.Assert(!IsAtEnd() && (_current.Kind.CanStartParam() || CheckToken(TokenKind.Comma)));

		var attrs = ParseOptionalAttrsList();
		var identifier = ParseIdentifier();
		var type = ParseOptionalColonType();
		var initializer = ParseOptionalEqualsExprClause();
		var commaToken = TryEatSimpleToken(TokenKind.Comma);

		return new ParamNode(_tree, attrs, identifier, type, initializer, commaToken);
	}

	/// <summary>
	/// Parses `(Type, Type)`
	/// </summary>
	private ParamTypesListNode ParseParamTypesList()
	{
		var openParenToken = EatExpectedSimpleToken(TokenKind.LeftParen);

		var paramTypes = new List<ParamTypeNode>();

		while (true)
		{
			VerifyLoopIsNotPossiblyInfinite(paramTypes);

			if (IsAtEnd() || CheckToken(TokenKind.RightParen) || (!_current.Kind.CanStartType() && !CheckToken(TokenKind.Comma)))
				break;

			paramTypes.Add(ParseParamType());
		}

		var closedParenToken = EatExpectedSimpleToken(TokenKind.RightParen);

		return new ParamTypesListNode(_tree, openParenToken, paramTypes, closedParenToken);
	}

	/// <summary>
	/// Parses `Type,`
	/// </summary>
	private ParamTypeNode ParseParamType()
	{
		Debug.Assert(!IsAtEnd() && (_current.Kind.CanStartType() || CheckToken(TokenKind.Comma)));

		var type = ParseType();
		var commaToken = TryEatSimpleToken(TokenKind.Comma);

		return new ParamTypeNode(_tree, type, commaToken);
	}

	/// <summary>
	/// Parses `(expr1, expr2)`
	/// Requirements: current is LeftParen.
	/// </summary>
	private ArgsListNode ParseArgsList()
	{
		var openParenToken = EatAssertedSimpleToken(TokenKind.LeftParen);

		var args = new List<ArgNode>();

		while (true)
		{
			VerifyLoopIsNotPossiblyInfinite(args);

			if (IsAtEnd() || CheckToken(TokenKind.RightParen) || (!_current.Kind.CanStartExpr() && !CheckToken(TokenKind.Comma)))
				break;

			args.Add(ParseArg());
		}

		var closedParenToken = EatExpectedSimpleToken(TokenKind.RightParen);

		return new ArgsListNode(_tree, openParenToken, args, closedParenToken);
	}

	private ArgNode ParseArg()
	{
		Debug.Assert(!IsAtEnd() && (_current.Kind.CanStartExpr() || CheckToken(TokenKind.Comma)));

		var expr = ParseExpr();
		var commaToken = TryEatSimpleToken(TokenKind.Comma);

		return new ArgNode(_tree, expr, commaToken);
	}

	/// <summary>
	/// In Method mode: Allows empty (initializer) and operators as identifiers for the sig's identifier.
	/// </summary>
	private enum FnSigParseMode
	{
		Fn,
		Method,
	}

	private FnSigNode ParseFnSig(FnSigParseMode mode)
	{
		var identifier = mode switch
		{
			FnSigParseMode.Fn => ParseIdentifier(),
			FnSigParseMode.Method => ParseMethodIdentifier(),
			_ => throw new UnreachableException(),
		};
		var @params = ParseParamsList();
		var returnType = ParseOptionalType();

		return new FnSigNode(_tree, identifier, @params, returnType);

		IdentifierToken ParseMethodIdentifier()
		{
			// Check ParseMemberDecl to see what tokens are expected here.

			if (_current?.Kind.IsMethodOp() ?? false)
			{
				Advance();
				var token = AssertedPrev();
				var (leading, trailing) = ConsumeTrivia(token);
				var lexeme = GetLexeme(token);
				return IdentifierToken.Create(_tree, leading, trailing, token.Span, lexeme.ToString());
			}
			else if (_current?.Kind is TokenKind.LeftParen)
			{
				// Empty identifier (initializer method)
				return IdentifierToken.Create(_tree, [], [], GetProperSpanForMissingThingAtCurrent(), string.Empty);
			}
			else
			{
				return ParseIdentifier();
			}
		}
	}

	private IdentifierToken ParseIdentifier()
	{
		return ParseOptionalIdentifier() ?? CreateMissingIdentifierToken();

		IdentifierToken CreateMissingIdentifierToken()
		{
			var token = IdentifierToken.CreateMissing(_tree, GetProperSpanForMissingThingAtCurrent());
			_diagnostics.Add(token.Span, DiagnosticLevel.Error, $"expected an identifier");
			return token;
		}
	}

	private IdentifierToken? ParseOptionalIdentifier()
	{
		if (!TryEatToken(TokenKind.Identifier))
		{
			return null;
		}

		var token = AssertedPrev();
		var lexeme = GetLexeme(token);

		var (leading, trailing) = ConsumeTrivia(token);
		return IdentifierToken.Create(_tree, leading, trailing, token.Span, lexeme.ToString());
	}

	private IdentifierToken ParseAssertedIdentifier()
	{
		var identifier = ParseOptionalIdentifier();
		Debug.Assert(identifier != null);
		return identifier;
	}

	/// <summary>
	/// Parses `@attr1 @attr2(false) ...`
	/// </summary>
	private AttrsListNode? ParseOptionalAttrsList()
	{
		var attrs = default(List<AttrNode>?);

		while (true)
		{
			VerifyLoopIsNotPossiblyInfinite(attrs);

			if (!CheckToken(TokenKind.AtBeforeIdentifier))
			{
				break;
			}

			var attr = ParseAttr();

			attrs ??= [];
			attrs.Add(attr);
		}

		if (attrs == null)
			return null;

		return new AttrsListNode(_tree, attrs);
	}

	/// <summary>
	/// Parses `@attr` or `@attr(...)`
	/// Requirements: current is AtBeforeIdentifier.
	/// </summary>
	private AttrNode ParseAttr()
	{
		var atToken = EatAssertedSimpleToken(TokenKind.AtBeforeIdentifier);
		var identifier = ParseAssertedIdentifier();

		var args = default(ArgsListNode?);
		if (CheckToken(TokenKind.LeftParen))
		{
			args = ParseArgsList();
		}

		return new AttrNode(_tree, atToken, identifier, args);
	}

	private void VerifyLoopIsNotPossiblyInfinite<T>(IReadOnlyList<T>? list, int threshold = 1_000, [CallerMemberName] string memberName = null!)
	{
		if (list != null && list.Count > threshold)
		{
			throw new Exception($"Possibly infinite loop adding to a list: {memberName}");
		}
	}
}
