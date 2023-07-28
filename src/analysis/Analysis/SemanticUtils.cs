namespace Surab.Analysis;

public static class SemanticUtils
{
    public static IEnumerable<LocalSymbol> CollectLocals(
        SemanticModel model,
        SyntaxNode node)
    {
        foreach (var n in node.DescendantNodes())
        {
            if (model.TryGetDeclaredSymbol(n, out var symbol) &&
                symbol is LocalSymbol localSymbol)
            {
                yield return localSymbol;
            }
        }
    }
}
