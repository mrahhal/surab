using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Surab.Analysis;

/// <summary>
/// A symbol representing an implementation of a trait inside a type.
/// </summary>
public abstract class ImplSymbol : Symbol
{
	public ImplSymbol() : base(SymbolKind.Impl)
	{
	}

	/// <summary>
	/// The raw children of the impl block. Can include invalid members.
	/// </summary>
	public abstract override IEnumerable<Symbol> Children { get; }

	// Generic trait usage is a part of the TraitType below, so  ImplSymbol does not have substitutes.
	public override Symbol OriginalDef => this;

	/// <summary>
	/// The trait type that this implements. Can be a generic.
	/// </summary>
	public abstract TypeSymbol TraitType { get; }

	/// <summary>
	/// Processed members declared by the trait whether they're properly implemented or not (replicated).
	/// </summary>
	public abstract ImmutableArray<ImplSymbolProcessedMember> ProcessedMembers { get; }

	/// <summary>
	/// Contains the impl members that correspond to a trait match, or the trait member itself
	/// if no implemented member exists.
	/// </summary>
	public IReadOnlyList<Symbol> Members
	{
		get
		{
			return [.. ProcessedMembers.Select(pm => pm.ImplSymbol)];
		}
	}

	public Symbol GetSingleMember(string identifier)
	{
		return GetMembers(identifier).Single();
	}

	public IReadOnlyList<Symbol> GetMembers(string identifier)
	{
		var list = new List<Symbol>();
		foreach (var member in Members)
		{
			if (member.IdentifierMatches(identifier))
			{
				list.Add(member);
			}
		}
		return list;
	}

	// Future: Reassignments/redefinitions of symbols, etc.
}

public class ImplSymbolProcessedMember(Symbol implSymbol, Symbol? matchingTraitSymbol)
{
	/// <summary>
	/// Either the actual implemented member or a replicated version of the trait member.
	/// </summary>
	public Symbol ImplSymbol { get; } = implSymbol;

	/// <summary>
	/// The matching symbol from the trait if any.
	/// </summary>
	public Symbol? MatchingTraitSymbol { get; } = matchingTraitSymbol;

	[MemberNotNullWhen(true, nameof(MatchingTraitSymbol))]
	public bool HasMatch => MatchingTraitSymbol != null;
}

internal sealed class SrcImplSymbol : ImplSymbol
{
	private readonly ImplDeclNode _node;
	private TypeSymbol? _traitType;
	private ImmutableArray<Symbol> _children;
	private ImmutableArray<ImplSymbolProcessedMember> _processedMembers;

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

	public override IEnumerable<Symbol> Children
	{
		get
		{
			if (_children.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _children, BuildChildren(), default);
			}
			return _children;
		}
	}

	public override ImmutableArray<ImplSymbolProcessedMember> ProcessedMembers
	{
		get
		{
			if (_processedMembers.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _processedMembers, BuildProcessedMembers(), default);
			}
			return _processedMembers;
		}
	}

	private TypeSymbol BuildTraitType()
	{
		return Reasoner.LookupSymbol(_node.Name).AsTypeSymbolOrUnknown();
	}

	private ImmutableArray<Symbol> BuildChildren()
	{
		var builder = ImmutableArray.CreateBuilder<Symbol>();
		foreach (var node in _node.Decls)
		{
			if (Reasoner.TryGetDeclaredSymbol(node, out var symbol))
			{
				builder.Add(symbol);
			}
		}
		return builder.ToImmutable();
	}

	private ImmutableArray<ImplSymbolProcessedMember> BuildProcessedMembers()
	{
		// Need to expose impl members here even if they're not implemented yet. (so that it appears in intellisense etc)
		// So don't include members that aren't defined by the trait, and include missing members by synthesizing them.
		// GoToDef goes to the trait declaration if it's not implemented.

		var builder = ImmutableArray.CreateBuilder<ImplSymbolProcessedMember>();

		var children = Children;
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
			var match = children.OfType<FnSymbol>().FirstOrDefault(implFn => CandidateMatcher.FnsMatch(traitFn, implFn));
			if (match != null)
			{
				builder.Add(new ImplSymbolProcessedMember(match, traitMember));
			}
			else
			{
				builder.Add(new ImplSymbolProcessedMember(new ReplicatedFnSymbol(traitFn, this), null));
			}
		}

		return builder.ToImmutable();
	}
}
