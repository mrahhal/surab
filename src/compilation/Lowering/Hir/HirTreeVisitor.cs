using System.Diagnostics;

namespace Surab.Compilation;

/// <summary>
/// An HIR tree visitor.
/// </summary>
internal abstract class HirTreeVisitor
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
	public virtual void Visit(HirNode node)
	{
		switch (node)
		{
			case HirFile n: VisitFile(n); break;
			case HirDeclList n: ThrowInvalidNode(n); break;
			case HirFnDecl n: VisitFnDecl(n); break;
			case HirFnBody n: VisitFnBody(n); break;
			case HirStructDecl n: VisitStructDecl(n); break;
			case HirLocalDeclareStmt n: VisitLocalDeclareStmt(n); break;
			case HirReturnStmt n: VisitReturnStmt(n); break;
			case HirExprStmt n: VisitExprStmt(n); break;
			case HirStmtList n: ThrowInvalidNode(n); break;
			case HirValueLoadExpr n: VisitValueLoadExpr(n); break;
			case HirValueStoreStmt n: VisitValueStoreExpr(n); break;
			case HirFieldLoadExpr n: VisitFieldLoadExpr(n); break;
			case HirFieldStoreStmt n: VisitFieldStoreExpr(n); break;
			case HirUnaryExpr n: VisitUnaryExpr(n); break;
			case HirBinaryExpr n: VisitBinaryExpr(n); break;
			case HirCallExpr n: VisitCallExpr(n); break;
			case HirLiteralExpr n: VisitLiteralExpr(n); break;
			case HirStmtListEndingWithExpr n: VisitStmtListEndingWithExpr(n); break;
			case HirElidedDecl n: ThrowInvalidNode(n); break;
			case HirElidedStmt n: ThrowInvalidNode(n); break;

			default:
				throw new UnreachableException();
		}
	}

	public virtual void VisitFile(HirFile node)
	{
		Walk(node);
	}

	public virtual void VisitFnDecl(HirFnDecl node)
	{
		Walk(node);
	}

	public virtual void VisitFnBody(HirFnBody node)
	{
		Walk(node);
	}

	public virtual void VisitStructDecl(HirStructDecl node)
	{
		Walk(node);
	}

	public virtual void VisitLocalDeclareStmt(HirLocalDeclareStmt node)
	{
		Walk(node);
	}

	public virtual void VisitReturnStmt(HirReturnStmt node)
	{
		Walk(node);
	}

	public virtual void VisitExprStmt(HirExprStmt node)
	{
		Walk(node);
	}

	public virtual void VisitValueLoadExpr(HirValueLoadExpr node)
	{
		Walk(node);
	}

	public virtual void VisitValueStoreExpr(HirValueStoreStmt node)
	{
		Walk(node);
	}

	public virtual void VisitFieldLoadExpr(HirFieldLoadExpr node)
	{
		Walk(node);
	}

	public virtual void VisitFieldStoreExpr(HirFieldStoreStmt node)
	{
		Walk(node);
	}

	public virtual void VisitUnaryExpr(HirUnaryExpr node)
	{
		Walk(node);
	}

	public virtual void VisitBinaryExpr(HirBinaryExpr node)
	{
		Walk(node);
	}

	public virtual void VisitCallExpr(HirCallExpr node)
	{
		Walk(node);
	}

	public virtual void VisitLiteralExpr(HirLiteralExpr node)
	{
		Walk(node);
	}

	public virtual void VisitStmtListEndingWithExpr(HirStmtListEndingWithExpr node)
	{
		Walk(node);
	}

	protected void VisitList<T>(IEnumerable<T> list)
		where T : HirNode
	{
		foreach (var item in list)
		{
			Visit(item);
		}
	}

	private void ThrowInvalidNode(HirNode node)
	{
		throw new InvalidOperationException($"Unexpected node: {node.GetType().Name}");
	}

	/// <summary>
	/// Walks the node's children calling the respective Visit* method for each child.
	/// </summary>
	/// <remarks>
	/// This method is virtual so that walking can be overriden and disabled if necessary.
	/// </remarks>
	protected virtual void Walk(HirNode node)
	{
		foreach (var child in node.EnumerateChildren())
		{
			Visit(child);
		}
	}
}
