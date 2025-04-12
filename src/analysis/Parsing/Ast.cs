using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Surab.Analysis;

public class SyntaxTree
{
	internal SyntaxTree(ProjectFile file)
	{
		File = file;
	}

	public ProjectFile File { get; }

	// Set during parsing.
	public SyntaxNode Root { get; internal set; } = default!;

	// Set during parsing.
	public DiagBag Diags { get; internal set; } = default!;

	// Set during parsing.
	public IReadOnlyList<int> LineOffsetToCharsCount { get; internal set; } = default!;

	/// <summary>
	/// Gets the deepest node at the given position.
	/// </summary>
	public SyntaxNode? GetNodeAt(LinePosition linePosition)
	{
		return Root.GetNodeAt(linePosition);
	}

	/// <summary>
	/// Gets the deepest node at the given position.
	/// </summary>
	public SyntaxNode? GetNodeAtWithTrivia(LinePosition linePosition)
	{
		return Root.GetNodeAtWithTrivia(linePosition);
	}

	/// <summary>
	/// Gets the deepest node at the given position.
	/// </summary>
	public SyntaxNode? GetNodeAt(int filePosition)
	{
		return Root.GetNodeAt(filePosition);
	}

	/// <summary>
	/// Gets the deepest node at the given position.
	/// </summary>
	public SyntaxNode? GetNodeAtWithTrivia(int filePosition)
	{
		return Root.GetNodeAtWithTrivia(filePosition);
	}

	/// <summary>
	/// Gets the token at the given position.
	/// </summary>
	public SyntaxToken? GetTokenAt(LinePosition linePosition)
	{
		return Root.GetTokenAt(linePosition);
	}

	/// <summary>
	/// Gets the token at the given position.
	/// </summary>
	public SyntaxToken? GetTokenAtWithTrivia(LinePosition linePosition)
	{
		return Root.GetTokenAtWithTrivia(linePosition);
	}
}

public abstract class SyntaxEntity : ISyntaxTreeVisitable
{
	protected SyntaxEntity(SyntaxTree tree)
	{
		Tree = tree;
	}

	public SyntaxNode? Parent { get; private set; }

	public TextSpan Span { get; init; }

	public TextSpan SpanWithTrivia { get; init; }

	public SyntaxTree Tree { get; }

	internal void SetParent(SyntaxNode? parent)
	{
		Parent = parent;
	}

	public abstract void Accept(SyntaxTreeVisitor visitor);
}

public enum SyntaxTokenKind
{
	Simple,
	UnaryOperator,
	BinaryOperator,
	Identifier,
	Literal,
	Skipped, // Couldn't parse
}

public abstract class SyntaxToken : SyntaxEntity
{
	private readonly ImmutableArray<SyntaxTrivia> _leadingTrivia = [];
	private readonly ImmutableArray<SyntaxTrivia> _trailingTrivia = [];

	public SyntaxToken(SyntaxTree tree, SyntaxTokenKind kind, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, TextSpan span)
		: base(tree)
	{
		Kind = kind;
		_leadingTrivia = ConvertToSyntaxTrivia(leadingTriviaTokens).ToImmutableArray();
		_trailingTrivia = ConvertToSyntaxTrivia(trailingTriviaTokens).ToImmutableArray();
		(Span, SpanWithTrivia) = TextSpanHelper.ComputeSpanningSpan(this, span);

		IEnumerable<SyntaxTrivia> ConvertToSyntaxTrivia(IEnumerable<Token> tokens)
		{
			foreach (var token in tokens)
			{
				yield return SyntaxTrivia.Create(token, this);
			}
		}
	}

	public new SyntaxNode Parent => base.Parent!;

	public SyntaxTokenKind Kind { get; }

	/// <summary>
	/// The leading trivia.
	/// </summary>
	public IEnumerable<SyntaxTrivia> LeadingTrivia => _leadingTrivia;

	/// <summary>
	/// The trailing trivia. Usually empty except for the last token in a file.
	/// </summary>
	public IEnumerable<SyntaxTrivia> TrailingTrivia => _trailingTrivia;

	public bool IsPositionInTrivia(LinePosition pos, [NotNullWhen(true)] out SyntaxTrivia? trivia, out bool leading)
	{
		leading = false;
		if (!Span.Line.Contains(pos) && SpanWithTrivia.Line.Contains(pos))
		{
			foreach (var t in _leadingTrivia)
			{
				if (t.Span.Line.Contains(pos))
				{
					leading = true;
					trivia = t;
					return true;
				}
			}
			foreach (var t in _trailingTrivia)
			{
				if (t.Span.Line.Contains(pos))
				{
					leading = false;
					trivia = t;
					return true;
				}
			}
		}
		trivia = null;
		return false;
	}
}

