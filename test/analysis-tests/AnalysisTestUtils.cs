namespace Surab.Analysis;

public static class AnalysisTestUtils
{
	public static (SurabProject, SourceFile) CreateProject(string src)
	{
		var sourceFile = SourceFile.FromText(src);
		var tree = Parser.ParseFile(sourceFile);
		return (SurabProject.Create([sourceFile]), sourceFile);
	}

	public static (SurabProject, SyntaxTree, Reasoner) CreateProjectAndTreeAndReasoner(string src)
	{
		var (project, sourceFile) = CreateProject(src);
		return (project, project.GetSyntaxTree(sourceFile), project.GetReasoner(sourceFile));
	}

	public static (SyntaxTree, Reasoner) CreateTreeAndReasoner(string src)
	{
		var (_,  tree, reasoner) = CreateProjectAndTreeAndReasoner(src);
		return (tree, reasoner);
	}
}
