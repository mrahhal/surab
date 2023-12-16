using System.Collections.Immutable;

namespace Surab.Analysis;

/// <summary>
/// Everything that makes up a Surab project.
/// </summary>
/// <remarks>
/// This isn't thread safe in general.
/// Initial plan is for concurrent queries to be thread safe, but updates can't occur at the same time (will need to use a reader/writer lock).
/// </remarks>
public class SurabProject
{
	private readonly List<SourceFile> _sourceFiles;
	private readonly Dictionary<SourceFile, SourceFileState> _sourceFileToStateMap = [];

	private SurabProject(IEnumerable<SourceFile> sourceFiles)
	{
		_sourceFiles = sourceFiles.ToList();

		foreach (var sourceFile in _sourceFiles)
		{
			InitializeSourceFile(sourceFile);
		}
	}

	public static SurabProject Create(
		IEnumerable<SourceFile> sourceFiles)
	{
		return new SurabProject(sourceFiles);
	}

	public IReadOnlyList<SourceFile> SourceFiles => _sourceFiles;

	public void AddSourceFile(SourceFile sourceFile)
	{
		_sourceFiles.Add(sourceFile);
		InitializeSourceFile(sourceFile);
		MarkChangedDependencies(sourceFile);
	}

	public void UpdateSourceFile(SourceFile sourceFile, string text)
	{
		sourceFile.UpdateText(text);
		MarkChangedDependencies(sourceFile);
	}

	public void RemoveSourceFile(SourceFile sourceFile)
	{
		_sourceFiles.Remove(sourceFile);
		_sourceFileToStateMap.Remove(sourceFile);
		MarkChangedDependencies(sourceFile);
	}

	private void MarkChangedDependencies(SourceFile sourceFile)
	{
		// TODO: Do this properly.
		// If the dependency only uses exported symbols that didn't change (or if exported symbols didn't change in the first place), don't mark it as changed.

		// souceFile is the file that changed. Go to all files that have a dependency on this file and mark them to be re-analyzed.
	}

	/// <summary>
	/// Analyze files that aren't analyzed.
	/// </summary>
	public void EnsureAnalyzed()
	{
		foreach (var sourceFile in _sourceFiles)
		{
			EnsureAnalyzeSourceFile(sourceFile);
		}
	}

	public IEnumerable<FileDiagnostics> GetProjectDiagnostics()
	{
		foreach (var sourceFileState in _sourceFileToStateMap.Values)
		{
			if (sourceFileState.FileDiagnostics != null)
				yield return sourceFileState.FileDiagnostics;
		}
	}

	public SyntaxTree GetSyntaxTree(SourceFile sourceFile)
	{
		return _sourceFileToStateMap[sourceFile].SyntaxTree;
	}

	public Reasoner GetReasoner(SourceFile sourceFile)
	{
		return _sourceFileToStateMap[sourceFile].Reasoner;
	}

	public IEnumerable<Reasoner> GetAllReasoners()
	{
		foreach (var sourceFileState in _sourceFileToStateMap.Values)
		{
			yield return sourceFileState.Reasoner;
		}
	}

	private void InitializeSourceFile(SourceFile sourceFile)
	{
		var tree = Parser.ParseModule(sourceFile);
		var reasoner = Reasoner.Create(tree);

		_sourceFileToStateMap[sourceFile] = new SourceFileState
		{
			IsAnalyzed = false,
			SyntaxTree = tree,
			Reasoner = reasoner,
		};
	}

	private void EnsureAnalyzeSourceFile(SourceFile sourceFile)
	{
		// Can call AnalyzeSourceFile on other dependent files.

		var state = _sourceFileToStateMap[sourceFile];
		//if (state.IsAnalyzed) return;

		AnalyzeSourceFile(sourceFile, state);
	}

	private void AnalyzeSourceFile(SourceFile sourceFile, SourceFileState state)
	{
		state.IsAnalyzed = true;

		// Later:
		// reasoner.GetDependentSourceFiles() => each => EnsureAnalyzeSourceFile()

		var tree = Parser.ParseModule(sourceFile);
		var reasoner = Reasoner.Create(tree);
		state.SyntaxTree = tree;
		state.Reasoner = reasoner;

		var diags = reasoner.GetAllDiagnostics().ToImmutableArray();
		state.FileDiagnostics = new FileDiagnostics(sourceFile, diags);
	}

	private class SourceFileState
	{
		public bool IsAnalyzed { get; set; }

		public required SyntaxTree SyntaxTree { get; set; }

		public required Reasoner Reasoner { get; set; }

		public FileDiagnostics? FileDiagnostics { get; set; }
	}
}
