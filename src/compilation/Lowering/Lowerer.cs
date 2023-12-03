using System.Collections.Immutable;
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

    private LoweredNode Lower(SyntaxNode node, RewriteExpr? rewrite = null)
    {
        var result = node switch
        {
            ModuleNode n => Lower(n),
            //ConstDeclNode n => Lower(n),
            FnDeclNode n => Lower(n),
            StructDeclNode n => Lower(n),
            //ClassDeclNode n => Lower(n),
            //MethodDeclNode n => Lower(n),
            VarStmtNode n => Lower(n),
            ReturnStmtNode n => Lower(n),
            ExprStmtNode n => Lower(n),
            BareExprStmtNode n => Lower(n),
            BlockExprNode n => Lower(n, rewrite),
            AssignmentExprNode n => Lower(n, rewrite),
            UnaryExprNode n => Lower(n),
            BinaryExprNode n => Lower(n),
            CallExprNode n => Lower(n),
            AccessExprNode n => Lower(n),
            LiteralExprNode n => Lower(n),
            IdentifierExprNode n => Lower(n),
            StructCreationExprNode n => Lower(n),

            // These are nodes that are taken care of in other places, there shouldn't be a case where they're called here.
            // => throw new Exception("Unexpected node: " + node.GetType().Name),

            _ => throw new NotImplementedException(),
        };

        // Catch the remaining simple exprs that didn't apply the rewrite.
        // This includes: Unary, Binary, Call, Literal, Identifier.
        if (rewrite != null &&
            result is LoweredExpr loweredExpr)
        {
            var rewrittenStmt = rewrite(loweredExpr);
            return rewrittenStmt;
        }

        return result;
    }

    private LoweredNode Lower(ModuleNode node)
    {
        return new LoweredModule(
            node,
            LowerWithCast(node.Decls));
    }

    private LoweredNode Lower(FnDeclNode node)
    {
        var fnSymbol = _model.GetDeclaredSymbol(node) as FnSymbol;
        Debug.Assert(fnSymbol != null);

        return new LoweredFnDecl(
            node,
            fnSymbol,
            node.Body == null ? null : LowerFnBlock(node.Body));
    }

    private LoweredNode Lower(StructDeclNode node)
    {
        var structTypeSymbol = _model.GetDeclaredSymbol(node) as StructTypeSymbol;
        Debug.Assert(structTypeSymbol != null);

        return new LoweredTypeDecl(
            node,
            structTypeSymbol,
            structTypeSymbol.Members.OfType<FieldSymbol>().ToImmutableArray());
    }

    private LoweredNode Lower(VarStmtNode node)
    {
        var symbol = _model.GetDeclaredSymbol(node) as LocalSymbol;
        Debug.Assert(symbol != null);

        var variableStmt = (LoweredStmt)new LoweredVariableStmt(
            node,
            symbol);

        if (node.Initializer == null)
        {
            return variableStmt;
        }

        var result = Lower(node.Initializer.Expr, rewrite: expr =>
        {
            return new LoweredVariableStoreStmt(
                node,
                symbol,
                expr);
        });

        Debug.Assert(result is LoweredStmt);
        var stmtResult = (LoweredStmt)result;

        return new LoweredStmtList(
            node,
            new[]
            {
                variableStmt,
                stmtResult,
            });
    }

    private LoweredNode Lower(ReturnStmtNode node)
    {
        var expr = node.Expr == null ? null : LowerWithCast(node.Expr);
        return new LoweredReturnStmt(
            node,
            expr);
    }

    private LoweredNode Lower(ExprStmtNode node)
    {
        // Result might not be an expr (e.g. `a = 2;` it'll be var store stmt), in which case return as is
        // as we won't need to wrap in an LoweredExprStmt in this case.
        var result = Lower(node.Expr);
        if (result is LoweredExpr expr)
        {
            return new LoweredExprStmt(
                node,
                expr);
        }
        else
        {
            return result;
        }
    }

    private LoweredNode Lower(BareExprStmtNode node)
    {
        // This is called in cases such as standalone blocks.
        return Lower(node.Expr);
    }

    private LoweredStmtList LowerFnBlock(BlockExprNode node)
    {
        var (stmts, bareReturnExpr) = LowerBlock(node);

        if (bareReturnExpr != null)
        {
            stmts.Add(new LoweredReturnStmt(
                node,
                bareReturnExpr));
        }

        return new LoweredStmtList(
            node,
            stmts);
    }

    // BareReturnExpr will only be set if there's a bare block return expr (that's not treated as a stmt).
    private (List<LoweredStmt> Stmts, LoweredExpr? BareReturnExpr) LowerBlock(BlockExprNode node)
    {
        var parsedStmts = node.Stmts.ToList();
        var lastParsedStmt = parsedStmts.LastOrDefault();

        var bareReturnExpr = default(LoweredExpr?);
        if (lastParsedStmt is BareExprStmtNode bareExprStmtNode &&
            !bareExprStmtNode.IsTreatedAsStmt)
        {
            parsedStmts.Remove(lastParsedStmt);
            bareReturnExpr = LowerWithCast(bareExprStmtNode.Expr);
        }

        var stmts = LowerWithCast(parsedStmts).ToList();

        return (stmts, bareReturnExpr);
    }

    private LoweredNode Lower(BlockExprNode node, RewriteExpr? rewrite)
    {
        var (stmts, bareExpr) = LowerBlock(node);

        if (bareExpr != null)
        {
            // There should always be a rewriter registered if we have a bare expr here.
            Debug.Assert(rewrite != null);
            stmts.Add(rewrite(bareExpr));
        }

        return new LoweredStmtList(
            node,
            stmts);
    }

    private LoweredNode Lower(AssignmentExprNode node, RewriteExpr? rewrite)
    {
        var symbol = _model.LookupSymbol(node.Lhs);
        if (symbol is LocalSymbol or ParamSymbol)
        {
            var result = Lower(node.Rhs, rewrite: expr =>
            {
                return new LoweredVariableStoreStmt(
                    node,
                    symbol,
                    expr);
            });

            Debug.Assert(result is LoweredStmt);
            var stmtResult = (LoweredStmt)result;

            if (rewrite == null)
            {
                return stmtResult;
            }

            var finalApply = rewrite(new LoweredVariableLoadExpr(
                node,
                ((ISymbolHasType)symbol).Type,
                symbol));

            return new LoweredStmtList(
                node,
                [
                    stmtResult,
                    finalApply,
                ]);
        }
        else if (symbol is FieldSymbol fieldSymbol)
        {
            var accessExpr = (AccessExprNode)node.Lhs; // Is this always valid?
            //var targetSymbol = _model.LookupSymbol(accessExpr.Target);
            var loweredTarget = LowerWithCast(accessExpr.Target);
            //var enclosingType = fieldSymbol.EnclosingType;

            var result = Lower(node.Rhs, rewrite: expr =>
            {
                return new LoweredFieldStoreStmt(
                    node,
                    loweredTarget,
                    fieldSymbol,
                    expr);
            });

            Debug.Assert(result is LoweredStmt);
            var stmtResult = (LoweredStmt)result;

            if (rewrite == null)
            {
                return stmtResult;
            }

            var finalApply = rewrite(new LoweredFieldLoadExpr(
                node,
                loweredTarget,
                fieldSymbol));

            return new LoweredStmtList(
                node,
                [
                    stmtResult,
                    finalApply,
                ]);
        }
        else
        {
            // ?
            throw new NotImplementedException();
        }
    }

    private LoweredExpr Lower(AccessExprNode node)
    {
        // Should only be here if we're loading a field?
        var symbol = (FieldSymbol)_model.LookupSymbol(node)!;
        var loweredTarget = LowerWithCast(node.Target);
        return new LoweredFieldLoadExpr(
            node,
            loweredTarget,
            symbol);
    }

    private LoweredNode Lower(UnaryExprNode node)
    {
        return new LoweredUnaryExpr(
            node,
            _model.LookupType(node)!,
            LowerWithCast(node.Expr));
    }

    private LoweredNode Lower(BinaryExprNode node)
    {
        return new LoweredBinaryExpr(
            node,
            _model.LookupType(node)!,
            LowerWithCast(node.Left),
            LowerWithCast(node.Right));
    }

    private LoweredNode Lower(CallExprNode node)
    {
        var symbol = _model.LookupSymbol(node.Callee)!;
        var fnType = (FnTypeSymbol)_model.LookupType(node.Callee)!;
        return new LoweredCallExpr(
            node,
            fnType,
            symbol,
            LowerWithCast(node.Args.Args.Select(arg => arg.Expr)));
    }

    private LoweredNode Lower(LiteralExprNode node)
    {
        var type = _model.LookupType(node)!;
        return new LoweredLiteralExpr(
            node,
            type,
            node.Literal);
    }

    private LoweredNode Lower(IdentifierExprNode node)
    {
        // Should only be here if we're loading a variable?
        var symbol = _model.LookupSymbol(node)!;
        Debug.Assert(symbol is ISymbolHasType);
        var symbolHasType = (ISymbolHasType)symbol;
        return new LoweredVariableLoadExpr(
            node,
            symbolHasType.Type,
            symbol);
    }

    private LoweredNode Lower(StructCreationExprNode node)
    {
        var symbol = (StructTypeSymbol)_model.LookupSymbol(node)!;
        return new LoweredStructCreationExpr(
            node,
            symbol,
            Lower(node.MapInitializer));
    }

    private LoweredMapInitializer Lower(MapInitializerNode node)
    {
        var items = new List<LoweredMapInitializerItem>();

        foreach (var expr in node.WellFormedExprs)
        {
            var identifierNode = (IdentifierExprNode)expr.Lhs;
            // Later, this will have to handle other cases than just a simple identifier signifying a field.

            items.Add(new(identifierNode.Identifier.Value, LowerWithCast(expr.Rhs)));
        }

        return new LoweredMapInitializer(
            node,
            items);
    }

    private LoweredDecl LowerWithCast(DeclNode node) => (LoweredDecl)Lower((SyntaxNode)node);

    private IEnumerable<LoweredDecl> LowerWithCast(IEnumerable<DeclNode> nodes)
    {
        return nodes.Select(LowerWithCast);
    }

    private LoweredStmt LowerWithCast(StmtNode node) => (LoweredStmt)Lower((SyntaxNode)node);

    private IEnumerable<LoweredStmt> LowerWithCast(IEnumerable<StmtNode> nodes)
    {
        return nodes.Select(LowerWithCast);
    }

    private LoweredExpr LowerWithCast(ExprNode node) => (LoweredExpr)Lower((SyntaxNode)node);

    private IEnumerable<LoweredExpr> LowerWithCast(IEnumerable<ExprNode> nodes)
    {
        return nodes.Select(LowerWithCast);
    }

    // Used by a parent to tell a parsed expr child to rewrite last lowered exprs (whether it's literal/call/etc, or bare).
    // When a parent specifies a rewrite, it has to be used at some point at least once (block with bare uses it once, if+else uses it twice, etc).
    private delegate LoweredStmt RewriteExpr(LoweredExpr expr);
}
