using System.Collections.Immutable;
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
        IEnumerable<LoweredDeclaration> declarations)
        : base(parsedNode)
    {
        Declarations = declarations.ToImmutableArray();
    }

    public ImmutableArray<LoweredDeclaration> Declarations { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        foreach (var child in Declarations)
        {
            yield return child;
        }
    }
}

internal abstract class LoweredDeclaration : LoweredNode
{
    public LoweredDeclaration(SyntaxNode parsedNode) : base(parsedNode)
    {
    }
}

// Lowered from both functions and methods.
internal sealed class LoweredFunctionDeclaration : LoweredDeclaration
{
    public LoweredFunctionDeclaration(
        SyntaxNode parsedNode,
        FunctionSymbol functionSymbol,
        LoweredStatementList body)
        : base(parsedNode)
    {
        FunctionSymbol = functionSymbol;
        Body = body;
    }

    public FunctionSymbol FunctionSymbol { get; }

    public LoweredStatementList Body { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        yield return Body;
    }
}

internal sealed class LoweredTypeDeclaration : LoweredDeclaration
{
    public LoweredTypeDeclaration(
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

internal abstract class LoweredStatement : LoweredNode
{
    public LoweredStatement(SyntaxNode parsedNode) : base(parsedNode)
    {
    }
}

// Lowered from local variables, ?
// Only declares the variable. (i.e `let a: Type;`)
internal sealed class LoweredVariableStatement : LoweredStatement
{
    public LoweredVariableStatement(
        SyntaxNode parsedNode,
        Symbol symbol)
        : base(parsedNode)
    {
        Symbol = symbol;
    }

    public Symbol Symbol { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren() => Enumerable.Empty<LoweredNode>();
}

internal sealed class LoweredReturnStatement : LoweredStatement
{
    public LoweredReturnStatement(
        SyntaxNode parsedNode,
        LoweredExpression? expression)
        : base(parsedNode)
    {
        Expression = expression;
    }

    public LoweredExpression? Expression { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        if (Expression != null)
            yield return Expression;
    }
}

// TODO: ExpressionStatementNode/BareExpressionStatementNode?

internal sealed class LoweredExpressionStatement : LoweredStatement
{
    public LoweredExpressionStatement(
        SyntaxNode parsedNode,
        LoweredExpression expression)
        : base(parsedNode)
    {
        Expression = expression;
    }

    public LoweredExpression Expression { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        yield return Expression;
    }
}

//internal class LoweredGotoStatement : LoweredStatement
//{
//}

//internal class LoweredConditionalGotoStatement : LoweredStatement
//{
//}

internal sealed class LoweredStatementList : LoweredStatement
{
    public LoweredStatementList(
        SyntaxNode parsedNode,
        IEnumerable<LoweredStatement> statements)
        : base(parsedNode)
    {
        Statements = statements.ToImmutableArray();
    }

    public ImmutableArray<LoweredStatement> Statements { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        foreach (var child in Statements)
        {
            yield return child;
        }

    }
}

internal abstract class LoweredExpression : LoweredNode
{
    public LoweredExpression(SyntaxNode parsedNode) : base(parsedNode)
    {
    }
}

// Statement list with an optional last expression.
// Useful in the case of blocks that return expressions, in order to hoist these statements when compiling.
internal sealed class LoweredStatementsWithLastExpression : LoweredExpression
{
    public LoweredStatementsWithLastExpression(
        SyntaxNode parsedNode,
        LoweredStatementList statementList,
        LoweredExpression? expression)
        : base(parsedNode)
    {
        StatementList = statementList;
        Expression = expression;
    }

    public LoweredStatementList StatementList { get; }

    public LoweredExpression? Expression { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        yield return StatementList;
        if (Expression != null)
            yield return Expression;
    }
}

internal sealed class LoweredVariableLoadExpression : LoweredExpression
{
    public LoweredVariableLoadExpression(
        SyntaxNode parsedNode,
        Symbol variable)
        : base(parsedNode)
    {
        Variable = variable;
    }

    public Symbol Variable { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren() => Enumerable.Empty<LoweredNode>();
}

//internal sealed class LoweredFieldLoadExpression : LoweredExpression
//{
//    public LoweredFieldLoadExpression(
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

internal sealed class LoweredVariableStoreStatement : LoweredStatement
{
    public LoweredVariableStoreStatement(
        SyntaxNode parsedNode,
        Symbol variable,
        LoweredExpression expression)
        : base(parsedNode)
    {
        Variable = variable;
        Expression = expression;
    }

    public Symbol Variable { get; }

    public LoweredExpression Expression { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        yield return Expression;
    }
}

//internal sealed class LoweredFieldStoreStatement : LoweredStatement
//{
//    public LoweredFieldStoreStatement(
//        SyntaxNode parsedNode,
//        Symbol instance,
//        Symbol field,
//        LoweredExpression expression)
//        : base(parsedNode)
//    {
//        Instance = instance;
//        Field = field;
//        Expression = expression;
//    }

//    public Symbol Instance { get; }

//    public Symbol Field { get; }

//    public LoweredExpression Expression { get; }

//    public override IEnumerable<LoweredNode> EnumerateChildren()
//    {
//        yield return Expression;
//    }
//}

internal sealed class LoweredUnaryExpression : LoweredExpression
{
    public LoweredUnaryExpression(
        SyntaxNode parsedNode,
        LoweredExpression expression)
        : base(parsedNode)
    {
        Expression = expression;
    }

    public new UnaryExpressionNode ParsedNode => (UnaryExpressionNode)base.ParsedNode;

    public LoweredExpression Expression { get; }

    public UnaryOperatorTokenKind Op => ParsedNode.Op.UnaryOperatorTokenKind;

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        yield return Expression;
    }
}

internal sealed class LoweredBinaryExpression : LoweredExpression
{
    public LoweredBinaryExpression(
        SyntaxNode parsedNode,
        LoweredExpression left,
        LoweredExpression right)
        : base(parsedNode)
    {
        Left = left;
        Right = right;
    }

    public new BinaryExpressionNode ParsedNode => (BinaryExpressionNode)base.ParsedNode;

    public LoweredExpression Left { get; set; }

    public LoweredExpression Right { get; set; }

    public BinaryOperatorTokenKind Op => ParsedNode.Op.BinaryOperatorTokenKind;

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        yield return Left;
        yield return Right;
    }
}

internal sealed class LoweredCallExpression : LoweredExpression
{
    public LoweredCallExpression(
        SyntaxNode parsedNode,
        FunctionSymbol callee,
        IEnumerable<LoweredExpression> arguments)
        : base(parsedNode)
    {
        Callee = callee;
        Arguments = arguments.ToImmutableArray();
    }

    public FunctionSymbol Callee { get; set; }

    public ImmutableArray<LoweredExpression> Arguments { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren()
    {
        foreach (var child in Arguments)
        {
            yield return child;
        }
    }
}

internal sealed class LoweredLiteralExpression : LoweredExpression
{
    public LoweredLiteralExpression(
        SyntaxNode parsedNode,
        LiteralToken literal)
        : base(parsedNode)
    {
        Literal = literal;
    }

    public LiteralToken Literal { get; }

    public override IEnumerable<LoweredNode> EnumerateChildren() => Enumerable.Empty<LoweredNode>();
}