public static class SyntaxTokenExtensions
{
	public static bool IsSimpleTokenOfKind([NotNullWhen(true)] this SyntaxToken? token, SimpleTokenKind kind, [NotNullWhen(true)] out SimpleToken? simpleToken)
	{
		if (token?.Kind is SyntaxTokenKind.Simple)
		{
			simpleToken = (SimpleToken)token;
			return simpleToken.SimpleTokenKind == kind;
		}
		simpleToken = null;
		return false;
	}
}

public enum ContextualKeyword
{
	As,
	Import,
}

public enum MethodOp
{
	Add,
	Sub,
	Mul,
	Div,
}

internal static class TokenHelper
{
	private static readonly Dictionary<ContextualKeyword, string> ContextualKeywordsMap = new()
	{
		{ ContextualKeyword.As, "as" },
		{ ContextualKeyword.Import, "import" },
	};

	private static readonly Dictionary<string, MethodOp> StringToMethodOpMap = new()
	{
		{ "+", MethodOp.Add },
		{ "-", MethodOp.Sub },
		{ "*", MethodOp.Mul },
		{ "/", MethodOp.Div },
	};

	public static bool IsContextualKeyword(ContextualKeyword contextualKeyword, ReadOnlySpan<char> str)
	{
		if (ContextualKeywordsMap.TryGetValue(contextualKeyword, out var value) && MemoryExtensions.Equals(str, value.AsSpan(), StringComparison.Ordinal))
		{
			return true;
		}
		return false;
	}

	public static bool TryGetMethodOp(string str, out MethodOp op)
	{
		if (StringToMethodOpMap.TryGetValue(str, out op))
		{
			return true;
		}
		return false;
	}
}

public enum SimpleTokenKind
{
	OpenParen,
	ClosedParen,
	OpenBrace,
	ClosedBrace,
	OpenChevron,
	ClosedChevron,
	Comma,
	Dot,
	ColonColon,
	Semicolon,
	Colon,
	Question,
	Equal,
	At,
	Asterisk,

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
	Field,
	Extern,
	Public,
	Return,

	If,
	Else,

	AsKeyword,
	ImportKeyword,
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} [{SimpleTokenKind}] {ToString()}")]
public sealed class SimpleToken : SyntaxToken
{
	private SimpleToken(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, TextSpan span)
		: base(tree, SyntaxTokenKind.Simple, leadingTriviaTokens, trailingTriviaTokens, span)
	{
	}

	public SimpleTokenKind SimpleTokenKind { get; init; }

	/// <summary>
	/// Gets whether the token is a keyword (reserved or contextual).
	/// </summary>
	public bool IsKeyword { get; init; }

	/// <summary>
	/// Gets whether the token is a contextual keyword.
	/// </summary>
	public bool IsContextualKeyword { get; init; }

	public bool IsMissing { get; init; }

	public static SimpleToken Create(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, Token token) =>
		Create(tree, leadingTriviaTokens, trailingTriviaTokens, token.Kind, token.Span, isMissing: false);

	public static SimpleToken CreateMissing(SyntaxTree tree, TokenKind tokenKind, TextSpan span) =>
		Create(tree, [], [], tokenKind, span, isMissing: true);

	private static SimpleToken Create(
		SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens,
		TokenKind tokenKind,
		TextSpan span,
		bool isMissing = false) => new(tree, leadingTriviaTokens, trailingTriviaTokens, span)
		{
			IsKeyword = tokenKind.IsReservedKeyword(),
			IsMissing = isMissing,
			SimpleTokenKind = tokenKind switch
			{
				TokenKind.OpenParen => SimpleTokenKind.OpenParen,
				TokenKind.ClosedParen => SimpleTokenKind.ClosedParen,
				TokenKind.OpenBrace => SimpleTokenKind.OpenBrace,
				TokenKind.ClosedBrace => SimpleTokenKind.ClosedBrace,
				TokenKind.Less => SimpleTokenKind.OpenChevron,
				TokenKind.Greater => SimpleTokenKind.ClosedChevron,
				TokenKind.Comma => SimpleTokenKind.Comma,
				TokenKind.Dot => SimpleTokenKind.Dot,
				TokenKind.ColonColon => SimpleTokenKind.ColonColon,
				TokenKind.Semicolon => SimpleTokenKind.Semicolon,
				TokenKind.Colon => SimpleTokenKind.Colon,
				TokenKind.Question => SimpleTokenKind.Question,
				TokenKind.Equal => SimpleTokenKind.Equal,
				TokenKind.At => SimpleTokenKind.At,
				TokenKind.AtBeforeIdentifier => SimpleTokenKind.At,
				TokenKind.Asterisk => SimpleTokenKind.Asterisk,

				TokenKind.Fn => SimpleTokenKind.Fn,
				TokenKind.Struct => SimpleTokenKind.Struct,
				TokenKind.Class => SimpleTokenKind.Class,
				TokenKind.Trait => SimpleTokenKind.Trait,
				TokenKind.Alias => SimpleTokenKind.Alias,
				TokenKind.Impl => SimpleTokenKind.Impl,
				TokenKind.This => SimpleTokenKind.This,
				TokenKind.True => SimpleTokenKind.True,
				TokenKind.False => SimpleTokenKind.False,
				TokenKind.Var => SimpleTokenKind.Var,
				TokenKind.Const => SimpleTokenKind.Const,
				TokenKind.Return => SimpleTokenKind.Return,

				TokenKind.If => SimpleTokenKind.If,
				TokenKind.Else => SimpleTokenKind.Else,

				_ => throw new Exception($"TokenKind {tokenKind} not supported."),
			},
		};

