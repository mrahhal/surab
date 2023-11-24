namespace Surab;

internal static class StringExtensions
{
    public static string NL(this string @this)
    {
        return @this + Environment.NewLine;
    }
}
