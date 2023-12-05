namespace Surab;

/// <summary>
/// A source file in the project.
/// </summary>
public class SourceFile
{
    private SourceFile(Uri? uri, string text)
    {
        Uri = uri;
        Text = text;
    }

    /// <summary>
    /// The uri of the source file.
    /// </summary>
    public Uri? Uri { get; }

    /// <summary>
    /// The text content of the source file.
    /// </summary>
    public string Text { get; private set; }

    public string? Description { get; init; }

    public static SourceFile Create(Uri uri, string text)
    {
        return new(uri, text);
    }

    public static SourceFile FromText(string text)
    {
        return new(null, text);
    }

    internal void UpdateText(string newText)
    {
        Text = newText;
    }
}
