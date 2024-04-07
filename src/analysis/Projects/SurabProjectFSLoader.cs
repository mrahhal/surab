namespace Surab.Analysis;

/// <summary>
/// Finds and loads Surab projects by looking inside a workspace. Uses physical file system.
/// </summary>
public static class SurabProjectFSLoader
{
	public static IEnumerable<string> FindProjects(string @base)
	{
		var projectConfigFiles = GetProjectConfigFilePaths(@base);
		var projects = projectConfigFiles.Select(Path.GetDirectoryName).Where(x => x != null);
		return projects!;
	}

	public static SurabProject CreateProjectAt(string projectPath)
	{
		var vfs = VFS.Create(projectPath);

		var projectConfigFiles = GetProjectConfigFilePaths(projectPath);
		var codeFiles = Directory
			.GetFiles(projectPath, Constants.CodeFilesGlob, SearchOption.AllDirectories);

		foreach (var filePath in projectConfigFiles.Concat(codeFiles))
		{
			vfs.AddFile(filePath, File.ReadAllText(filePath));
		}

		return SurabProject.Create(vfs);
	}

	private static IEnumerable<string> GetProjectConfigFilePaths(string @base)
	{
		return Directory.GetFiles(@base, Constants.ProjectConfigFileName, SearchOption.AllDirectories);
	}
}
