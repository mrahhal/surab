using System.Diagnostics;

namespace Surab.Compilation.LLVM;

internal static class ClangRunner
{
    // TODO: How to get this properly?
    private static readonly string LibcPath = @"C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\14.38.33130\lib\x64";

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
            {{string.Join(' ', llFiles)}}
            {{rpfsPath}}
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
        var exited = p.WaitForExit(TimeSpan.FromSeconds(15));

        if (p.ExitCode != 0)
        {
            var err = p.StandardError.ReadToEnd();
            throw new Exception("clang failed: " + err);
        }

        return outputFilePath;
    }
}
