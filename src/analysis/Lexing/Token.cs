using System.Diagnostics;
using static Surab.Analysis.TokenKind;

namespace Surab.Analysis;

/// <summary>
/// A lexical token kind.
/// </summary>
public enum TokenKind
{
	OpenParen,
	ClosedParen,
	OpenBrace,
	ClosedBrace,
	Comma,
	Dot,
	Semicolon,
	Colon,
	ColonColon,
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
	Trait,
	Alias,
	Impl,
	This,
	True,
	False,
	Var,
	Const,
	Return,

	If,
	Else,

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
			OpenParen => "(",
			ClosedParen => ")",
			OpenBrace => "{",
			ClosedBrace => "}",
			Comma => ",",
			Dot => ".",
			Semicolon => ";",
			Colon => ":",
			ColonColon => "::",
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
			Trait => "trait",
			Alias => "alias",
			Impl => "impl",
			This => "this",
			True => "true",
			False => "false",
			Var => "let",
			Const => "const",
			Return => "return",

			If => "if",
			Else => "else",

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
		{ "trait", Trait },
		{ "alias", Alias },
		{ "impl", Impl },
		{ "this", This },
		{ "true", True },
		{ "false", False },
		{ "var", Var },
		{ "const", Const },
		{ "return", Return },
		{ "if", If },
		{ "else", Else },
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
		Bang or
		Asterisk or
		And;

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

	public static bool IsMethodOp(this TokenKind kind) => kind is
		Plus or
		Minus or
		Asterisk or
		Slash;

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

	/// <summary>
	/// Returns if the token can start a decl.
	/// Note that this doesn't include member decls as those have their own contextual parsing.
	/// </summary>
	public static bool CanStartDecl(this TokenKind kind) => kind is
		Const or
		Var or
		Fn or
		Struct or
		Class or
		Trait or
		Alias or
		Impl ||
		CanStartAttribute(kind);

	public static bool CanStartStmt(this TokenKind kind) =>
		kind.CanStartDecl() ||
		kind.CanStartExpr() ||
		kind is Return or Semicolon;

	public static bool CanStartExpr(this TokenKind kind) =>
		kind.IsUnary() ||
		kind.IsLiteral() ||
		kind is OpenParen or OpenBrace or This or Identifier or If;

	public static bool CanStartParam(this TokenKind kind) =>
		kind is Identifier ||
		CanStartAttribute(kind); // TODO: Not parsing attrs for params right now?

	public static bool CanStartType(this TokenKind kind) =>
		kind is Asterisk or Fn or Identifier ||
		CanStartAttribute(kind);
}
