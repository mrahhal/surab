using System.Diagnostics;

namespace Surab;

[DebuggerDisplay("{Value}")]
/// <summary>
/// Represents a normalized fs path. The goal is to always use this type instead of a string for fs paths
/// so that we can ensure we have consistent paths across operating systems.
///
/// Directory separator is always `/` in tests and hard coded places. This type will replace these with the proper
/// separator of the environment we're running in.
/// </summary>
public readonly struct NormalizedPath
{
	public static readonly NormalizedPath Empty = new() { Value = string.Empty };

	public required string Value { get; init; }

	public bool IsEmpty => Value == string.Empty;

	// string -> NormalizedPath: implicit
	public static implicit operator NormalizedPath(string path)
	{
		ArgumentNullException.ThrowIfNull(path);

		if (string.IsNullOrWhiteSpace(path))
		{
			return Empty;
		}

		path = path.Replace('/', Path.DirectorySeparatorChar).Trim();
		return new NormalizedPath
		{
			Value = path,
		};
	}

	// NormalizedPath -> string: explicit
	public static explicit operator string(NormalizedPath normalizedPath)
	{
		return normalizedPath.Value;
	}
}
