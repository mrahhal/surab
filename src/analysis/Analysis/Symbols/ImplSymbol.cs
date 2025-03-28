using System.Collections.Immutable;

namespace Surab.Analysis;

/// <summary>
/// A symbol representing an implementation of a trait inside a type.
/// </summary>
public abstract class ImplSymbol : Symbol
{
	public ImplSymbol() : base(SymbolKind.Impl)
	{
	}

	// Generic trait usage is a part of the TraitType below, so  ImplSymbol does not have substitutes.
	public override Symbol OriginalDef => this;

	/// <summary>
	/// The trait type that this implements. Can be a generic.
	/// </summary>
	public abstract TypeSymbol TraitType { get; }

	/// <summary>
	/// Will always include the members declared by the trait, whether they're properly implemented or not (synthesized).
	/// </summary>
	public abstract ImmutableArray<Symbol> Members { get; }

	public Symbol? GetMember(string identifier)
	{
		// Move from bottom to top trying to match the identifier.
		for (var i = Members.Length - 1; i >= 0; i--)
		{
			var item = Members[i];
			if (item.IdentifierMatches(identifier))
			{
				return item;
			}
		}
		return null;
	}

	// Future: Reassignments/redefinitions of symbols, etc.
}

internal sealed class SrcImplSymbol : ImplSymbol
{
	private readonly ImplDeclNode _node;
	private TypeSymbol? _traitType;
	private ImmutableArray<Symbol> _members;

	public SrcImplSymbol(ImplDeclNode node)
	{
		_node = node;
	}

	public override SyntaxNode? DeclaringNode => _node;

	internal override IdentifierToken? IdentifierToken => _node.Name.Identifier;

	/// <summary>
	/// The trait type that this implements. This could be a non trait in invalid code.
	/// </summary>
	public override TypeSymbol TraitType
	{
		get
		{
			if (_traitType != null)
			{
				return _traitType;
			}
			_traitType = BuildTraitType();
			return _traitType;
		}
	}

	public override ImmutableArray<Symbol> Members
	{
		get
		{
			if (_members.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _members, BuildMembers(), default);
			}
			return _members;
		}
	}

	private TypeSymbol BuildTraitType()
	{
		return Reasoner.LookupSymbol(_node.Name).AsTypeSymbolOrUnknown();
	}

	private ImmutableArray<Symbol> BuildMembers()
	{
		// Need to expose impl members here even if they're not implemented yet. (so that it appears in intellisense etc)
		// So don't include members that aren't defined by the trait, and include missing members by synthesizing them.
		// GoToDef goes to the trait declaration if it's not implemented.

		var builder = ImmutableArray.CreateBuilder<Symbol>();

		var implMembers = BuildImplMembers();
		var traitMembers = TraitType.Members;

		foreach (var traitMember in traitMembers)
		{
			// 1. Try to find a matching implementation in implMembers.
			// 2. If we can't find it, replicate one from the traitMember.
			// (we can't use traitMember as is b/c we need the EnclosingType of that to be the correct type, and not the trait)
			if (traitMember is not FnSymbol traitFn)
			{
				throw new NotImplementedException();
			}
			var match = implMembers.OfType<FnSymbol>().FirstOrDefault(implFn => CandidateMatcher.FnsMatch(traitFn, implFn));
			if (match != null)
			{
				builder.Add(match);
			}
			else
			{
				builder.Add(new ReplicatedFnSymbol(traitFn, this));
			}
		}

		return builder.ToImmutable();
	}

	private ImmutableArray<Symbol> BuildImplMembers()
	{
		var builder = ImmutableArray.CreateBuilder<Symbol>();
		foreach (var node in _node.Members)
		{
			if (Reasoner.TryGetDeclaredSymbol(node, out var symbol))
			{
				builder.Add(symbol);
			}
		}
		return builder.ToImmutable();
	}
}
