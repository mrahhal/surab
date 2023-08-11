using System.Diagnostics;

namespace Surab.Analysis;

/// <summary>
/// A visitor that detects all semantic diagnostics for a given <see cref="SemanticModel"/>.
/// </summary>
internal class SemanticAnalyzer : Visitor
{
    private readonly SemanticModel _model;
    private readonly DiagnosticBag _diagnostics = new();

    public SemanticAnalyzer(SemanticModel model)
    {
        _model = model;
    }

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

    public override void VisitConstDeclaration(ConstDeclarationNode node)
    {
        // Temp: Should be allowed only in top level.
        if (node.Parent is not ModuleNode)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a const declaration is allowed only in top level");
        }

        base.VisitConstDeclaration(node);
    }

    public override void VisitFnDeclaration(FnDeclarationNode node)
    {
        // Temp: Should be allowed only in top level.
        if (node.Parent is not ModuleNode)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a function declaration is allowed only in top level");
        }

        base.VisitFnDeclaration(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationNode node)
    {
        // Temp: Should be allowed only in top level.
        if (node.Parent is not ModuleNode)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a class declaration is allowed only in top level");
        }

        base.VisitClassDeclaration(node);
    }

    public override void VisitFieldDeclaration(FieldDeclarationNode node)
    {
        if (node.Parent is not ClassDeclarationNode)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a field declaration is not allowed in this context");
        }

        base.VisitFieldDeclaration(node);
    }

    public override void VisitMethodDeclaration(MethodDeclarationNode node)
    {
        if (node.Parent is not ClassDeclarationNode)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a method declaration is not allowed in this context");
        }

        base.VisitMethodDeclaration(node);
    }

    public override void VisitReturnStatement(ReturnStatementNode node)
    {
        Debug.Assert(node.Parent is BlockExpressionNode);
        var block = (BlockExpressionNode)node.Parent;

        // If it's not the last statement, warn.
        // TODO: Removed fror now, as this needs control flow analysis.
        // Example: `if () { return 1; } else { return 2; }`?
        //if (block.Statements.Last() != node)
        //{
        //    _diagnostics.Add(node.Span, DiagnosticLevel.Warning, $"unreachable code after the return statement");
        //}

        // TODO: Error if return has incompatible type with function return type.

        base.VisitReturnStatement(node);
    }

    public override void VisitBlockExpression(BlockExpressionNode node)
    {
        // If this block returns an expression, it can't stand on its own.
        // i.e. It shouldn't be parented by ExpressionStatementNode or BareExpressionStatementNode.
        if (node.ReturnsExpression &&
            node.Parent is ExpressionStatementNode or BareExpressionStatementNode)
        {
            _diagnostics.Add(
                node.Span, DiagnosticLevel.Error,
                $"a block returning an expression can not stand on its own");
        }

        base.VisitBlockExpression(node);
    }

    public override void VisitBareExpressionStatement(BareExpressionStatementNode node)
    {
        // Should only be allowed as the last statement unless this bare is treated as a statement (i.e. a block without
        // a bare expression (because the block gets lowered to a list of statements in that case).

        Debug.Assert(node.Parent is BlockExpressionNode);
        var parentBlock = (BlockExpressionNode)node.Parent;

        if (!node.IsTreatedAsStatement && parentBlock.Statements.Last() != node)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"a bare expression can only exist as the last statement of a block");
        }

        base.VisitBareExpressionStatement(node);
    }

    public override void VisitAssignmentExpression(AssignmentExpressionNode node)
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
            //case AccessExpressionNode accessExpressionNode:
            //    break;
            case IdentifierExpressionNode identifierExpressionNode:
                break;

            default:
                Debug.Assert(false, "Invalid target of assignment.");
                throw new NotImplementedException();
        }*/

        base.VisitAssignmentExpression(node);
    }

    public override void VisitUnaryExpression(UnaryExpressionNode node)
    {
        // TODO: Should work on traits or something.

        var expressionType = _model.LookupType(node.Expression);

        switch (node.Op.UnaryOperatorTokenKind)
        {
            case UnaryOperatorTokenKind.Pos:
            case UnaryOperatorTokenKind.Neg:
                if (expressionType != BuiltInTypeSystem.Number)
                {
                    _diagnostics.Add(node.Op.Span, DiagnosticLevel.Error, $"only valid with a number type");
                }
                break;
            case UnaryOperatorTokenKind.Not:
                if (expressionType != BuiltInTypeSystem.Bool)
                {
                    _diagnostics.Add(node.Op.Span, DiagnosticLevel.Error, $"only valid with a bool type");
                }
                break;
        }

        base.VisitUnaryExpression(node);
    }

    public override void VisitBinaryExpression(BinaryExpressionNode node)
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
            var expressionType = leftType;
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
                    if (expressionType != BuiltInTypeSystem.Number)
                    {
                        _diagnostics.Add(node.Op.Span, DiagnosticLevel.Error, $"only valid with a number type");
                    }
                    break;
                case BinaryOperatorTokenKind.And:
                case BinaryOperatorTokenKind.Or:
                    if (expressionType != BuiltInTypeSystem.Bool)
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
                    if (expressionType != BuiltInTypeSystem.Number)
                    {
                        _diagnostics.Add(node.Op.Span, DiagnosticLevel.Error, $"only valid with a number type");
                    }
                    break;

                default:
                    break;
            }
        }

        base.VisitBinaryExpression(node);
    }

    public override void VisitCallExpression(CallExpressionNode node)
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
            var args = node.Arguments.Arguments.ToList();
            if (fnSymbol.Parameters.Length != args.Count)
            {
                _diagnostics.Add(node.Arguments.Span, DiagnosticLevel.Error, $"invalid invocation: wrong number of arguments");
            }
            else
            {
                for (var i = 0; i < args.Count; i++)
                {
                    var arg = args[i];
                    var param = fnSymbol.Parameters[i];
                    var argType = _model.LookupType(arg);
                    if (param.Type != argType)
                    {
                        _diagnostics.Add(arg.Span, DiagnosticLevel.Error, $"incompatible types");
                    }
                }
            }
        }

        base.VisitCallExpression(node);
    }

    public override void VisitAccessExpression(AccessExpressionNode node)
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

        base.VisitAccessExpression(node);
    }

    public override void VisitThisExpression(ThisExpressionNode node)
    {
        var thisType = _model.LookupType(node);
        if (thisType == null)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"unknown this context");
        }

        base.VisitThisExpression(node);
    }

    public override void VisitParameter(ParameterNode node)
    {
        if (node.Type == null)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"specifying type is required");
        }

        base.VisitParameter(node);
    }

    public override void VisitFnSignature(FnSignatureNode node)
    {
        // TODO: Should ReturnTy.Type be optional, so we could parse it but raise diag here?

        base.VisitFnSignature(node);
    }

    public override void VisitIdentifierExpression(IdentifierExpressionNode node)
    {
        var symbol = _model.LookupSymbol(node);
        if (symbol == null)
        {
            _diagnostics.Add(node.Span, DiagnosticLevel.Error, $"unknown name");
        }

        base.VisitIdentifierExpression(node);
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
    // - LetStatementNode.Type and Initializer:
    //   - Initializer should always be given a value as a start
    // - TypeSymbol being the ErrorType
    // - BlockExpression can't appear as an l-value
}
