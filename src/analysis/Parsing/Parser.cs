using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Surab.Analysis;

public static class Parser
{
    /// <summary>
    /// Parses the src into a module.
    /// </summary>
    public static SyntaxTree ParseModule(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src);
        var parser = new ParserImpl(src, tokens);
        return parser.ParseAsModule();
    }

    /// <summary>
    /// Parses the src into a declaration.
    /// </summary>
    public static SyntaxTree ParseDecl(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src);
        var parser = new ParserImpl(src, tokens);
        return parser.ParseAsDecl();
    }

    /// <summary>
    /// Parses the src into a statement.
    /// </summary>
    public static SyntaxTree ParseStmt(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src);
        var parser = new ParserImpl(src, tokens);
        return parser.ParseAsStmt();
    }

    /// <summary>
    /// Parses the src into an expression.
    /// </summary>
    public static SyntaxTree ParseExpr(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src);
        var parser = new ParserImpl(src, tokens);
        return parser.ParseAsExpr();
    }
}

internal class ParserImpl
{
    private readonly string _src;
    private readonly Peeker<Token> _tokens;
    private Token? _current;
    private Token? _prev;
    private readonly List<Token> _tokensConsumed = [];
    private DiagnosticBag _diagnostics = new();

    public ParserImpl(
        string src,
        IEnumerable<Token> tokens)
    {
        _src = src;
        _tokens = new(tokens);

        // So that the parser starts with the first token in current.
        Advance();
    }

    public SyntaxTree MakeSyntaxTree(SyntaxNode root)
    {
        var diagnostics = _diagnostics;
        _diagnostics = new();

        return new(root, diagnostics);
    }

    /// <summary>
    /// Advances current to point to the next token.
    /// </summary>
    private void Advance()
    {
        _tokens.TryMoveNext(out var token);
        if (_prev != null)
        {
            _tokensConsumed.Add(_prev);
        }
        _prev = _current;
        _current = token;
    }

    /// <summary>
    /// Only called when a current token is expected to exist. Throws if it doesn't.
    /// </summary>
    private Token AssertedCurrent() =>
        _current ?? throw new Exception("Expected current token to exist.");

    private Token AssertedPrev() =>
        _prev ?? throw new Exception("Expected prev token to exist.");

    [MemberNotNullWhen(false, nameof(_current))]
    private bool IsAtEnd() => _current == null;

    /// <summary>
    /// Peek ahead by n tokens. n = 0 => next token.
    /// </summary>
    private Token? Peek(int n)
    {
        if (_tokens.TryPeek(n, out var result))
        {
            return result;
        }
        return null;
    }

    private Token? P1() => Peek(0);
    //private Token? P2() => Peek(1);

    // ---

    public SyntaxTree ParseAsModule()
    {
        var decls = new List<DeclNode>();

        while (!IsAtEnd())
        {
            var delcaration = ParseDecl();
            decls.Add(delcaration);
        }

        var module = new ModuleNode(decls);
        return MakeSyntaxTree(module);
    }

    public SyntaxTree ParseAsDecl()
    {
        var decl = ParseDecl();
        return MakeSyntaxTree(decl);
    }

    private DeclNode ParseDecl()
    {
        var decl = ParseDecl_();
        if (decl != null)
        {
            ResetTokensConsumed();
            return decl;
        }
        return RecoverDecl();
    }

    public SyntaxTree ParseAsStmt()
    {
        var stmt = ParseStmt();
        return MakeSyntaxTree(stmt);
    }

    private StmtNode ParseStmt()
    {
        var stmt = ParseStmt_();
        if (stmt != null)
        {
            ResetTokensConsumed();
            return stmt;
        }
        return RecoverStmt();
    }

    public SyntaxTree ParseAsExpr()
    {
        var expr = ParseExpr();
        return MakeSyntaxTree(expr);
    }

