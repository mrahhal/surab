namespace Surab.Analysis;

public static class AnalysisTestUtils
{
	public static (SurabProject, ProjectFile) CreateProject(string code, bool typeCheckAndAssertNoErrors = false)
	{
		var vfs = VFS.Create();
		var file = vfs.AddCodeFile(code);
		var project = SurabProject.Create(vfs);
		if (typeCheckAndAssertNoErrors)
		{
			project.EnsureAnalyzedAsync().GetAwaiter().GetResult();
			var totalErrors = project.GetProjectDiags().SelectMany(x => x.Diags).Where(x => x.Level >= DiagLevel.Error).ToList();
			AnalysisAssert.Diags(code, [], totalErrors);
		}
		return (project, file);
	}

	public static (SurabProject, SyntaxTree, Reasoner) CreateProjectAndTreeAndReasoner(string code, bool typeCheckAndAssertNoErrors = false)
	{
		var (project, file) = CreateProject(code, typeCheckAndAssertNoErrors);
		return (project, project.GetSyntaxTree(file), project.GetReasoner(file));
	}

	public static (SyntaxTree, Reasoner) CreateTreeAndReasoner(string code, bool typeCheckAndAssertNoErrors = false)
	{
		var (_,  tree, reasoner) = CreateProjectAndTreeAndReasoner(code, typeCheckAndAssertNoErrors);
		return (tree, reasoner);
	}
}
