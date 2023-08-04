using System.Diagnostics;
using Surab.Analysis;

namespace Surab.Compilation;

internal class Lowerer
{
    private readonly SemanticModel _model;

    private Lowerer(SemanticModel model)
    {
        _model = model;
    }

    internal static LoweredTree Lower(SemanticModel model)
    {
        var lowerer = new Lowerer(model);
        return lowerer.Lower();
    }

    private LoweredTree Lower()
    {
        var root = Lower(_model.Tree.Root);
        return new LoweredTree(root);
    }

    private LoweredNode Lower(SyntaxNode node, RewriteExpression? rewrite = null)
    {
        var result = node switch
        {
            ModuleNode n => Lower(n),
            //ConstDeclarationNode n => Lower(n),
            FunctionDeclarationNode n => Lower(n),
            //ClassDeclarationNode n => Lower(n),
            //FieldDeclarationNode n => Lower(n),
            //MethodDeclarationNode n => Lower(n),
            LetStatementNode n => Lower(n),
            ReturnStatementNode n => Lower(n),
            ExpressionStatementNode n => Lower(n),
            BareExpressionStatementNode n => Lower(n),
            BlockExpressionNode n => Lower(n, rewrite),
            AssignmentExpressionNode n => Lower(n, rewrite),
            UnaryExpressionNode n => Lower(n),
            BinaryExpressionNode n => Lower(n),
            CallExpressionNode n => Lower(n),
            //AccessExpressionNode n => Lower(n),
            LiteralExpressionNode n => Lower(n),
            IdentifierExpressionNode n => Lower(n),

            // These are nodes that are taken care of in other places, there shouldn't be a case where they're called here.
            // => throw new Exception("Unexpected node: " + node.GetType().Name),

            _ => throw new NotImplementedException(),
        };

        // Catch the remaining simple expressions that didn't apply the rewrite.
        // This includes: Unary, Binary, Call, Literal, Identifier.
        if (rewrite != null &&
            result is LoweredExpression loweredExpression)
        {
            var rewrittenStatement = rewrite(loweredExpression);
            return rewrittenStatement;
        }

        return result;
    }

    private LoweredNode Lower(ModuleNode node)
    {
        return new LoweredModule(
            node,
            LowerWithCast(node.Declarations));
    }

    private LoweredNode Lower(FunctionDeclarationNode node)
    {
        var functionSymbol = _model.GetDeclaredSymbol(node) as FunctionSymbol;
        Debug.Assert(functionSymbol != null);

        return new LoweredFunctionDeclaration(
            node,
            functionSymbol,
            LowerFunctionBlock(node.Body));
    }

    private LoweredNode Lower(LetStatementNode node)
    {
        var symbol = _model.GetDeclaredSymbol(node);
        Debug.Assert(symbol != null);

        var variableStatement = (LoweredStatement)new LoweredVariableStatement(
            node,
            symbol);

        if (node.Initializer == null)
        {
            return variableStatement;
        }

        var result = Lower(node.Initializer.Expression, rewrite: expression =>
        {
            return new LoweredVariableStoreStatement(
                node,
                symbol,
                expression);
        });

        Debug.Assert(result is LoweredStatement);
        var statementResult = (LoweredStatement)result;

        return new LoweredStatementList(
            node,
            new[]
            {
                variableStatement,
                statementResult,
            });
    }

    private LoweredNode Lower(ReturnStatementNode node)
    {
        var expression = node.Expression == null ? null : LowerWithCast(node.Expression);
        return new LoweredReturnStatement(
            node,
            expression);
    }

    private LoweredNode Lower(ExpressionStatementNode node)
    {
        // Result might not be an expression (e.g. `a = 2;` it'll be var store statement), in which case return as is
        // as we won't need to wrap in an LoweredExpressionStatement in this case.
        var result = Lower(node.Expression);
        if (result is LoweredExpression expression)
        {
            return new LoweredExpressionStatement(
                node,
                expression);
        }
        else
        {
            return result;
        }
    }

    private LoweredNode Lower(BareExpressionStatementNode node)
    {
        // This is called in cases such as standalone blocks.
        return Lower(node.Expression);
    }

    private LoweredStatementList LowerFunctionBlock(BlockExpressionNode node)
    {
        var (statements, bareReturnExpression) = LowerBlock(node);

        if (bareReturnExpression != null)
        {
            statements.Add(new LoweredReturnStatement(
                node,
                bareReturnExpression));
        }

        return new LoweredStatementList(
            node,
            statements);
    }

