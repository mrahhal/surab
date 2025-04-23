namespace Surab.Compilation.LLVM;

internal static class LldRunner
{
	// Required native libs by Rust.
	// The order and any duplication can be significant on some platforms.
	// To get this list use `cargo rustc -- --print native-static-libs`.
	private static readonly string AdditionalLibs = @"kernel32.lib advapi32.lib bcrypt.lib kernel32.lib ntdll.lib userenv.lib ws2_32.lib kernel32.lib ws2_32.lib kernel32.lib ntdll.lib kernel32.lib msvcrt.lib";

	public static string Run(IEnumerable<string> llFiles, string? outputFileNameWithoutExtension = null)
	{
		outputFileNameWithoutExtension ??= "main";
		var outputFileName = $"{outputFileNameWithoutExtension}.exe";

		// TODO:
		// - Use debug vs release libs depending on a compilation flag.
		// - Add -O3 for release?

		var surabRuntimeLib = Path.Combine(PathHelper.GetRuntimeTargetFolderPath(), "debug/surabrt.lib");

		var wd = Directory.GetCurrentDirectory();
		ConsoleHelper.WriteInfo($"Running lld command... Dir: {wd}".NL());
		var outputFilePath = Path.Combine(wd, outputFileName);

		if (!SCPP.Lld(
			AdditionalLibs.Split(' '),
			[surabRuntimeLib, .. llFiles.Select(llFile => Path.Combine(wd, llFile))],
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
