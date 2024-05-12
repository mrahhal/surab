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
	Alias,
}

public enum KnownTypeTag
{
	None,

	i32,
	i64,
	u32,
	u64,
	f32,
	f64,

	@bool,

	@char,
	@string,
}

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
	/// Members can include fields, initializers, methods, and nested declarations.
	/// (So among other things, anything that can be accessed through an access expression.
	/// </summary>
	public abstract ImmutableArray<Symbol> Members { get; }

	public Symbol? GetMember(IdentifierToken identifierToken) => GetMember(identifierToken.Value);

	public Symbol? GetMember(string identifier)
	{
		// REVIEW: Some duplication between symbols and scopes here (and GetCreationMember)?

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

	/// <summary>
	/// If `true` is returned, the <paramref name="from"/> type can be assigned to this type. i.e. `this = <paramref name="from"/>` is valid.
	/// This also performs aliased type unwrapping on both sides.
	/// </summary>
	public bool IsAssignableFrom(TypeSymbol? from)
	{
		if (from == null) return false;
		return IsAssignableFromCore(UnwrapTypeAliases(), from.UnwrapTypeAliases());
	}

	protected virtual bool IsAssignableFromCore(TypeSymbol to, TypeSymbol from)
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
