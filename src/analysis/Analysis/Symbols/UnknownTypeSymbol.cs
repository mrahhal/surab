using System.Collections.Immutable;

namespace Surab.Analysis;

/// <summary>
/// Represents an unknown type symbol.
/// </summary>
public abstract class UnknownTypeSymbol : TypeSymbol
{
	internal UnknownTypeSymbol()
	{
	}

	public override TypeSymbolKind TypeKind => TypeSymbolKind.Unknown;

	public override TypeSymbol OriginalDef => this;

	public override string Name => "_?_";

	public override SyntaxNode? DeclaringNode => null;

	public override ImmutableArray<Symbol> Members => [];

	public override ImmutableArray<Symbol> AccessibleMembers => [];

	internal override IdentifierToken? IdentifierToken => null;

	public static UnknownTypeSymbol Create() => new SimpleUnknownTypeSymbol();

	public static UnknownTypeSymbol CreateInvalidClosedGeneric(
		Symbol symbol,
		IEnumerable<TypeSymbol> genericArgs) => new InvalidClosedGenericUnknownTypeSymbol(symbol, genericArgs);

	internal override TypeSymbol SubstituteCore(SubstitutionMap smap)
	{
		throw new NotImplementedException();
	}
}

public sealed class SimpleUnknownTypeSymbol : UnknownTypeSymbol
{
}

public sealed class InvalidClosedGenericUnknownTypeSymbol : UnknownTypeSymbol
{
	public InvalidClosedGenericUnknownTypeSymbol(
		Symbol symbol,
		IEnumerable<TypeSymbol> genericArgs)
	{
		Symbol = symbol;
		GenericArgs = genericArgs.ToImmutableArray();
	}

	public Symbol Symbol { get; }

	public ImmutableArray<TypeSymbol> GenericArgs { get; }
}
