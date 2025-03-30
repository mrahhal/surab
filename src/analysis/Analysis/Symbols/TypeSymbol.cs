using System.Collections.Immutable;
using System.Diagnostics;

namespace Surab.Analysis;

public enum TypeSymbolKind
{
	Unknown = 0,
	GenericParam,
	Fn,
	Ptr,
	Struct,
	Trait,
	Alias,
}

public enum KnownTypeTag
{
	@void,

	i8,
	i16,
	i32,
	i64,
	isize,

	u8,
	u16,
	u32,
	u64,
	usize,

	f32,
	f64,

	@bool,

	@char,
	@string,
}

//public enum AccessMode
//{
//	Any,
//	Instance, // Access through an instance. e.g. `variable.field`
//	Type, // Access through a type. e.g. `Type.Fn()`
//}

/// <summary>
/// Represents a type symbol. Can also be unnamed or synthesized.
/// </summary>
/// <remarks>
/// <see cref="IsAssignableFrom(TypeSymbol?)"/> should be used instead of referential equality.
/// </remarks>
public abstract class TypeSymbol : Symbol
{
	protected TypeSymbol() : base(SymbolKind.Type)
	{
	}

	public abstract TypeSymbolKind TypeKind { get; }

	public override abstract TypeSymbol OriginalDef { get; }

	public KnownTypeTag? KnownTypeTag { get; internal set; }

	/// <summary>
	/// Members include fields, initializers, methods, nested declarations, impl blocks, etc.
	/// It does not include non-direct symbols such as methods inside an impl block.
	/// To find accessible symbols including non-direct ones, use <see cref="LookupSingleAccessibleSymbol"/>.
	/// </summary>
	public abstract ImmutableArray<Symbol> Members { get; }

	/// <summary>
	/// This only includes direct symbols that have identifiable names (impl symbols are not returned).
	/// </summary>
	private Symbol? GetDirectIdentifiableMember(string identifier)
	{
		// REVIEW: Some duplication between symbols and scopes here (and GetCreationMember, and ImplSymbol.GetMember)?

		// Move from bottom to top trying to match the identifier.
		for (var i = Members.Length - 1; i >= 0; i--)
		{
			var member = Members[i];
			if (member.Kind is SymbolKind.Impl)
			{
				continue;
			}
			if (member.IdentifierMatches(identifier))
			{
				return member;
			}
		}
		return null;
	}

	/// <summary>
	/// Looks up a single symbol accessible on the type that match the given identifier.
	/// If no or more than one symbol match, an exception is thrown.
	/// This method is only provided for convenience.
	/// </summary>
	internal Symbol LookupSingleAccessibleSymbol(string identifier)
	{
		return LookupAccessibleSymbols(identifier).Single();
	}

	// TODO: Make use of AccessMode in callers?
	/// <summary>
	/// Looks up all symbols accessible on the type that match the given identifier.
	/// </summary>
	public ImmutableArray<Symbol> LookupAccessibleSymbols(string identifier)
	{
		// TODO: Store and use a lookup table?

		// Prioritize direct members first, then look into impl members.

		var directMember = GetDirectIdentifiableMember(identifier);
		if (directMember != null)
		{
			// This replicates languages like rust, where a direct member is always selected if it matches, and no ambiguity is detected.
			return [directMember];
		}

		// A bit of an optimization.
		if (!SymbolFacts.CanContainImpl(this))
		{
			return [];
		}

		var builder = ImmutableArray.CreateBuilder<Symbol>();
		foreach (var impl in Members.OfType<ImplSymbol>())
		{
			var member = impl.GetMember(identifier);
			if (member != null)
			{
				builder.Add(member.ImplSymbol);
			}
		}
		return builder.ToImmutable();
	}

	/// <summary>
	/// Unwraps aliases and return the actual type. The result should always be a non aliased type.
	/// </summary>
	public virtual TypeSymbol UnwrapTypeAliases()
	{
		return this;
	}

	/// <summary>
	/// Unwraps pointers to return the inner type. The result should always be a non pointer type or null if it's an opaque pointer.
	/// This is useful because the compiler does auto derefing/addressing, so in most cases we want to pass through to the inner non pointer type for type analysis.
	/// </summary>
	public virtual TypeSymbol? UnwrapPointers()
	{
		return this;
	}

	// TODO: Add an allow coercion option?
	/// <summary>
	/// If `true` is returned, the <paramref name="from"/> type can be assigned to this type. i.e. `this = <paramref name="from"/>` is valid if coersion is allowed.
	/// This also performs aliased type unwrapping on both sides.
	/// </summary>
	public bool IsAssignableFrom(TypeSymbol? from, bool allowCoercion = true)
	{
		if (from == null) return false;
		return IsAssignableFromCore(UnwrapTypeAliases(), from.UnwrapTypeAliases(), allowCoercion);
	}

	protected virtual bool IsAssignableFromCore(TypeSymbol to, TypeSymbol from, bool allowCoercion = true)
	{
		return Equals(to, from);
	}

	internal override abstract TypeSymbol SubstituteCore(SubstitutionMap smap);
}

public static class TypeSymbolExtensions
{
	public static bool IsUnknown(this TypeSymbol? symbol) => symbol?.TypeKind is TypeSymbolKind.Unknown;

	public static bool IsFn(this TypeSymbol? symbol) => symbol?.TypeKind is TypeSymbolKind.Fn;
}

public readonly struct TypeCreationMemberSymbol
{
	internal TypeCreationMemberSymbol(Symbol symbol, bool isConflicting)
	{
		Symbol = symbol;
		IsConflicting = isConflicting;
	}

	public Symbol Symbol { get; }

	/// <summary>
	/// Gets whether this symbol conflicts with another creation symbol.
	/// </summary>
	public bool IsConflicting { get; }

	public readonly bool IsOptional => Symbol switch
	{
		FieldSymbol fieldSymbol => fieldSymbol.HasInitializer,
		ParamSymbol paramSymbol => paramSymbol.HasInitializer,
		_ => throw new UnreachableException(),
	};
}
