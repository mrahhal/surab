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
			if (child != null)
			{
				yield return child;
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

internal sealed class LoweredElidedDecl(SyntaxNode parsedNode) : LoweredDecl(parsedNode)
{
	public override IEnumerable<LoweredNode> EnumerateChildren() => [];
}

// Lowered from both functions and methods.
internal sealed class LoweredFnDecl(
	SyntaxNode parsedNode,
	FnSymbol fnSymbol,
	LoweredStmtList? body) : LoweredDecl(parsedNode)
{
	public FnSymbol FnSymbol { get; } = fnSymbol;

	public LoweredStmtList? Body { get; } = body;

	public override IEnumerable<LoweredNode> EnumerateChildren()
	{
		if (Body != null)
		{
			yield return Body;
		}
	}

	public override string ToString() => $"fn {FnSymbol.Name} (...){FnSymbol.ReturnType.Name}{(Body == null ? ";" : " { ... }")}";
}

internal sealed class LoweredTypeDecl(
	SyntaxNode parsedNode,
	TypeSymbol typeSymbol,
	IEnumerable<FieldSymbol> fields,
	IEnumerable<LoweredDecl> decls) : LoweredDecl(parsedNode)
{
	public TypeSymbol TypeSymbol { get; } = typeSymbol;

	// Only includes data members. Behavior (methods) will be their own functions.
	public ImmutableArray<FieldSymbol> Fields { get; } = fields.ToImmutableArray();

	public ImmutableArray<LoweredDecl> Decls { get; } = decls.ToImmutableArray();

	public override IEnumerable<LoweredNode> EnumerateChildren() => [.. Decls];

	public override string ToString() => $"type decl: {TypeSymbol.Name}";
}

internal abstract class LoweredStmt(SyntaxNode parsedNode) : LoweredNode(parsedNode)
{
}

internal sealed class LoweredStmtList(
	SyntaxNode parsedNode,
	IEnumerable<LoweredStmt> stmts) : LoweredStmt(parsedNode)
{
	public ImmutableArray<LoweredStmt> Stmts { get; } = stmts.ToImmutableArray();

	public override IEnumerable<LoweredNode> EnumerateChildren() => [.. Stmts];
}

// Lowered from local variables, ?
// Only declares the variable. (i.e `var a: Type;`)
internal sealed class LoweredVariableStmt(
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
	TempSymbol tempSymbol) : LoweredStmt(parsedNode)
{
	public TypeSymbol Type { get; } = type;

	public TempSymbol TempSymbol { get; } = tempSymbol;

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

internal sealed class LoweredVariableLoadExpr(
	SyntaxNode parsedNode,
	TypeSymbol type,
	Symbol variable) : LoweredExpr(parsedNode, type)
{
	public Symbol Variable { get; } = variable;

	public override IEnumerable<LoweredNode> EnumerateChildren() => [];

	public override string ToString() => $"{Variable.Name}";
}

internal sealed class LoweredVariableStoreStmt(
	SyntaxNode parsedNode,
	Symbol variable,
	LoweredExpr expr) : LoweredStmt(parsedNode)
{
	public Symbol Variable { get; } = variable;

	public LoweredExpr Expr { get; } = expr;

	public override IEnumerable<LoweredNode> EnumerateChildren() => [Expr];

	public override string ToString() => $"{Variable.Name} = {Expr};";
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
	IEnumerable<LoweredExpr> args) : LoweredExpr(parsedNode, fnType.ReturnType)
{
	public LoweredExpr Callee { get; set; } = callee;

	public FnTypeSymbol FnType { get; set; } = fnType;

	public ImmutableArray<LoweredExpr> Args { get; } = args.ToImmutableArray();

	public override IEnumerable<LoweredNode> EnumerateChildren() => [.. Args];

	public override string ToString() => $"{Callee}(...)";
}

internal sealed class LoweredLiteralExpr(
	SyntaxNode parsedNode,
	TypeSymbol type,
	LiteralToken literal) : LoweredExpr(parsedNode, type)
{
	public LiteralToken Literal { get; } = literal;

	public override IEnumerable<LoweredNode> EnumerateChildren() => [];

	public override string ToString() => $"{Literal}";
}

internal sealed class LoweredSymbolLoadExpr(
	SyntaxNode node,
	TypeSymbol type,
	Symbol symbol) : LoweredExpr(node, type)
{
	public Symbol Symbol { get; } = symbol;

	public override IEnumerable<LoweredNode> EnumerateChildren() => [];
}
