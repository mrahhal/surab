namespace Surab;

internal static class CollectionHelper
{
	public static bool AreEqual<T>(IReadOnlyList<T> x, IReadOnlyList<T> y)
	{
		if (x.Count != y.Count)
			return false;
		if (x == y)
			return true;
		for (var i = 0; i < x.Count; i++)
		{
			if (!EqualityComparer<T>.Default.Equals(x[i], y[i]))
				return false;
		}
		return true;
	}
}