	public static SimpleToken Create(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, ContextualKeyword contextualKeyword, Token token) =>
		Create(tree, leadingTriviaTokens, trailingTriviaTokens, contextualKeyword, token.Span, isMissing: false);

	public static SimpleToken CreateMissing(SyntaxTree tree, ContextualKeyword contextualKeyword, TextSpan span) =>
		Create(tree, [], [], contextualKeyword, span, isMissing: true);

	private static SimpleToken Create(
		SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens,
		ContextualKeyword contextualKeyword,
		TextSpan span,
		bool isMissing = false) => new(tree, leadingTriviaTokens, trailingTriviaTokens, span)
		{
			IsKeyword = true,
			IsContextualKeyword = true,
			IsMissing = isMissing,
			SimpleTokenKind = contextualKeyword switch
			{
				ContextualKeyword.As => SimpleTokenKind.AsKeyword,
				ContextualKeyword.Import => SimpleTokenKind.ImportKeyword,
				_ => throw new UnreachableException(),
			},
		};

	public override string ToString()
	{
		return SimpleTokenKind switch
		{
			SimpleTokenKind.OpenParen => "(",
			SimpleTokenKind.ClosedParen => ")",
			SimpleTokenKind.OpenBrace => "{",
			SimpleTokenKind.ClosedBrace => "}",
			SimpleTokenKind.OpenChevron => "<",
			SimpleTokenKind.ClosedChevron => ">",
			SimpleTokenKind.Comma => ",",
			SimpleTokenKind.Dot => ".",
			SimpleTokenKind.ColonColon => "::",
			SimpleTokenKind.Semicolon => ";",
			SimpleTokenKind.Colon => ":",
			SimpleTokenKind.Question => "?",
			SimpleTokenKind.Equal => "=",
			SimpleTokenKind.At => "@",
			SimpleTokenKind.Asterisk => "*",

			SimpleTokenKind.Fn => "fn",
			SimpleTokenKind.Struct => "struct",
			SimpleTokenKind.Class => "class",
			SimpleTokenKind.Trait => "trait",
			SimpleTokenKind.Alias => "alias",
			SimpleTokenKind.Impl => "impl",
			SimpleTokenKind.This => "this",
			SimpleTokenKind.True => "true",
			SimpleTokenKind.False => "false",
			SimpleTokenKind.Var => "var",
			SimpleTokenKind.Const => "const",
			SimpleTokenKind.Return => "return",

			SimpleTokenKind.If => "if",
			SimpleTokenKind.Else => "else",

			SimpleTokenKind.AsKeyword => "as",
			SimpleTokenKind.ImportKeyword => "import",

			_ => throw new NotImplementedException(),
		};
	}

	public override void Accept(SyntaxTreeVisitor visitor)
	{
		visitor.VisitSimpleToken(this);
	}
}

public enum UnaryOperatorTokenKind
{
	Pos, // Usually redundant but must be represented in the AST.
	Neg,
	Not,
	Address,
	Deref,
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} [{UnaryOperatorTokenKind}] {ToString()}")]
public sealed class UnaryOperatorToken : SyntaxToken
{
	private UnaryOperatorToken(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, TextSpan span)
		: base(tree, SyntaxTokenKind.UnaryOperator, leadingTriviaTokens, trailingTriviaTokens, span)
	{
	}

	public UnaryOperatorTokenKind UnaryOperatorTokenKind { get; init; }

