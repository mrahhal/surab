using System.Collections.Immutable;
using System.Diagnostics;
using Surab.Analysis;

namespace Surab.Compilation;

internal class LoweredTree(LoweredNode root)
{
	public LoweredNode Root { get; } = root;
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
internal abstract class LoweredNode(SyntaxNode parsedNode)
{
	public SyntaxNode ParsedNode { get; } = parsedNode;

	public abstract IEnumerable<LoweredNode> EnumerateChildren();

	// No ancestor methods because we don't store the parent right now..

	/// <summary>
	/// Enumerates descendant nodes in breadth first order.
	/// </summary>
	public IEnumerable<LoweredNode> DescendantNodes() => DescendantNodesCore(includeSelf: false);

	/// <summary>
	/// Enumerates descendant nodes and self in breadth first order.
	/// </summary>
	public IEnumerable<LoweredNode> DescendantNodesAndSelf() => DescendantNodesCore(includeSelf: true);

	private IEnumerable<LoweredNode> DescendantNodesCore(bool includeSelf)
	{
		if (includeSelf)
		{
			yield return this;
		}

		foreach (var child in EnumerateChildren())
		{
			yield return child;
		}

		foreach (var child in EnumerateChildren())
		{
			foreach (var item in child.DescendantNodes())
			{
				yield return item;
			}
		}
	}
}

internal sealed class LoweredFile(
	SyntaxNode parsedNode,
	IEnumerable<LoweredDecl> decls) : LoweredNode(parsedNode)
{
	public ImmutableArray<LoweredDecl> Decls { get; } = decls.ToImmutableArray();

	public override IEnumerable<LoweredNode> EnumerateChildren() => [.. Decls];
}

internal abstract class LoweredDecl(SyntaxNode parsedNode) : LoweredNode(parsedNode)
{
}

/// <summary>
/// Represents an elided decl. The parsed node will also be null so this whole elided node shouldn't be used.
/// </summary>
internal sealed class LoweredElidedDecl : LoweredDecl
{
	private LoweredElidedDecl() : base(null!)
	{
	}

	public static LoweredElidedDecl Instance { get; } = new();

	public override IEnumerable<LoweredNode> EnumerateChildren() => [];
}

// Used when hoisting the different decls.
internal sealed class LoweredDeclList(
	SyntaxNode parsedNode,
	IEnumerable<LoweredDecl> decls) : LoweredDecl(parsedNode)
{
	public ImmutableArray<LoweredDecl> Decls { get; } = decls.ToImmutableArray();

	public override IEnumerable<LoweredNode> EnumerateChildren() => [.. Decls];
}

// Lowered from both functions and methods.
internal sealed class LoweredFnDecl(
	SyntaxNode parsedNode,
	FnSymbol fnSymbol,
	IEnumerable<GenericParamTypeSymbol> genericParams,
	LoweredStmtList? body) : LoweredDecl(parsedNode)
{
	public FnSymbol FnSymbol { get; } = fnSymbol;

	public ImmutableArray<GenericParamTypeSymbol> GenericParams { get; } = genericParams.ToImmutableArray();

	public LoweredStmtList? Body { get; } = body;

	public override IEnumerable<LoweredNode> EnumerateChildren()
	{
		if (Body != null)
		{
			yield return Body;
		}
	}

	public override string ToString()
	{
		var fnType = FnSymbol.Type;
		var returnTypeText = fnType.ReturnType == null ? "" : "...";
		var bodyText = Body == null ? ";" : " { ... }";
		return $"fn {FnSymbol.Name} (...) {returnTypeText}{bodyText}";
	}
}

internal sealed class LoweredTypeDecl(
	SyntaxNode parsedNode,
	TypeSymbol typeSymbol,
	IEnumerable<GenericParamTypeSymbol> genericParams,
	IEnumerable<FieldSymbol> fields) : LoweredDecl(parsedNode)
{
	public TypeSymbol TypeSymbol { get; } = typeSymbol;

	public ImmutableArray<GenericParamTypeSymbol> GenericParams { get; } = genericParams.ToImmutableArray();

	// Only includes data members. Behavior (methods) will be their own lowered functions (hoisted).
	public ImmutableArray<FieldSymbol> Fields { get; } = fields.ToImmutableArray();

	public override IEnumerable<LoweredNode> EnumerateChildren() => [];

	public override string ToString() => $"type decl: {TypeSymbol.Name}";
}

internal abstract class LoweredStmt(SyntaxNode parsedNode) : LoweredNode(parsedNode)
{
}

/// <summary>
/// Represents an elided stmt. The parsed node will also be null so this whole elided node shouldn't be used.
/// </summary>
internal sealed class LoweredElidedStmt : LoweredStmt
{
	private LoweredElidedStmt() : base(null!)
	{
	}

