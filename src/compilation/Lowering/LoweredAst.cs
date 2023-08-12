using System.Collections.Immutable;
using System.Diagnostics;
using Surab.Analysis;

namespace Surab.Compilation;

internal class LoweredTree
{
    public LoweredTree(LoweredNode root)
    {
        Root = root;
    }

    public LoweredNode Root { get; }
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\}")]
internal abstract class LoweredNode
{
    public LoweredNode(SyntaxNode parsedNode)
    {
        ParsedNode = parsedNode;
    }

    public SyntaxNode ParsedNode { get; }

    public abstract IEnumerable<LoweredNode> EnumerateChildren();
}

internal sealed class LoweredModule : LoweredNode
{
    public LoweredModule(
        SyntaxNode parsedNode,
        IEnumerable<LoweredDecl> decls)
        : base(parsedNode)
    {
        Decls = decls.ToImmutableArray();
    }

    public ImmutableArray<LoweredDecl> Decls { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        foreach (var child in Decls)
        {
            yield return child;
        }
    }
}

internal abstract class LoweredDecl : LoweredNode
{
    public LoweredDecl(SyntaxNode parsedNode) : base(parsedNode)
    {
    }
}

// Lowered from both functions and methods.
internal sealed class LoweredFnDecl : LoweredDecl
{
    public LoweredFnDecl(
        SyntaxNode parsedNode,
        FnSymbol fnSymbol,
        LoweredStmtList body)
        : base(parsedNode)
    {
        FnSymbol = fnSymbol;
        Body = body;
    }

    public FnSymbol FnSymbol { get; }

    public LoweredStmtList Body { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        yield return Body;
    }
}

internal sealed class LoweredTypeDecl : LoweredDecl
{
    public LoweredTypeDecl(
        SyntaxNode parsedNode,
        TypeSymbol typeSymbol,
        ImmutableArray<Symbol> members)
        : base(parsedNode)
    {
        TypeSymbol = typeSymbol;
        Members = members;
    }

    public TypeSymbol TypeSymbol { get; }

    // Only includes data members. Behavior (methods) will be their own functions.
    public ImmutableArray<Symbol> Members { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren() => Enumerable.Empty<LoweredNode>();
}

internal abstract class LoweredStmt : LoweredNode
{
    public LoweredStmt(SyntaxNode parsedNode) : base(parsedNode)
    {
    }
}

// Lowered from local variables, ?
// Only declares the variable. (i.e `let a: Type;`)
[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
internal sealed class LoweredVariableStmt : LoweredStmt
{
    public LoweredVariableStmt(
        SyntaxNode parsedNode,
        LocalSymbol symbol)
        : base(parsedNode)
    {
        Symbol = symbol;
    }

    public LocalSymbol Symbol { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren() => Enumerable.Empty<LoweredNode>();

    public override string ToString() => $"let {Symbol.Name}: {Symbol.Type.Name};";
}

internal sealed class LoweredReturnStmt : LoweredStmt
{
    public LoweredReturnStmt(
        SyntaxNode parsedNode,
        LoweredExpr? expr)
        : base(parsedNode)
    {
        Expr = expr;
    }

    public LoweredExpr? Expr { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        if (Expr != null)
            yield return Expr;
    }
}

internal sealed class LoweredExprStmt : LoweredStmt
{
    public LoweredExprStmt(
        SyntaxNode parsedNode,
        LoweredExpr expr)
        : base(parsedNode)
    {
        Expr = expr;
    }

    public LoweredExpr Expr { get; }

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

internal sealed class LoweredStmtList : LoweredStmt
{
    public LoweredStmtList(
        SyntaxNode parsedNode,
        IEnumerable<LoweredStmt> stmts)
        : base(parsedNode)
    {
        Stmts = stmts.ToImmutableArray();
    }

    public ImmutableArray<LoweredStmt> Stmts { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        foreach (var child in Stmts)
        {
            yield return child;
        }

    }
}

internal abstract class LoweredExpr : LoweredNode
{
    public LoweredExpr(SyntaxNode parsedNode) : base(parsedNode)
    {
    }
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
internal sealed class LoweredVariableLoadExpr : LoweredExpr
{
    public LoweredVariableLoadExpr(
        SyntaxNode parsedNode,
        Symbol variable)
        : base(parsedNode)
    {
        Variable = variable;
    }

    public Symbol Variable { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren() => Enumerable.Empty<LoweredNode>();

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

//    public override IEnumerable<LoweredNode> EnumerateChildren() => Enumerable.Empty<LoweredNode>();
//}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
internal sealed class LoweredVariableStoreStmt : LoweredStmt
{
    public LoweredVariableStoreStmt(
        SyntaxNode parsedNode,
        Symbol variable,
        LoweredExpr expr)
        : base(parsedNode)
    {
        Variable = variable;
        Expr = expr;
    }

    public Symbol Variable { get; }

    public LoweredExpr Expr { get; }

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
internal sealed class LoweredUnaryExpr : LoweredExpr
{
    public LoweredUnaryExpr(
        SyntaxNode parsedNode,
        LoweredExpr expr)
        : base(parsedNode)
    {
        Expr = expr;
    }

    public new UnaryExprNode ParsedNode => (UnaryExprNode)base.ParsedNode;

    public LoweredExpr Expr { get; }

    public UnaryOperatorTokenKind Op => ParsedNode.Op.UnaryOperatorTokenKind;

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        yield return Expr;
    }

    public override string ToString() => Op.ToString();
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
internal sealed class LoweredBinaryExpr : LoweredExpr
{
    public LoweredBinaryExpr(
        SyntaxNode parsedNode,
        LoweredExpr left,
        LoweredExpr right)
        : base(parsedNode)
    {
        Left = left;
        Right = right;
    }

    public new BinaryExprNode ParsedNode => (BinaryExprNode)base.ParsedNode;

    public LoweredExpr Left { get; set; }

    public LoweredExpr Right { get; set; }

    public BinaryOperatorTokenKind Op => ParsedNode.Op.BinaryOperatorTokenKind;

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        yield return Left;
        yield return Right;
    }

    public override string ToString() => Op.ToString();
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
internal sealed class LoweredCallExpr : LoweredExpr
{
    public LoweredCallExpr(
        SyntaxNode parsedNode,
        FnSymbol callee,
        IEnumerable<LoweredExpr> args)
        : base(parsedNode)
    {
        Callee = callee;
        Args = args.ToImmutableArray();
    }

    public FnSymbol Callee { get; set; }

    public ImmutableArray<LoweredExpr> Args { get; }

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
internal sealed class LoweredLiteralExpr : LoweredExpr
{
    public LoweredLiteralExpr(
        SyntaxNode parsedNode,
        LiteralToken literal)
        : base(parsedNode)
    {
        Literal = literal;
    }

    public LiteralToken Literal { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren() => Enumerable.Empty<LoweredNode>();

    public override string ToString() => $"{Literal}";
}
