namespace Surab.Analyzer;

public enum Associativity
{
    Left,
    Right,
}

public enum Precedence
{
    None,
    Assignment,     // =
    ConditionalOr,  // ||
    ConditionalAnd, // &&
    InclusiveOr,    // |
    ExclusiveOr,    // ^
    And,            // &
    Equality,       // == !=
    Comparison,     // < > <= >=
    Sum,            // + -
    Product,        // * /
    Exponent,       // ^^
    Prefix,         // ! - ++ --
    Postfix,        // -- ++
    Call,           // . ()
}

public record InfixRule(
    Precedence Precedence,
    Associativity Associativity)
{
    public static InfixRule LeftAssociative(Precedence precedence) => new(precedence, Associativity.Left);

    public static InfixRule RightAssociative(Precedence precedence) => new(precedence, Associativity.Right);

    public Precedence AdjustedPreference() => Associativity switch
    {
        Associativity.Right => Precedence - 1,
        _ => Precedence,
    };
}

public static class Pratt
{
    private static readonly Dictionary<TokenKind, InfixRule> InfixRules = new()
    {
        { TokenKind.Equal, InfixRule.RightAssociative(Precedence.Assignment) },
        { TokenKind.ConditionalOr, InfixRule.LeftAssociative(Precedence.ConditionalOr) },
        { TokenKind.ConditionalAnd, InfixRule.LeftAssociative(Precedence.ConditionalAnd) },
        { TokenKind.InclusiveOr, InfixRule.LeftAssociative(Precedence.InclusiveOr) },
        { TokenKind.ExclusiveOr, InfixRule.LeftAssociative(Precedence.ExclusiveOr) },
        { TokenKind.And, InfixRule.LeftAssociative(Precedence.And) },
        { TokenKind.EqualEqual, InfixRule.LeftAssociative(Precedence.Equality) },
        { TokenKind.BangEqual, InfixRule.LeftAssociative(Precedence.Equality) },
        { TokenKind.Greater, InfixRule.LeftAssociative(Precedence.Comparison) },
        { TokenKind.GreaterEqual, InfixRule.LeftAssociative(Precedence.Comparison) },
        { TokenKind.Less, InfixRule.LeftAssociative(Precedence.Comparison) },
        { TokenKind.LessEqual, InfixRule.LeftAssociative(Precedence.Comparison) },
        { TokenKind.Minus, InfixRule.LeftAssociative(Precedence.Sum) },
        { TokenKind.Plus, InfixRule.LeftAssociative(Precedence.Sum) },
        { TokenKind.Slash, InfixRule.LeftAssociative(Precedence.Product) },
        { TokenKind.Asterisk, InfixRule.LeftAssociative(Precedence.Product) },
        { TokenKind.CaretCaret, InfixRule.RightAssociative(Precedence.Exponent) },
        { TokenKind.LeftParen, InfixRule.LeftAssociative(Precedence.Call) },
        { TokenKind.Dot, InfixRule.RightAssociative(Precedence.Call) },
    };

    public static bool IsPrefixRule(TokenKind tokenKind) => tokenKind is
        TokenKind.LeftParen or
        TokenKind.LeftBrace or
        TokenKind.Identifier or
        TokenKind.NumberLiteral or
        TokenKind.CharLiteral or
        TokenKind.StringLiteral or
        TokenKind.True or
        TokenKind.False or
        TokenKind.Plus or
        TokenKind.Minus or
        TokenKind.Bang;

    public static InfixRule? GetInfixRule(TokenKind tokenKind)
    {
        InfixRules.TryGetValue(tokenKind, out var rule);
        return rule;
    }
}
