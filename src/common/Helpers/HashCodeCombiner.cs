namespace Surab;

internal static class HashCodeCombiner
{
	public static int Combine<T>(IEnumerable<T> ts)
	{
		var hc = new HashCode();
		foreach (var t in ts)
		{
			hc.Add(t);
		}
		return hc.ToHashCode();
	}

	public static int Combine<T1, T2>(T1 t1, IEnumerable<T2> t2s)
	{
		var hc = new HashCode();
		hc.Add(t1);
		foreach (var t2 in t2s)
		{
			hc.Add(t2);
		}
		return hc.ToHashCode();

		//////unchecked
		//////{
		//////	var hash = 92821;
		//////	hash = hash * 29 + t1?.GetHashCode() ?? 0;
		//////	foreach (var t2 in t2s)
		//////	{
		//////		hash = hash * 29 + t2.GetHashCode();
		//////	}
		//////	return hash;
		//////}
	}
}
