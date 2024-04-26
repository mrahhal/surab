using System.Collections.Immutable;

namespace Surab.Analysis;

public abstract class AliasTypeSymbol : TypeSymbol, IPossiblyGenericSymbol
{
	private string? _name;

	public override TypeSymbolKind TypeKind => TypeSymbolKind.Alias;

	public override Symbol OriginalDef => this;

	public override string Name
	{
		get
		{
			return _name ??= GenericHelper.CreateName(this, Identifier);
		}
	}

	public abstract ImmutableArray<GenericParamTypeSymbol> GenericParams { get; }

	public abstract ImmutableArray<TypeSymbol> GenericArgs { get; }

	public abstract TypeSymbol TargetType { get; }

	public override TypeSymbol UnwrapTypeAliases()
	{
		return TargetType.UnwrapTypeAliases();
	}

	// REVIEW: Is this what we should really do? Shouldn't we depend on unwrapping instead?

	public override ImmutableArray<Symbol> Members => TargetType.Members;

	internal override AliasTypeSymbol SubstituteCore(SubstitutionMap smap)
	{
		return new SubstitutedAliasTypeSymbol(this, [], smap);
	}

	Symbol IPossiblyGenericSymbol.SubstituteConstructedGeneric(SubstitutionMap smap, IReadOnlyList<TypeSymbol> genericArgs)
	{
		return new SubstitutedAliasTypeSymbol(this, genericArgs, smap);
	}
}

public class SrcAliasTypeSymbol : AliasTypeSymbol
{
	private readonly AliasDeclNode _node;
	private ImmutableArray<GenericParamTypeSymbol> _genericParams;
	private TypeSymbol? _targetType;

	internal SrcAliasTypeSymbol(AliasDeclNode node)
	{
		_node = node;
	}

	public override AliasDeclNode DeclaringNode => _node;

	internal override IdentifierToken? IdentifierToken => _node.AsIdentifier.Name.Identifier;

	public override ImmutableArray<GenericParamTypeSymbol> GenericParams
	{
		get
		{
			if (_genericParams.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _genericParams, BuildGenericParams(), default);
			}
			return _genericParams;
		}
	}

	public override ImmutableArray<TypeSymbol> GenericArgs => [];

	private ImmutableArray<GenericParamTypeSymbol> BuildGenericParams()
	{
		return GetGenericParamsList()?
			.GenericParams
			.Select(Reasoner.GetDeclaredSymbol)
			.ToImmutableArray() ?? [];
	}

	private GenericParamsListNode? GetGenericParamsList() => _node.AsIdentifier.Name.GenericParamsList;

	public override TypeSymbol TargetType
	{
		get
		{
			return _targetType ??= Reasoner.ResolveType(DeclaringNode.TargetType).Type;
		}
	}
}

internal class SubstitutedAliasTypeSymbol : AliasTypeSymbol
{
	private readonly AliasTypeSymbol _original;
	private readonly SubstitutionMap _smap;

	private TypeSymbol? _targetType;

	public SubstitutedAliasTypeSymbol(
		AliasTypeSymbol original,
		IEnumerable<TypeSymbol> genericArgs,
		SubstitutionMap smap)
	{
		_original = original;
		SetParent(original.Parent);
		GenericArgs = genericArgs.ToImmutableArray();
		_smap = smap.CreateSub();
	}

	public override Symbol OriginalDef => _original.OriginalDef;

	public override TypeSymbolKind TypeKind => _original.TypeKind;

	public override SyntaxNode? DeclaringNode => _original.DeclaringNode;

	internal override IdentifierToken? IdentifierToken => _original.IdentifierToken;

	public override ImmutableArray<GenericParamTypeSymbol> GenericParams => _original.GenericParams;

	public override ImmutableArray<TypeSymbol> GenericArgs { get; }

	public override TypeSymbol TargetType
	{
		get
		{
			return _targetType ??= (TypeSymbol)_original.TargetType.SubstituteWithHierarchyPatching(_smap);
		}
	}
}
