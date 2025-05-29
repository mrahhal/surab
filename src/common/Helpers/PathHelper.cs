using System.Runtime.CompilerServices;

namespace Surab;

internal static class PathHelper
{
	public static string GetRepoRootFolderPath()
	{
		// Climb up until we find it.
		var cd = Environment.CurrentDirectory;
		// Match when we find surab/src.
		while (Path.GetFileName(cd) != "src" && Path.GetFileName(Path.GetDirectoryName(cd)) != "surab")
		{
			cd = Path.GetDirectoryName(cd);
		}
		// One more to get repo directory here.
		cd = Path.GetDirectoryName(cd);

		return cd!;
	}

	public static string GetSrcFolderPath()
	{
		return Path.Combine(GetRepoRootFolderPath(), "src");
	}

	public static string GetRuntimeSrcFolderPath()
	{
		return Path.Combine(GetRepoRootFolderPath(), "src/runtime");
	}

	public static string GetSamplesFolderPath()
	{
		return Path.Combine(GetRepoRootFolderPath(), "samples");
	}

	public static string GetRuntimeTargetFolderPath()
	{
		return Path.Combine(GetRepoRootFolderPath(), "target");
	}

	// Note: Used to use this in GetRepoRootFolderPath but CallerFilePath computes the path on
	// compile time so it might produce results not compatible with linux (e.g. if debugging in wsl
	// in VS).
	private static string GetCallerFilePath([CallerFilePath] string callerFilePath = null!)
	{
		return callerFilePath;
	}
}