    private ExprNode ParseExpr()
    {
        var expr = ParseExpr_();
        if (expr != null)
        {
            ResetTokensConsumed();
            return expr;
        }
        return RecoverExpr();
    }

    private DeclNode RecoverDecl()
    {
        var skipped = RecoverMain();
        return new SkippedTokensDeclNode(skipped);
    }

    private StmtNode RecoverStmt()
    {
        var skipped = RecoverMain();
        return new SkippedTokensStmtNode(skipped);
    }

    private ExprNode RecoverExpr()
    {
        var skipped = RecoverMain();
        return new SkippedTokensExprNode(skipped);
    }

    private SkippedToken RecoverMain()
    {
        // Keep advancing till we find a good place to stop.

        while (true)
        {
            Advance();
            if (IsAtEnd()) break;

            if (_prev?.Kind is TokenKind.Semicolon)
            {
                break;
            }

            var current = AssertedCurrent();
            if (current.Kind.CanStartDecl() || current.Kind.CanStartStmt())
            {
                break;
            }
        }

        var tokens = TakeTokensConsumed();
        var span = TextSpan.FromTo(tokens.First().Span, tokens.Last().Span);

        return new SkippedToken(span, tokens);
    }

    private void ResetTokensConsumed()
    {
        _tokensConsumed.Clear();
    }

    // This happens in one of the recovery of the three main items: decls, stmts, exprs.
    private List<Token> TakeTokensConsumed()
    {
        var tokens = _tokensConsumed;
        if (_prev != null)
        {
            tokens.Add(_prev);
        }
        return tokens;
    }

    // ---

