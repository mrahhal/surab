using System.Runtime.CompilerServices;

namespace Surab;

internal static class PathHelper
{
    public static string GetSrcFolderPath()
    {
        return Path.Combine(GetCallerFilePath(), "../../../");
    }

    private static string GetCallerFilePath([CallerFilePath] string callerFilePath = null!)
    {
        return callerFilePath;
    }
}