	public static LoweredElidedStmt Instance { get; } = new();

	public override IEnumerable<LoweredNode> EnumerateChildren() => [];
}

internal sealed class LoweredStmtList(
	SyntaxNode parsedNode,
	IEnumerable<LoweredStmt> stmts) : LoweredStmt(parsedNode)
{
	public ImmutableArray<LoweredStmt> Stmts { get; } = stmts.ToImmutableArray();

	public override IEnumerable<LoweredNode> EnumerateChildren() => [.. Stmts];
}

// Lowered from local variables, ?
// Only declares the variable. (i.e. `var a: Type;`)
internal sealed class LoweredVariableDeclareStmt(
	SyntaxNode parsedNode,
	LocalSymbol symbol) : LoweredStmt(parsedNode)
{
	public LocalSymbol Symbol { get; } = symbol;

	public override IEnumerable<LoweredNode> EnumerateChildren() => [];

	public override string ToString() => $"var {Symbol.Name}: {Symbol.Type.Name};";
}

internal sealed class LoweredReturnStmt(
	SyntaxNode parsedNode,
	LoweredExpr? expr) : LoweredStmt(parsedNode)
{
	public LoweredExpr? Expr { get; } = expr;

	public override IEnumerable<LoweredNode> EnumerateChildren()
	{
		if (Expr != null)
		{
			yield return Expr;
		}
	}

	public override string ToString() => $"return{(Expr == null ? "" : " " + Expr)};";
}

internal sealed class LoweredExprStmt(
	SyntaxNode parsedNode,
	LoweredExpr expr) : LoweredStmt(parsedNode)
{
	public LoweredExpr Expr { get; } = expr;

	public override IEnumerable<LoweredNode> EnumerateChildren() => [Expr];

	public override string ToString() => $"{Expr};";
}

internal sealed class LoweredObjectAllocStmt(
	SyntaxNode parsedNode,
	TypeSymbol type,
	TempSymbol tempSymbol,
	IEnumerable<TypeSymbol> genericArgs) : LoweredStmt(parsedNode)
{
	public TypeSymbol Type { get; } = type;

	public TempSymbol TempSymbol { get; } = tempSymbol;

	public ImmutableArray<TypeSymbol> GenericArgs { get; } = genericArgs.ToImmutableArray();

	public override IEnumerable<LoweredNode> EnumerateChildren() => [];

	public override string ToString() => $"alloca {Type.Name};";
}

//internal class LoweredGotoStmt : LoweredStmt
//{
//}

//internal class LoweredConditionalGotoStmt : LoweredStmt
//{
//}

internal abstract class LoweredExpr(SyntaxNode parsedNode, TypeSymbol type) : LoweredNode(parsedNode)
{
	public TypeSymbol Type { get; } = type;
}

internal sealed class LoweredStmtListEndingWithExpr(
	SyntaxNode parsedNode,
	IEnumerable<LoweredStmt> stmts,
	LoweredExpr endingExpr) : LoweredExpr(parsedNode, endingExpr.Type)
{
	public ImmutableArray<LoweredStmt> Stmts { get; } = stmts.ToImmutableArray();

	public LoweredExpr EndingExpr { get; } = endingExpr;

	public override IEnumerable<LoweredNode> EnumerateChildren() => [.. Stmts, EndingExpr];

	public override string ToString() => $"{Stmts.Length} stmts followed by ending expr: {EndingExpr}";
}

internal sealed class LoweredValueLoadExpr(
	SyntaxNode parsedNode,
	TypeSymbol type,
	Symbol value) : LoweredExpr(parsedNode, type)
{
	public Symbol Value { get; } = value;

	public override IEnumerable<LoweredNode> EnumerateChildren() => [];

	public override string ToString() => $"{Value.Name}";
}

internal sealed class LoweredValueStoreStmt(
	SyntaxNode parsedNode,
	Symbol value,
	LoweredExpr expr) : LoweredStmt(parsedNode)
{
	public Symbol Value { get; } = value;

	public LoweredExpr Expr { get; } = expr;

	public override IEnumerable<LoweredNode> EnumerateChildren() => [Expr];

	public override string ToString() => $"{Value.Name} = {Expr};";
}

internal sealed class LoweredFieldLoadExpr(
	SyntaxNode parsedNode,
	TypeSymbol type,
	LoweredExpr target,
	FieldSymbol field) : LoweredExpr(parsedNode, type)
{
	public LoweredExpr Target { get; } = target;

	public FieldSymbol Field { get; } = field;

	public override IEnumerable<LoweredNode> EnumerateChildren() => [];

	public override string ToString() => $"{Field.Name}";
}

internal sealed class LoweredFieldStoreStmt(
	SyntaxNode parsedNode,
	LoweredExpr target,
	FieldSymbol field,
	LoweredExpr expr) : LoweredStmt(parsedNode)
{
	public LoweredExpr Target { get; } = target;

	public FieldSymbol Field { get; } = field;

	public LoweredExpr Expr { get; } = expr;

	public override IEnumerable<LoweredNode> EnumerateChildren() => [Expr];

	public override string ToString() => $"{Field.Name} = {Expr};";
}

internal sealed class LoweredThisLoadExpr(
	SyntaxNode parsedNode,
	TypeSymbol type) : LoweredExpr(parsedNode, type)
{
	public override IEnumerable<LoweredNode> EnumerateChildren() => [];
}

internal sealed class LoweredUnaryExpr(
	SyntaxNode parsedNode,
	TypeSymbol type,
	LoweredExpr expr,
	UnaryOperatorTokenKind op) : LoweredExpr(parsedNode, type)
{
	public LoweredExpr Expr { get; } = expr;

	public UnaryOperatorTokenKind Op { get; } = op;

	public override IEnumerable<LoweredNode> EnumerateChildren() => [Expr];

	public override string ToString() => $"{Op}";
}

internal sealed class LoweredBinaryExpr(
	SyntaxNode parsedNode,
	TypeSymbol type,
	LoweredExpr left,
	LoweredExpr right) : LoweredExpr(parsedNode, type)
{
	public new BinaryExprNode ParsedNode => (BinaryExprNode)base.ParsedNode;

	public LoweredExpr Left { get; set; } = left;

	public LoweredExpr Right { get; set; } = right;

	public BinaryOperatorTokenKind Op => ParsedNode.Op.BinaryOperatorTokenKind;

	public override IEnumerable<LoweredNode> EnumerateChildren() => [Left, Right];

	public override string ToString() => $"{Op}";
}

internal sealed class LoweredCallExpr(
	SyntaxNode parsedNode,
	FnTypeSymbol fnType,
	LoweredExpr callee,
	IEnumerable<TypeSymbol> genericArgs,
	IEnumerable<LoweredExpr> args) : LoweredExpr(parsedNode, fnType.ReturnType)
{
	public FnTypeSymbol FnType { get; set; } = fnType;

	public LoweredExpr Callee { get; set; } = callee;

	public ImmutableArray<TypeSymbol> GenericArgs { get; } = genericArgs.ToImmutableArray();

	public ImmutableArray<LoweredExpr> Args { get; } = args.ToImmutableArray();

	public override IEnumerable<LoweredNode> EnumerateChildren() => [.. Args];

	public override string ToString() => $"{Callee}(...)";
}

internal sealed class LoweredLiteralExpr(
	SyntaxNode parsedNode,
	TypeSymbol type,
	LiteralData data) : LoweredExpr(parsedNode, type)
{
	public LiteralData Data { get; } = data;

	public override IEnumerable<LoweredNode> EnumerateChildren() => [];

	public override string ToString() => "lit";
}

public enum LiteralDataKind
{
	NumericInteger,
	NumericFloat,
	Char,
	String,
	Bool,
}

internal sealed class LiteralData
{
	private LiteralData(LiteralDataKind kind, object? data)
	{
		Kind = kind;
		Data = data;
	}

