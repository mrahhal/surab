namespace Surab.Analyzer;

/// <summary>
/// Represents a type that can be visited by an <see cref="IVisitor"/>.
/// </summary>
public interface IVisitable
{
    /// <summary>
    /// Calls the Visit* method on the visitor that is respective to the current type.
    /// </summary>
    void Accept(IVisitor visitor);
}

/// <summary>
/// An AST visitor.
/// </summary>
public partial interface IVisitor
{
    // Visit*
    // ======
    // You can override these methods to implement the logic you want.
    // Overriding a visit method disables walking into the children of the node.
    // You can call the `Walk` method to use the default logic of walking after processing the node if
    // that's the desirable behavior.

    /// <summary>
    /// Visits the given node. Calls the respective concrete Visit* method.
    /// </summary>
    public void Visit(SyntaxNode node)
    {
        node.Accept(this);
    }

    /// <summary>
    /// Visits the given token. Calls the respective concrete Visit* method.
    /// </summary>
    public void Visit(SyntaxToken token)
    {
        token.Accept(this);
    }

    /// <summary>
    /// Walks the node's children (child tokens first, then child nodes) calling the respective Visit* method for each child.
    /// </summary>
    public void Walk(SyntaxNode node)
    {
        foreach (var child in node.ChildTokens)
        {
            child?.Accept(this);
        }
        foreach (var child in node.ChildNodes)
        {
            child?.Accept(this);
        }
    }

    // Tokens

    public void VisitSimpleToken(SimpleToken simpleToken) { }

    public void VisitUnaryOperatorToken(UnaryOperatorToken unaryOperatorToken) { }

    public void VisitBinaryOperatorToken(BinaryOperatorToken binaryOperatorToken) { }

    public void VisitIdentifierToken(IdentifierToken identifierToken) { }

    public void VisitLiteralToken(LiteralToken literalToken) { }

    public void VisitSkippedToken(SkippedToken skippedToken) { }

    // Rest of the impl is in the generated Visitor.g.cs file.
}
