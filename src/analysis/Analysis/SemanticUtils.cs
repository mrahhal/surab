namespace Surab.Analysis;

public static class SemanticUtils
{
	public static IEnumerable<LocalSymbol> CollectLocals(
		Reasoner reasoner,
		SyntaxNode node)
	{
		foreach (var n in node.DescendantNodes())
		{
			if (reasoner.TryGetDeclaredSymbol(n, out var symbol) &&
				symbol is LocalSymbol localSymbol)
			{
				yield return localSymbol;
			}
		}
	}
}
