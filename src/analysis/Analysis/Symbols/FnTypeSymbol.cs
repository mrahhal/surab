using System.Collections.Immutable;
using System.Diagnostics;

namespace Surab.Analysis;

/// <summary>
/// Represents a key that is used to structurally compare fn types.
/// Any two fn types with the same signature will have equal keys.
/// </summary>
[DebuggerDisplay("{ToString()}")]
internal readonly struct FnTypeKey(
	bool isExtern,
	TypeSymbol returnType,
	IEnumerable<TypeSymbol> paramTypes) : IEquatable<FnTypeKey>
{
	public readonly bool IsExtern { get; } = isExtern;
	public readonly TypeSymbol ReturnType { get; } = returnType;
	public readonly ImmutableArray<TypeSymbol> ParamTypes { get; } = paramTypes.ToImmutableArray();

	public override bool Equals(object? obj) => obj is FnTypeKey other && Equals(other);

	public bool Equals(FnTypeKey other)
	{
		return
			IsExtern == other.IsExtern &&
			ReturnTypesEqual(ReturnType, other.ReturnType) &&
			ParamTypesEqual(this, other);
	}

	public bool IsAssignableFrom(FnTypeKey other)
	{
		return
			IsExtern == other.IsExtern &&
			ReturnTypesAssignable(ReturnType, other.ReturnType) &&
			ParamTypesAssignable(this, other);
	}

	private static bool ReturnTypesEqual(TypeSymbol x, TypeSymbol y) => x == y;
	private static bool ReturnTypesAssignable(TypeSymbol x, TypeSymbol y) => x.IsAssignableFrom(y);

	private static bool ParamTypesEqual(FnTypeKey x, FnTypeKey y) => ParamTypesCompare(x, y, (x, y) => x == y);
	private static bool ParamTypesAssignable(FnTypeKey x, FnTypeKey y) => ParamTypesCompare(x, y, (x, y) => x.IsAssignableFrom(y));
	private static bool ParamTypesCompare(FnTypeKey x, FnTypeKey y, Func<TypeSymbol, TypeSymbol, bool> compare)
	{
		if (x.ParamTypes.Length != y.ParamTypes.Length)
			return false;
		for (var i = 0; i < x.ParamTypes.Length; i++)
		{
			if (!compare(x.ParamTypes[i], y.ParamTypes[i]))
				return false;
		}
		return true;
	}

	public override int GetHashCode()
	{
		// Shouldn't call UnwrapTypeAliases() so we get different keys for different compatible types.
		// This is fine because analyzing compatibility uses IsAssignableFrom.

		unchecked
		{
			var hash = 92821;
			hash = hash * 29 + IsExtern.GetHashCode();
			hash = hash * 29 + ReturnType.GetHashCode();
			foreach (var p in ParamTypes)
			{
				hash = hash * 29 + p.GetHashCode();
			}
			return hash;
		}
	}

	public override string ToString()
	{
		IEnumerable<string?> @params = [.. ParamTypes.Select(p => p.Name)];
		return $"{(IsExtern ? "@extern " : "")}fn ({string.Join(", ", @params)}) {ReturnType.Name}";
	}
}

/// <summary>
/// Represents an fn type symbol.
/// A symbol will be created for each custom named fn type. Another shared symbol will be created for untyped fn types.
/// This symbol overrides equality so that comparison is structural instead of referential.
/// i.e. A named fn's fn type symbol and a method's fn type symbol and an alias' fn type symbol are equal if they're structurally compatible (signatures match).
/// </summary>
public class FnTypeSymbol : TypeSymbol
{
	// AliasDeclNode (node is null for FnDeclNode, MethodDeclNode because these create shared fn type symbols)
	//private readonly DeclNode? _node;

	// node will be null for untyped fn types (fns and methods).
	internal FnTypeSymbol(FnTypeKey key, SymbolInterner symbolCache)
	{
		Key = key;
		SymbolInterner = symbolCache;
	}

	internal FnTypeKey Key { get; }

	public override TypeSymbolKind TypeKind => TypeSymbolKind.Fn;

	public override FnTypeSymbol OriginalDef => this;

	public override SyntaxNode? DeclaringNode => null;

	internal override IdentifierToken? IdentifierToken => null;

	public bool IsExtern => Key.IsExtern;

	public TypeSymbol ReturnType => Key.ReturnType;

	/// <summary>
	/// Param types including the receiver if it exists.
	/// </summary>
	public ImmutableArray<TypeSymbol> ParamTypes => Key.ParamTypes;

	// TODO: If it's a named fn type (alias), display that instead?
	public override string Name => Key.ToString();

	public override ImmutableArray<Symbol> Members => throw new NotImplementedException();

	protected override bool IsAssignableFromCore(TypeSymbol to, TypeSymbol from)
	{
		if (to is not FnTypeSymbol toFnType || from is not FnTypeSymbol fromFnType)
			return false;
		return toFnType.Key.IsAssignableFrom(fromFnType.Key);
	}

	internal override TypeSymbol SubstituteCore(SubstitutionMap smap)
	{
		var key = new FnTypeKey(
			Key.IsExtern,
			(TypeSymbol)smap.Replace(Key.ReturnType),
			Key.ParamTypes.Select(x => (TypeSymbol)smap.Replace(x)));
		return SymbolInterner.GetFnType(key);
	}

	public override string ToString()
	{
		return Key.ToString();
	}
}
