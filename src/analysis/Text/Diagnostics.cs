using System.Collections.Immutable;

namespace Surab;

public enum DiagnosticLevel
{
	Suggestion,
	Warning,
	Error,
}

public class Diagnostic(TextSpan span, DiagnosticLevel level, string message)
{
	public TextSpan Span { get; } = span;

	public DiagnosticLevel Level { get; } = level;

	public string Message { get; } = message;

	public string Visualize(string text)
	{
		var start = Span.File.Start;
		var end = Span.File.End;

		var windowStart = BTill(start);
		var windowEnd = FTill(end);
		var lineNumber = Span.Line.Start.Line + 1;

		var leading = $"{lineNumber}: ";
		var v = leading + text[windowStart..windowEnd] + Environment.NewLine;
		v += string.Concat(v[..(leading.Length + start - windowStart)].Select(ch =>
		{
			if (char.IsWhiteSpace(ch))
				return ch;
			else
				return ' ';
		}));
		v += string.Concat(Enumerable.Repeat('^', end - start));

		return $"[{Level}] " + Message + Environment.NewLine + Environment.NewLine + v;

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

public interface IDiagnosticsProvider
{
	IEnumerable<Diagnostic> GetDiagnostics();

	bool IsHealthy();
}

public class DiagnosticBag
{
	public List<Diagnostic> Diagnostics { get; } = [];

	public Diagnostic Add(TextSpan span, DiagnosticLevel level, string message)
	{
		var diagnostic = new Diagnostic(span, level, message);
		Diagnostics.Add(diagnostic);
		return diagnostic;
	}
}

public class FileDiagnostics(NormalizedPath filePath, ImmutableArray<Diagnostic> diagnostics, DiagnosticLevel? highestLevel)
{
	public NormalizedPath FilePath { get; } = filePath;

	public ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;

	public DiagnosticLevel? HighestLevel { get; } = highestLevel;
}
