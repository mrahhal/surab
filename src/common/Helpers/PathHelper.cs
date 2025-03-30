using System.Runtime.CompilerServices;

namespace Surab;

internal static class PathHelper
{
	public static string GetRepoRootFolderPath()
	{
		return Path.Combine(GetCallerFilePath(), "../../../../");
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

	private static string GetCallerFilePath([CallerFilePath] string callerFilePath = null!)
	{
		return callerFilePath;
	}
}
