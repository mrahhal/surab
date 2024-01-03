using System.Collections.Immutable;
using System.Diagnostics;

namespace Surab.Analysis;

public class SyntaxTree
{
	internal SyntaxTree(SourceFile sourceFile)
	{
		SourceFile = sourceFile;
	}

	public SourceFile SourceFile { get; }

	// Set during parsing.
	public SyntaxNode Root { get; internal set; } = default!;

	// Set during parsing.
	public DiagnosticBag Diagnostics { get; internal set; } = default!;
}

public abstract class SyntaxEntity : ISyntaxTreeVisitable
{
	protected SyntaxEntity(SyntaxTree tree)
	{
		Tree = tree;
	}

	public SyntaxNode? Parent { get; private set; }

	public TextSpan Span { get; init; }

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

	public SyntaxToken(SyntaxTree tree, SyntaxTokenKind kind, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens) : base(tree)
	{
		Kind = kind;
		_leadingTrivia = ConvertToSyntaxTrivia(leadingTriviaTokens).ToImmutableArray();
		_trailingTrivia = ConvertToSyntaxTrivia(trailingTriviaTokens).ToImmutableArray();

		IEnumerable<SyntaxTrivia> ConvertToSyntaxTrivia(IEnumerable<Token> tokens)
		{
			foreach (var token in tokens)
			{
				yield return SyntaxTrivia.Create(token, this);
			}
		}
	}

	public SyntaxTokenKind Kind { get; }

	public IEnumerable<SyntaxTrivia> LeadingTrivia => _leadingTrivia;

	public IEnumerable<SyntaxTrivia> TrailingTrivia => _trailingTrivia;
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
	At,
	Asterisk,

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
	Field,
	Extern,
	Public,
	Return,