	public LiteralDataKind Kind { get; init; }

	public object? Data { get; init; }

	public static LiteralData CreateNumericInteger(ulong data)
	{
		return new LiteralData(LiteralDataKind.NumericInteger, data);
	}

	public static LiteralData CreateNumericFloat(double data)
	{
		return new LiteralData(LiteralDataKind.NumericFloat, data);
	}

	public static LiteralData CreateChar(char data)
	{
		return new LiteralData(LiteralDataKind.Char, data);
	}

	public static LiteralData CreateString(string data)
	{
		return new LiteralData(LiteralDataKind.String, data);
	}

	public static LiteralData CreateBool(bool data)
	{
		return new LiteralData(LiteralDataKind.Bool, data);
	}

	public static LiteralData FromLiteralToken(LiteralToken token)
	{
		var kind = token.LiteralTokenKind switch
		{
			LiteralTokenKind.NumericInteger => LiteralDataKind.NumericInteger,
			LiteralTokenKind.NumericFloat => LiteralDataKind.NumericFloat,
			LiteralTokenKind.Char => LiteralDataKind.Char,
			LiteralTokenKind.String => LiteralDataKind.String,
			LiteralTokenKind.Bool => LiteralDataKind.Bool,
			_ => throw new UnreachableException(),
		};
		var data = token.Data;
		return new LiteralData(kind, data);
	}

