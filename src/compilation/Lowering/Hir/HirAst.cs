using System.Collections.Immutable;
using System.Diagnostics;
using Surab.Analysis;

namespace Surab.Compilation;

internal class HirTree(SyntaxTree syntaxTree, HirNode root)
{
	public SyntaxTree SyntaxTree { get; } = syntaxTree;

	public HirNode Root { get; } = root;
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
internal abstract class HirNode(SyntaxNode syntaxNode)
{
	public SyntaxNode SyntaxNode { get; } = syntaxNode;

	public abstract IEnumerable<HirNode> EnumerateChildren();

	public abstract IEnumerable<HirValue> EnumerateValues();

	public abstract IEnumerable<HirType> EnumerateTypes();

	// No ancestor methods because we don't store the parent right now..

	/// <summary>
	/// Enumerates descendant nodes in breadth first order.
	/// </summary>
	public IEnumerable<HirNode> DescendantNodes() => DescendantNodesCore(includeSelf: false);

	/// <summary>
	/// Enumerates descendant nodes and self in breadth first order.
	/// </summary>
	public IEnumerable<HirNode> DescendantNodesAndSelf() => DescendantNodesCore(includeSelf: true);

	private IEnumerable<HirNode> DescendantNodesCore(bool includeSelf)
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

internal sealed class HirFile(
	SyntaxNode syntaxNode,
	IEnumerable<HirDecl> decls) : HirNode(syntaxNode)
{
	public ImmutableArray<HirDecl> Decls { get; } = decls.ToImmutableArray();

	public override IEnumerable<HirNode> EnumerateChildren() => [.. Decls];

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [];
}

internal abstract class HirDecl(SyntaxNode syntaxNode) : HirNode(syntaxNode)
{
}

/// <summary>
/// Represents an elided decl. The parsed node will also be null so this whole elided node shouldn't be used.
/// </summary>
internal sealed class HirElidedDecl : HirDecl
{
	private HirElidedDecl() : base(null!)
	{
	}

	public static HirElidedDecl Instance { get; } = new();

	public override IEnumerable<HirNode> EnumerateChildren() => [];

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [];
}

// Used when hoisting the different decls.
internal sealed class HirDeclList(
	SyntaxNode syntaxNode,
	IEnumerable<HirDecl> decls) : HirDecl(syntaxNode)
{
	public ImmutableArray<HirDecl> Decls { get; } = decls.ToImmutableArray();

	public override IEnumerable<HirNode> EnumerateChildren() => [.. Decls];

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [];
}

// Lowered from both functions and methods.
internal sealed class HirFnDecl(
	SyntaxNode syntaxNode,
	HirFnValue fnValue,
	HirFnBody? body) : HirDecl(syntaxNode)
{
	public HirFnValue FnValue { get; } = fnValue;

	public HirFnBody? Body { get; } = body;

	public override IEnumerable<HirNode> EnumerateChildren()
	{
		if (Body != null)
		{
			yield return Body;
		}
	}

	public override IEnumerable<HirValue> EnumerateValues() => [FnValue];

	public override IEnumerable<HirType> EnumerateTypes() => [];

	public override string ToString()
	{
		var returnTypeText = FnValue.Type.ReturnType == null ? "" : "...";
		var bodyText = Body == null ? ";" : " { ... }";
		return $"fn {FnValue.Name} (...) {returnTypeText}{bodyText}";
	}
}

internal sealed class HirFnBody(
	SyntaxNode syntaxNode,
	IEnumerable<HirStmt> stmts) : HirStmt(syntaxNode)
{
	public ImmutableArray<HirStmt> Stmts { get; } = stmts.ToImmutableArray();

	public override IEnumerable<HirNode> EnumerateChildren() => [.. Stmts];

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [];
}

internal sealed class HirStructDecl(
	SyntaxNode syntaxNode,
	HirStructType type) : HirDecl(syntaxNode)
{
	public HirStructType Type { get; } = type;

	public override IEnumerable<HirNode> EnumerateChildren() => [];

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [Type];

	public override string ToString() => $"type decl: {Type.Name}";
}

internal abstract class HirStmt(SyntaxNode syntaxNode) : HirNode(syntaxNode)
{
}

/// <summary>
/// Represents an elided stmt. The parsed node will also be null so this whole elided node shouldn't be used.
/// </summary>
internal sealed class HirElidedStmt : HirStmt
{
	private HirElidedStmt() : base(null!)
	{
	}

