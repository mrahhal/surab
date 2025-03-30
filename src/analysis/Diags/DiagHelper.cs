namespace Surab.Analysis;

internal static class DiagHelper
{
	public static string Visualize(string text, FileSpan fileSpan, LineSpan? lineSpan, string? id, string? level, string? message)
	{
		var start = fileSpan.Start;
		var end = fileSpan.End;

		var windowStart = BTill(start);
		var windowEnd = FTill(end);
		var lineNumber = lineSpan.HasValue ? (lineSpan.Value.Start.Line + 1).ToString() : null;

		var leading = lineNumber != null ? $"{lineNumber}: " : string.Empty;
		var v = leading + text[windowStart..windowEnd] + Environment.NewLine;
		v += string.Concat(v[..(leading.Length + start - windowStart)].Select(ch =>
		{
			if (char.IsWhiteSpace(ch))
				return ch;
			else
				return ' ';
		}));
		v += string.Concat(Enumerable.Repeat('^', end - start));

		var idText = id != null ? $"[{id}:{level}] " : string.Empty;
		var messageText = message ?? string.Empty;
		var idAndMessageText = idText + messageText;

		return string.IsNullOrEmpty(idAndMessageText) ? v : idAndMessageText.NL().NL() + v;

		int BTill(int p)
		{
			while (p > 0 && text[p - 1] != '\n')
			{
				p--;
			}
			return p;
		}
		int FTill(int p)
		{
			while (p < text.Length - 1 && text[p] != '\r' && text[p] != '\n')
			{
				p++;
			}
			return p;
		}
	}
}
