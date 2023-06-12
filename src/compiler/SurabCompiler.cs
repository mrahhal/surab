using System.Diagnostics;
using System.Text.Json;
using Surab.Analyzer;

namespace Surab.Compiler;

public sealed class SurabCompiler : Visitor
{
    private readonly SemanticModel _model;
    private readonly IRModule _ir;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    private SurabCompiler(SemanticModel model)
    {
        _model = model;
        _ir = new();
    }

    public static string Compile(SemanticModel model)
    {
        var compiler = new SurabCompiler(model);
        compiler.Compile();
        return Serialize(compiler._ir);
    }

    private static string Serialize(IRModule ir)
    {
        return JsonSerializer.Serialize(ir, _jsonOptions);
    }

    private void Compile()
    {
        Visit(_model.Tree.Root);
    }

    public override void VisitFunctionDeclaration(FunctionDeclarationNode functionDeclarationNode)
    {
        var (locals, code) = CompileFunction(functionDeclarationNode.Signature, functionDeclarationNode.Body);
        _ir.Functions.Add(new IRFunction
        {
            Name = functionDeclarationNode.Signature.Identifier.Identifier,
            Locals = locals,
            Code = code,
        });

        base.VisitFunctionDeclaration(functionDeclarationNode);
    }

    private (List<IRLocal> locals, byte[] code) CompileFunction(
        FunctionSignatureNode signature,
        BlockExpressionNode body)
    {
        return FunctionCompiler.Compile(_model, signature, body);
    }
}

internal sealed class FunctionCompiler : Visitor
{
    private readonly SemanticModel _model;
    private readonly FunctionSignatureNode _signature;
    private readonly BlockExpressionNode _body;

    private readonly List<IRLocal> _locals = new();
    private readonly Dictionary<Symbol, int /* offset */> _localToOffsetMap = new();
    private readonly Emitter _emitter = new();

    public FunctionCompiler(
        SemanticModel model,
        FunctionSignatureNode signature,
        BlockExpressionNode body)
    {
        _model = model;
        _signature = signature;
        _body = body;
    }

    public static (List<IRLocal> locals, byte[] code) Compile(
        SemanticModel model,
        FunctionSignatureNode signature,
        BlockExpressionNode body)
    {
        var c = new FunctionCompiler(model, signature, body);
        c.Compile();
        return (c._locals, c._emitter.Chunk);
    }

    private void Compile()
    {
        Visit(_body);
    }

    public override void VisitExpressionStatement(ExpressionStatementNode expressionStatementNode)
    {
        Walk(expressionStatementNode);

        // Discard the value.
        _emitter.Emit(OpCode.pop);
    }

    public override void VisitLetStatement(LetStatementNode letStatementNode)
    {
        if (_model.TryGetDeclaredSymbol(letStatementNode, out var symbol) && symbol is LocalSymbol localSymbol)
        {
            var offset = _localToOffsetMap.Count;
            var local = new IRLocal
            {
                // TODO: Avoid suppressions.
                Name = localSymbol.Name!.Identifier,
                Ty = localSymbol.Type!.Name!.Identifier,
            };
            _locals.Add(local);
            _localToOffsetMap[localSymbol] = offset;

            Visit(letStatementNode.Initializer);

            _emitter.Emit(OpCode.stloc, offset);
        }
    }

    public override void VisitCallExpression(CallExpressionNode callExpressionNode)
    {
        // Built in support for a function called "print".
        if (callExpressionNode.Callee is IdentifierExpressionNode identifierExpressionNode &&
            identifierExpressionNode.Identifier.Identifier == "print")
        {
            Debug.Assert(callExpressionNode.Arguments.Arguments.Count() == 1);
            _emitter.Emit(OpCode.print);
        }

        // TODO: Proper support for resolving the function call.

        //Walk(callExpressionNode);
    }

    public override void VisitBinaryExpression(BinaryExpressionNode binaryExpressionNode)
    {
        Visit(binaryExpressionNode.Left);
        Visit(binaryExpressionNode.Right);

        switch (binaryExpressionNode.Op.BinaryOperatorTokenKind)
        {
            case BinaryOperatorTokenKind.Add:
                _emitter.Emit(OpCode.add);
                break;
            case BinaryOperatorTokenKind.Sub:
                _emitter.Emit(OpCode.sub);
                break;
            // TODO: Missing
            default:
                throw new NotImplementedException();
        }
    }

    public override void VisitAssignmentExpression(AssignmentExpressionNode assignmentExpressionNode)
    {
        // Walk instead of Visit because we're dealing with the outermost expression represented by Lhs in this method.
        Walk(assignmentExpressionNode.Lhs);
        Visit(assignmentExpressionNode.Rhs);

        switch (assignmentExpressionNode.Lhs)
        {
            //case AccessExpressionNode accessExpressionNode:
            //    break;
            case IdentifierExpressionNode identifierExpressionNode:
                var local = _model.LookupSymbol(identifierExpressionNode);
                Debug.Assert(local != null);
                var offset = _localToOffsetMap[local];
                _emitter.Emit(OpCode.stloc, offset);

                break;
            default:
                // TODO: Add to SemanticAnalyzer: Invalid target of assignment.
                Debug.Assert(false, "Should not be here.");
                throw new NotImplementedException();
        }
    }

    public override void VisitIdentifierExpression(IdentifierExpressionNode identifierExpressionNode)
    {
        // We'll only be here if this is a definite load.

        var target = _model.LookupSymbol(identifierExpressionNode);
        Debug.Assert(target != null);
        // TODO: Local is not the only possibility here.
        var offset = _localToOffsetMap[target];

        _emitter.Emit(OpCode.ldloc, offset);
    }

    public override void VisitLiteralExpression(LiteralExpressionNode literalExpressionNode)
    {
        _emitter.Emit(OpCode.ldlit);

        switch (literalExpressionNode.Literal.LiteralTokenKind)
        {
            case LiteralTokenKind.Number:
                Debug.Assert(literalExpressionNode.Literal.TryGetNumberValue(out var value));
                // TODO: Deal with double?
                _emitter.Emit((int)value);
                break;
            // TODO: Missing
            default:
                // TODO: Add to SemanticAnalyzer:
                Debug.Assert(false, "Should not be here.");
                throw new NotImplementedException();
        }
    }
}