	public ulong GetNumericValueAsInteger()
	{
		return Kind switch
		{
			LiteralDataKind.NumericInteger => (ulong)Data!,
			_ => throw new InvalidOperationException(),
		};
	}

	public double GetNumericValueAsFloat()
	{
		return Kind switch
		{
			LiteralDataKind.NumericInteger => (ulong)Data!,
			LiteralDataKind.NumericFloat => (double)Data!,
			_ => throw new InvalidOperationException(),
		};
	}

	public bool TryGetNumericIntegerValue(out ulong value)
	{
		value = default!;
		if (Kind != LiteralDataKind.NumericInteger)
		{
			return false;
		}
		value = (ulong)Data!;
		return true;
	}

	public bool TryGetNumericFloatValue(out double value)
	{
		value = default!;
		if (Kind != LiteralDataKind.NumericFloat)
		{
			return false;
		}
		value = (double)Data!;
		return true;
	}

	public bool TryGetCharValue(out char? value)
	{
		value = default;
		if (Kind != LiteralDataKind.Char)
		{
			return false;
		}
		value = (char?)Data;
		return true;
	}

	public bool TryGetStringValue(out string? value)
	{
		value = default;
		if (Kind != LiteralDataKind.String)
		{
			return false;
		}
		value = (string?)Data;
		return true;
	}

	public bool TryGetBoolValue(out bool value)
	{
		value = default;
		if (Kind != LiteralDataKind.Bool)
		{
			return false;
		}
		value = (bool)Data!;
		return true;
	}
}

internal static class LoweredNodeExtensions
{
	/// <summary>
	/// Skips container nodes and returns an enumeration of the non container children:
	/// <see cref="LoweredDeclList"/>, <see cref="LoweredStmtList"/>
	/// </summary>
	public static IEnumerable<LoweredNode> UnwrapContainers(this LoweredNode node, bool includeSelf = false)
	{
		Debug.Assert(node != null);

		if (includeSelf && node is not LoweredDeclList and not LoweredStmtList)
		{
			yield return node;
			yield break;
		}

		foreach (var child in node.EnumerateChildren())
		{
			foreach (var c in child.UnwrapContainers(includeSelf: true))
			{
				yield return c;
			}
		}
	}
}
