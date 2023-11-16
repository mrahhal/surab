using System.Text;

public static class StringExtensions
{
    private static readonly char[] Delimeters = [' ', '-', '_'];

    public static string ToComment(this string? comment)
    {
        return string.IsNullOrWhiteSpace(comment) ? string.Empty : $"// {comment}";
    }

    public static string ToDocComment(this string? comment)
    {
        return string.IsNullOrWhiteSpace(comment) ? string.Empty : $"/// {comment}";
    }

    public static string ToPascalCase(this string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return SymbolsPipe(
            source,
            '\0',
            (s, i) => new char[] { char.ToUpperInvariant(s) });
    }

    public static string ToCamelCase(this string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return SymbolsPipe(
            source,
            '\0',
            (s, disableFrontDelimeter) =>
            {
                if (disableFrontDelimeter)
                {
                    return [char.ToLowerInvariant(s)];
                }

                return [char.ToUpperInvariant(s)];
            });
    }

    public static string ToKebabCase(this string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return SymbolsPipe(
            source,
            '-',
            (s, disableFrontDelimeter) =>
            {
                if (disableFrontDelimeter)
                {
                    return [char.ToLowerInvariant(s)];
                }

                return ['-', char.ToLowerInvariant(s)];
            });
    }

    public static string ToSnakeCase(this string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return SymbolsPipe(
            source,
            '_',
            (s, disableFrontDelimeter) =>
            {
                if (disableFrontDelimeter)
                {
                    return [char.ToLowerInvariant(s)];
                }

                return ['_', char.ToLowerInvariant(s)];
            });
    }

    public static string ToTrainCase(this string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return SymbolsPipe(
            source,
            '-',
            (s, disableFrontDelimeter) =>
            {
                if (disableFrontDelimeter)
                {
                    return [char.ToUpperInvariant(s)];
                }

                return ['-', char.ToUpperInvariant(s)];
            });
    }

    public static bool IsDigitsOnly(this string source) =>
        source.All(c => (c >= '0') && (c <= '9'));

    private static string SymbolsPipe(
        string source,
        char mainDelimeter,
        Func<char, bool, char[]> newWordSymbolHandler)
    {
        var builder = new StringBuilder();

        var nextSymbolStartsNewWord = true;
        var disableFrontDelimeter = true;
        for (var i = 0; i < source.Length; i++)
        {
            var symbol = source[i];
            if (Delimeters.Contains(symbol))
            {
                if (symbol == mainDelimeter)
                {
                    builder.Append(symbol);
                    disableFrontDelimeter = true;
                }

                nextSymbolStartsNewWord = true;
            }
            else if (!char.IsLetterOrDigit(symbol))
            {
                builder.Append(symbol);
                disableFrontDelimeter = true;
                nextSymbolStartsNewWord = true;
            }
            else
            {
                if (nextSymbolStartsNewWord || char.IsUpper(symbol))
                {
                    builder.Append(newWordSymbolHandler(symbol, disableFrontDelimeter));
                    disableFrontDelimeter = false;
                    nextSymbolStartsNewWord = false;
                }
                else
                {
                    builder.Append(symbol);
                }
            }
        }

        return builder.ToString();
    }

    public static bool StringsEqual(string str1, string str2)
    {
        var normalizedStr1 = str1?.Trim();
        var normalizedStr2 = str2?.Trim();

        if (string.IsNullOrEmpty(normalizedStr1) && string.IsNullOrEmpty(normalizedStr2)) return true;
        if (string.IsNullOrEmpty(normalizedStr1) && !string.IsNullOrEmpty(normalizedStr2)) return false;
        if (!string.IsNullOrEmpty(normalizedStr1) && string.IsNullOrEmpty(normalizedStr2)) return false;

        return normalizedStr1 == normalizedStr2;
    }

    public static string NL(this string str, int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            str += Environment.NewLine;
        }
        return str;
    }
}
