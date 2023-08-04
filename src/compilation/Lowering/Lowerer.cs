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

    private LoweredNode Lower(SyntaxNode node, ExpressionRewriteDef? rewriteDef = null)
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
            BlockExpressionNode n => Lower(n, rewriteDef),
            AssignmentExpressionNode n => Lower(n, rewriteDef),
            UnaryExpressionNode n => Lower(n),
            BinaryExpressionNode n => Lower(n),
            CallExpressionNode n => Lower(n),
            //AccessExpressionNode n => Lower(n),
            LiteralExpressionNode n => Lower(n),
            IdentifierExpressionNode n => Lower(n),

            _ => throw new NotImplementedException(),
        };

        // Catch the remaining simple expressions that didn't apply the rewrite.
        // This includes: Unary, Binary, Call, Literal, Identifier.
        if (rewriteDef != null &&
            result is LoweredExpression loweredExpression)
        {
            var rewrittenStatement = rewriteDef.Apply(loweredExpression);
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
        Debug.Assert(_model.TryGetDeclaredSymbol(node, out var symbol));
        var functionSymbol = (FunctionSymbol)symbol;

        return new LoweredFunctionDeclaration(
            node,
            functionSymbol,
            LowerFunctionBlock(node.Body));
    }

    private LoweredNode Lower(LetStatementNode node)
    {
        Debug.Assert(_model.TryGetDeclaredSymbol(node, out var symbol));

        var variableStatement = (LoweredStatement)new LoweredVariableStatement(
            node,
            symbol);

        if (node.Initializer == null)
        {
            return variableStatement;
        }

        var rewriteDef = new ExpressionRewriteDef(exp =>
        {
            // Which of these two:
            //// exp is always the final expression to apply (whether it's literal/call/etc, or bare).
            //// exp is always a bare? (If there's no bare, this won't be called?)

            // This rewrite will apply for this expr (whether it's literal/call/etc, or bare).
            return new LoweredVariableStoreStatement(
                node,
                symbol,
                exp);
        });

        var result = Lower(node.Initializer.Expression, rewriteDef);

        //var result = RegisterExpressionRewrite(() =>
        //{
        //    return Lower(node.Initializer.Expression, rewriteDef);
        //}, exp =>
        //{
        //    // Which of these two:
        //    //// exp is always the final expression to apply (whether it's literal/call/etc, or bare).
        //    //// exp is always a bare? (If there's no bare, this won't be called?)

        //    // This rewrite will apply for this expr (whether it's literal/call/etc, or bare).
        //    return new LoweredVariableStoreStatement(
        //        node,
        //        symbol,
        //        exp);
        //});

        Debug.Assert(result is LoweredStatement);
        var statementResult = (LoweredStatement)result; // Either the statement above, or a list (in case of block for example).

        //// result will be the rewritten Expression above (e.g. literal) or
        //// statement list containing that at the end (when there's a bare).
        //// result will be the Expression above (e.g. literal) or
        //// statement list containing that at the end (when there's a bare)?
        // result will always be a LoweredStatement, whether that's a list (block and bare) or a statement directly (literal, etc)

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
        return new LoweredExpressionStatement(
            node,
            LowerWithCast(node.Expression));
    }

    // The only place BareExpressionStatementNode will show up is in the block processing in LowerBlock.
    //private LoweredExpressionStatement Lower(BareExpressionStatementNode node)

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

    // BareReturnExpression will only be set if there's a bare block return expression.
    private (List<LoweredStatement> Statements, LoweredExpression? BareExpression) LowerBlock(BlockExpressionNode node)
    {
        var parsedStatements = node.Statements.ToList();
        var lastParsedStatement = parsedStatements.LastOrDefault();

        var bareReturnExpression = default(LoweredExpression?);
        if (lastParsedStatement is BareExpressionStatementNode bareExpressionStatementNode)
        {
            parsedStatements.Remove(lastParsedStatement);
            bareReturnExpression = LowerWithCast(bareExpressionStatementNode.Expression);
        }

        var statements = LowerWithCast(parsedStatements).ToList();

        return (statements, bareReturnExpression);
    }

    //// StatementList or StatementListWithLastExpression
    private LoweredNode Lower(BlockExpressionNode node, ExpressionRewriteDef? rewriteDef)
    {
        // TODO: If we're here, it's definitely a stand alone block? So we should add a statement to pop or something?
        var (statements, bareExpression) = LowerBlock(node);

        if (bareExpression != null)
        {
            Debug.Assert(rewriteDef != null);
            // There should always be a rewriter registered if we have a bare expr here.
            statements.Add(rewriteDef.Apply(bareExpression));
        }

        return new LoweredStatementList(
            node,
            statements);
    }

    private LoweredNode Lower(AssignmentExpressionNode node, ExpressionRewriteDef? rewriteDef)
    {
        //// assignment: test if rhs is block, and then call LowerBlock and deal with it
        //// test lhs, rhs, depending on that, this will translate to Store?

        //// But what about `if ({ true }) { }`,
        //// Decision: In the lowering of the if, hoist the expression up.

        var leftSymbol = _model.LookupSymbol(node.Lhs);
        if (leftSymbol is LocalSymbol or ParameterSymbol)
        {
            var rewriteDefInner = new ExpressionRewriteDef(exp =>
            {
                // Which of these two:
                //// exp is always the final expression to apply (whether it's literal/call/etc, or bare).
                //// exp is always a bare? (If there's no bare, this won't be called?)

                // This rewrite will apply for this expr (whether it's literal/call/etc, or bare).
                return new LoweredVariableStoreStatement(
                    node,
                    leftSymbol,
                    exp);
            });

            var result = Lower(node.Rhs, rewriteDefInner);

            Debug.Assert(result is LoweredStatement);
            var statementResult = (LoweredStatement)result;

            if (rewriteDef == null)
            {
                return statementResult;
            }

            // We have a direct parent giving us a rewrite def (e.g. let), so we're responsible about applying it.
            var finalApply = rewriteDef.Apply(new LoweredVariableLoadExpression(
                node,
                leftSymbol));

            return new LoweredStatementList(
                node,
                new[]
                {
                    statementResult,
                    finalApply,
                });
        }
        else if (leftSymbol is FieldSymbol)
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

    private class ExpressionRewriteDef
    {
        private readonly Func<LoweredExpression, LoweredStatement> _rewrite;

        public ExpressionRewriteDef(Func<LoweredExpression, LoweredStatement> rewrite)
        {
            _rewrite = rewrite;
        }

        public LoweredStatement Apply(LoweredExpression expression)
        {
            return _rewrite(expression);
        }
    }
}