	public static HirElidedStmt Instance { get; } = new();

	public override IEnumerable<HirNode> EnumerateChildren() => [];

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [];
}

internal sealed class HirStmtList(
	SyntaxNode syntaxNode,
	IEnumerable<HirStmt> stmts) : HirStmt(syntaxNode)
{
	public ImmutableArray<HirStmt> Stmts { get; } = stmts.ToImmutableArray();

	public override IEnumerable<HirNode> EnumerateChildren() => [.. Stmts];

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [];
}

// Only declares the variable. (i.e. `var a: Type;`)
internal sealed class HirLocalDeclareStmt(
	SyntaxNode syntaxNode,
	HirValue value) : HirStmt(syntaxNode)
{
	public HirValue Value { get; } = value;

	public override IEnumerable<HirNode> EnumerateChildren() => [];

	public override IEnumerable<HirValue> EnumerateValues() => [Value];

	public override IEnumerable<HirType> EnumerateTypes() => [];

	public override string ToString() => $"var {Value.Name}: {Value.Type.Name};";
}

internal sealed class HirReturnStmt(
	SyntaxNode syntaxNode,
	HirExpr? expr) : HirStmt(syntaxNode)
{
	public HirExpr? Expr { get; } = expr;

	public override IEnumerable<HirNode> EnumerateChildren()
	{
		if (Expr != null)
		{
			yield return Expr;
		}
	}

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [];

	public override string ToString() => $"return{(Expr == null ? "" : " " + Expr)};";
}

// Should not pop
internal sealed class HirBlockReturnStmt(
	SyntaxNode syntaxNode,
	HirExpr expr) : HirStmt(syntaxNode)
{
	public HirExpr Expr { get; } = expr;

	public override IEnumerable<HirNode> EnumerateChildren() => [Expr];

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [];

	public override string ToString() => $"block_return {Expr};";
}

// Should pop
internal sealed class HirExprStmt(
	SyntaxNode syntaxNode,
	HirExpr expr) : HirStmt(syntaxNode)
{
	public HirExpr Expr { get; } = expr;

	public override IEnumerable<HirNode> EnumerateChildren() => [Expr];

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [];

	public override string ToString() => $"{Expr};";
}

internal abstract class HirExpr(SyntaxNode syntaxNode, HirType type) : HirNode(syntaxNode)
{
	public HirType Type { get; } = type;
}

internal sealed class HirValueLoadExpr(
	SyntaxNode syntaxNode,
	HirType type,
	HirValue value) : HirExpr(syntaxNode, type)
{
	public HirValue Value { get; } = value;

	public override IEnumerable<HirNode> EnumerateChildren() => [];

	public override IEnumerable<HirValue> EnumerateValues() => [Value];

	public override IEnumerable<HirType> EnumerateTypes() => [];

	public override string ToString() => $"{Value.Name}";
}

internal sealed class HirValueStoreStmt(
	SyntaxNode syntaxNode,
	HirValue value,
	HirExpr expr) : HirStmt(syntaxNode)
{
	public HirValue Value { get; } = value;

	public HirExpr Expr { get; } = expr;

	public override IEnumerable<HirNode> EnumerateChildren() => [Expr];

	public override IEnumerable<HirValue> EnumerateValues() => [Value];

	public override IEnumerable<HirType> EnumerateTypes() => [];

	public override string ToString() => $"{Value.Name} = {Expr};";
}

internal sealed class HirFieldLoadExpr(
	SyntaxNode syntaxNode,
	HirType type,
	HirExpr target,
	HirFieldValue fieldValue) : HirExpr(syntaxNode, type)
{
	public HirExpr Target { get; } = target;

	public HirFieldValue FieldValue { get; } = fieldValue;

	public override IEnumerable<HirNode> EnumerateChildren() => [];

	public override IEnumerable<HirValue> EnumerateValues() => [FieldValue];

	public override IEnumerable<HirType> EnumerateTypes() => [];

	public override string ToString() => $"{FieldValue.Name}";
}

internal sealed class HirFieldStoreStmt(
	SyntaxNode syntaxNode,
	HirExpr target,
	HirFieldValue fieldValue,
	HirExpr expr) : HirStmt(syntaxNode)
{
	public HirExpr Target { get; } = target;

	public HirFieldValue FieldValue { get; } = fieldValue;

	public HirExpr Expr { get; } = expr;

	public override IEnumerable<HirNode> EnumerateChildren() => [Expr];

	public override IEnumerable<HirValue> EnumerateValues() => [FieldValue];

	public override IEnumerable<HirType> EnumerateTypes() => [];

	public override string ToString() => $"{FieldValue.Name} = {Expr};";
}

internal sealed class HirUnaryExpr(
	SyntaxNode syntaxNode,
	HirType type,
	HirExpr expr,
	UnaryOperatorTokenKind op) : HirExpr(syntaxNode, type)
{
	public HirExpr Expr { get; } = expr;

	public UnaryOperatorTokenKind Op { get; } = op;

	public override IEnumerable<HirNode> EnumerateChildren() => [Expr];

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [];

	public override string ToString() => $"{Op}";
}

internal sealed class HirBinaryExpr(
	SyntaxNode syntaxNode,
	HirType type,
	HirExpr left,
	HirExpr right) : HirExpr(syntaxNode, type)
{
	public new BinaryExprNode SyntaxNode => (BinaryExprNode)base.SyntaxNode;

	public HirExpr Left { get; set; } = left;

	public HirExpr Right { get; set; } = right;

	public BinaryOperatorTokenKind Op => SyntaxNode.Op.BinaryOperatorTokenKind;

	public override IEnumerable<HirNode> EnumerateChildren() => [Left, Right];

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [];

	public override string ToString() => $"{Op}";
}

internal sealed class HirCallExpr(
	SyntaxNode syntaxNode,
	HirFnType fnType,
	HirExpr callee,
	IEnumerable<HirExpr> args) : HirExpr(syntaxNode, fnType.ReturnType)
{
	public HirFnType FnType { get; set; } = fnType;

	public HirExpr Callee { get; set; } = callee;

	public ImmutableArray<HirExpr> Args { get; } = args.ToImmutableArray();

	public override IEnumerable<HirNode> EnumerateChildren() => [Callee, .. Args];

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [FnType];

	public override string ToString() => $"{Callee}(...)";
}

internal sealed class HirBlockExpr(
	SyntaxNode syntaxNode,
	HirType type,
	IEnumerable<HirStmt> stmts,
	HirExpr? blockReturnExpr) : HirExpr(syntaxNode, type)
{
	public ImmutableArray<HirStmt> Stmts { get; } = stmts.ToImmutableArray();

	public HirExpr? BlockReturnExpr { get; } = blockReturnExpr;

	public override IEnumerable<HirNode> EnumerateChildren()
	{
		foreach (var stmt in Stmts)
		{
			yield return stmt;
		}
		if (BlockReturnExpr != null)
		{
			yield return BlockReturnExpr;
		}
	}

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [];

	public override string ToString() => "{ ... }";
}

internal sealed class HirMatchExpr(
	SyntaxNode syntaxNode,
	HirType type,
	IEnumerable<HirMatchArm> arms) : HirExpr(syntaxNode, type)
{
	// If this match expr returns something, all arms must return and must be exhaustive.
	// Return type should match this match's return type (which could be void).
	public ImmutableArray<HirMatchArm> Arms { get; } = arms.ToImmutableArray();

	public override IEnumerable<HirNode> EnumerateChildren() => [.. Arms];

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [];

	public override string ToString() => $"match";
}

internal sealed class HirMatchArm(
	SyntaxNode syntaxNode,
	HirExpr? condition,
	HirExpr block) : HirNode(syntaxNode)
{
	public HirExpr? Condition { get; } = condition;

	public HirExpr Block { get; } = block;

	public override IEnumerable<HirNode> EnumerateChildren() => [];

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [];

	public override string ToString() => $"match arm";
}

internal sealed class HirLiteralExpr(
	SyntaxNode syntaxNode,
	HirType type,
	LiteralData data) : HirExpr(syntaxNode, type)
{
	public LiteralData Data { get; } = data;

	public override IEnumerable<HirNode> EnumerateChildren() => [];

	public override IEnumerable<HirValue> EnumerateValues() => [];

	public override IEnumerable<HirType> EnumerateTypes() => [];

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
