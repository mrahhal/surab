using System.Collections.Immutable;

namespace Surab.Analysis;

public abstract class GenericParamTypeSymbol : TypeSymbol
{
	public override TypeSymbolKind TypeKind => TypeSymbolKind.GenericParam;

	// TODO: If the generic type has constraints, members will be available.
	public override ImmutableArray<Symbol> Members => [];

	internal override TypeSymbol SubstituteCore(SubstitutionMap smap)
	{
		// This is kind of the *base* case in the substitution.
		// For other symbols, we return a specialized substituted symbol impl, whereas here we directly replace this type.
		return (TypeSymbol)smap.Replace(this);
	}
}

public sealed class SrcGenericParamTypeSymbol : GenericParamTypeSymbol
{
	private readonly GenericParamNode _node;

	internal SrcGenericParamTypeSymbol(GenericParamNode node, int index)
	{
		_node = node;
		Index = index;
	}

	public override Symbol OriginalDef => this;

	public override GenericParamNode DeclaringNode => _node;

	internal override IdentifierToken IdentifierToken => _node.Identifier;

	public int Index { get; }
}
