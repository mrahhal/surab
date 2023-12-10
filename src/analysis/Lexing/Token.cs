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
    At, // Isolated '@'
    AtBeforeIdentifier, // '@' directly followed by what looks like an identifier

    Fn,
    Struct,
    Class,
    Alias,
    New,
    This,
    True,
    False,
    Var,
    Const,
    Return,

    NumericLiteral,
    NumericLiteralType,
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
            At => "@",
            AtBeforeIdentifier => "@",

            Fn => "fn",
            Struct => "struct",
            Class => "class",
            Alias => "alias",
            New => "new",
            This => "this",
            True => "true",
            False => "false",
            Var => "let",
            Const => "const",
            Return => "return",

            NumericLiteral => "numlit",
            NumericLiteralType => "numlit_type",
            CharLiteral => "charlit",
            StringLiteral => "stringlit",
            Identifier => "ident",

            Whitespace => "{ws}",
            LineComment => "{lc}",
            BlockComment => "{bc}",

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

internal static class TokenKindHelper
{
    public static readonly Dictionary<string, TokenKind> ReservedKeywordsMap = new()
    {
        { "fn", Fn },
        { "struct", Struct },
        { "class", Class },
        { "alias", Alias },
        { "new", New },
        { "this", This },
        { "true", True },
        { "false", False },
        { "var", Var },
        { "const", Const },
        { "return", Return },
    };

    public static readonly HashSet<TokenKind> ReservedKeywordsSet = [.. ReservedKeywordsMap.Values];

    public static bool TryGetReservedKeyword(string key, out TokenKind kind)
    {
        return ReservedKeywordsMap.TryGetValue(key, out kind);
    }
}

public static class TokenKindExtensions
{
    public static bool IsReservedKeyword(this TokenKind kind) => TokenKindHelper.ReservedKeywordsSet.Contains(kind);

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
        NumericLiteral or
        CharLiteral or
        StringLiteral;

    public static bool IsTrivia(this TokenKind kind) => kind is
        Whitespace or
        LineComment or
        BlockComment;

    public static bool CanStartAttribute(this TokenKind kind) => kind is
        AtBeforeIdentifier;

    public static bool CanStartDecl(this TokenKind kind) => kind is
        Const or
        Fn or
        Struct or
        Class or
        Alias or
        Identifier ||
        CanStartAttribute(kind); // While an attribute isn't a part of the decl in the structural sense, we need to account for it here.

    public static bool CanStartStmt(this TokenKind kind) =>
        kind.CanStartExpr() ||
        kind is Identifier or This or Var or Return;

    public static bool CanStartExpr(this TokenKind kind) =>
        kind.IsUnary() ||
        kind.IsLiteral() ||
        kind is LeftParen or LeftBrace or This or Identifier;
}
