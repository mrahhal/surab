namespace Surab.Analysis;

/// <summary>
/// Represents a type that can be visited by a <see cref="Visitor"/>.
/// </summary>
public interface IVisitable
{
    /// <summary>
    /// Calls the Visit* method on the visitor that is respective to the current type.
    /// </summary>
    void Accept(Visitor visitor);
}

/// <summary>
/// An AST visitor.
/// </summary>
public abstract partial class Visitor
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
    public virtual void Visit(SyntaxNode node)
    {
        node.Accept(this);
    }

    /// <summary>
    /// Visits the given token. Calls the respective concrete Visit* method.
    /// </summary>
    public virtual void Visit(SyntaxToken token)
    {
        token.Accept(this);
    }

    /// <summary>
    /// Walks the node's children (child tokens first, then child nodes) calling the respective Visit* method for each child.
    /// </summary>
    /// <remarks>
    /// This method is virtual so that walking can be overriden and disabled if necessary.
    /// </remarks>
    protected virtual void Walk(SyntaxNode node)
    {
        foreach (var child in node.ChildTokens)
        {
            if (child != null)
            {
                Visit(child);
            }
        }
        if (node is DeclNode declNode && declNode.Attrs != null)
        {
            Visit(declNode.Attrs);
        }
        foreach (var child in node.ChildNodes)
        {
            if (child != null)
            {
                Visit(child);
            }
        }
    }

    // Tokens

    public virtual void VisitSimpleToken(SimpleToken simpleToken) { }

    public virtual void VisitUnaryOperatorToken(UnaryOperatorToken unaryOperatorToken) { }

    public virtual void VisitBinaryOperatorToken(BinaryOperatorToken binaryOperatorToken) { }

    public virtual void VisitIdentifierToken(IdentifierToken identifierToken) { }

    public virtual void VisitLiteralToken(LiteralToken literalToken) { }

    public virtual void VisitSkippedToken(SkippedToken skippedToken) { }

    // Rest of the impl is in the generated Visitor.g.cs file.
}
