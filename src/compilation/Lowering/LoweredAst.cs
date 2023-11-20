using System.Collections.Immutable;
using System.Diagnostics;
using Surab.Analysis;

namespace Surab.Compilation;

internal class LoweredTree(LoweredNode root)
{
    public LoweredNode Root { get; } = root;
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\}")]
internal abstract class LoweredNode(SyntaxNode parsedNode)
{
    public SyntaxNode ParsedNode { get; } = parsedNode;

    public abstract IEnumerable<LoweredNode> EnumerateChildren();
}

internal sealed class LoweredModule(
    SyntaxNode parsedNode,
    IEnumerable<LoweredDecl> decls) : LoweredNode(parsedNode)
{
    public ImmutableArray<LoweredDecl> Decls { get; } = decls.ToImmutableArray();

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        foreach (var child in Decls)
        {
            yield return child;
        }
    }
}

internal abstract class LoweredDecl(SyntaxNode parsedNode) : LoweredNode(parsedNode)
{
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
        if (Body == null)
        {
            yield break;
        }
        yield return Body;
    }
}

internal sealed class LoweredTypeDecl(
    SyntaxNode parsedNode,
    TypeSymbol typeSymbol,
    ImmutableArray<FieldSymbol> members) : LoweredDecl(parsedNode)
{
    public TypeSymbol TypeSymbol { get; } = typeSymbol;

    // Only includes data members. Behavior (methods) will be their own functions.
    public ImmutableArray<FieldSymbol> Members { get; } = members;

    public override IEnumerable<LoweredNode> EnumerateChildren() => [];
}

internal abstract class LoweredStmt(SyntaxNode parsedNode) : LoweredNode(parsedNode)
{
}

// Lowered from local variables, ?
// Only declares the variable. (i.e `var a: Type;`)
[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
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
            yield return Expr;
    }
}

internal sealed class LoweredExprStmt(
    SyntaxNode parsedNode,
    LoweredExpr expr) : LoweredStmt(parsedNode)
{
    public LoweredExpr Expr { get; } = expr;

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        yield return Expr;
    }
}

//internal class LoweredGotoStmt : LoweredStmt
//{
//}

//internal class LoweredConditionalGotoStmt : LoweredStmt
//{
//}

internal sealed class LoweredStmtList(
    SyntaxNode parsedNode,
    IEnumerable<LoweredStmt> stmts) : LoweredStmt(parsedNode)
{
    public ImmutableArray<LoweredStmt> Stmts { get; } = stmts.ToImmutableArray();

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        foreach (var child in Stmts)
        {
            yield return child;
        }

    }
}

internal abstract class LoweredExpr(SyntaxNode parsedNode) : LoweredNode(parsedNode)
{
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
internal sealed class LoweredVariableLoadExpr(
    SyntaxNode parsedNode,
    Symbol variable) : LoweredExpr(parsedNode)
{
    public Symbol Variable { get; } = variable;

    public override IEnumerable<LoweredNode> EnumerateChildren() => [];

    public override string ToString() => $"{Variable.Name}";
}

//internal sealed class LoweredFieldLoadExpr : LoweredExpr
//{
//    public LoweredFieldLoadExpr(
//        SyntaxNode parsedNode,
//        Symbol instance,
//        Symbol field)
//        : base(parsedNode)
//    {
//        Instance = instance;
//        Field = field;
//    }

//    public Symbol Instance { get; }

//    public Symbol Field { get; }

//    public override IEnumerable<LoweredNode> EnumerateChildren() => [];
//}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
internal sealed class LoweredVariableStoreStmt(
    SyntaxNode parsedNode,
    Symbol variable,
    LoweredExpr expr) : LoweredStmt(parsedNode)
{
    public Symbol Variable { get; } = variable;

    public LoweredExpr Expr { get; } = expr;

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        yield return Expr;
    }

    public override string ToString() => $"{Variable.Name} = ...;";
}

//internal sealed class LoweredFieldStoreStmt : LoweredStmt
//{
//    public LoweredFieldStoreStmt(
//        SyntaxNode parsedNode,
//        Symbol instance,
//        Symbol field,
//        LoweredExpr expr)
//        : base(parsedNode)
//    {
//        Instance = instance;
//        Field = field;
//        Expr = expr;
//    }

//    public Symbol Instance { get; }

//    public Symbol Field { get; }

//    public LoweredExpr Expr { get; }

//    public override IEnumerable<LoweredNode> EnumerateChildren()
//    {
//        yield return Expr;
//    }
//}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
internal sealed class LoweredUnaryExpr(
    SyntaxNode parsedNode,
    LoweredExpr expr) : LoweredExpr(parsedNode)
{
    public new UnaryExprNode ParsedNode => (UnaryExprNode)base.ParsedNode;

    public LoweredExpr Expr { get; } = expr;

    public UnaryOperatorTokenKind Op => ParsedNode.Op.UnaryOperatorTokenKind;

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        yield return Expr;
    }

    public override string ToString() => Op.ToString();
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
internal sealed class LoweredBinaryExpr(
    SyntaxNode parsedNode,
    LoweredExpr left,
    LoweredExpr right) : LoweredExpr(parsedNode)
{
    public new BinaryExprNode ParsedNode => (BinaryExprNode)base.ParsedNode;

    public LoweredExpr Left { get; set; } = left;

    public LoweredExpr Right { get; set; } = right;

    public BinaryOperatorTokenKind Op => ParsedNode.Op.BinaryOperatorTokenKind;

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        yield return Left;
        yield return Right;
    }

    public override string ToString() => Op.ToString();
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
internal sealed class LoweredCallExpr(
    SyntaxNode parsedNode,
    FnSymbol callee,
    IEnumerable<LoweredExpr> args) : LoweredExpr(parsedNode)
{
    public FnSymbol Callee { get; set; } = callee;

    public ImmutableArray<LoweredExpr> Args { get; } = args.ToImmutableArray();

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        foreach (var child in Args)
        {
            yield return child;
        }
    }

    public override string ToString() => $"{Callee.Name}(...)";
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
internal sealed class LoweredLiteralExpr(
    SyntaxNode parsedNode,
    LiteralToken literal) : LoweredExpr(parsedNode)
{
    public LiteralToken Literal { get; } = literal;

    public override IEnumerable<LoweredNode> EnumerateChildren() => Enumerable.Empty<LoweredNode>();

    public override string ToString() => $"{Literal}";
}
