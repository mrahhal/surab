using System.Collections.Immutable;
using System.Diagnostics;

namespace Surab.Analysis;

/// <summary>
/// Represents a key that is used to structurally compare ptr types.
/// Any two ptr types with the same inner type will have equal keys.
/// </summary>
[DebuggerDisplay("{ToString()}")]
internal readonly record struct PtrTypeKey(TypeSymbol? innerType)
{
	public readonly TypeSymbol? InnerType { get; } = innerType;

	public bool IsAssignableFrom(PtrTypeKey other)
	{
		if (InnerType == null)
		{
			// Opaque ptr.
			return other.InnerType == null;
		}
		return InnerType.IsAssignableFrom(other.InnerType);
	}

	public override string ToString()
	{
		return $"*{InnerType?.Name}";
	}
}

public class PtrTypeSymbol : TypeSymbol
{
	internal PtrTypeSymbol(PtrTypeKey key)
	{
		Key = key;
	}

	internal PtrTypeKey Key { get; }

	public override TypeSymbolKind TypeKind => TypeSymbolKind.Ptr;

	public override Symbol OriginalDef => this;

	public override SyntaxNode? DeclaringNode => null;

	internal override IdentifierToken? IdentifierToken => null;

	/// <summary>
	/// Gets the inner type of the pointer. Will be null for an opaque pointer.
	/// </summary>
	public TypeSymbol? InnerType => Key.InnerType;

	public override string Name => $"*{InnerType?.Name}";

	protected override bool IsAssignableFromCore(TypeSymbol to, TypeSymbol from)
	{
		if (to is not PtrTypeSymbol toPtrType || from is not PtrTypeSymbol fromPtrType)
			return false;
		return toPtrType.Key.IsAssignableFrom(fromPtrType.Key);
	}

	public override TypeSymbol? UnwrapPointers()
	{
		return InnerType?.UnwrapPointers();
	}

	// REVIEW: Is this what we should really do? Shouldn't we depend on unwrapping instead?

	public override ImmutableArray<Symbol> Members => InnerType?.Members ?? [];

	public override string ToString() => Key.ToString();

	internal override TypeSymbol SubstituteCore(SubstitutionMap smap)
	{
		if (InnerType != null)
		{
			return SymbolCache.GetPtrType((TypeSymbol)InnerType.SubstituteWithHierarchyPatching(smap));
		}
		return this;
	}
}
