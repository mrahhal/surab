namespace Surab.Compilation;

/// <summary>
/// A lowered tree visitor.
/// </summary>
internal abstract partial class LoweredVisitor
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
    public virtual void Visit(LoweredNode node)
    {
        switch (node)
        {
            case LoweredModule n: VisitModule(n); break;
            case LoweredFunctionDeclaration n: VisitFunctionDeclaration(n); break;
            case LoweredTypeDeclaration n: VisitTypeDeclaration(n); break;
            case LoweredVariableStatement n: VisitVariableStatement(n); break;
            case LoweredReturnStatement n: VisitReturnStatement(n); break;
            case LoweredExpressionStatement n: VisitExpressionStatement(n); break;
            case LoweredStatementList n: VisitStatementList(n); break;
            case LoweredVariableLoadExpression n: VisitVariableLoadExpression(n); break;
            //case LoweredFieldLoadExpression n: VisitFieldLoadExpression(n); break;
            case LoweredVariableStoreStatement n: VisitVariableStoreExpression(n); break;
            //case LoweredFieldStoreStatement n: VisitFieldStoreExpression(n); break;
            case LoweredUnaryExpression n: VisitUnaryExpression(n); break;
            case LoweredBinaryExpression n: VisitBinaryExpression(n); break;
            case LoweredCallExpression n: VisitCallExpression(n); break;
            case LoweredLiteralExpression n: VisitLiteralExpression(n); break;

            default:
                break;
        }
    }

    public virtual void VisitModule(LoweredModule node)
    {
        Walk(node);
    }

    public virtual void VisitFunctionDeclaration(LoweredFunctionDeclaration node)
    {
        Walk(node);
    }

    public virtual void VisitTypeDeclaration(LoweredTypeDeclaration node)
    {
        Walk(node);
    }

    public virtual void VisitVariableStatement(LoweredVariableStatement node)
    {
        Walk(node);
    }

    public virtual void VisitReturnStatement(LoweredReturnStatement node)
    {
        Walk(node);
    }

    public virtual void VisitExpressionStatement(LoweredExpressionStatement node)
    {
        Walk(node);
    }

    public virtual void VisitStatementList(LoweredStatementList node)
    {
        Walk(node);
    }

    public virtual void VisitVariableLoadExpression(LoweredVariableLoadExpression node)
    {
        Walk(node);
    }

    //public virtual void VisitFieldLoadExpression(LoweredFieldLoadExpression node)
    //{
    //    Walk(node);
    //}

    public virtual void VisitVariableStoreExpression(LoweredVariableStoreStatement node)
    {
        Walk(node);
    }

    //public virtual void VisitFieldStoreExpression(LoweredFieldStoreStatement node)
    //{
    //    Walk(node);
    //}

    public virtual void VisitUnaryExpression(LoweredUnaryExpression node)
    {
        Walk(node);
    }

    public virtual void VisitBinaryExpression(LoweredBinaryExpression node)
    {
        Walk(node);
    }

    public virtual void VisitCallExpression(LoweredCallExpression node)
    {
        Walk(node);
    }

    public virtual void VisitLiteralExpression(LoweredLiteralExpression node)
    {
        Walk(node);
    }

    protected void VisitList<T>(IEnumerable<T> list)
        where T : LoweredNode
    {
        foreach (var item in list)
        {
            Visit(item);
        }
    }

    /// <summary>
    /// Walks the node's children calling the respective Visit* method for each child.
    /// </summary>
    /// <remarks>
    /// This method is virtual so that walking can be overriden and disabled if necessary.
    /// </remarks>
    protected virtual void Walk(LoweredNode node)
    {
        foreach (var child in node.EnumerateChildren())
        {
            Visit(child);
        }
    }
}
