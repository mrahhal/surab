namespace Surab;

internal static class StringExtensions
{
    /// <summary>
    /// Appends an <see cref="Environment.NewLine"/> to the string.
    /// </summary>
    public static string NL(this string @this)
    {
        return @this + Environment.NewLine;
    }

    public static string ToReadableSize(this long bytes)
    {
        if (bytes < 1024)
        {
            return bytes + " b";
        }
        else if (bytes < 1024 * 1024)
        {
            return Math.Round(bytes / ((decimal)1024), 2) + " KB";
        }
        else if (bytes < 1024 * 1024 * 1024)
        {
            return Math.Round(bytes / ((decimal)1024 * 1024), 2) + " MB";
        }
        else
        {
            return Math.Round(bytes / ((decimal)1024 * 1024 * 1024), 2) + " GB";
        }
    }
}
