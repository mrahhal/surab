namespace Surab;

/// <summary>
/// A source file in a Surab project.
/// </summary>
public class SourceFile
{
	private SourceFile(string path, string text)
	{
		Path = path;
		Text = text;
	}

	// TODO: Should this be a uri instead?
	/// <summary>
	/// The file system path of the source file. Always absolute.
	/// </summary>
	public NormalizedPath Path { get; }

	/// <summary>
	/// The text content of the source file.
	/// </summary>
	public string Text { get; private set; }

	public string? Description { get; init; }

	public bool IsSpecialConfigFile => System.IO.Path.GetFileName(Path.Value) == "__cfg.surab";

	public static SourceFile Create(string path, string text)
	{
		return new(path, text);
	}

	public static SourceFile FromText(string text)
	{
		return new(string.Empty, text);
	}

	internal void UpdateText(string newText)
	{
		Text = newText;
	}
}
