using Surab.Analysis;

namespace Surab.Compilation;

internal static class LoweringUtils
{
	public static List<LoweredVariableStmt> CollectLocalVariableStmts(LoweredNode node)
	{
		return LocalVariableStmtsCollector.Collect(node);
	}

	// Will skip returning container nodes such as LoweredStmtList and enumerate the children directly.
	public static IEnumerable<LoweredNode> EnumerateUnwrappedDescendants(LoweredNode node, bool includeSelf = false)
	{
		if (includeSelf && node is not LoweredStmtList)
		{
			yield return node;
			yield break;
		}

		foreach (var child in node.EnumerateChildren())
		{
			foreach (var c in EnumerateUnwrappedDescendants(child, includeSelf: true))
			{
				yield return c;
			}
		}
	}
}

file class LocalVariableStmtsCollector : LoweredTreeVisitor
{
	private readonly List<LoweredVariableStmt> _stmts = [];

	public static List<LoweredVariableStmt> Collect(LoweredNode node)
	{
		var collector = new LocalVariableStmtsCollector();
		collector.Visit(node);
		return collector._stmts;
	}

	public override void VisitVariableStmt(LoweredVariableStmt node)
	{
		_stmts.Add(node);
	}
}
