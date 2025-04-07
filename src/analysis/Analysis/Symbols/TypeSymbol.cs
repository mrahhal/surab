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
	/// Members that make up this type. Includes implemented/inherited symbols, initializers, etc.
	/// Note that this is different from AST type members which are closer to <see cref="Children"/>.
	/// </summary>
	public abstract ImmutableArray<Symbol> Members { get; }

	/// <summary>
	/// Members that can be accessed when the type is dotted into. This excludes symbols with no
	/// identifier, operator symbols, etc.
	/// </summary>
	public abstract ImmutableArray<Symbol> AccessibleMembers { get; }

	/// <summary>
	/// Direct accessible members are those under the type directly.
	/// </summary>
	public virtual IEnumerable<Symbol> DirectAccessibleMembers => AccessibleMembers.Where(m => m.Parent == this);
	
	/// <summary>
	/// Indirect accessible members are implemented members (inside impl blocks), etc.
	/// </summary>
	public virtual IEnumerable<Symbol> IndirectAccessibleMembers => AccessibleMembers.Where(m => m.Parent != this);

	/// <summary>
	/// Looks up a single symbol accessible on the type that match the given identifier.
	/// If no or more than one symbol match, an exception is thrown.
	/// This method is only provided for convenience.
	/// </summary>
	internal Symbol GetSingleAccessibleMember(string identifier)
	{
		return GetAccessibleMembers(identifier).Single();
	}

	/// <summary>
	/// Looks up all symbols accessible on the type that match the given identifier.
	/// </summary>
	public IReadOnlyList<Symbol> GetAccessibleMembers(string identifier)
	{
		// Prioritize direct members first, then look into indirect members (impl, etc).
		// This is similar to languages like rust, where a direct member is always selected if it
		// matches, and no ambiguity is detected if a direct member conflicts with a trait member.

		var list = new List<Symbol>();
		foreach (var m in DirectAccessibleMembers)
		{
			if (m.IdentifierMatches(identifier))
			{
				list.Add(m);
			}
		}

		if (list.Count != 0)
			return list;

		// A bit of an optimization.
		if (!SymbolFacts.CanContainIndirectMembers(this))
			return [];

		foreach (var m in IndirectAccessibleMembers)
		{
			if (m.IdentifierMatches(identifier))
			{
				list.Add(m);
			}
		}

		return list;
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
