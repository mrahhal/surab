using System.Collections.Concurrent;
using System.Diagnostics;

namespace Surab.Analysis;

internal class SymbolInterner
{
	private readonly ConcurrentDictionary<FnTypeKey, FnTypeSymbol> UnnamedFnTypeMap = [];

	private readonly ConcurrentDictionary<PtrTypeKey, PtrTypeSymbol> PtrTypeMap = [];

	private readonly ConcurrentDictionary<ThisSymbolKey, ThisSymbol> ThisSymbolMap = [];

	private readonly ConcurrentDictionary<SubstitutedGenericSymbolKey, Symbol> SubstitutedSymbolMap = [];

	public readonly ConcurrentDictionary<AttrsListNode, AttrsList> AttrsMap = [];

	private T SetInterner<T>(T s)
		where T: Symbol
	{
		s.SymbolInterner = this;
		return s;
	}

	public PtrTypeSymbol GetPtrType(TypeSymbol? innerType)
	{
		var key = new PtrTypeKey(innerType);
		return PtrTypeMap.GetOrAdd(key, key => SetInterner(new PtrTypeSymbol(key)));
	}

	public ThisSymbol GetThis(TypeSymbol type)
	{
		var key = new ThisSymbolKey(type);
		return ThisSymbolMap.GetOrAdd(key, key => SetInterner(new ThisSymbol(key)));
	}

	public FnTypeSymbol GetFnType(FnTypeKey key)
	{
		return UnnamedFnTypeMap.GetOrAdd(key, key => SetInterner(new FnTypeSymbol(key, this)));
	}

	/// <summary>
	/// Closes a generic symbol, using the given generic params/args as substitutions.
	/// </summary>
	public Symbol CloseGeneric(
		Symbol symbol,
		IReadOnlyList<GenericParamTypeSymbol> genericParams,
		IReadOnlyList<TypeSymbol> genericArgs)
	{
		if (symbol is not IPossiblyGenericSymbol genericSymbol)
			throw new Exception("Expected a possibly generic symbol.");

		var smap = SubstitutionMap.Create();
		smap.AddGenericSubstitutions(genericParams, genericArgs);

		var key = new SubstitutedGenericSymbolKey(symbol, genericArgs);
		return SubstitutedSymbolMap.GetOrAdd(
			key,
			_ => genericSymbol.SubstituteConstructedGeneric(smap, genericArgs));
	}

	/// <summary>
	/// Closes the generic symbol using the given smap. Any generic args substitutions should already be in the smap.
	/// </summary>
	internal Symbol CloseGeneric(
		Symbol symbol,
		SubstitutionMap smap)
	{
		if (symbol is not IPossiblyGenericSymbol genericSymbol)
			throw new Exception("Expected a possibly generic symbol.");

		Debug.Assert(genericSymbol.GenericParams.Length > 0 && genericSymbol.GenericArgs.Length == 0);

		smap = smap.CreateSub();
		var genericArgs = smap.ReplaceGenericTypes(genericSymbol.GenericParams);

		var key = new SubstitutedGenericSymbolKey(symbol, genericArgs);
		return SubstitutedSymbolMap.GetOrAdd(
			key,
			_ => genericSymbol.SubstituteConstructedGeneric(smap, genericArgs));
	}
}
