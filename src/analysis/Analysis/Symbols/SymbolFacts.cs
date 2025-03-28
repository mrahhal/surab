namespace Surab.Analysis;

public static class SymbolFacts
{
	public static bool CanContainImpl(Symbol symbol)
	{
		if (symbol.Kind is not SymbolKind.Type)
		{
			return false;
		}
		if (((TypeSymbol)symbol).TypeKind is TypeSymbolKind.Struct)
		{
			return true;
		}
		return false;
	}
}
