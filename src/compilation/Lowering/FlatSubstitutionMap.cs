using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Surab.Compilation;

/// <summary>
/// A simpler flat version of <see cref="Analysis.SubstitutionMap"/> that deals with HIR types and
/// is used mainly with monomorphization.
///
/// Unlike in the original smap, there's no need for hierarchy or chain resolve. This is a simple
/// one to one substitution map.
/// </summary>
internal class FlatSubstitutionMap
{
	private readonly Dictionary<object, object> _map = [];

	public static readonly FlatSubstitutionMap Empty = new();

	public void AddGenericSubstitutions(IReadOnlyList<HirType> from, IReadOnlyList<HirType> to)
	{
		Debug.Assert(from.Count == to.Count);
		Debug.Assert(from.All(x => x.Kind is HirTypeKind.GenericParam));

		for (var i = 0; i < from.Count; i++)
		{
			Add(from[i], to[i]);
		}
	}

	public void Add(object from, object to)
	{
		Debug.Assert(
			(from is HirType && to is HirType) ||
			(from is HirValue && to is HirValue));
		_map.Add(from, to);
	}

	[return: NotNullIfNotNull(nameof(o))]
	public object? Replace(object? o)
	{
		if (o == null)
			return null;
		if (_map.TryGetValue(o, out var r))
		{
			return r;
		}
		return o;
	}

	[return: NotNullIfNotNull(nameof(value))]
	public HirValue? Replace(HirValue? value) => (HirValue?)Replace((object?)value);

	[return: NotNullIfNotNull(nameof(type))]
	public HirType? Replace(HirType? type) => (HirType?)Replace((object?)type);

	public HirType ReplaceGenericParam(
		HirType genericParam)
	{
		Debug.Assert(genericParam.Kind is HirTypeKind.GenericParam && _map.ContainsKey(genericParam));
		return (HirType)_map[genericParam];
	}
}
