namespace Surab.Analysis;

/// <summary>
/// Represents a type that can be visited by a <see cref="SyntaxTreeVisitor"/>.
/// </summary>
public interface ISyntaxTreeVisitable
{
	/// <summary>
	/// Calls the relevant Visit* method on the visitor.
	/// </summary>
	void Accept(SyntaxTreeVisitor visitor);
}

/// <summary>
/// A syntax tree visitor.
/// </summary>
public abstract partial class SyntaxTreeVisitor
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
			Visit(child);
		}

		foreach (var child in node.ChildNodes)
		{
			Visit(child);
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
