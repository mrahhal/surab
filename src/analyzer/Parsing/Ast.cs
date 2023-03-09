namespace Surab.Analyzer;

public class SyntaxTree
{
    public required SyntaxNode Root { get; init; }

    public required DiagnosticsBag Diagnostics { get; init; }
}

public abstract class SyntaxEntity : IVisitable
{
    public SyntaxNode? Parent { get; private set; }

    public TextSpan Span { get; init; }

    internal void SetParent(SyntaxNode? parent)
    {
        Parent = parent;
    }

    public abstract void Accept(IVisitor visitor);
}

public enum SyntaxTokenKind
{
    Simple,
    UnaryOperator,
    BinaryOperator,
    Identifier,
    Literal,
    Skipped,
}

public abstract class SyntaxToken : SyntaxEntity
{
    public SyntaxToken(SyntaxTokenKind kind)
    {
        Kind = kind;
    }

    public SyntaxTokenKind Kind { get; }
}

public enum SimpleTokenKind
{
    LeftParen,
    RightParen,
    LeftBrace,
    RightBrace,
    Comma,
    Dot,
    Semicolon,
    Colon,
    Question,
    Equal,
    Function,
    Class,
    New,
    This,
    True,
    False,
    Let,
    Const,
    Field,
    Public,
    Return,
}

public class SimpleToken : SyntaxToken
{
    public SimpleToken() : base(SyntaxTokenKind.Simple)
    {
    }

    public SimpleTokenKind SimpleTokenKind { get; init; }

    public bool IsMissing { get; init; }

    public static SimpleToken Missing(TokenKind tokenKind, TextSpan span) =>
        From(tokenKind, span, isMissing: true);

    public static SimpleToken From(Token token, bool isMissing = false) =>
        From(token.Kind, token.Span, isMissing);

    public static SimpleToken From(
        TokenKind tokenKind,
        TextSpan span,
        bool isMissing = false) => new()
        {
            Span = span,
            IsMissing = isMissing,
            SimpleTokenKind = tokenKind switch
            {
                TokenKind.LeftParen => SimpleTokenKind.LeftParen,
                TokenKind.RightParen => SimpleTokenKind.RightParen,
                TokenKind.LeftBrace => SimpleTokenKind.LeftBrace,
                TokenKind.RightBrace => SimpleTokenKind.RightBrace,
                TokenKind.Comma => SimpleTokenKind.Comma,
                TokenKind.Dot => SimpleTokenKind.Dot,
                TokenKind.Semicolon => SimpleTokenKind.Semicolon,
                TokenKind.Colon => SimpleTokenKind.Colon,
                TokenKind.Question => SimpleTokenKind.Question,
                TokenKind.Equal => SimpleTokenKind.Equal,
                TokenKind.Function => SimpleTokenKind.Function,
                TokenKind.Class => SimpleTokenKind.Class,
                TokenKind.New => SimpleTokenKind.New,
                TokenKind.This => SimpleTokenKind.This,
                TokenKind.True => SimpleTokenKind.True,
                TokenKind.False => SimpleTokenKind.False,
                TokenKind.Let => SimpleTokenKind.Let,
                TokenKind.Const => SimpleTokenKind.Const,
                TokenKind.Field => SimpleTokenKind.Field,
                TokenKind.Public => SimpleTokenKind.Public,
                TokenKind.Return => SimpleTokenKind.Return,
                _ => throw new Exception($"TokenKind {tokenKind} not supported."),
            },
        };

    public override string ToString() => SimpleTokenKind.ToString();

    public override void Accept(IVisitor visitor)
    {
        visitor.VisitSimpleToken(this);
    }
}

public enum UnaryOperatorTokenKind
{
    Pos, // Usually redundant but must be represented in the AST.
    Neg,
    Not,
}

public class UnaryOperatorToken : SyntaxToken
{
    public UnaryOperatorToken() : base(SyntaxTokenKind.UnaryOperator)
    {
    }

