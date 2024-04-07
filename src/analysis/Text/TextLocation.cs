namespace Surab;

/// <summary>
/// A location in a file.
/// If <paramref name="Span"/> is 0 in size, the location points to an offset in file (caret). If it's non 0, the location points to a range of text in file (selection).
/// </summary>
/// <param name="FilePath">The file path of the location.</param>
/// <param name="Span">The span of the location.</param>
public readonly record struct TextLocation(
	NormalizedPath FilePath,
	TextSpan Span);
