namespace Surab.Analysis;

internal class ModuleResolver(SurabProject project)
{
	private SurabProject Project { get; } = project;

	public SourceFile? ResolveModule(SourceFile current, string modulePath)
	{
		if (current.Path == null)
			throw new Exception("Can't resolve modules from a text source file.");

		var currentFileDirectory = Path.GetDirectoryName(current.Path);
		if (currentFileDirectory == null)
			return null;

		var targetModulePath = Path.GetFullPath(Path.Combine(currentFileDirectory, modulePath));
		return Project.GetSourceFile(targetModulePath);
	}
}