    public UnaryOperatorTokenKind UnaryOperatorTokenKind { get; init; }

    public static UnaryOperatorToken From(
        Token token) => new()
        {
            Span = token.Span,
            UnaryOperatorTokenKind = token.Kind switch
            {
                TokenKind.Plus => UnaryOperatorTokenKind.Pos,
                TokenKind.Minus => UnaryOperatorTokenKind.Neg,
                TokenKind.Bang => UnaryOperatorTokenKind.Not,
                _ => throw new Exception($"TokenKind {token.Kind} not supported."),
            },
        };

    public override string ToString() => UnaryOperatorTokenKind switch
    {
        UnaryOperatorTokenKind.Pos => "+",
        UnaryOperatorTokenKind.Neg => "-",
        UnaryOperatorTokenKind.Not => "!",
        _ => throw new NotImplementedException(),
    };

    public override void Accept(IVisitor visitor)
    {
        visitor.VisitUnaryOperatorToken(this);
    }
}

public enum BinaryOperatorTokenKind
{
    Add,
    Sub,
    Mul,
    Div,
    Pow,
    And,
    Or,
    BitAnd,
    BitOr,
    BitXor,
    Eq,
    Ne,
    Lt,
    Le,
    Gt,
    Ge,
}

public class BinaryOperatorToken : SyntaxToken
{
    public BinaryOperatorToken() : base(SyntaxTokenKind.BinaryOperator)
    {
    }

    public BinaryOperatorTokenKind BinaryOperatorTokenKind { get; init; }

    public static BinaryOperatorToken From(
        Token token) => new()
        {
            Span = token.Span,
            BinaryOperatorTokenKind = token.Kind switch
            {
                TokenKind.Plus => BinaryOperatorTokenKind.Add,
                TokenKind.Minus => BinaryOperatorTokenKind.Sub,
                TokenKind.Asterisk => BinaryOperatorTokenKind.Mul,
                TokenKind.Slash => BinaryOperatorTokenKind.Div,
                TokenKind.CaretCaret => BinaryOperatorTokenKind.Pow,
                TokenKind.ConditionalAnd => BinaryOperatorTokenKind.And,
                TokenKind.ConditionalOr => BinaryOperatorTokenKind.Or,
                TokenKind.And => BinaryOperatorTokenKind.BitAnd,
                TokenKind.InclusiveOr => BinaryOperatorTokenKind.BitOr,
                TokenKind.ExclusiveOr => BinaryOperatorTokenKind.BitXor,
                TokenKind.EqualEqual => BinaryOperatorTokenKind.Eq,
                TokenKind.BangEqual => BinaryOperatorTokenKind.Ne,
                TokenKind.Less => BinaryOperatorTokenKind.Lt,
                TokenKind.LessEqual => BinaryOperatorTokenKind.Le,
                TokenKind.Greater => BinaryOperatorTokenKind.Gt,
                TokenKind.GreaterEqual => BinaryOperatorTokenKind.Ge,
                _ => throw new Exception($"TokenKind {token.Kind} not supported."),
            },
        };

    public override string ToString() => BinaryOperatorTokenKind switch
    {
        BinaryOperatorTokenKind.Add => "+",
        BinaryOperatorTokenKind.Sub => "-",
        BinaryOperatorTokenKind.Mul => "*",
        BinaryOperatorTokenKind.Div => "/",
        BinaryOperatorTokenKind.Pow => "^^",
        BinaryOperatorTokenKind.And => "&&",
        BinaryOperatorTokenKind.Or => "||",
        BinaryOperatorTokenKind.BitAnd => "&",
        BinaryOperatorTokenKind.BitOr => "|",
        BinaryOperatorTokenKind.BitXor => "^",
        BinaryOperatorTokenKind.Eq => "==",
        BinaryOperatorTokenKind.Ne => "!=",
        BinaryOperatorTokenKind.Lt => "<",
        BinaryOperatorTokenKind.Le => "<=",
        BinaryOperatorTokenKind.Gt => ">",
        BinaryOperatorTokenKind.Ge => ">=",
        _ => throw new NotImplementedException(),
    };

