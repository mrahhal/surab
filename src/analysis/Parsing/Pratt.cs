namespace Surab.Analysis;

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
	Call,           // . :: () {}
}

public class InfixRule(
	Precedence precedence,
	Associativity associativity,
	Func<ExprNode, bool>? leftPredicate)
{
	public Precedence Precedence { get; } = precedence;

	public Associativity Associativity { get; } = associativity;

	public Func<ExprNode, bool>? LeftPredicate { get; } = leftPredicate;

	public static InfixRule LeftAssociative(Precedence precedence, Func<ExprNode, bool>? leftPredicate = null) => new(precedence, Associativity.Left, leftPredicate);

	public static InfixRule RightAssociative(Precedence precedence, Func<ExprNode, bool>? leftPredicate = null) => new(precedence, Associativity.Right, leftPredicate);

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
		{ TokenKind.Dot, InfixRule.RightAssociative(Precedence.Call) },
		{ TokenKind.ColonColon, InfixRule.RightAssociative(Precedence.Call) },
		{ TokenKind.LeftParen, InfixRule.LeftAssociative(Precedence.Call) },
		// The predicate avoids a situation where `{ 2 } { ... }` is regarded as creation.
		{ TokenKind.LeftBrace, InfixRule.LeftAssociative(Precedence.Call, leftExpr => leftExpr is IdentifierExprNode or AccessExprNode or ModuleAccessExprNode) },
	};

	public static bool IsPrefixRule(TokenKind tokenKind) => tokenKind is
		TokenKind.LeftParen or
		TokenKind.LeftBrace or
		TokenKind.Identifier or
		TokenKind.This or
		TokenKind.NumericLiteral or
		TokenKind.CharLiteral or
		TokenKind.StringLiteral or
		TokenKind.True or
		TokenKind.False or
		TokenKind.Plus or
		TokenKind.Minus or
		TokenKind.Bang or
		TokenKind.Asterisk or
		TokenKind.And;

	public static InfixRule? GetInfixRule(TokenKind tokenKind)
	{
		InfixRules.TryGetValue(tokenKind, out var rule);
		return rule;
	}
}
