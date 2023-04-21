namespace Surab.Analyzer;

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
    public static SyntaxTree ParseDeclaration(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src);
        var parser = new ParserImpl(src, tokens);
        return parser.ParseAsDeclaration();
    }

    /// <summary>
    /// Parses the src into a statement.
    /// </summary>
    public static SyntaxTree ParseStatement(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src);
        var parser = new ParserImpl(src, tokens);
        return parser.ParseAsStatement();
    }

    /// <summary>
    /// Parses the src into an expression.
    /// </summary>
    public static SyntaxTree ParseExpression(string src)
    {
        var tokens = Lexer.LexSkipTrivia(src);
        var parser = new ParserImpl(src, tokens);
        return parser.ParseAsExpression();
    }
}

internal class ParserImpl
{
    private readonly string _src;
    private readonly Peeker<Token> _tokens;
    private Token? _current;
    private Token? _prev;
    private readonly List<Token> _tokensConsumed = new();
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

    private bool IsAtEnd() => _current == null;

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
        var declarations = new List<DeclarationNode>();

        while (!IsAtEnd())
        {
            var delcaration = ParseDeclaration();
            declarations.Add(delcaration);
        }

        var module = new ModuleNode(declarations);
        return MakeSyntaxTree(module);
    }

    public SyntaxTree ParseAsDeclaration()
    {
        var declaration = ParseDeclaration();
        return MakeSyntaxTree(declaration);
    }

    private DeclarationNode ParseDeclaration()
    {
        var declaration = ParseDeclaration_();
        if (declaration != null)
        {
            ResetTokensConsumed();
            return declaration;
        }
        return RecoverDeclaration();
    }

    public SyntaxTree ParseAsStatement()
    {
        var statement = ParseStatement();
        return MakeSyntaxTree(statement);
    }

    private StatementNode ParseStatement()
    {
        var statement = ParseStatement_();
        if (statement != null)
        {
            ResetTokensConsumed();
            return statement;
        }
        return RecoverStatement();
    }

    public SyntaxTree ParseAsExpression()
    {
        var expression = ParseExpression();
        return MakeSyntaxTree(expression);
    }

    private ExpressionNode ParseExpression()
    {
        var expression = ParseExpression_();
        if (expression != null)
        {
            ResetTokensConsumed();
            return expression;
        }
        return RecoverExpression();
    }

    private DeclarationNode RecoverDeclaration()
    {
        var skipped = RecoverMain();
        return new SkippedTokensDeclarationNode(skipped);
    }

    private StatementNode RecoverStatement()
    {
        var skipped = RecoverMain();
        return new SkippedTokensStatementNode(skipped);
    }

    private ExpressionNode RecoverExpression()
    {
        var skipped = RecoverMain();
        return new SkippedTokensExpressionNode(skipped);
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
            if (current.Kind.CanStartDeclaration() || current.Kind.CanStartStatement())
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

    // This happens in one of the recovery of the three main items: declarations, statements, expressions.
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

    // Declarations
    // ------------

    /// <summary>
    /// Parses outer level declaration.
    /// </summary>
    private DeclarationNode? ParseDeclaration_()
    {
        var visibility = TryParseVisibility();
        return _current?.Kind switch
        {
            TokenKind.Const => ParseConstDeclaration(visibility),
            TokenKind.Function => ParseFunctionDeclaration(visibility),
            TokenKind.Class => ParseClassDeclaration(visibility),

            _ => null,
        };
    }

    private DeclarationNode? ParseInnerDeclaration()
    {
        var visibility = TryParseVisibility();
        return _current?.Kind switch
        {
            // `identifier: Ty = 2;` or `identifier = 2;`
            TokenKind.Identifier when P1()?.Kind is TokenKind.Colon or TokenKind.Equal => ParseFieldDeclaration(visibility),

            // `identifier()`
            TokenKind.Identifier when P1()?.Kind is TokenKind.LeftParen => ParseMethodDeclaration(visibility),

            _ => null,
        };
    }

    /// <summary>
    /// Parses `const identifier = expr;`
    /// </summary>
    private DeclarationNode? ParseConstDeclaration(SimpleToken? visibility)
    {
        var constToken = EatAssertedSimpleToken(TokenKind.Const);
        var identifier = ParseIdentifier();
        if (identifier == null) return null;
        var (ty, elided) = ParseOptionalColonTy();
        if (!elided && ty == null) return null;
        var equalToken = EatExpectedSimpleToken(TokenKind.Equal);
        var value = ParseExpression_();
        if (value == null) return null;

        return new ConstDeclarationNode(visibility, constToken, identifier, ty, equalToken, value);
    }

    /// <summary>
    /// Parses `function identifier(param1, param2) : Ty { ... }`
    /// </summary>
    private DeclarationNode? ParseFunctionDeclaration(SimpleToken? visibility)
    {
        var functionToken = EatAssertedSimpleToken(TokenKind.Function);
        var signature = ParseFunctionSignature();
        if (signature == null) return null;
        var body = ParseBlockExpression();
        if (body == null) return null;

        return new FunctionDeclarationNode(visibility, functionToken, signature, body);
    }

    /// <summary>
    /// Parses `class Name { ... }`
    /// </summary>
    private DeclarationNode? ParseClassDeclaration(SimpleToken? visibility)
    {
        var classToken = EatAssertedSimpleToken(TokenKind.Class);
        var identifier = ParseIdentifier();
        if (identifier == null) return null;
        var members = new List<DeclarationNode>();

        var openBraceToken = EatExpectedSimpleToken(TokenKind.LeftBrace);
        while (!CheckToken(TokenKind.RightBrace))
        {
            var declaration = ParseInnerDeclaration();
            if (declaration == null) return null;
            members.Add(declaration);
        }
        var closedBraceToken = EatExpectedSimpleToken(TokenKind.RightBrace);

        return new ClassDeclarationNode(visibility, classToken, identifier, openBraceToken, members, closedBraceToken);
    }

    /// <summary>
    /// Parses `identifier: Ty = expression;`
    /// </summary>
    private DeclarationNode? ParseFieldDeclaration(SimpleToken? visibility)
    {
        var identifier = ParseIdentifier();
        if (identifier == null) return null;
        var (ty, elided) = ParseOptionalColonTy();
        if (!elided && ty == null) return null;
        var initializer = ParseEqualsValueClause();
        if (initializer == null) return null;
        var semicolonToken = EatExpectedSimpleToken(TokenKind.Semicolon);

        return new FieldDeclarationNode(visibility, identifier, ty, initializer, semicolonToken);
    }

    /// <summary>
    /// Parses `identifier(param1, param2)`
    /// </summary>
    private DeclarationNode? ParseMethodDeclaration(SimpleToken? visibility)
    {
        var signature = ParseFunctionSignature();
        if (signature == null) return null;
        var body = ParseBlockExpression();
        if (body == null) return null;

        return new MethodDeclarationNode(visibility, signature, body);
    }

    // Statements
    // ----------

    private StatementNode? ParseStatement_()
    {
        var current = _current;
        if (current == null) return null;

        return current.Kind switch
        {
            TokenKind.Let => ParseLetStatement(),
            TokenKind.Return => ParseReturnStatement(),
            _ when current.Kind.CanStartExpression() => ParseExpressionStatement(),

            _ => null,
        };
    }

    /// <summary>
    /// Parses `let a: Type? = expression;`
    /// </summary>
    private StatementNode? ParseLetStatement()
    {
        var letToken = EatAssertedSimpleToken(TokenKind.Let);
        var identifier = ParseIdentifier();
        if (identifier == null) return null;
        var (ty, elided) = ParseOptionalColonTy();
        if (!elided && ty == null) return null;
        var initializer = ParseEqualsValueClause();
        if (initializer == null) return null;
        var semicolonToken = EatExpectedSimpleToken(TokenKind.Semicolon);

        return new LetStatementNode(letToken, identifier, ty, initializer, semicolonToken);
    }

    /// <summary>
    /// Parses `return expression;`
    /// </summary>
    private StatementNode? ParseReturnStatement()
    {
        var returnToken = EatAssertedSimpleToken(TokenKind.Return);
        var expression = ParseExpression_();
        if (expression == null) return null;
        var semicolonToken = EatExpectedSimpleToken(TokenKind.Semicolon);

        return new ReturnStatementNode(returnToken, expression, semicolonToken);
    }

    private StatementNode? ParseExpressionStatement()
    {
        var expression = ParseExpression_();
        if (expression == null) return null;
        var semicolonToken = TryEatSimpleToken(TokenKind.Semicolon);

        return semicolonToken is null ?
            new BareExpressionStatementNode(expression) :
            new ExpressionStatementNode(expression, semicolonToken);
    }

    // Expressions
    // -----------

    private ExpressionNode? ParseExpression_() => ParseExpressionForPrecedence(Precedence.None);

    /// <summary>
    /// Keeps parsing right expressions as long as the next precedence is greater than the given `precedence`.
    /// </summary>
    private ExpressionNode? ParseExpressionForPrecedence(Precedence precedence)
    {
        if (_current == null) return null;
        if (!Pratt.IsPrefixRule(_current.Kind))
        {
            return null;
        }
        var expression = ParsePrefixExpression();
        if (expression == null) return null;

        while (_current != null)
        {
            var rule = Pratt.GetInfixRule(_current.Kind);
            if (rule == null) break;

            if (precedence >= rule.Precedence)
            {
                break;
            }

            expression = ParseInfixExpression(rule, expression);
            if (expression == null) return null;
        }

        return expression;
    }

    private ExpressionNode? ParsePrefixExpression()
    {
        var token = AssertedCurrent();
        return token.Kind switch
        {
            TokenKind.LeftParen => ParseGroupingExpression(),
            TokenKind.LeftBrace => ParseBlockExpression(),
            TokenKind.Identifier => ParseIdentifierExpression(),
            _ when token.Kind.IsUnary() => ParseUnaryExpression(),
            _ when token.Kind.IsLiteral() => ParseLiteralExpression(),
            _ => throw new Exception("unreachable"),
        };
    }

    private ExpressionNode? ParseGroupingExpression()
    {
        var openParenToken = EatAssertedSimpleToken(TokenKind.LeftParen);
        var expression = ParseExpression_();
        if (expression == null) return null;
        var closedParenToken = EatExpectedSimpleToken(TokenKind.RightParen);

        return new GroupingExpressionNode(openParenToken, expression, closedParenToken);
    }

    private ExpressionNode? ParseIdentifierExpression()
    {
        var identifier = ParseIdentifier();
        if (identifier == null) return null;

        return new IdentifierExpressionNode(identifier);
    }

    /// <summary>
    /// Parses `!a`
    /// </summary>
    private ExpressionNode? ParseUnaryExpression()
    {
        Advance();
        var op = UnaryOperatorToken.From(AssertedPrev());
        var expression = ParseExpressionForPrecedence(Precedence.Prefix);
        if (expression == null) return null;

        return new UnaryExpressionNode(op, expression);
    }

    private ExpressionNode? ParseLiteralExpression()
    {
        var literal = ParseLiteralToken();

        return new LiteralExpressionNode(literal);
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
            _ => throw new Exception("unreachable"),
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

    private ExpressionNode? ParseInfixExpression(InfixRule rule, ExpressionNode left)
    {
        var token = AssertedCurrent();
        var targetPrecedence = rule.AdjustedPreference();

        return token.Kind switch
        {
            TokenKind.LeftParen => ParseCallExpression(targetPrecedence, left),
            TokenKind.Dot => ParseAccessExpression(targetPrecedence, left),
            TokenKind.Equal => ParseAssignmentExpression(targetPrecedence, left),
            _ when token.Kind.IsBinary() => ParseBinaryExpression(targetPrecedence, left),
            _ => throw new Exception("unreachable"),
        };
    }

    private ExpressionNode? ParseCallExpression(Precedence precedence, ExpressionNode left)
    {
        var arguments = ParseArgumentsList(precedence);
        if (arguments == null) return null;

        return new CallExpressionNode(left, arguments);
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private ExpressionNode? ParseAccessExpression(Precedence precedence, ExpressionNode left)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        var dotToken = EatAssertedSimpleToken(TokenKind.Dot);
        var identifier = ParseIdentifier();
        if (identifier == null) return null;

        return new AccessExpressionNode(left, dotToken, identifier);
    }

    /// <summary>
    /// Parses `left = right`
    /// </summary>
    private ExpressionNode? ParseAssignmentExpression(Precedence precedence, ExpressionNode left)
    {
        var equalToken = EatAssertedSimpleToken(TokenKind.Equal);
        var rhs = ParseExpressionForPrecedence(precedence);
        if (rhs == null) return null;

        return new AssignmentExpressionNode(left, equalToken, rhs);
    }

    private ExpressionNode? ParseBinaryExpression(Precedence precedence, ExpressionNode left)
    {
        Advance();
        var op = BinaryOperatorToken.From(AssertedPrev());
        var right = ParseExpressionForPrecedence(precedence);
        if (right == null) return null;

        return new BinaryExpressionNode(left, op, right);
    }

    /// <summary>
    /// Parses `{ ... }`
    /// </summary>
    private BlockExpressionNode? ParseBlockExpression()
    {
        var openBraceToken = EatAssertedSimpleToken(TokenKind.LeftBrace);
        var statements = new List<StatementNode>();

        while (!CheckToken(TokenKind.RightBrace))
        {
            statements.Add(ParseStatement());
        }

        var closedBraceToken = EatExpectedSimpleToken(TokenKind.RightBrace);

        return new BlockExpressionNode(openBraceToken, statements, closedBraceToken);
    }

    // ---

    private EqualsValueClauseNode? ParseEqualsValueClause()
    {
        var equalToken = EatExpectedSimpleToken(TokenKind.Equal);
        var expression = ParseExpression_();
        if (expression == null) return null;

        return new EqualsValueClauseNode(equalToken, expression);
    }

    /// <summary>
    /// Parses `: Type?`
    /// </summary>
    private (ColonTyNode? Node, bool Elided) ParseColonTy_(bool optional)
    {
        if (optional && !CheckToken(TokenKind.Colon))
        {
            return (null, true);
        }

        var colonToken = EatExpectedSimpleToken(TokenKind.Colon);
        var ty = ParseTy();
        if (ty == null) return (null, false);

        return (new ColonTyNode(colonToken, ty), false);
    }

    private (ColonTyNode? Node, bool Elided) ParseOptionalColonTy() => ParseColonTy_(optional: true);

    /// <summary>
    /// Parses `: Type?`
    /// </summary>
    private ColonTyNode? ParseColonTy()
    {
        return ParseColonTy_(false).Node;
    }

    /// <summary>
    /// Parses `Type?`
    /// </summary>
    private TyNode? ParseTy()
    {
        var identifier = ParseIdentifier();
        if (identifier == null) return null;
        var nullableToken = TryEatSimpleToken(TokenKind.Question);

        return new TyNode(identifier, nullableToken);
    }

    /// <summary>
    /// Parses `(ident1: Ty, ident2: Ty)`
    /// Requirements: current is LeftParen.
    /// </summary>
    private ParametersListNode? ParseParametersList()
    {
        var openParenToken = EatAssertedSimpleToken(TokenKind.LeftParen);

        var parameters = new List<ParameterNode>();

        while (true)
        {
            if (_current == null) return null;

            if (CheckToken(TokenKind.RightParen))
            {
                break;
            }

            var parameter = ParseParameter();
            if (parameter == null) return null;

            parameters.Add(parameter);
        }

        var closedParenToken = EatExpectedSimpleToken(TokenKind.RightParen);

        return new ParametersListNode(openParenToken, parameters, closedParenToken);
    }

    /// <summary>
    /// Parses `field identifier: Ty,`
    /// </summary>
    private ParameterNode? ParseParameter()
    {
        var fieldToken = TryEatSimpleToken(TokenKind.Field);
        var identifier = ParseIdentifier();
        if (identifier == null) return null;
        var ty = ParseColonTy();
        if (ty == null) return null;
        var commaToken = P1()?.Kind switch
        {
            TokenKind.RightParen => null,
            _ => EatExpectedSimpleToken(TokenKind.Comma),
        };

        return new ParameterNode(fieldToken, identifier, ty, commaToken);
    }

    /// <summary>
    /// Parses `(expression1, expression2)`
    /// Requirements: current is LeftParen.
    /// </summary>
    private ArgumentsListNode? ParseArgumentsList(Precedence precedence)
    {
        var openParenToken = EatAssertedSimpleToken(TokenKind.LeftParen);

        var arguments = new List<ArgumentNode>();

        while (true)
        {
            if (_current == null) return null;

            if (CheckToken(TokenKind.RightParen))
            {
                break;
            }

            var argument = ParseArgument(precedence);
            if (argument == null) return null;

            arguments.Add(argument);
        }

        var closedParenToken = EatExpectedSimpleToken(TokenKind.RightParen);

        return new ArgumentsListNode(openParenToken, arguments, closedParenToken);
    }

    private ArgumentNode? ParseArgument(Precedence precedence)
    {
        var expression = ParseExpressionForPrecedence(precedence);
        if (expression == null) return null;
        var commaToken = P1()?.Kind switch
        {
            TokenKind.RightParen => null,
            _ => EatExpectedSimpleToken(TokenKind.Comma),
        };

        return new ArgumentNode(expression, commaToken);
    }

    private FunctionSignatureNode? ParseFunctionSignature()
    {
        var identifier = ParseIdentifier();
        if (identifier == null) return null;
        var parameters = ParseParametersList();
        if (parameters == null) return null;
        var (returnTy, elided) = ParseOptionalColonTy();
        if (!elided && returnTy == null) return null;

        return new FunctionSignatureNode(identifier, parameters, returnTy);
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
}
