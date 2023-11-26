using System.Diagnostics;

namespace Surab.Compilation.LLVM;

internal static class ClangRunner
{
    // Required native libs by Rust, minus msvcrt.lib as that creates the printf not found symbol error for some reason.
    // The order and any duplication can be significant on some platforms.
    private static readonly string AdditionalLibs = @"kernel32.lib advapi32.lib bcrypt.lib kernel32.lib ntdll.lib userenv.lib ws2_32.lib kernel32.lib ws2_32.lib kernel32.lib ntdll.lib kernel32.lib";

    public static string Run(IEnumerable<string> llFiles, string? outputFileNameWithoutExtension = null)
    {
        outputFileNameWithoutExtension ??= "main";
        var outputFileName = $"{outputFileNameWithoutExtension}.exe";

        // TODO:
        // - Use debug vs release libs depending on a compilation flag.
        // - Add -O3 for release?

        var surabRuntimeLib = Path.Combine(PathHelper.GetRuntimeTargetFolderPath(), "debug/surabrt.lib");

        var nativeLibs = string.Join(' ', AdditionalLibs.Split(' ').Select(x => $"-l{x}"));
        var arguments = $$"""
            -fuse-ld=lld-link
            {{nativeLibs}}
            {{surabRuntimeLib}}
            {{string.Join(' ', llFiles)}}
            -o {{outputFileName}}
            -v
            """;

        var p = new Process
        {
            StartInfo =
            {
                FileName = "clang.exe",
                Arguments = arguments,
                WorkingDirectory = Directory.GetCurrentDirectory(),
            }
        };

        var outputFilePath = Path.Combine(p.StartInfo.WorkingDirectory, outputFileName);

        var clangCommand = p.StartInfo.FileName + " " + p.StartInfo.Arguments.ReplaceLineEndings(" ");
        ConsoleHelper.WriteInfo("Running clang command:".NL());
        ConsoleHelper.Write(clangCommand.NL());
        ConsoleHelper.WriteInfo("===".NL());

        p.Start();
        var exited = p.WaitForExit(TimeSpan.FromSeconds(30));

        if (exited && p.ExitCode != 0)
        {
            throw new Exception("Clang failed.");
        }
        else if (!exited)
        {
            throw new Exception("Clang failed to exit.");
        }

        var exeFileInfo = new FileInfo(outputFilePath);
        var exeSize = exeFileInfo.Length.ToReadableSize();
        ConsoleHelper.WriteInfo($"Compiled to file of size {exeSize}".NL());

        return outputFilePath;
    }
}