	public static UnaryOperatorToken Create(
		SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens,
		Token token) => new(tree, leadingTriviaTokens, trailingTriviaTokens, token.Span)
		{
			UnaryOperatorTokenKind = token.Kind switch
			{
				TokenKind.Plus => UnaryOperatorTokenKind.Pos,
				TokenKind.Minus => UnaryOperatorTokenKind.Neg,
				TokenKind.Bang => UnaryOperatorTokenKind.Not,
				TokenKind.And => UnaryOperatorTokenKind.Address,
				TokenKind.Asterisk => UnaryOperatorTokenKind.Deref,
				_ => throw new Exception($"TokenKind {token.Kind} not supported."),
			},
		};

	public override string ToString() => UnaryOperatorTokenKind switch
	{
		UnaryOperatorTokenKind.Pos => "+",
		UnaryOperatorTokenKind.Neg => "-",
		UnaryOperatorTokenKind.Not => "!",
		UnaryOperatorTokenKind.Address => "&",
		UnaryOperatorTokenKind.Deref => "*",
		_ => throw new NotImplementedException(),
	};

	public override void Accept(SyntaxTreeVisitor visitor)
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
	And,    // &&
	Or,     // ||
	BitAnd, // &
	BitOr,  // |
	BitXor, // ^
	Eq,
	Ne,
	Lt,
	Le,
	Gt,
	Ge,
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} [{BinaryOperatorTokenKind}] {ToString()}")]
public sealed class BinaryOperatorToken : SyntaxToken
{
	private BinaryOperatorToken(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, TextSpan span)
		: base(tree, SyntaxTokenKind.BinaryOperator, leadingTriviaTokens, trailingTriviaTokens, span)
	{
	}

	public BinaryOperatorTokenKind BinaryOperatorTokenKind { get; init; }

