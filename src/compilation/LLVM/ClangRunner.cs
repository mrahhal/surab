using System.Diagnostics;

namespace Surab.Compilation.LLVM;

internal static class ClangRunner
{
    // TODO: How to get this properly?
    private static readonly string LibcPath = @"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.37.32822\lib\x64";

    public static string Run(IEnumerable<string> llFiles, string? outputFileNameWithoutExtension = null)
    {
        outputFileNameWithoutExtension ??= "main";
        var outputFileName = $"{outputFileNameWithoutExtension}.exe";

        // TODO: Add -O3 for release?

        var arguments = $$"""
            -fuse-ld=lld-link
            -Xlinker /libpath:"{{LibcPath}}"
            {{string.Join(' ', llFiles)}}
            -o {{outputFileName}}
            """;

        var p = new Process
        {
            StartInfo =
            {
                FileName = "clang.exe",
                Arguments = arguments,
                WorkingDirectory = Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            }
        };

        var outputFilePath = Path.Combine(p.StartInfo.WorkingDirectory, outputFileName);

        p.Start();
        p.WaitForExit();

        if (p.ExitCode != 0)
        {
            var err = p.StandardError.ReadToEnd();
            throw new Exception("clang failed: " + err);
        }

        return outputFilePath;
    }
}
