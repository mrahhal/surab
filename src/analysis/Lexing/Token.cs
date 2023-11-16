using System.Diagnostics;
using static Surab.Analysis.TokenKind;

namespace Surab.Analysis;

/// <summary>
/// A lexical token kind.
/// </summary>
public enum TokenKind
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
    Plus,
    Minus,
    Asterisk,
    Slash,
    Bang,
    BangEqual,
    Equal,
    EqualEqual,
    Greater,
    GreaterEqual,
    Less,
    LessEqual,
    ConditionalAnd,
    ConditionalOr,
    And,
    InclusiveOr,
    ExclusiveOr,
    CaretCaret,

    Fn,
    Struct,
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

    NumberLiteral,
    CharLiteral,
    StringLiteral,
    Identifier,

    Whitespace,
    LineComment,
    BlockComment,

    Unknown,
}

/// <summary>
/// A lexical token.
/// </summary>
[DebuggerDisplay("Token: [{Kind}] {ToString()}")]
public class Token(TokenKind kind, TextSpan span, object? data)
{
    public TokenKind Kind { get; } = kind;

    public TextSpan Span { get; } = span;

    /// <summary>
    /// An optional data object. The actual type (if it exists) is always well known for each <see cref="Kind"/>.
    /// </summary>
    /// <remarks>
    /// The following <see cref="TokenKind"/>s have data objects:
    /// - <see cref="TokenKind.CharLiteral"/>
    /// - <see cref="TokenKind.StringLiteral"/>
    /// - <see cref="TokenKind.BlockComment"/>
    /// </remarks>
    public object? Data { get; } = data;

    public override string ToString()
    {
        return Kind switch
        {
            LeftParen => "(",
            RightParen => ")",
            LeftBrace => "{",
            RightBrace => "}",
            Comma => ",",
            Dot => ".",
            Semicolon => ";",
            Colon => ":",
            Question => "?",
            Minus => "-",
            Plus => "+",
            Slash => "/",
            Asterisk => "*",
            Bang => "!",
            BangEqual => "!=",
            Equal => "=",
            EqualEqual => "==",
            Greater => ">",
            GreaterEqual => ">=",
            Less => "<",
            LessEqual => "<=",
            ConditionalAnd => "&&",
            ConditionalOr => "||",
            And => "&",
            InclusiveOr => "|",
            ExclusiveOr => "^",
            CaretCaret => "^^",
            Fn => "fn",
            Class => "class",
            New => "new",
            This => "this",
            True => "true",
            False => "false",
            Let => "let",
            Const => "const",
            Field => "field",
            Public => "public",
            Return => "return",
            NumberLiteral => "numlit",
            CharLiteral => "charlit",
            StringLiteral => "stringlit",
            Identifier => "ident",
            Whitespace => "ws",
            LineComment => "lc",
            BlockComment => "bc",
            Unknown => "unknown",

            _ => throw new NotImplementedException(),
        };
    }
}

public class CharLiteralTokenData
{
    public bool IsTerminated { get; init; }
}

public class StringLiteralTokenData
{
    public bool IsTerminated { get; init; }
}

public class BlockCommentTokenData
{
    public bool IsTerminated { get; init; }
}

public static class TokenKindExtensions
{
    public static bool IsUnary(this TokenKind kind) => kind is
        Plus or
        Minus or
        Bang;

    public static bool IsBinary(this TokenKind kind) => kind is
        Plus or
        Minus or
        Asterisk or
        Slash or
        CaretCaret or
        ConditionalAnd or
        ConditionalOr or
        And or
        InclusiveOr or
        ExclusiveOr or
        EqualEqual or
        BangEqual or
        Less or
        LessEqual or
        Greater or
        GreaterEqual;

    public static bool IsLiteral(this TokenKind kind) => kind is
        False or
        True or
        NumberLiteral or
        CharLiteral or
        StringLiteral;

    public static bool IsTrivia(this TokenKind kind) => kind is
        Whitespace or
        LineComment or
        BlockComment;

    public static bool CanStartDecl(this TokenKind kind) => kind is
        Const or
        Fn or
        Class or
        Identifier or
        Public;

    public static bool CanStartStmt(this TokenKind kind) =>
        kind.CanStartExpr() ||
        kind is Identifier or This or Let or Return;

    public static bool CanStartExpr(this TokenKind kind) =>
        kind.IsUnary() ||
        kind.IsLiteral() ||
        kind is LeftParen or LeftBrace or This or Identifier;
}