	public static BinaryOperatorToken Create(
		SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens,
		Token token) => new(tree, leadingTriviaTokens, trailingTriviaTokens, token.Span)
		{
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

	public override void Accept(SyntaxTreeVisitor visitor)
	{
		visitor.VisitBinaryOperatorToken(this);
	}
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
public sealed class IdentifierToken : SyntaxToken
{
	private IdentifierToken(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, TextSpan span, string value, bool isMissing)
		: base(tree, SyntaxTokenKind.Identifier, leadingTriviaTokens, trailingTriviaTokens, span)
	{
		Value = value;
		IsMissing = isMissing;
	}

	public string Value { get; }

	public bool IsMissing { get; }

	public static IdentifierToken Create(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, TextSpan span, string value)
	{
		return new(tree, leadingTriviaTokens, trailingTriviaTokens, span, value, isMissing: false);
	}

	public static IdentifierToken CreateMissing(SyntaxTree tree, TextSpan span)
	{
		return new(tree, [], [], span, string.Empty, isMissing: true);
	}

	public override void Accept(SyntaxTreeVisitor visitor)
	{
		visitor.VisitIdentifierToken(this);
	}

	public bool IsOp()
	{
		return Value != null && Value.Length != 0 && !Chars.IsIdentifierStart(Value[0]);
	}

	public override string ToString() => Value;
}

public enum LiteralTokenKind
{
	/// <summary>
	/// Token's Data stored as ulong for non fractional literals.
	/// Note that this doesn't necessarily mean the literal node's type is ix or fx, it could be either.
	/// </summary>
	NumericInteger,
	/// <summary>
	/// Token's Data stored as double for fractional literals.
	/// Note that this doesn't necessarily mean the literal node's type is ix or fx, it could be either.
	/// </summary>
	NumericFloat,
	Char,
	String,
	Bool,
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} [{LiteralTokenKind}] {Data}")]
public sealed class LiteralToken : SyntaxToken
{
	private LiteralToken(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, TextSpan span, LiteralTokenKind literalTokenKind, object? data)
		: base(tree, SyntaxTokenKind.Literal, leadingTriviaTokens, trailingTriviaTokens, span)
	{
		Span = span;
		LiteralTokenKind = literalTokenKind;
		Data = data;
	}

	public LiteralTokenKind LiteralTokenKind { get; init; }

	public object? Data { get; init; }

	public string? NumericType { get; internal set; }

	public static LiteralToken FromBool(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, Token token, bool value) =>
		new(tree, leadingTriviaTokens, trailingTriviaTokens, token.Span, LiteralTokenKind.Bool, value);

	public static LiteralToken FromNumericInteger(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, Token token, ulong value) =>
		new(tree, leadingTriviaTokens, trailingTriviaTokens, token.Span, LiteralTokenKind.NumericInteger, value);

	public static LiteralToken FromNumericFloat(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, Token token, double value) =>
		new(tree, leadingTriviaTokens, trailingTriviaTokens, token.Span, LiteralTokenKind.NumericFloat, value);

	public static LiteralToken FromChar(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, Token token, char? value) =>
		new(tree, leadingTriviaTokens, trailingTriviaTokens, token.Span, LiteralTokenKind.Char, value);

	public static LiteralToken FromString(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, Token token, string? value) =>
		new(tree, leadingTriviaTokens, trailingTriviaTokens, token.Span, LiteralTokenKind.String, value);

	/// <summary>
	/// Returns the numeric value as an integer (ulong).
	/// </summary>
	/// <exception cref="InvalidOperationException">If the token is not numeric or can't be returned as a ulong.</exception>
	public ulong GetNumericValueAsInteger()
	{
		return LiteralTokenKind switch
		{
			LiteralTokenKind.NumericInteger => (ulong)Data!,
			LiteralTokenKind.NumericFloat => throw new InvalidOperationException("Literal is a float."),
			_ => throw new InvalidOperationException("Literal is not numeric."),
		};
	}

	/// <summary>
	/// Returns the numeric value as a float (double).
	/// </summary>
	/// <exception cref="InvalidOperationException">If the token is not numeric or can't be returned as a double.</exception>
	public double GetNumericValueAsFloat()
	{
		return LiteralTokenKind switch
		{
			LiteralTokenKind.NumericInteger => (ulong)Data!,
			LiteralTokenKind.NumericFloat => (double)Data!,
			_ => throw new InvalidOperationException("Literal is not numeric."),
		};
	}

	public bool TryGetNumericIntegerValue(out ulong value)
	{
		value = default!;
		if (LiteralTokenKind != LiteralTokenKind.NumericInteger)
		{
			return false;
		}
		value = (ulong)Data!;
		return true;
	}

	public bool TryGetNumericFloatValue(out double value)
	{
		value = default!;
		if (LiteralTokenKind != LiteralTokenKind.NumericFloat)
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

	public override void Accept(SyntaxTreeVisitor visitor)
	{
		visitor.VisitLiteralToken(this);
	}

	public override string ToString() => $"{Data}";
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
public sealed class SkippedToken : SyntaxToken
{
	internal SkippedToken(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, Token token)
		: base(tree, SyntaxTokenKind.Skipped, leadingTriviaTokens, trailingTriviaTokens, token.Span)
	{
		Token = token;
	}

	public Token Token { get; }

	public override void Accept(SyntaxTreeVisitor visitor)
	{
		visitor.VisitSkippedToken(this);
	}

	public override string ToString()
	{
		return Token.ToString();
	}
}

public enum SyntaxTriviaKind
{
	Whitespace,
	LineComment,
	BlockComment,
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} \\{{Kind,nq}\\} {ToString()}")]
public readonly struct SyntaxTrivia
{
	private SyntaxTrivia(SyntaxTriviaKind kind, TextSpan span, SyntaxToken token)
	{
		Kind = kind;
		Span = span;
		Token = token;
	}

	public static SyntaxTrivia Create(Token triviaToken, SyntaxToken parentToken)
	{
		var triviaKind = triviaToken.Kind switch
		{
			TokenKind.Whitespace => SyntaxTriviaKind.Whitespace,
			TokenKind.LineComment => SyntaxTriviaKind.LineComment,
			TokenKind.BlockComment => SyntaxTriviaKind.BlockComment,
			_ => throw new UnreachableException(),
		};
		return new(triviaKind, triviaToken.Span, parentToken);
	}

	public SyntaxTriviaKind Kind { get; }

	public TextSpan Span { get; }

	public SyntaxToken Token { get; }

	public override string ToString()
	{
		return Token.Tree.File.Text[Span.File.GetRange()];
	}
}

public interface ISyntaxNodeHasMainIdentifier
{
	IdentifierToken Identifier { get; }
}

public interface ISyntaxNodeHasMainNameDef
{
	NameDefNode Name { get; }
}

public interface ISyntaxNodeHasAttrs
{
	AttrsListNode? Attrs { get; }
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
public abstract class SyntaxNode : SyntaxEntity
{
	protected SyntaxNode(SyntaxTree tree, SyntaxNodeKind kind) : base(tree)
	{
		Kind = kind;
	}

	// Set by the generated nodes.
	protected List<SyntaxToken?> _childTokens = default!;
	protected List<SyntaxNode?> _childNodes = default!;

	public SyntaxNodeKind Kind { get; }

	/// <summary>
	/// Gets the child tokens of this node. Guaranteed to be ordered by the start of the child token's Span.
	/// </summary>
	public IEnumerable<SyntaxToken> ChildTokens
	{
		[DebuggerStepThrough]
		get
		{
			foreach (var token in _childTokens)
			{
				if (token != null)
				{
					yield return token;
				}
			}
		}
	}

	/// <summary>
	/// Gets the child nodes of this node. Guaranteed to be ordered by the start of the child node's Span.
	/// </summary>
	public IEnumerable<SyntaxNode> ChildNodes
	{
		[DebuggerStepThrough]
		get
		{
			foreach (var node in _childNodes)
			{
				if (node != null)
				{
					yield return node;
				}
			}
		}
	}

	protected void SetChildrenParent()
	{
		foreach (var c in ChildNodes)
		{
			c.SetParent(this);
		}
		foreach (var c in ChildTokens)
		{
			c.SetParent(this);
		}
	}

	/// <summary>
	/// Enumerates ancestor nodes in breadth first order.
	/// </summary>
	public IEnumerable<SyntaxNode> Ancestors() => AncestorsCore(includeSelf: false);

	/// <summary>
	/// Enumerates ancestor nodes and self in breadth first order.
	/// </summary>
	public IEnumerable<SyntaxNode> AncestorsAndSelf() => AncestorsCore(includeSelf: true);

	private IEnumerable<SyntaxNode> AncestorsCore(bool includeSelf)
	{
		if (includeSelf)
		{
			yield return this;
		}

		for (var current = Parent; current != null; current = current.Parent)
		{
			yield return current;
		}
	}

	/// <summary>
	/// Enumerates descendant nodes in breadth first order.
	/// </summary>
	public IEnumerable<SyntaxNode> DescendantNodes() => DescendantNodesCore(includeSelf: false);

	/// <summary>
	/// Enumerates descendant nodes and self in breadth first order.
	/// </summary>
	public IEnumerable<SyntaxNode> DescendantNodesAndSelf() => DescendantNodesCore(includeSelf: true);

	private IEnumerable<SyntaxNode> DescendantNodesCore(bool includeSelf)
	{
		if (includeSelf)
		{
			yield return this;
		}

		foreach (var child in ChildNodes)
		{
			yield return child;
		}

		foreach (var child in ChildNodes)
		{
			foreach (var item in child.DescendantNodes())
			{
				yield return item;
			}
		}
	}

	/// <summary>
	/// Enumerates descendant tokens in breadth first order.
	/// </summary>
	public IEnumerable<SyntaxToken> DescendantTokens()
	{
		foreach (var item in EnumerateBySpanStart())
		{
			if (item.Token != null)
			{
				yield return item.Token;
			}
			else if (item.Node != null)
			{
				foreach (var token in item.Node.DescendantTokens())
				{
					yield return token;
				}
			}
		}
	}

	/// <summary>
	/// Gets the deepest node at the given position.
	/// </summary>
	public SyntaxNode? GetNodeAt(LinePosition linePosition)
	{
		return GetNodeAtRecursive(this, linePosition);
	}

	/// <summary>
	/// Gets the deepest node at the given position.
	/// </summary>
	public SyntaxNode? GetNodeAtWithTrivia(LinePosition linePosition)
	{
		return GetNodeAtRecursiveWithTrivia(this, linePosition);
	}

	/// <summary>
	/// Gets the deepest node at the given position.
	/// </summary>
	public SyntaxNode? GetNodeAt(int filePosition)
	{
		return GetNodeAtRecursive(this, filePosition);
	}

	/// <summary>
	/// Gets the deepest node at the given position.
	/// </summary>
	public SyntaxNode? GetNodeAtWithTrivia(int filePosition)
	{
		return GetNodeAtRecursiveWithTrivia(this, filePosition);
	}

	private static SyntaxNode? GetNodeAtRecursive(SyntaxNode node, LinePosition linePosition)
	{
		foreach (var childNode in node.ChildNodes)
		{
			if (childNode.Span.Line.Contains(linePosition))
			{
				return GetNodeAtRecursive(childNode, linePosition);
			}
		}
		return node;
	}

	private static SyntaxNode? GetNodeAtRecursiveWithTrivia(SyntaxNode node, LinePosition linePosition)
	{
		foreach (var childNode in node.ChildNodes)
		{
			if (childNode.SpanWithTrivia.Line.Contains(linePosition))
			{
				return GetNodeAtRecursiveWithTrivia(childNode, linePosition);
			}
		}
		return node;
	}

	private static SyntaxNode? GetNodeAtRecursive(SyntaxNode node, int filePosition)
	{
		foreach (var childNode in node.ChildNodes)
		{
			if (childNode.Span.File.Contains(filePosition))
			{
				return GetNodeAtRecursive(childNode, filePosition);
			}
		}
		return node;
	}

	private static SyntaxNode? GetNodeAtRecursiveWithTrivia(SyntaxNode node, int filePosition)
	{
		foreach (var childNode in node.ChildNodes)
		{
			if (childNode.SpanWithTrivia.File.Contains(filePosition))
			{
				return GetNodeAtRecursiveWithTrivia(childNode, filePosition);
			}
		}
		return node;
	}

	/// <summary>
	/// Gets the token at the position.
	/// </summary>
	public SyntaxToken? GetTokenAt(LinePosition linePosition)
	{
		var deepestNode = GetNodeAt(linePosition);
		return deepestNode?.GetDirectTokenAt(linePosition);
	}

	/// <summary>
	/// Gets the token at the position.
	/// </summary>
	public SyntaxToken? GetTokenAtWithTrivia(LinePosition linePosition)
	{
		var deepestNode = GetNodeAtWithTrivia(linePosition);
		return deepestNode?.GetDirectTokenAtWithTrivia(linePosition);
	}

	/// <summary>
	/// Gets the direct (this node's children) token at the position.
	/// </summary>
	public SyntaxToken? GetDirectTokenAt(LinePosition linePosition)
	{
		foreach (var token in ChildTokens)
		{
			if (token.Span.Line.Contains(linePosition))
			{
				return token;
			}
			else if (token.Span.Line.Start > linePosition)
			{
				break;
			}
		}
		return null;
	}

	/// <summary>
	/// Gets the direct (this node's children) token at the position.
	/// </summary>
	public SyntaxToken? GetDirectTokenAtWithTrivia(LinePosition linePosition)
	{
		foreach (var token in ChildTokens)
		{
			if (token.SpanWithTrivia.Line.Contains(linePosition))
			{
				return token;
			}
			else if (token.SpanWithTrivia.Line.Start > linePosition)
			{
				break;
			}
		}
		return null;
	}

	/// <summary>
	/// Gets the direct (this node's children) token at the position.
	/// </summary>
	public SyntaxToken? GetDirectTokenAt(int filePosition)
	{
		foreach (var token in ChildTokens)
		{
			if (token.Span.File.Contains(filePosition))
			{
				return token;
			}
			else if (token.Span.File.Start > filePosition)
			{
				break;
			}
		}
		return null;
	}

	/// <summary>
	/// Gets the direct (this node's children) token at the position.
	/// </summary>
	public SyntaxToken? GetDirectTokenAtWithTrivia(int filePosition)
	{
		foreach (var token in ChildTokens)
		{
			if (token.SpanWithTrivia.File.Contains(filePosition))
			{
				return token;
			}
			else if (token.SpanWithTrivia.File.Start > filePosition)
			{
				break;
			}
		}
		return null;
	}

	/// <summary>
	/// Yields back child nodes or tokens in span start order.
	/// </summary>
	public IEnumerable<(SyntaxNode? Node, SyntaxToken? Token)> EnumerateBySpanStart()
	{
		IEnumerable<(SyntaxNode? Node, SyntaxToken? Token, TextSpan Span)> items = [
			.. ChildTokens.Select<SyntaxToken, (SyntaxNode? Node, SyntaxToken? Token, TextSpan span)>(t => (null, t, t.Span)),
			.. ChildNodes.Select<SyntaxNode, (SyntaxNode? Node, SyntaxToken? Token, TextSpan span)>(n => (n, null, n.Span)),
		];

		return items.OrderBy(x => x.Span.File.Start).Select(x => (x.Node, x.Token));
	}

	public override string ToString()
	{
		return Tree.File.Text[Span.File.GetRange()];
	}

	/// <summary>
	/// Returns whether or not this node declares a symbol. i.e. Calling <see cref="Reasoner.GetDeclaredSymbol(SyntaxNode)"/> on this should never return `null`.
	/// This is only for the specific nodes that map to a symbol, and doesn't correspond to the fuzzier GetDeclaredSymbol.
	/// </summary>
	public bool DeclaresSymbol()
	{
		return this is DeclNode or DeclStmtNode or ParamNode or GenericParamNode;
	}
}

public abstract class DeclNode(SyntaxTree tree, SyntaxNodeKind kind) : SyntaxNode(tree, kind)
{
}

public abstract class StmtNode(SyntaxTree tree, SyntaxNodeKind kind) : SyntaxNode(tree, kind)
{
}

public abstract class ExprNode(SyntaxTree tree, SyntaxNodeKind kind) : SyntaxNode(tree, kind)
{
	public bool IsMissing => this is MissingExprNode;
}

public abstract class TypeNode(SyntaxTree tree, SyntaxNodeKind kind) : SyntaxNode(tree, kind)
{
	public bool IsMissing => this is MissingTypeNode;
}

// ---

public partial class FileNode
{
	public IEnumerable<ImportNode> Imports => ImportsList.Imports;

	public IEnumerable<DeclNode> Decls => DeclsList?.Decls ?? [];
}

public partial class MethodDeclNode
{
	/// <summary>
	/// Gets whether the method is normally callable (not initializer or op).
	/// </summary>
	public bool IsNormallyCallable => !IsInitializer && !IsMethodOp;

	/// <summary>
	/// Gets whether the method is the special initializer method `() { }`.
	/// </summary>
	public bool IsInitializer => !Sig.Name.Identifier.IsMissing && Sig.Name.Identifier.Value == string.Empty;

	/// <summary>
	/// Gets whether the method is an implementation of an operator.
	/// </summary>
	public bool IsMethodOp => TokenHelper.TryGetMethodOp(Sig.Name.Identifier.Value, out _);
}

public partial class FnTypeNode
{
	public IEnumerable<ParamTypeNode>? ParamTypes => ParamTypesList?.ParamTypes;
}

public partial class FnSigNode
{
	public AttrsListNode? Attrs => Parent switch
	{
		FnDeclNode fnDecl => fnDecl.Attrs,
		MethodDeclNode fnDecl => fnDecl.Attrs,
		_ => throw new UnreachableException(),
	};

	public IEnumerable<ParamNode> Params => ParamsList.Params;

	public bool FnHasBody => Parent switch
	{
		FnDeclNode fnDecl => fnDecl.Body != null,
		MethodDeclNode fnDecl => fnDecl.Body != null,
		_ => throw new UnreachableException(),
	};
}

public partial class ConstDeclNode
{
}

public partial class StructDeclNode
{
	public IEnumerable<DeclNode> Decls => DeclsList?.Decls ?? [];
}

public partial class TraitDeclNode
{
	public IEnumerable<DeclNode> Decls => DeclsList?.Decls ?? [];
}

public partial class ImplDeclNode
{
	public IEnumerable<DeclNode> Decls => DeclsList?.Decls ?? [];
}

public partial class CallExprNode
{
	public IEnumerable<ArgNode> Args => ArgsList.Args;
}

public static class SyntaxNodeKindExtensions
{
	/// <summary>
	/// Returns if the node is <see cref="FnDeclNode"/> or <see cref="MethodDeclNode"/>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsFnOrMethod(this SyntaxNodeKind kind) => kind is
		SyntaxNodeKind.FnDecl or
		SyntaxNodeKind.MethodDecl;

	/// <summary>
	/// Returns if the node can contain executable code (i.e. a function, a method, etc).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool CanContainExecutableCode(this SyntaxNodeKind kind) => kind.IsFnOrMethod();

	/// <summary>
	/// Returns if the node is a logical enclosure, which is one of the following node types:
	/// <see cref="StructDeclNode"/>, <see cref="FnDeclNode"/>, <see cref="MethodDeclNode"/>
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsLogicalEnclosure(this SyntaxNodeKind kind) => kind is
		// TODO: Add lambdas.
		SyntaxNodeKind.StructDecl or
		SyntaxNodeKind.FnDecl or
		SyntaxNodeKind.MethodDecl;
}

public static class SyntaxNodeExtensions
{
	public static SyntaxNode GetMainNameNodeOrSelf(this SyntaxNode node)
	{
		var mainIdentifierToken = node.GetMainIdentifierToken();
		return mainIdentifierToken != null ? mainIdentifierToken.Parent : node;
	}

	public static IdentifierToken? GetMainIdentifierToken(this SyntaxNode? node)
	{
		return node switch
		{
			ISyntaxNodeHasMainIdentifier n => n.Identifier,
			ISyntaxNodeHasMainNameDef n => n.Name.Identifier,
			FnDeclNode n => n.Sig.Name.Identifier,
			MethodDeclNode n => n.Sig.Name.Identifier,
			AliasDeclNode n => n.AsName.Name.Identifier,
			_ => null,
		};
	}

	public static string? GetFriendlyDeclTag(this SyntaxNode? node)
	{
		return node?.Kind switch
		{
			SyntaxNodeKind.StructDecl => "struct",
			SyntaxNodeKind.TraitDecl => "trait",
			SyntaxNodeKind.AliasDecl => "alias",
			SyntaxNodeKind.VarDecl => "var",
			SyntaxNodeKind.FnDecl => "fn",
			SyntaxNodeKind.ConstDecl => "const",
			SyntaxNodeKind.ImplDecl => "impl",
			_ => null,
		};
	}

	/// <summary>
	/// Climbs up and returns the first logical enclosure node encountered.
	/// </summary>
	public static SyntaxNode? GetLogicalEnclosure(this SyntaxNode? node)
	{
		return node?.Ancestors()
			.Where(x => x.Kind.IsLogicalEnclosure())
			.FirstOrDefault();
	}
}
