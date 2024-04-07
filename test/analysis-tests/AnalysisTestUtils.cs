namespace Surab.Analysis;

public static class AnalysisTestUtils
{
	public static (SurabProject, ProjectFile) CreateProject(string code)
	{
		var vfs = VFS.Create();
		var file = vfs.AddCodeFile(code);
		var project = SurabProject.Create(vfs);
		return (project, file);
	}

	public static (SurabProject, SyntaxTree, Reasoner) CreateProjectAndTreeAndReasoner(string code)
	{
		var (project, file) = CreateProject(code);
		return (project, project.GetSyntaxTree(file), project.GetReasoner(file));
	}

	public static (SyntaxTree, Reasoner) CreateTreeAndReasoner(string code)
	{
		var (_,  tree, reasoner) = CreateProjectAndTreeAndReasoner(code);
		return (tree, reasoner);
	}
}
