using System.Collections.Immutable;

namespace Surab.Analysis;

public enum DiagLevel
{
	Suggestion,
	Warning,
	Error,
}

public class Diag(string? id, TextSpan span, DiagLevel level, string message)
{
	public string? Id { get; } = id;

	public TextSpan Span { get; } = span;

	public DiagLevel Level { get; } = level;

	public string Message { get; } = message;

	public string Visualize(string text)
	{
		return DiagHelper.Visualize(text, Span.File, Span.Line, Id, Level.ToString(), Message);
	}
}

// Used in tests.
internal class ExpectedDiag(string? id, FileSpan fileSpan)
{
	public string? Id { get; } = id;

	public FileSpan FileSpan { get; } = fileSpan;

	public string Visualize(string text)
	{
		return DiagHelper.Visualize(text, FileSpan, null, Id, null, null);
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

	public Diag Add(DiagDef def, TextSpan span, object[]? templateArgs = null)
	{
		var message = string.Format(def.MessageTemplate, templateArgs ?? []);
		return Add(def.Id, span, def.DefaultLevel, message);
	}

	public Diag Add(TextSpan span, DiagLevel level, string message)
	{
		return Add(null, span, level, message);
	}

	public Diag Add(string? id, TextSpan span, DiagLevel level, string message)
	{
		var diag = new Diag(id, span, level, message);
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
