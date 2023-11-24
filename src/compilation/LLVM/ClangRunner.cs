using System.Diagnostics;

namespace Surab.Compilation.LLVM;

internal static class ClangRunner
{
#pragma warning disable IDE0051 // Remove unused private members
    // TODO: How to get this properly?
    private static readonly string LibcPath = @"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.38.33130\lib\x64";
#pragma warning restore IDE0051 // Remove unused private members

    private static readonly string rpfsPath = @"C:\dev\src\Projects\_T\rust-playground-for-surab\target\debug\rpfs.lib";

    // Required by rust. From rust's `cargo rustc -- --print native-static-libs`. Minus msvcrt.lib as that creates the printf not found symbol error for some reason.
    // The order and any duplication can be significant on some platforms.
    private static readonly string AdditionalLibs = @"kernel32.lib advapi32.lib bcrypt.lib kernel32.lib ntdll.lib userenv.lib ws2_32.lib kernel32.lib ws2_32.lib kernel32.lib ntdll.lib kernel32.lib";

    public static string Run(IEnumerable<string> llFiles, string? outputFileNameWithoutExtension = null)
    {
        outputFileNameWithoutExtension ??= "main";
        var outputFileName = $"{outputFileNameWithoutExtension}.exe";

        // TODO: Add -O3 for release?

        // Linker is working without `-Xlinker /libpath:"{{LibcPath}}"` now for some reason?
        // It was needed for linking against libc.

        var additionalLibs = string.Join(' ', AdditionalLibs.Split(' ').Select(x => $"-l{x}"));
        var arguments = $$"""
            -fuse-ld=lld-link
            {{additionalLibs}}
            {{rpfsPath}}
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