    public override void Accept(IVisitor visitor)
    {
        visitor.VisitBinaryOperatorToken(this);
    }
}

public class IdentifierToken : SyntaxToken
{
    public IdentifierToken(TextSpan span, string identifier)
        : base(SyntaxTokenKind.Identifier)
    {
        Span = span;
        Identifier = identifier;
    }

    public string Identifier { get; init; }

    public override void Accept(IVisitor visitor)
    {
        visitor.VisitIdentifierToken(this);
    }
}

public enum LiteralTokenKind
{
    Number,
    Char,
    String,
    Bool,
}

public class LiteralToken : SyntaxToken
{
    public LiteralToken(TextSpan span, LiteralTokenKind literalTokenKind, object? data) : base(SyntaxTokenKind.Literal)
    {
        Span = span;
        LiteralTokenKind = literalTokenKind;
        Data = data;
    }

    public LiteralTokenKind LiteralTokenKind { get; init; }

    public object? Data { get; init; }

    public static LiteralToken FromBool(Token token, bool value) =>
        new(token.Span, LiteralTokenKind.Bool, value);

    public static LiteralToken FromNumber(Token token, double value) =>
        new(token.Span, LiteralTokenKind.Number, value);

    public static LiteralToken FromChar(Token token, char? value) =>
        new(token.Span, LiteralTokenKind.Char, value);

    public static LiteralToken FromString(Token token, string? value) =>
        new(token.Span, LiteralTokenKind.String, value);

    public bool TryGetNumberValue(out double value)
    {
        value = default;
        if (LiteralTokenKind != LiteralTokenKind.Number)
        {
            return false;
        }
        value = (double)Data!;
        return true;
    }

    public bool TryGetCharValue(out char? value)
    {
        value = default;
        if (LiteralTokenKind != LiteralTokenKind.Char)
        {
            return false;
        }
        value = (char?)Data;
        return true;
    }

    public bool TryGetStringValue(out string? value)
    {
        value = default;
        if (LiteralTokenKind != LiteralTokenKind.String)
        {
            return false;
        }
        value = (string?)Data;
        return true;
    }

    public bool TryGetBoolValue(out bool value)
    {
        value = default;
        if (LiteralTokenKind != LiteralTokenKind.Bool)
        {
            return false;
        }
        value = (bool)Data!;
        return true;
    }

    public override void Accept(IVisitor visitor)
    {
        visitor.VisitLiteralToken(this);
    }
}

public class SkippedToken : SyntaxToken
{
    public SkippedToken(TextSpan span, List<Token> tokens)
        : base(SyntaxTokenKind.Skipped)
    {
        Span = span;
        Tokens = tokens;
    }

    public List<Token> Tokens { get; init; }

    public override void Accept(IVisitor visitor)
    {
        visitor.VisitSkippedToken(this);
    }
}

public abstract class SyntaxNode : SyntaxEntity
{
    public SyntaxNode(SyntaxNodeKind kind)
    {
        Kind = kind;
    }

    public SyntaxNodeKind Kind { get; init; }

    public List<SyntaxNode?> ChildNodes { get; protected set; } = default!;

    public List<SyntaxToken?> ChildTokens { get; protected set; } = default!;

    protected void SetChildrenParent()
    {
        foreach (var c in ChildNodes)
        {
            c?.SetParent(this);
        }
        foreach (var c in ChildTokens)
        {
            c?.SetParent(this);
        }
    }
}

public abstract class DeclarationNode : SyntaxNode
{
    public DeclarationNode(SyntaxNodeKind kind)
        : base(kind)
    {
    }
}

public abstract class StatementNode : SyntaxNode
{
    public StatementNode(SyntaxNodeKind kind)
        : base(kind)
    {
    }
}

public abstract class ExpressionNode : SyntaxNode
{
    public ExpressionNode(SyntaxNodeKind kind)
        : base(kind)
    {
    }
}
