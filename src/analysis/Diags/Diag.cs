using System.Collections.Immutable;

namespace Surab.Analysis;

public enum DiagLevel
{
	Suggestion,
	Warning,
	Error,
}

public class Diag(TextSpan span, DiagLevel level, string message)
{
	public TextSpan Span { get; } = span;

	public DiagLevel Level { get; } = level;

	public string Message { get; } = message;

	public string Visualize(string text)
	{
		return DiagHelper.Visualize(text, Span.File, Span.Line, Level.ToString(), Message);
	}
}

public interface IDiagsProvider
{
	IEnumerable<Diag> GetDiags();

	bool IsHealthy();
}

public class DiagBag
{
	public List<Diag> Diags { get; } = [];

	public Diag Add(TextSpan span, DiagLevel level, string message)
	{
		var diag = new Diag(span, level, message);
		Diags.Add(diag);
		return diag;
	}
}

public class FileDiags(NormalizedPath filePath, ImmutableArray<Diag> diags, DiagLevel? highestLevel)
{
	public NormalizedPath FilePath { get; } = filePath;

	public ImmutableArray<Diag> Diags { get; } = diags;

	public DiagLevel? HighestLevel { get; } = highestLevel;
}
