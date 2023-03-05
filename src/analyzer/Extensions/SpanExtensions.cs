namespace Surab.Analyzer;

internal static class SpanExtensions
{
    public static bool All<T>(this ReadOnlySpan<T> span, Func<T, bool> predicate)
    {
        foreach (var item in span)
        {
            if (!predicate(item))
            {
                return false;
            }
        }
        return true;
    }
}