    // BareReturnExpression will only be set if there's a bare block return expression (that's not treated as a statement).
    private (List<LoweredStatement> Statements, LoweredExpression? BareReturnExpression) LowerBlock(BlockExpressionNode node)
    {
        var parsedStatements = node.Statements.ToList();
        var lastParsedStatement = parsedStatements.LastOrDefault();

        var bareReturnExpression = default(LoweredExpression?);
        if (lastParsedStatement is BareExpressionStatementNode bareExpressionStatementNode &&
            !bareExpressionStatementNode.IsTreatedAsStatement)
        {
            parsedStatements.Remove(lastParsedStatement);
            bareReturnExpression = LowerWithCast(bareExpressionStatementNode.Expression);
        }

        var statements = LowerWithCast(parsedStatements).ToList();

        return (statements, bareReturnExpression);
    }

    private LoweredNode Lower(BlockExpressionNode node, RewriteExpression? rewrite)
    {
        var (statements, bareExpression) = LowerBlock(node);

        if (bareExpression != null)
        {
            // There should always be a rewriter registered if we have a bare expression here.
            Debug.Assert(rewrite != null);
            statements.Add(rewrite(bareExpression));
        }

        return new LoweredStatementList(
            node,
            statements);
    }

    private LoweredNode Lower(AssignmentExpressionNode node, RewriteExpression? rewrite)
    {
        var symbol = _model.LookupSymbol(node.Lhs);
        if (symbol is LocalSymbol or ParameterSymbol)
        {
            var result = Lower(node.Rhs, rewrite: expression =>
            {
                return new LoweredVariableStoreStatement(
                    node,
                    symbol,
                    expression);
            });

            Debug.Assert(result is LoweredStatement);
            var statementResult = (LoweredStatement)result;

            if (rewrite == null)
            {
                return statementResult;
            }

            var finalApply = rewrite(new LoweredVariableLoadExpression(
                node,
                symbol));

            return new LoweredStatementList(
                node,
                new[]
                {
                    statementResult,
                    finalApply,
                });
        }
        else if (symbol is FieldSymbol)
        {
            // TODO: Lookup the lowered class in some map. Use that here.
            throw new NotImplementedException();
        }
        else
        {
            // ?
            throw new NotImplementedException();
        }
    }

    private LoweredNode Lower(UnaryExpressionNode node)
    {
        return new LoweredUnaryExpression(
            node,
            LowerWithCast(node.Expression));
    }

    private LoweredNode Lower(BinaryExpressionNode node)
    {
        return new LoweredBinaryExpression(
            node,
            LowerWithCast(node.Left),
            LowerWithCast(node.Right));
    }

    private LoweredNode Lower(CallExpressionNode node)
    {
        // TODO: Not always an identifier?
        var functionSymbol = (FunctionSymbol)_model.LookupSymbol(node.Callee)!;

        return new LoweredCallExpression(
            node,
            functionSymbol,
            LowerWithCast(node.Arguments.Arguments.Select(arg => arg.Expression)));
    }

    //private LoweredExpression Lower(AccessExpressionNode node)
    //{
    //}

    private static LoweredNode Lower(LiteralExpressionNode node)
    {
        return new LoweredLiteralExpression(
            node,
            node.Literal);
    }

    private LoweredNode Lower(IdentifierExpressionNode node)
    {
        // Should only be here if we're accessing a variable?
        var symbol = _model.LookupSymbol(node)!;
        return new LoweredVariableLoadExpression(
            node,
            symbol);
    }

    private LoweredDeclaration LowerWithCast(DeclarationNode node) => (LoweredDeclaration)Lower((SyntaxNode)node);

    private IEnumerable<LoweredDeclaration> LowerWithCast(IEnumerable<DeclarationNode> nodes)
    {
        return nodes.Select(LowerWithCast);
    }

    private LoweredStatement LowerWithCast(StatementNode node) => (LoweredStatement)Lower((SyntaxNode)node);

    private IEnumerable<LoweredStatement> LowerWithCast(IEnumerable<StatementNode> nodes)
    {
        return nodes.Select(LowerWithCast);
    }

    private LoweredExpression LowerWithCast(ExpressionNode node) => (LoweredExpression)Lower((SyntaxNode)node);

    private IEnumerable<LoweredExpression> LowerWithCast(IEnumerable<ExpressionNode> nodes)
    {
        return nodes.Select(LowerWithCast);
    }

    // Used by a parent to tell a parsed expression child to rewrite last lowered expressions (whether it's literal/call/etc, or bare).
    // When a parent specifies a rewrite, it has to be used at some point at least once (block with bare uses it once, if+else uses it twice, etc).
    private delegate LoweredStatement RewriteExpression(LoweredExpression expression);
}
