using System.Diagnostics;
using System.Linq.Expressions;

namespace Surab.Analyzer;

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

    public override void VisitConstDeclaration(ConstDeclarationNode constDeclarationNode)
    {
        // Temp: Should be allowed only in top level.
        if (constDeclarationNode.Parent is not ModuleNode)
        {
            _diagnostics.Add(constDeclarationNode.Span, DiagnosticLevel.Error, $"a const declaration is allowed only in top level");
        }

        base.VisitConstDeclaration(constDeclarationNode);
    }

    public override void VisitFunctionDeclaration(FunctionDeclarationNode functionDeclarationNode)
    {
        // Temp: Should be allowed only in top level.
        if (functionDeclarationNode.Parent is not ModuleNode)
        {
            _diagnostics.Add(functionDeclarationNode.Span, DiagnosticLevel.Error, $"a function declaration is allowed only in top level");
        }

        base.VisitFunctionDeclaration(functionDeclarationNode);
    }

    public override void VisitClassDeclaration(ClassDeclarationNode classDeclarationNode)
    {
        // Temp: Should be allowed only in top level.
        if (classDeclarationNode.Parent is not ModuleNode)
        {
            _diagnostics.Add(classDeclarationNode.Span, DiagnosticLevel.Error, $"a class declaration is allowed only in top level");
        }

        base.VisitClassDeclaration(classDeclarationNode);
    }

    public override void VisitFieldDeclaration(FieldDeclarationNode fieldDeclarationNode)
    {
        if (fieldDeclarationNode.Parent is not ClassDeclarationNode)
        {
            _diagnostics.Add(fieldDeclarationNode.Span, DiagnosticLevel.Error, $"a field declaration is not allowed in this context");
        }

        base.VisitFieldDeclaration(fieldDeclarationNode);
    }

    public override void VisitMethodDeclaration(MethodDeclarationNode methodDeclarationNode)
    {
        if (methodDeclarationNode.Parent is not ClassDeclarationNode)
        {
            _diagnostics.Add(methodDeclarationNode.Span, DiagnosticLevel.Error, $"a method declaration is not allowed in this context");
        }

        base.VisitMethodDeclaration(methodDeclarationNode);
    }

    public override void VisitReturnStatement(ReturnStatementNode returnStatementNode)
    {
        // If it's not the last statement, warn.
        Debug.Assert(returnStatementNode.Parent is BlockExpressionNode);
        var block = (BlockExpressionNode)returnStatementNode.Parent;
        if (block.Statements.Last() != returnStatementNode)
        {
            _diagnostics.Add(returnStatementNode.Span, DiagnosticLevel.Warning, $"unreachable code after the return statement");
        }

        base.VisitReturnStatement(returnStatementNode);
    }

    public override void VisitBareExpressionStatement(BareExpressionStatementNode bareExpressionStatementNode)
    {
        // Should only be allowed as the last statement.
        Debug.Assert(bareExpressionStatementNode.Parent is BlockExpressionNode);
        var block = (BlockExpressionNode)bareExpressionStatementNode.Parent;
        if (block.Statements.Last() != bareExpressionStatementNode)
        {
            _diagnostics.Add(bareExpressionStatementNode.Span, DiagnosticLevel.Error, $"a bare expression can only exist as the last statement of a block");
        }

        base.VisitBareExpressionStatement(bareExpressionStatementNode);
    }

    public override void VisitAssignmentExpression(AssignmentExpressionNode assignmentExpressionNode)
    {
        // Compatibility
        var lhsSymbol = _model.LookupSymbol(assignmentExpressionNode.Lhs);
        var lhsType = _model.LookupType(assignmentExpressionNode.Lhs);

        var rhsSymbol = _model.LookupSymbol(assignmentExpressionNode.Rhs);
        var rhsType = _model.LookupType(assignmentExpressionNode.Rhs);

        if (lhsSymbol == null || rhsSymbol == null)
        {
            // Shouldn't report.
        }
        else if (lhsType == null || rhsType == null || lhsType != rhsType)
        {
            // Incompatible types.
            _diagnostics.Add(assignmentExpressionNode.Span, DiagnosticLevel.Error, $"invalid assignment because of incompatible types");
        }

        base.VisitAssignmentExpression(assignmentExpressionNode);
    }

    public override void VisitUnaryExpression(UnaryExpressionNode unaryExpressionNode)
    {
        // TODO: Should work on traits or something.

        var expressionType = _model.LookupType(unaryExpressionNode.Expression);

        switch (unaryExpressionNode.Op.UnaryOperatorTokenKind)
        {
            case UnaryOperatorTokenKind.Pos:
            case UnaryOperatorTokenKind.Neg:
                if (expressionType != BuiltInTypeSystem.Number)
                {
                    _diagnostics.Add(unaryExpressionNode.Op.Span, DiagnosticLevel.Error, $"only valid with a number type");
                }
                break;
            case UnaryOperatorTokenKind.Not:
                if (expressionType != BuiltInTypeSystem.Bool)
                {
                    _diagnostics.Add(unaryExpressionNode.Op.Span, DiagnosticLevel.Error, $"only valid with a bool type");
                }
                break;
        }

        base.VisitUnaryExpression(unaryExpressionNode);
    }

    public override void VisitBinaryExpression(BinaryExpressionNode binaryExpressionNode)
    {
        var leftType = _model.LookupType(binaryExpressionNode.Left);
        var rightType = _model.LookupType(binaryExpressionNode.Right);

        if (leftType != null || rightType != null)
        {
            // Shouldn't report.
        }
        else if (leftType != rightType)
        {
            _diagnostics.Add(binaryExpressionNode.Span, DiagnosticLevel.Error, $"incompatible types");
        }
        else
        {
            var expressionType = leftType;
            switch (binaryExpressionNode.Op.BinaryOperatorTokenKind)
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
                        _diagnostics.Add(binaryExpressionNode.Op.Span, DiagnosticLevel.Error, $"only valid with a number type");
                    }
                    break;
                case BinaryOperatorTokenKind.And:
                case BinaryOperatorTokenKind.Or:
                    if (expressionType != BuiltInTypeSystem.Bool)
                    {
                        _diagnostics.Add(binaryExpressionNode.Op.Span, DiagnosticLevel.Error, $"only valid with a bool type");
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
                        _diagnostics.Add(binaryExpressionNode.Op.Span, DiagnosticLevel.Error, $"only valid with a number type");
                    }
                    break;

                default:
                    break;
            }
        }

        base.VisitBinaryExpression(binaryExpressionNode);
    }

    public override void VisitCallExpression(CallExpressionNode callExpressionNode)
    {
        var calleeSymbol = _model.LookupSymbol(callExpressionNode.Callee);

        // Ensure calle is a function and validate args.
        if (calleeSymbol is not FunctionSymbol functionSymbol)
        {
            _diagnostics.Add(callExpressionNode.Span, DiagnosticLevel.Error, $"invocation on a non function");
        }
        else
        {
            var args = callExpressionNode.Arguments.Arguments.ToList();
            if (functionSymbol.Parameters.Length != args.Count)
            {
                _diagnostics.Add(callExpressionNode.Arguments.Span, DiagnosticLevel.Error, $"invalid invocation: wrong number of arguments");
            }
            else
            {
                for (var i = 0; i < args.Count; i++)
                {
                    var arg = args[i];
                    var param = functionSymbol.Parameters[i];
                    var argType = _model.LookupSymbol(arg);
                    if (param.Type != argType)
                    {
                        _diagnostics.Add(arg.Span, DiagnosticLevel.Error, $"incompatible types");
                    }
                }
            }
        }

        base.VisitCallExpression(callExpressionNode);
    }

    public override void VisitAccessExpression(AccessExpressionNode accessExpressionNode)
    {
        var targetSymbol = _model.LookupSymbol(accessExpressionNode.Target);
        // TODO: ?
        //targetSymbol.Members

        // TODO: Shouldn't this be TypeSymbol?
        if (targetSymbol is not NamedTypeSymbol typeSymbol)
        {
            _diagnostics.Add(accessExpressionNode.Span, DiagnosticLevel.Error, $"unallowed access");
        }
        else
        {
            // TODO: Store and use a lookup table for this.
            var matched = typeSymbol.Members.FirstOrDefault(x => x.Name?.Identifier == accessExpressionNode.Identifier.Identifier);
            if (matched == null)
            {
                _diagnostics.Add(accessExpressionNode.Identifier.Span, DiagnosticLevel.Error, $"unknown member access");
            }
        }

        base.VisitAccessExpression(accessExpressionNode);
    }

    public override void VisitThisExpression(ThisExpressionNode thisExpressionNode)
    {
        var thisType = _model.LookupType(thisExpressionNode);
        if (thisType == null)
        {
            _diagnostics.Add(thisExpressionNode.Span, DiagnosticLevel.Error, $"unknown this context");
        }

        base.VisitThisExpression(thisExpressionNode);
    }

    public override void VisitParameter(ParameterNode parameterNode)
    {
        if (parameterNode.Ty == null)
        {
            _diagnostics.Add(parameterNode.Span, DiagnosticLevel.Error, $"specifying type is required");
        }

        base.VisitParameter(parameterNode);
    }

    public override void VisitFunctionSignature(FunctionSignatureNode functionSignatureNode)
    {
        // Temp: Require return type.
        if (functionSignatureNode.ReturnTy == null)
        {
            _diagnostics.Add(functionSignatureNode.Span, DiagnosticLevel.Error, $"return type of function or method should be specified");
        }

        base.VisitFunctionSignature(functionSignatureNode);
    }

    public override void VisitIdentifierExpression(IdentifierExpressionNode identifierExpressionNode)
    {
        var symbol = _model.LookupSymbol(identifierExpressionNode);
        if (symbol == null)
        {
            _diagnostics.Add(identifierExpressionNode.Span, DiagnosticLevel.Error, $"unknown name");
        }

        base.VisitIdentifierExpression(identifierExpressionNode);
    }

    public override void VisitTy(TyNode tyNode)
    {
        var symbol = _model.LookupSymbol(tyNode);
        if (symbol == null)
        {
            _diagnostics.Add(tyNode.Span, DiagnosticLevel.Error, $"unknown type");
        }
        else if (symbol is not TypeSymbol)
        {
            _diagnostics.Add(tyNode.Span, DiagnosticLevel.Error, $"not a type");
        }

        base.VisitTy(tyNode);
    }
}
