using System.Diagnostics;

namespace Surab.Compilation;

internal class MonomorphizationCollection
{
	/// <summary>
	/// From a value or a type to a list of generic usages. Key can be non generic, but values
	/// will always be generic.
	///
	/// The reason we record generic usages inside non generic symbols is because we need to
	/// replace these closed generic usages with the actual mono replacement generated
	/// in monomorphization later.
	///
	/// An entry existing means the value or type should be patched and replaced when monomorphing.
	/// </summary>
	public required IReadOnlyDictionary<HirValueOrType, Usage> UsageMap { get; init; }

	public required IReadOnlyDictionary<HirValueOrType, IReadOnlyList<Concrete>> ConcretesMap { get; init; }

	// For use in tests.
	public Usage? GetUsageOrNull(string identifier)
	{
		return UsageMap.FirstOrDefault(x => x.Key.Identifier == identifier).Value;
	}

	// For use in tests.
	public IReadOnlyList<Concrete>? GetConcretesOrNull(string identifier)
	{
		return ConcretesMap.FirstOrDefault(x => x.Key.Identifier == identifier).Value;
	}

	[DebuggerDisplay("{ToString(),nq}")]
	internal class Usage
	{
		public required IReadOnlyList<HirGenericParamType> GenericParamTypesUsed { get; init; }

		public required IReadOnlyList<HirValueOrType> Dependents { get; init; }

		public override string ToString()
		{
			var genericParamTypesUsed = string.Join(", ", GenericParamTypesUsed.Select(t => t.Name));
			var dependents = string.Join(", ", Dependents.Select(t => t.Identifier));
			return $"[{genericParamTypesUsed}], [{dependents}]";
		}
	}

	[DebuggerDisplay("{ToString(),nq}")]
	internal class Concrete
	{
		public required IReadOnlyList<HirType> Types { get; init; }

		public override string ToString()
		{
			var types = string.Join(", ", Types.Select(t => t.Name));
			return $"[{types}]";
		}
	}
}
