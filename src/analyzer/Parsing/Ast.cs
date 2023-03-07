using Surab.Analyzer.Core;
using Surab.Analyzer.Lexing;

namespace Surab.Analyzer.Parsing;

public class SyntaxTree
{
    public required SyntaxNode Root { get; init; }

    public required DiagnosticsBag Diagnostics { get; init; }
}

public abstract class SyntaxEntity : IVisitable
{
    public SyntaxNode? Parent { get; private set; }

    public TextSpan Span { get; init; }

    public void SetParent(SyntaxNode? parent)
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

    public static SimpleToken From(
        Token token,
        bool isMissing) => new()
        {
            Span = token.Span,
            IsMissing = isMissing,
            SimpleTokenKind = token.Kind switch
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
                _ => throw new Exception($"TokenKind {token.Kind} not supported."),
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
    public IdentifierToken() : base(SyntaxTokenKind.Identifier)
    {
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
    public LiteralToken() : base(SyntaxTokenKind.Literal)
    {
    }

    public LiteralTokenKind LiteralTokenKind { get; init; }

    public object Data { get; init; }

    public bool TryGetNumberValue(out double value)
    {
        value = default;
        if (LiteralTokenKind != LiteralTokenKind.Number)
        {
            return false;
        }
        value = (double)Data;
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
        value = (bool)Data;
        return true;
    }

    public override void Accept(IVisitor visitor)
    {
        visitor.VisitLiteralToken(this);
    }
}

public class SkippedToken : SyntaxToken
{
    public SkippedToken() : base(SyntaxTokenKind.Skipped)
    {
    }

    public List<Token> Tokens { get; init; }

    public override void Accept(IVisitor visitor)
    {
        visitor.VisitSkippedToken(this);
    }
}

public abstract class SyntaxNode : SyntaxEntity
{
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
}

public abstract class StatementNode : SyntaxNode
{
}

public abstract class ExpressionNode : SyntaxNode
{
}
