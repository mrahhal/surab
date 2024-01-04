namespace Surab.Compilation;

/// <summary>
/// A lowered tree visitor.
/// </summary>
internal abstract partial class LoweredTreeVisitor
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
			case LoweredFile n: VisitFile(n); break;
			case LoweredFnDecl n: VisitFnDecl(n); break;
			case LoweredTypeDecl n: VisitTypeDecl(n); break;
			case LoweredVariableStmt n: VisitVariableStmt(n); break;
			case LoweredReturnStmt n: VisitReturnStmt(n); break;
			case LoweredExprStmt n: VisitExprStmt(n); break;
			case LoweredObjectAllocStmt n: VisitObjectAllocStmt(n); break;
			case LoweredStmtList n: VisitStmtList(n); break;
			case LoweredVariableLoadExpr n: VisitVariableLoadExpr(n); break;
			case LoweredVariableStoreStmt n: VisitVariableStoreExpr(n); break;
			case LoweredFieldLoadExpr n: VisitFieldLoadExpr(n); break;
			case LoweredFieldStoreStmt n: VisitFieldStoreExpr(n); break;
			case LoweredThisLoadExpr n: VisitThisLoadExpr(n); break;
			case LoweredUnaryExpr n: VisitUnaryExpr(n); break;
			case LoweredBinaryExpr n: VisitBinaryExpr(n); break;
			case LoweredCallExpr n: VisitCallExpr(n); break;
			case LoweredLiteralExpr n: VisitLiteralExpr(n); break;
			case LoweredStmtListEndingWithExpr n: VisitStmtListEndingWithExpr(n); break;
			case LoweredSymbolLoadExpr n: VisitSymbolLoadExpr(n); break;

			default:
				break;
		}
	}

	public virtual void VisitFile(LoweredFile node)
	{
		Walk(node);
	}

	public virtual void VisitFnDecl(LoweredFnDecl node)
	{
		Walk(node);
	}

	public virtual void VisitTypeDecl(LoweredTypeDecl node)
	{
		Walk(node);
	}

	public virtual void VisitVariableStmt(LoweredVariableStmt node)
	{
		Walk(node);
	}

	public virtual void VisitReturnStmt(LoweredReturnStmt node)
	{
		Walk(node);
	}

	public virtual void VisitExprStmt(LoweredExprStmt node)
	{
		Walk(node);
	}

	public virtual void VisitObjectAllocStmt(LoweredObjectAllocStmt node)
	{
		Walk(node);
	}

	public virtual void VisitStmtList(LoweredStmtList node)
	{
		Walk(node);
	}

	public virtual void VisitVariableLoadExpr(LoweredVariableLoadExpr node)
	{
		Walk(node);
	}

	public virtual void VisitVariableStoreExpr(LoweredVariableStoreStmt node)
	{
		Walk(node);
	}

	public virtual void VisitFieldLoadExpr(LoweredFieldLoadExpr node)
	{
		Walk(node);
	}

	public virtual void VisitFieldStoreExpr(LoweredFieldStoreStmt node)
	{
		Walk(node);
	}

	public virtual void VisitThisLoadExpr(LoweredThisLoadExpr node)
	{
		Walk(node);
	}

	public virtual void VisitUnaryExpr(LoweredUnaryExpr node)
	{
		Walk(node);
	}

	public virtual void VisitBinaryExpr(LoweredBinaryExpr node)
	{
		Walk(node);
	}

	public virtual void VisitCallExpr(LoweredCallExpr node)
	{
		Walk(node);
	}

	public virtual void VisitLiteralExpr(LoweredLiteralExpr node)
	{
		Walk(node);
	}

	public virtual void VisitStmtListEndingWithExpr(LoweredStmtListEndingWithExpr node)
	{
		Walk(node);
	}

	public virtual void VisitSymbolLoadExpr(LoweredSymbolLoadExpr node)
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
