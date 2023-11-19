using System.Diagnostics;

namespace Surab.Analysis;

/// <summary>
/// A visitor that detects all semantic diagnostics for a given <see cref="SemanticModel"/>.
/// </summary>
internal class SemanticAnalyzer(SemanticModel model) : Visitor
{
    private readonly SemanticModel _model = model;
    private readonly DiagnosticBag _diagnostics = new();

    public static DiagnosticBag Analyze(SemanticModel model)
    {
        var analyzer = new SemanticAnalyzer(model);
        analyzer.Analyze();
        return analyzer._diagnostics;
    }

    private void Analyze()
    {
        Visit(_model.Tree.Root);
    }

    public override void VisitConstDecl(ConstDeclNode node)
    {
        // Temp: Should be allowed only in top level.
        if (node.Parent is not ModuleNode)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a const decl is allowed only in top level");
        }

        base.VisitConstDecl(node);
    }

    public override void VisitFnDecl(FnDeclNode node)
    {
        // Temp: Should be allowed only in top level.
        if (node.Parent is not ModuleNode)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a function decl is allowed only in top level");
        }

        base.VisitFnDecl(node);
    }

    public override void VisitClassDecl(ClassDeclNode node)
    {
        // Temp: Should be allowed only in top level.
        if (node.Parent is not ModuleNode)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a class decl is allowed only in top level");
        }

        base.VisitClassDecl(node);
    }

    public override void VisitFieldDecl(FieldDeclNode node)
    {
        if (node.Parent is not StructDeclNode or ClassDeclNode)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a field decl is not allowed in this context");
        }

        base.VisitFieldDecl(node);
    }

    public override void VisitMethodDecl(MethodDeclNode node)
    {
        if (node.Parent is not ClassDeclNode)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a method decl is not allowed in this context");
        }

        base.VisitMethodDecl(node);
    }

    public override void VisitReturnStmt(ReturnStmtNode node)
    {
        Debug.Assert(node.Parent is BlockExprNode);
        var block = (BlockExprNode)node.Parent;

        // If it's not the last stmt, warn.
        // TODO: Removed fror now, as this needs control flow analysis.
        // Example: `if () { return 1; } else { return 2; }`?
        //if (block.Stmts.Last() != node)
        //{
        //    _diagnostics.Add(node.Span, DiagnosticLevel.Warning, $"unreachable code after the return stmt");
        //}

        // TODO: Error if return has incompatible type with function return type.

        base.VisitReturnStmt(node);
    }

    public override void VisitBlockExpr(BlockExprNode node)
    {
        // If this block returns an expr, it can't stand on its own.
        // i.e. It shouldn't be parented by ExprStmtNode or BareExprStmtNode.
        if (node.ReturnsExpr &&
            node.Parent is ExprStmtNode or BareExprStmtNode)
        {
            _diagnostics.Add(
                node.Span, DiagnosticLevel.Error,
                $"a block returning an expr can not stand on its own");
        }

        base.VisitBlockExpr(node);
    }

    public override void VisitBareExprStmt(BareExprStmtNode node)
    {
        // Should only be allowed as the last stmt unless this bare is treated as a stmt (i.e. a block without
        // a bare expr (because the block gets lowered to a list of stmts in that case).

        Debug.Assert(node.Parent is BlockExprNode);
        var parentBlock = (BlockExprNode)node.Parent;

        if (!node.IsTreatedAsStmt && parentBlock.Stmts.Last() != node)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a bare expr can only exist as the last stmt of a block");
        }

        base.VisitBareExprStmt(node);
    }

    public override void VisitAssignmentExpr(AssignmentExprNode node)
    {
        // Compatibility
        var lhsSymbol = _model.LookupSymbol(node.Lhs);
        var lhsType = _model.LookupType(node.Lhs);

        var rhsSymbol = _model.LookupSymbol(node.Rhs);
        var rhsType = _model.LookupType(node.Rhs);

        if (lhsSymbol == null || rhsSymbol == null)
        {
            // Shouldn't report. Will be reported elsewhere.
        }
        else if (lhsType == null || rhsType == null || lhsType != rhsType)
        {
            // Incompatible types.
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"invalid assignment because of incompatible types");
        }

        // TODO: Add to SemanticAnalyzer: Invalid target of assignment.
        /*switch (node.Lhs)
        {
            //case AccessExprNode accessExprNode:
            //    break;
            case IdentifierExprNode identifierExprNode:
                break;

            default:
                Debug.Assert(false, "Invalid target of assignment.");
                throw new NotImplementedException();
        }*/

        base.VisitAssignmentExpr(node);
    }

    public override void VisitUnaryExpr(UnaryExprNode node)
    {
        // TODO: Should work on traits or something.

        var exprType = _model.LookupType(node.Expr);

        switch (node.Op.UnaryOperatorTokenKind)
        {
            case UnaryOperatorTokenKind.Pos:
            case UnaryOperatorTokenKind.Neg:
                if (exprType != BuiltInTypeSystem.Number)
                {
                    _diagnostics.Add(node.Op.Span, DiagnosticLevel.Error, $"only valid with a number type");
                }
                break;
            case UnaryOperatorTokenKind.Not:
                if (exprType != BuiltInTypeSystem.Bool)
                {
                    _diagnostics.Add(node.Op.Span, DiagnosticLevel.Error, $"only valid with a bool type");
                }
                break;
        }

        base.VisitUnaryExpr(node);
    }

    public override void VisitBinaryExpr(BinaryExprNode node)
    {
        var leftType = _model.LookupType(node.Left);
        var rightType = _model.LookupType(node.Right);

        if (leftType == null || rightType == null)
        {
            // Shouldn't report. Will be reported elsewhere.
        }
        else if (leftType != rightType)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"incompatible types");
        }
        else
        {
            var exprType = leftType;
            switch (node.Op.BinaryOperatorTokenKind)
            {
                case BinaryOperatorTokenKind.Add:
                case BinaryOperatorTokenKind.Sub:
                case BinaryOperatorTokenKind.Mul:
                case BinaryOperatorTokenKind.Div:
                case BinaryOperatorTokenKind.Pow:
                case BinaryOperatorTokenKind.BitAnd:
                case BinaryOperatorTokenKind.BitOr:
                case BinaryOperatorTokenKind.BitXor:
                    if (exprType != BuiltInTypeSystem.Number)
                    {
                        _diagnostics.Add(node.Op.Span, DiagnosticLevel.Error, $"only valid with a number type");
                    }
                    break;
                case BinaryOperatorTokenKind.And:
                case BinaryOperatorTokenKind.Or:
                    if (exprType != BuiltInTypeSystem.Bool)
                    {
                        _diagnostics.Add(node.Op.Span, DiagnosticLevel.Error, $"only valid with a bool type");
                    }
                    break;
                //case BinaryOperatorTokenKind.Eq:
                //    break;
                //case BinaryOperatorTokenKind.Ne:
                //    break;
                case BinaryOperatorTokenKind.Lt:
                case BinaryOperatorTokenKind.Le:
                case BinaryOperatorTokenKind.Gt:
                case BinaryOperatorTokenKind.Ge:
                    if (exprType != BuiltInTypeSystem.Number)
                    {
                        _diagnostics.Add(node.Op.Span, DiagnosticLevel.Error, $"only valid with a number type");
                    }
                    break;

                default:
                    break;
            }
        }

        base.VisitBinaryExpr(node);
    }

    public override void VisitCallExpr(CallExprNode node)
    {
        var calleeSymbol = _model.LookupSymbol(node.Callee);

        // Ensure callee is a function and validate args.
        if (calleeSymbol is not FnSymbol fnSymbol)
        {
            // TODO: Shouldn't raise this when invoking a function that doesn't exist. Should say "unknown symbol" or something.
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"invocation on a non function");
        }
        else
        {
            var args = node.Args.Args.ToList();
            if (fnSymbol.Params.Length != args.Count)
            {
                _diagnostics.Add(node.Args.Span, DiagnosticLevel.Error, $"invalid invocation: wrong number of arguments");
            }
            else
            {
                for (var i = 0; i < args.Count; i++)
                {
                    var arg = args[i];
                    var param = fnSymbol.Params[i];
                    var argType = _model.LookupType(arg);
                    if (param.Type != argType)
                    {
                        _diagnostics.Add(arg.Span, DiagnosticLevel.Error, $"incompatible types");
                    }
                }
            }
        }

        base.VisitCallExpr(node);
    }

    public override void VisitAccessExpr(AccessExprNode node)
    {
        var targetSymbol = _model.LookupSymbol(node.Target);
        // TODO: ?
        //targetSymbol.Members

        // TODO: Shouldn't this be TypeSymbol?
        if (targetSymbol is not NamedTypeSymbol typeSymbol)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"unallowed access");
        }
        else
        {
            // TODO: Store and use a lookup table for this.
            var matched = typeSymbol.Members.FirstOrDefault(x => x.Name == node.Identifier.Value);
            if (matched == null)
            {
                _diagnostics.Add(node.Identifier.Span, DiagnosticLevel.Error, $"unknown member access");
            }
        }

        base.VisitAccessExpr(node);
    }

    public override void VisitThisExpr(ThisExprNode node)
    {
        var thisType = _model.LookupType(node);
        if (thisType == null)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"unknown this context");
        }

        base.VisitThisExpr(node);
    }

    public override void VisitParam(ParamNode node)
    {
        if (node.Type == null)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"specifying type is required");
        }

        base.VisitParam(node);
    }

    public override void VisitFnSig(FnSigNode node)
    {
        // TODO: Should ReturnTy.Type be optional, so we could parse it but raise diag here?

        base.VisitFnSig(node);
    }

    public override void VisitIdentifierExpr(IdentifierExprNode node)
    {
        var symbol = _model.LookupSymbol(node);
        if (symbol == null)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"unknown name");
        }

        base.VisitIdentifierExpr(node);
    }

    public override void VisitType(TypeNode node)
    {
        var symbol = _model.LookupSymbol(node);
        if (symbol == null)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"unknown type");
        }
        else if (symbol is not TypeSymbol)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"not a type");
        }

        base.VisitType(node);
    }

    // TODO: Things to consider adding here.
    // - LetStmtNode.Type and Initializer:
    //   - Initializer should always be given a value as a start
    // - TypeSymbol being the ErrorType
    // - BlockExpr can't appear as an l-value
}
