namespace Surab.Analysis;

public static class AnalysisTestUtils
{
	public static (SurabProject, ProjectFile) CreateProject(string code, bool typeCheckAndAssertNoErrors = false)
	{
		var vfs = VFS.Create();
		var file = vfs.AddCodeFile(code);
		var project = SurabWorkspace.CreateInMemoryProject(vfs);
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

	public static NodeAnnotatedResult CreateFromNodeAnnotatedCode(string annotatedCode)
	{
		var result = AnnotatedCodeUtil.Parse(annotatedCode, treatAnnotatedContentAsCode: true);
		var (project, tree, reasoner) = CreateProjectAndTreeAndReasoner(result.Code);

		return new NodeAnnotatedResult
		{
			Project = project,
			Tree = tree,
			Reasoner = reasoner,
			Annotations = result.Annotations,
		};
	}

	public class NodeAnnotatedResult
	{
		public required SurabProject Project { get; init; }
		public required SyntaxTree Tree { get; init; }
		public required Reasoner Reasoner { get; init; }
		public required IReadOnlyList<AnnotatedCodeUtil.Annotation> Annotations { get; init; }

		public AnnotatedCodeUtil.Annotation GetAnnotationWithName(string name)
		{
			var annotation = Annotations.FirstOrDefault(x => x.Name == name)
				?? throw new Exception($"Could not find '{name}' in annotated code.");
			return annotation;
		}

		public SyntaxNode GetNode(string name)
		{
			var annotation = GetAnnotationWithName(name);
			var node = Tree.GetNodeAt(annotation.FileSpan.Start)
				?? throw new Exception($"Could not find node at annotation of name '{name}'.");
			return node;
		}

		public T GetNode<T>(string name)
			where T : SyntaxNode
		{
			var node = GetNode(name);
			return node.Should().BeAssignableTo<T>().Subject;
		}
	}
}
