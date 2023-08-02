using Surab.Analysis;

namespace Surab.Compilation;

internal static class LoweringUtils
{
    public static List<LoweredVariableStatement> CollectLocalVariableStatements(LoweredNode node)
    {
        return LocalVariableStatementsCollector.Collect(node);
    }

    // Will skip returning container nodes such as LoweredStatementList and enumerate the children directly.
    public static IEnumerable<LoweredNode> EnumerateUnwrappedDescendants(LoweredNode node, bool includeSelf = false)
    {
        if (includeSelf && node is not LoweredStatementList)
        {
            yield return node;
            yield break;
        }

        foreach (var child in node.EnumerateChildren())
        {
            if (child is not LoweredStatementList)
            {
                yield return child;
            }
            else
            {
                foreach (var c in EnumerateUnwrappedDescendants(child))
                {
                    yield return c;
                }
            }
        }
    }
}

file class LocalVariableStatementsCollector : LoweredVisitor
{
    private readonly List<LoweredVariableStatement> _statements = new();

    public static List<LoweredVariableStatement> Collect(LoweredNode node)
    {
        var collector = new LocalVariableStatementsCollector();
        collector.Visit(node);
        return collector._statements;
    }

    public override void VisitVariableStatement(LoweredVariableStatement node)
    {
        // TODO: Is this always true?
        if (node.Symbol is LocalSymbol)
        {
            _statements.Add(node);
        }
    }
}
