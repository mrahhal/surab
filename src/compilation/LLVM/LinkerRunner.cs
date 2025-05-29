namespace Surab.Compilation.LLVM;

internal static class LinkerRunner
{
	// Required native libs by Rust.
	// The order and any duplication can be significant on some platforms.
	// To get this list use `cargo rustc -- --print native-static-libs`.
	private static readonly string AdditionalLibsWindows = @"kernel32.lib advapi32.lib bcrypt.lib kernel32.lib ntdll.lib userenv.lib ws2_32.lib kernel32.lib ws2_32.lib kernel32.lib ntdll.lib kernel32.lib msvcrt.lib";
	private static readonly string AdditionalLibsLinux = @"gcc_s util rt pthread m dl c";

	public static string Run(string triple, IEnumerable<string> objectFiles, string? outputFileNameWithoutExtension = null)
	{
		outputFileNameWithoutExtension ??= "main";
		var outputFileName = $"{outputFileNameWithoutExtension}";
		var additionalLibs = string.Empty;

		var surabRuntimeLibFileName = string.Empty;

		if (OperatingSystem.IsWindows())
		{
			additionalLibs = AdditionalLibsWindows;
			surabRuntimeLibFileName = "surabrt.lib";
			outputFileName += ".exe";
		}
		else if (OperatingSystem.IsLinux())
		{
			additionalLibs = AdditionalLibsLinux;
			surabRuntimeLibFileName = "libsurabrt.a";
		}
		else
		{
			throw new Exception();
		}

		// TODO: debug/release?
		var surabRuntimeLib = Path.GetFullPath(Path.Combine(PathHelper.GetRuntimeTargetFolderPath(), $"debug/{surabRuntimeLibFileName}"));

		var wd = Directory.GetCurrentDirectory();
		ConsoleHelper.WriteInfo($"Running lld command... Dir: {wd}".NL());
		var outputFilePath = Path.Combine(wd, outputFileName);

		// NOTE: Order is important. In general, dependent should be linked first.
		// This means runtime lib should come after the program's object files.
		// scpp will link the additional libs after input files.

		if (!SCPP.Clang(
			triple,
			additionalLibs.Split(' '),
			[.. objectFiles.Select(objectFile => Path.Combine(wd, objectFile)), surabRuntimeLib],
			outputFilePath,
			out var error))
		{
			throw new Exception("Calling clang failed: " + error);
		}

		var exeFileInfo = new FileInfo(outputFilePath);
		var exeSize = exeFileInfo.Length.ToReadableSize();
		ConsoleHelper.WriteInfo($"Compiled to file of size {exeSize}".NL());

		return outputFilePath;
	}
}
