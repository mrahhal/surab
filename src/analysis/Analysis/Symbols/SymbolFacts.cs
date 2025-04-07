using System.Diagnostics.CodeAnalysis;

namespace Surab.Analysis;

public static class SymbolFacts
{
	public static bool IsType([NotNullWhen(true)] Symbol? symbol)
	{
		return symbol?.Kind is SymbolKind.Type;
	}

	public static bool IsMethod([NotNullWhen(true)] Symbol? symbol)
	{
		return symbol?.Kind is SymbolKind.Fn && ((FnSymbol)symbol).IsMethod;
	}

	public static bool IsInitializer([NotNullWhen(true)] Symbol? symbol)
	{
		return symbol?.Kind is SymbolKind.Fn && ((FnSymbol)symbol).IsInitializerMethod;
	}

	public static bool IsStructType([NotNullWhen(true)] Symbol? symbol)
	{
		return
			symbol is TypeSymbol typeSymbol &&
			typeSymbol.TypeKind is TypeSymbolKind.Struct;
	}

	public static bool IsClassType([NotNullWhen(true)] Symbol? symbol)
	{
		return false;
		// TODO:
		//return
		//	symbol is TypeSymbol typeSymbol &&
		//	typeSymbol.TypeKind is TypeSymbolKind.Class;
	}

	public static bool IsTraitType([NotNullWhen(true)] Symbol? symbol)
	{
		return
			symbol is TypeSymbol typeSymbol &&
			typeSymbol.TypeKind is TypeSymbolKind.Trait;
	}

	public static bool IsImpl([NotNullWhen(true)] Symbol? symbol)
	{
		return symbol?.Kind is SymbolKind.Impl;
	}

	public static bool CanContainIndirectMembers(Symbol symbol)
	{
		return IsStructType(symbol) || IsClassType(symbol);
	}

	public static bool IsAllowedInTrait(Symbol symbol)
	{
		return IsMethod(symbol) && !IsInitializer(symbol);
	}

	public static bool IsFieldInAllowedContext(FieldSymbol symbol)
	{
		return symbol.EnclosingType.TypeKind is TypeSymbolKind.Struct;
	}

	public static bool IsFnInAllowedContext(FnSymbol symbol)
	{
		if (symbol.IsInitializerMethod)
		{
			return IsStructType(symbol.Parent) || IsClassType(symbol.Parent);
		}

		if (symbol.IsMethod)
		{
			return
				IsStructType(symbol.Parent) || IsClassType(symbol.Parent) ||
				IsTraitType(symbol.Parent) ||
				IsImpl(symbol.Parent);
		}

		return true;
	}
}
