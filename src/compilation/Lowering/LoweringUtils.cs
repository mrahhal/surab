using Surab.Analysis;

namespace Surab.Compilation;

internal static class LoweringUtils
{
	public static List<LoweredVariableDeclareStmt> CollectLocalVariableDeclareStmts(LoweredNode node)
	{
		return LocalVariableDeclareStmtsCollector.Collect(node);
	}

	/// <summary>
	/// Skips container nodes such as <see cref="LoweredStmtList"/> and returns an enumeration of the children directly.
	/// </summary>
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

file class LocalVariableDeclareStmtsCollector : LoweredTreeVisitor
{
	private readonly List<LoweredVariableDeclareStmt> _stmts = [];

	public static List<LoweredVariableDeclareStmt> Collect(LoweredNode node)
	{
		var collector = new LocalVariableDeclareStmtsCollector();
		collector.Visit(node);
		return collector._stmts;
	}

	public override void VisitVariableDeclareStmt(LoweredVariableDeclareStmt node)
	{
		_stmts.Add(node);
	}
}
