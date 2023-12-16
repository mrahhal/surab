namespace Surab.Compilation;

public sealed class CompilationOptions
{
	public required string Name { get; set; }

	public string LLFileName => $"{Name}.ll";

	public static CompilationOptions CreateDefault()
	{
		return new CompilationOptions
		{
			Name = "main",
		};
	}
}
