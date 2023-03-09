namespace Surab.Analyzer;

internal static class Chars
{
    public const char EOF = '\0';

    public static bool IsWhitespace(char ch) => ch is
        '\u0009' or // \t
        '\u000A' or // \n
        '\u000B' or // vertical tab
        '\u000C' or // form feed
        '\u000D' or // \r
        '\u0020' or // space

        // NEXT LINE from latin1
        '\u0085' or

        // Bidi markers
        '\u200E' or // LEFT-TO-RIGHT MARK
        '\u200F' or // RIGHT-TO-LEFT MARK

        // Dedicated whitespace characters from
        '\u2028' or // LINE SEPARATOR
        '\u2029';   // PARAGRAPH SEPARATOR

    /// <summary>
    /// Returns `true` if `ch` is a new line or a start of one (either '\r' or '\n').
    /// </summary>
    public static bool IsNewlineStart(char ch) => ch is '\r' or '\n';

    public static bool IsIdentifierStart(char ch) =>
        // TODO: XID
        ('a' <= ch && ch <= 'z') ||
        ('A' <= ch && ch <= 'Z') ||
        ch == '_';

    public static bool IsIdentifierContinue(char ch) =>
        // TODO: XID
        IsIdentifierStart(ch) ||
        ('0' <= ch && ch <= '9');

    public static bool IsIdentifier(ReadOnlySpan<char> span) =>
        !span.IsEmpty &&
        IsIdentifierStart(span[0]) &&
        span.All(IsIdentifierContinue);
}
