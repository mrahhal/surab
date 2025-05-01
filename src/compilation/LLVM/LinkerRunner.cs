namespace Surab.Compilation.LLVM;

internal static class LinkerRunner
{
	// Required native libs by Rust.
	// The order and any duplication can be significant on some platforms.
	// To get this list use `cargo rustc -- --print native-static-libs`.
	private static readonly string AdditionalLibs = @"kernel32.lib advapi32.lib bcrypt.lib kernel32.lib ntdll.lib userenv.lib ws2_32.lib kernel32.lib ws2_32.lib kernel32.lib ntdll.lib kernel32.lib msvcrt.lib";

	public static string Run(string triple, IEnumerable<string> objectFiles, string? outputFileNameWithoutExtension = null)
	{
		outputFileNameWithoutExtension ??= "main";
		// TODO: Platform
		var outputFileName = $"{outputFileNameWithoutExtension}.exe";

		var surabRuntimeLib = Path.Combine(PathHelper.GetRuntimeTargetFolderPath(), "debug/surabrt.lib");

		var wd = Directory.GetCurrentDirectory();
		ConsoleHelper.WriteInfo($"Running lld command... Dir: {wd}".NL());
		var outputFilePath = Path.Combine(wd, outputFileName);

		// TODO: Platform. Provide the particular lld arg to use depending on platform.
		if (!SCPP.Clang(
			triple,
			AdditionalLibs.Split(' '),
			[surabRuntimeLib, .. objectFiles.Select(objectFile => Path.Combine(wd, objectFile))],
			outputFilePath,
			out var error))
		{
			throw new Exception("Calling lld failed: " + error);
		}

		var exeFileInfo = new FileInfo(outputFilePath);
		var exeSize = exeFileInfo.Length.ToReadableSize();
		ConsoleHelper.WriteInfo($"Compiled to file of size {exeSize}".NL());

		return outputFilePath;
	}
}