	AsKeyword,
	ImportKeyword,
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} [{SimpleTokenKind}] {ToString()}")]
public sealed class SimpleToken : SyntaxToken
{
	private SimpleToken(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens)
		: base(tree, SyntaxTokenKind.Simple, leadingTriviaTokens, trailingTriviaTokens)
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
		bool isMissing = false) => new(tree, leadingTriviaTokens, trailingTriviaTokens)
		{
			Span = span,
			IsKeyword = tokenKind.IsReservedKeyword(),
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
				TokenKind.At => SimpleTokenKind.At,
				TokenKind.AtBeforeIdentifier => SimpleTokenKind.At,
				TokenKind.Asterisk => SimpleTokenKind.Asterisk,

				TokenKind.Fn => SimpleTokenKind.Fn,
				TokenKind.Struct => SimpleTokenKind.Struct,
				TokenKind.Class => SimpleTokenKind.Class,
				TokenKind.Alias => SimpleTokenKind.Alias,
				TokenKind.New => SimpleTokenKind.New,
				TokenKind.This => SimpleTokenKind.This,
				TokenKind.True => SimpleTokenKind.True,
				TokenKind.False => SimpleTokenKind.False,
				TokenKind.Var => SimpleTokenKind.Var,
				TokenKind.Const => SimpleTokenKind.Const,
				TokenKind.Return => SimpleTokenKind.Return,

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
		bool isMissing = false) => new(tree, leadingTriviaTokens, trailingTriviaTokens)
		{
			Span = span,
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
			SimpleTokenKind.LeftParen => "(",
			SimpleTokenKind.RightParen => ")",
			SimpleTokenKind.LeftBrace => "{",
			SimpleTokenKind.RightBrace => "}",
			SimpleTokenKind.Comma => ",",
			SimpleTokenKind.Dot => ".",
			SimpleTokenKind.Semicolon => ";",
			SimpleTokenKind.Colon => ":",
			SimpleTokenKind.Question => "?",
			SimpleTokenKind.Equal => "=",
			SimpleTokenKind.At => "@",
			SimpleTokenKind.Asterisk => "*",

			SimpleTokenKind.Fn => "fn",
			SimpleTokenKind.Struct => "struct",
			SimpleTokenKind.Class => "class",
			SimpleTokenKind.Alias => "alias",
			SimpleTokenKind.New => "new",
			SimpleTokenKind.This => "this",
			SimpleTokenKind.True => "true",
			SimpleTokenKind.False => "false",
			SimpleTokenKind.Var => "let",
			SimpleTokenKind.Const => "const",
			SimpleTokenKind.Return => "return",

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
	private UnaryOperatorToken(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens) : base(tree, SyntaxTokenKind.UnaryOperator, leadingTriviaTokens, trailingTriviaTokens)
	{
	}

	public UnaryOperatorTokenKind UnaryOperatorTokenKind { get; init; }

	public static UnaryOperatorToken Create(
		SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens,
		Token token) => new(tree, leadingTriviaTokens, trailingTriviaTokens)
		{
			Span = token.Span,
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
	private BinaryOperatorToken(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens) : base(tree, SyntaxTokenKind.BinaryOperator, leadingTriviaTokens, trailingTriviaTokens)
	{
	}

	public BinaryOperatorTokenKind BinaryOperatorTokenKind { get; init; }

	public static BinaryOperatorToken Create(
		SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens,
		Token token) => new(tree, leadingTriviaTokens, trailingTriviaTokens)
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

	public override void Accept(SyntaxTreeVisitor visitor)
	{
		visitor.VisitBinaryOperatorToken(this);
	}
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
public sealed class IdentifierToken : SyntaxToken
{
	private IdentifierToken(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, TextSpan span, string value, bool isMissing)
		: base(tree, SyntaxTokenKind.Identifier, leadingTriviaTokens, trailingTriviaTokens)
	{
		Span = span;
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

	public override string ToString() => Value;

	public bool DeclaresSymbol()
	{
		return (Parent?.DeclaresSymbol() ?? false) || Parent is FnSigNode;
	}

	public SyntaxNode? GetParentNodeThatDeclaresSymbol()
	{
		if (Parent?.DeclaresSymbol() ?? false)
		{
			return Parent;
		}

		if (Parent is FnSigNode sig)
		{
			return sig.Parent!; // fn or method
		}

		return null;
	}
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
	public LiteralToken(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, TextSpan span, LiteralTokenKind literalTokenKind, object? data)
		: base(tree, SyntaxTokenKind.Literal, leadingTriviaTokens, trailingTriviaTokens)
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
	public SkippedToken(SyntaxTree tree, List<Token> leadingTriviaTokens, List<Token> trailingTriviaTokens, Token token)
		: base(tree, SyntaxTokenKind.Skipped, leadingTriviaTokens, trailingTriviaTokens)
	{
		Span = token.Span;
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
		return Token.Tree.SourceFile.Text[Span.File.GetRange()];
	}
}

public interface ISyntaxNodeHasMainIdentifier
{
	IdentifierToken Identifier { get; }
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

	public IEnumerable<SyntaxToken> ChildTokens
	{
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

	public IEnumerable<SyntaxNode> ChildNodes
	{
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
		return Tree.SourceFile.Text[Span.File.GetRange()];
	}

	/// <summary>
	/// Returns whether or not this node declares a symbol. i.e. Calling <see cref="Reasoner.GetDeclaredSymbol(SyntaxNode)"/> on this should never return `null`.
	/// </summary>
	public bool DeclaresSymbol()
	{
		return this is DeclNode or VarStmtNode or ParamNode;
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

public partial class ModuleNode
{
	public IEnumerable<ImportNode> Imports => ImportsList.Imports;

	public IEnumerable<DeclNode> Decls => DeclsList.Decls;
}

public partial class ImportNode
{
	public string WellFormedModulePath
	{
		get
		{
			var succeeded = ((LiteralExprNode)ModulePath).Literal.TryGetStringValue(out var value);
			Debug.Assert(succeeded);
			return value!;
		}
	}
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
	public bool IsInitializer => !Sig.Identifier.IsMissing && Sig.Identifier.Value == string.Empty;

	/// <summary>
	/// Gets whether the method is an implementation of an operator.
	/// </summary>
	public bool IsMethodOp => TokenHelper.TryGetMethodOp(Sig.Identifier.Value, out _);
}

public partial class BareExprStmtNode
{
	private bool? _isTreatedAsStmt;

	public bool IsTreatedAsStmt
	{
		get
		{
			if (_isTreatedAsStmt.HasValue)
				return _isTreatedAsStmt.Value;

			_isTreatedAsStmt = Expr is BlockExprNode childBlock && !childBlock.ReturnsExpr;
			return _isTreatedAsStmt.Value;
		}
	}
}

public partial class BlockExprNode
{
	private bool? _returnsExpr;

	public bool ReturnsExpr
	{
		get
		{
			if (_returnsExpr.HasValue)
				return _returnsExpr.Value;

			_returnsExpr = Stmts.Any() && Stmts.Last() is BareExprStmtNode bareExprStmt && !bareExprStmt.IsTreatedAsStmt;
			return _returnsExpr.Value;
		}
	}
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
	public IEnumerable<DeclNode> Members => DeclsList.Decls;
}

public partial class ClassDeclNode
{
	public IEnumerable<DeclNode> Members => DeclsList.Decls;
}

public partial class CallExprNode
{
	public IEnumerable<ArgNode> Args => ArgsList.Args;
}

public partial class MapInitializerNode
{
	public IEnumerable<AssignmentExprNode> WellFormedExprs => Items.Select(x => x.Expr).Cast<AssignmentExprNode>();
}

public static class AttrsListNodeExtensions
{
	// Extension instead of a method in a partial to be able to accept a nullable AttrsListNode.
	public static bool HasAttr(this AttrsListNode? @this, string attrName)
	{
		return @this?.Attrs.Any(x => x.Identifier.Value == attrName) ?? false;
	}
}

public static class SyntaxNodeExtensions
{
	public static IdentifierToken? GetMainIdentifierToken(this SyntaxNode? node)
	{
		return node switch
		{
			ISyntaxNodeHasMainIdentifier n => n.Identifier,
			FnDeclNode n => GetMainIdentifierToken(n.Sig),
			MethodDeclNode n => GetMainIdentifierToken(n.Sig),
			_ => null,
		};
	}
}
