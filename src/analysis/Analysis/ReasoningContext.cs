using System.Collections.Concurrent;

namespace Surab.Analysis;

internal class ReasoningContext
{
	public readonly ConcurrentDictionary<FnTypeKey, FnTypeSymbol> UnnamedFnTypeMap = [];

	public readonly ConcurrentDictionary<PtrTypeKey, PtrTypeSymbol> PtrTypeMap = [];
}
