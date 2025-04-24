namespace Surab.Compilation;

public sealed class CompilationOptions
{
	public required string Name { get; set; }

	public string LLFileName => $"{Name}.ll";

	public string ObjectFileName => $"{Name}.o";

	public static CompilationOptions CreateDefault()
	{
		return new CompilationOptions
		{
			Name = "main",
		};
	}
}