    /// <summary>
    /// Returns `true` if the current token matches the given <see cref="TokenKind"/>.
    /// </summary>
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
            return SimpleToken.From(AssertedPrev());
        }
        return null;
    }

    private void EatAssertedToken(TokenKind tokenKind)
    {
        if (!TryEatToken(tokenKind))
        {
            throw new Exception($"Expected an asserted token {tokenKind}.");
        }
    }

    private SimpleToken EatAssertedSimpleToken(TokenKind tokenKind)
    {
        EatAssertedToken(tokenKind);
        return SimpleToken.From(AssertedPrev());
    }

    private bool EatExpectedToken(TokenKind tokenKind)
    {
        if (!TryEatToken(tokenKind))
        {
            var span = _prev!.Span;
            _diagnostics.Add(span, DiagnosticLevel.Error, $"expected '{tokenKind}'");
            return false;
        }
        return true;
    }

    private SimpleToken EatExpectedSimpleToken(TokenKind tokenKind)
    {
        if (EatExpectedToken(tokenKind))
        {
            return SimpleToken.From(AssertedPrev());
        }
        else
        {
            return SimpleToken.Missing(tokenKind, ShrinkToEndSpanAtCurrent());
        }
    }

    private TextSpan ShrinkToEndSpanAtCurrent() =>
        _current?.Span.ShrinkToEnd() ?? new();

    // TODO: Find usages of this to intern strings.
    private ReadOnlySpan<char> GetLexeme(Token token)
    {
        var (start, end) = token.Span.File;
        return _src.AsSpan()[start..end];
    }

    // Decls
    // ------------

    /// <summary>
    /// Parses outer level decl.
    /// </summary>
    private DeclNode? ParseDecl_()
    {
        var visibility = TryParseVisibility();
        var @extern = TryParseExtern();
        return _current?.Kind switch
        {
            TokenKind.Const => ParseConstDecl(visibility),
            TokenKind.Fn => ParseFnDecl(visibility, @extern),
            TokenKind.Struct => ParseStructDecl(visibility, @extern),
            TokenKind.Class => ParseClassDecl(visibility),

            _ => null,
        };
    }

    private DeclNode? ParseInnerDecl()
    {
        var visibility = TryParseVisibility();
        return _current?.Kind switch
        {
            // `identifier: Type = 2;` or `identifier = 2;`
            TokenKind.Identifier when P1()?.Kind is TokenKind.Colon or TokenKind.Equal => ParseFieldDecl(visibility),

            // `identifier()`
            TokenKind.Identifier when P1()?.Kind is TokenKind.LeftParen => ParseMethodDecl(visibility),

            _ => null,
        };
    }

    /// <summary>
    /// Parses `const identifier = expr;`
    /// </summary>
    private DeclNode? ParseConstDecl(SimpleToken? visibility)
    {
        var constToken = EatAssertedSimpleToken(TokenKind.Const);
        var identifier = ParseIdentifier();
        if (identifier == null) return null;
        var (type, elided) = ParseOptionalColonTy();
        if (!elided && type == null) return null;
        var equalToken = EatExpectedSimpleToken(TokenKind.Equal);
        var value = ParseExpr_();
        if (value == null) return null;
        var semicolonToken = EatExpectedSimpleToken(TokenKind.Semicolon);

        return new ConstDeclNode(visibility, constToken, identifier, type, equalToken, value, semicolonToken);
    }

    /// <summary>
    /// Parses:
    /// - `fn identifier(param1, param2) : Type { ... }`
    /// - `extern fn identifier(param1, param2) : Type;`
    /// </summary>
    private DeclNode? ParseFnDecl(SimpleToken? visibility, SimpleToken? externToken)
    {
        var fnToken = EatAssertedSimpleToken(TokenKind.Fn);
        var sig = ParseFnSig();
        if (sig == null) return null;
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

        return new FnDeclNode(visibility, externToken, fnToken, sig, body, semicolonToken);
    }

    /// <summary>
    /// Parses `struct Name { ... }`
    /// </summary>
    private DeclNode? ParseStructDecl(SimpleToken? visibility, SimpleToken? externToken)
    {
        var structToken = EatAssertedSimpleToken(TokenKind.Struct);
        var identifier = ParseIdentifier();
        if (identifier == null) return null;
        var members = new List<DeclNode>();

        var openBraceToken = EatExpectedSimpleToken(TokenKind.LeftBrace);
        while (!CheckToken(TokenKind.RightBrace))
        {
            var decl = ParseInnerDecl();
            if (decl == null) return null;
            members.Add(decl);
        }
        var closedBraceToken = EatExpectedSimpleToken(TokenKind.RightBrace);

        return new StructDeclNode(visibility, externToken, structToken, identifier, openBraceToken, members, closedBraceToken);
    }

    /// <summary>
    /// Parses `class Name { ... }`
    /// </summary>
    private DeclNode? ParseClassDecl(SimpleToken? visibility)
    {
        var classToken = EatAssertedSimpleToken(TokenKind.Class);
        var identifier = ParseIdentifier();
        if (identifier == null) return null;
        var members = new List<DeclNode>();

        var openBraceToken = EatExpectedSimpleToken(TokenKind.LeftBrace);
        while (!CheckToken(TokenKind.RightBrace))
        {
            var decl = ParseInnerDecl();
            if (decl == null) return null;
            members.Add(decl);
        }
        var closedBraceToken = EatExpectedSimpleToken(TokenKind.RightBrace);

        return new ClassDeclNode(visibility, classToken, identifier, openBraceToken, members, closedBraceToken);
    }

    /// <summary>
    /// Parses `identifier: Type = expr;`
    /// </summary>
    private DeclNode? ParseFieldDecl(SimpleToken? visibility)
    {
        var identifier = ParseIdentifier();
        if (identifier == null) return null;
        var (type, elided) = ParseOptionalColonTy();
        if (!elided && type == null) return null;
        var initializer = ParseEqualsValueClause();
        var semicolonToken = EatExpectedSimpleToken(TokenKind.Semicolon);

        return new FieldDeclNode(visibility, identifier, type, initializer, semicolonToken);
    }

    /// <summary>
    /// Parses `identifier(param1, param2)`
    /// </summary>
    private DeclNode? ParseMethodDecl(SimpleToken? visibility)
    {
        var sig = ParseFnSig();
        if (sig == null) return null;
        // TODO: Need to accept null body (and semicolon) to report errors.
        var body = ParseBlockExpr();
        if (body == null) return null;

        return new MethodDeclNode(visibility, sig, body);
    }

    // Stmts
    // ----------

    private StmtNode? ParseStmt_()
    {
        if (IsAtEnd()) return null;
        var current = _current;

        return current.Kind switch
        {
            TokenKind.Let => ParseLetStmt(),
            TokenKind.Return => ParseReturnStmt(),
            _ when current.Kind.CanStartExpr() => ParseExprStmt(),

            _ => null,
        };
    }

    /// <summary>
    /// Parses `let a: Type? = expr;`
    /// </summary>
    private StmtNode? ParseLetStmt()
    {
        var letToken = EatAssertedSimpleToken(TokenKind.Let);
        var identifier = ParseIdentifier();
        if (identifier == null) return null;
        var (type, elided) = ParseOptionalColonTy();
        if (!elided && type == null) return null;
        var initializer = ParseEqualsValueClause();
        var semicolonToken = EatExpectedSimpleToken(TokenKind.Semicolon);

        return new LetStmtNode(letToken, identifier, type, initializer, semicolonToken);
    }

    /// <summary>
    /// Parses `return expr;`
    /// </summary>
    private StmtNode? ParseReturnStmt()
    {
        var returnToken = EatAssertedSimpleToken(TokenKind.Return);
        var expr = ParseExpr_();
        var semicolonToken = EatExpectedSimpleToken(TokenKind.Semicolon);

        return new ReturnStmtNode(returnToken, expr, semicolonToken);
    }

    private StmtNode? ParseExprStmt()
    {
        var expr = ParseExpr_();
        if (expr == null) return null;
        var semicolonToken = TryEatSimpleToken(TokenKind.Semicolon);

        return semicolonToken is null ?
            new BareExprStmtNode(expr) :
            new ExprStmtNode(expr, semicolonToken);
    }

    // Exprs
    // -----------

    private ExprNode? ParseExpr_() => ParseExprForPrecedence(Precedence.None);

    /// <summary>
    /// Keeps parsing right exprs as long as the next precedence is greater than the given `precedence`.
    /// </summary>
    private ExprNode? ParseExprForPrecedence(Precedence precedence)
    {
        if (IsAtEnd()) return null;

        // Struct creation takes priority.
        if (_current.Kind == TokenKind.Identifier && P1()?.Kind == TokenKind.LeftBrace)
        {
            return ParseStructCreationExpr();
        }

        if (!Pratt.IsPrefixRule(_current.Kind))
        {
            return null;
        }

        var expr = ParsePrefixExpr();
        if (expr == null) return null;

        while (_current != null)
        {
            var rule = Pratt.GetInfixRule(_current.Kind);
            if (rule == null) break;

            if (precedence >= rule.Precedence)
            {
                break;
            }

            expr = ParseInfixExpr(rule, expr);
            if (expr == null) return null;
        }

        return expr;
    }

    private ExprNode? ParsePrefixExpr()
    {
        var token = AssertedCurrent();
        return token.Kind switch
        {
            TokenKind.LeftParen => ParseGroupingExpr(),
            TokenKind.LeftBrace => ParseBlockExpr(),
            TokenKind.Identifier => ParseIdentifierExpr(),
            _ when token.Kind.IsUnary() => ParseUnaryExpr(),
            _ when token.Kind.IsLiteral() => ParseLiteralExpr(),
            _ => throw new UnreachableException(),
        };
    }

    private ExprNode? ParseGroupingExpr()
    {
        var openParenToken = EatAssertedSimpleToken(TokenKind.LeftParen);
        var expr = ParseExpr_();
        if (expr == null) return null;
        var closedParenToken = EatExpectedSimpleToken(TokenKind.RightParen);

        return new GroupingExprNode(openParenToken, expr, closedParenToken);
    }

    private ExprNode? ParseIdentifierExpr()
    {
        var identifier = ParseIdentifier();
        if (identifier == null) return null;

        return new IdentifierExprNode(identifier);
    }

    /// <summary>
    /// Parses `!a`
    /// </summary>
    private ExprNode? ParseUnaryExpr()
    {
        Advance();
        var op = UnaryOperatorToken.From(AssertedPrev());
        var expr = ParseExprForPrecedence(Precedence.Prefix);
        if (expr == null) return null;

        return new UnaryExprNode(op, expr);
    }

    private ExprNode? ParseLiteralExpr()
    {
        var literal = ParseLiteralToken();

        return new LiteralExprNode(literal);
    }

    private LiteralToken ParseLiteralToken()
    {
        return AssertedCurrent().Kind switch
        {
            TokenKind.False or TokenKind.True => ParseLiteralBool(),
            TokenKind.NumberLiteral => ParseLiteralNumber(),
            TokenKind.CharLiteral => ParseLiteralChar(),
            TokenKind.StringLiteral => ParseLiteralString(),
            _ => throw new Exception("unexpected literal"),
        };
    }

    private LiteralToken ParseLiteralBool()
    {
        Advance();
        var token = AssertedPrev();
        return token.Kind switch
        {
            TokenKind.False => LiteralToken.FromBool(token, false),
            TokenKind.True => LiteralToken.FromBool(token, true),
            _ => throw new UnreachableException(),
        };
    }

    private LiteralToken ParseLiteralNumber()
    {
        Advance();
        var token = AssertedPrev();
        var lexeme = GetLexeme(token);
        return LiteralToken.FromNumber(token, double.Parse(lexeme));
    }

    private LiteralToken ParseLiteralChar()
    {
        Advance();
        var token = AssertedPrev();
        var data = (CharLiteralTokenData)token.Data!;
        if (data.IsTerminated)
        {
            var lexeme = GetLexeme(token);
            var ch = lexeme[1];
            return LiteralToken.FromChar(token, ch);
        }
        else
        {
            return LiteralToken.FromChar(token, null);
        }
    }

    private LiteralToken ParseLiteralString()
    {
        Advance();
        var token = AssertedPrev();
        var data = (StringLiteralTokenData)token.Data!;
        if (data.IsTerminated)
        {
            var lexeme = GetLexeme(token);
            return LiteralToken.FromString(token, lexeme[1..^1].ToString());
        }
        else
        {
            return LiteralToken.FromString(token, null);
        }
    }

    private ExprNode? ParseInfixExpr(InfixRule rule, ExprNode left)
    {
        var token = AssertedCurrent();
        var targetPrecedence = rule.AdjustedPreference();

        return token.Kind switch
        {
            TokenKind.LeftParen => ParseCallExpr(left),
            TokenKind.Dot => ParseAccessExpr(left),
            TokenKind.Equal => ParseAssignmentExpr(targetPrecedence, left),
            _ when token.Kind.IsBinary() => ParseBinaryExpr(targetPrecedence, left),
            _ => throw new UnreachableException(),
        };
    }

    private ExprNode? ParseCallExpr(ExprNode left)
    {
        // Inside the args list of a call expr, parsing will continue until we hit the closing paren,
        // so we don't pass the precedence (otherwise parsing will prematurely stop since a call has the strongest precedence).
        var args = ParseArgsList();
        if (args == null) return null;

        return new CallExprNode(left, args);
    }

    private ExprNode? ParseAccessExpr(ExprNode left)
    {
        // In an access expr, we always parse one identifier, so we don't pass the precedence.
        var dotToken = EatAssertedSimpleToken(TokenKind.Dot);
        var identifier = ParseIdentifier();
        if (identifier == null) return null;

        return new AccessExprNode(left, dotToken, identifier);
    }

    /// <summary>
    /// Parses `left = right`
    /// </summary>
    private ExprNode? ParseAssignmentExpr(Precedence precedence, ExprNode left)
    {
        var equalToken = EatAssertedSimpleToken(TokenKind.Equal);
        var rhs = ParseExprForPrecedence(precedence);
        if (rhs == null) return null;

        return new AssignmentExprNode(left, equalToken, rhs);
    }

    private ExprNode? ParseBinaryExpr(Precedence precedence, ExprNode left)
    {
        Advance();
        var op = BinaryOperatorToken.From(AssertedPrev());
        var right = ParseExprForPrecedence(precedence);
        if (right == null) return null;

        return new BinaryExprNode(left, op, right);
    }

    /// <summary>
    /// Parses `{ ... }`
    /// </summary>
    private BlockExprNode? ParseBlockExpr()
    {
        var openBraceToken = EatAssertedSimpleToken(TokenKind.LeftBrace);
        var stmts = new List<StmtNode>();

        while (!CheckToken(TokenKind.RightBrace))
        {
            stmts.Add(ParseStmt());
        }

        var closedBraceToken = EatExpectedSimpleToken(TokenKind.RightBrace);

        return new BlockExprNode(openBraceToken, stmts, closedBraceToken);
    }

    /// <summary>
    /// Parses `Struct1 { a = b, c = d }`
    /// Requirements: current is Identifier and next is LeftParen.
    /// </summary>
    private StructCreationExprNode? ParseStructCreationExpr()
    {
        Debug.Assert(CheckToken(TokenKind.Identifier));

        var identifier = ParseIdentifier();
        Debug.Assert(identifier != null);

        var mapInitializer = ParseMapInitializer();
        if (mapInitializer == null) return null;

        return new StructCreationExprNode(identifier, mapInitializer);
    }

    /// <summary>
    /// Parses `{ a = b, c = d }`
    /// Requirements: current is LeftParen.
    /// </summary>
    /// <returns></returns>
    private MapInitializerNode? ParseMapInitializer()
    {
        var openBraceToken = EatAssertedSimpleToken(TokenKind.LeftBrace);
        var exprs = new List<ExprNode>();

        while (true)
        {
            if (IsAtEnd()) return null;

            if (CheckToken(TokenKind.RightBrace))
            {
                break;
            }

            // Will parse a whole single assignment expr.
            var expr = ParseExpr_();
            if (expr == null) return null;

            exprs.Add(expr);

            // TODO: Not doing anything with this comma token for now?
            _ = _current?.Kind switch
            {
                TokenKind.RightBrace => null,
                _ => EatExpectedSimpleToken(TokenKind.Comma),
            };
        }

        var closedBraceToken = EatExpectedSimpleToken(TokenKind.RightBrace);

        return new MapInitializerNode(openBraceToken, exprs, closedBraceToken);
    }

    // ---

    private EqualsValueClauseNode? ParseEqualsValueClause()
    {
        // Some common patterns where code doesn't add an initializer at all.
        // In these cases, return null directly instead of adding a diag for expected equal token.
        if (_current?.Kind is not TokenKind.Equal and
            (TokenKind.Semicolon or TokenKind.RightParen)) return null;

        var equalToken = EatExpectedSimpleToken(TokenKind.Equal);
        var expr = ParseExpr_();
        if (expr == null) return null;

        return new EqualsValueClauseNode(equalToken, expr);
    }

    /// <summary>
    /// Parses `: Type?`
    /// </summary>
    private (ColonTypeNode? Node, bool Elided) ParseColonTy_(bool optional)
    {
        if (optional && !CheckToken(TokenKind.Colon))
        {
            return (null, true);
        }

        var colonToken = EatExpectedSimpleToken(TokenKind.Colon);
        var type = ParseTy();
        if (type == null) return (null, false);

        return (new ColonTypeNode(colonToken, type), false);
    }

    private (ColonTypeNode? Node, bool Elided) ParseOptionalColonTy() => ParseColonTy_(optional: true);

    /// <summary>
    /// Parses `: Type?`
    /// </summary>
    private ColonTypeNode? ParseColonTy()
    {
        return ParseColonTy_(false).Node;
    }

    /// <summary>
    /// Parses `Type?`
    /// </summary>
    private TypeNode? ParseTy()
    {
        var identifier = ParseIdentifier();
        if (identifier == null) return null;
        var nullableToken = TryEatSimpleToken(TokenKind.Question);

        return new TypeNode(identifier, nullableToken);
    }

    /// <summary>
    /// Parses `(ident1: Type, ident2: Type)`
    /// Requirements: current is LeftParen.
    /// </summary>
    private ParamsListNode? ParseParamsList()
    {
        var openParenToken = EatAssertedSimpleToken(TokenKind.LeftParen);

        var @params = new List<ParamNode>();

        while (true)
        {
            if (IsAtEnd()) return null;

            if (CheckToken(TokenKind.RightParen))
            {
                break;
            }

            var param = ParseParam();
            if (param == null) return null;

            @params.Add(param);
        }

        var closedParenToken = EatExpectedSimpleToken(TokenKind.RightParen);

        return new ParamsListNode(openParenToken, @params, closedParenToken);
    }

    /// <summary>
    /// Parses `field identifier: Type,`
    /// </summary>
    private ParamNode? ParseParam()
    {
        var fieldToken = TryEatSimpleToken(TokenKind.Field);
        var identifier = ParseIdentifier();
        if (identifier == null) return null;
        var (type, elided) = ParseOptionalColonTy();
        if (!elided && type == null) return null;
        var commaToken = _current?.Kind switch
        {
            TokenKind.RightParen => null,
            _ => EatExpectedSimpleToken(TokenKind.Comma),
        };

        return new ParamNode(fieldToken, identifier, type, commaToken);
    }

    /// <summary>
    /// Parses `(expr1, expr2)`
    /// Requirements: current is LeftParen.
    /// </summary>
    private ArgsListNode? ParseArgsList()
    {
        var openParenToken = EatAssertedSimpleToken(TokenKind.LeftParen);

        var args = new List<ArgNode>();

        while (true)
        {
            if (IsAtEnd()) return null;

            if (CheckToken(TokenKind.RightParen))
            {
                break;
            }

            var arg = ParseArg();
            if (arg == null) return null;

            args.Add(arg);
        }

        var closedParenToken = EatExpectedSimpleToken(TokenKind.RightParen);

        return new ArgsListNode(openParenToken, args, closedParenToken);
    }

    private ArgNode? ParseArg()
    {
        var expr = ParseExpr_();
        if (expr == null) return null;
        var commaToken = _current?.Kind switch
        {
            TokenKind.RightParen => null,
            _ => EatExpectedSimpleToken(TokenKind.Comma),
        };

        return new ArgNode(expr, commaToken);
    }

    private FnSigNode? ParseFnSig()
    {
        var identifier = ParseIdentifier();
        if (identifier == null) return null;
        var @params = ParseParamsList();
        if (@params == null) return null;
        var (returnTy, elided) = ParseOptionalColonTy();
        if (!elided && returnTy == null) return null;

        return new FnSigNode(identifier, @params, returnTy);
    }

    private IdentifierToken? ParseIdentifier()
    {
        if (TryEatToken(TokenKind.Identifier))
        {
            var token = AssertedPrev();
            var lexeme = GetLexeme(token);

            return new IdentifierToken(token.Span, lexeme.ToString());
        }
        return null;
    }

    private SimpleToken? TryParseVisibility() =>
        TryEatSimpleToken(TokenKind.Public);

    private SimpleToken? TryParseExtern() =>
        TryEatSimpleToken(TokenKind.Extern);
}
