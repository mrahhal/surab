using System.Collections.Immutable;
using System.Xml.Linq;

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

	private readonly ModuleResolver _moduleResolver;
	private readonly ReasoningContext _reasoningContext = new();

	private Reasoner _preludeReasoner = default!;

	private SurabProject(IEnumerable<SourceFile> sourceFiles)
	{
		_moduleResolver = new(this);
		_sourceFiles = sourceFiles.ToList();
	}

	public static SurabProject Create(
		IEnumerable<SourceFile> sourceFiles)
	{
		var project = new SurabProject(sourceFiles);
		project.LoadPrelude();
		project.InitializeSourceFiles();
		return project;
	}

	public IReadOnlyList<SourceFile> SourceFiles => _sourceFiles;

	internal ReasoningContext ReasoningContext => _reasoningContext;

	internal Reasoner PreludeReasoner => _preludeReasoner;

	internal ModuleResolver ModuleResolver => _moduleResolver;

	public SourceFile? GetSourceFile(string path)
	{
		// TODO: Optimize.
		return _sourceFiles.FirstOrDefault(x => x.Path == path);
	}

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

	public TypeSymbol? ResolveKnownTypeSymbolFromName(string? name)
	{
		var tag = KnownTypes.GetTag(name);
		if (tag == null)
			return null;

		return ResolveKnownTypeSymbol(tag.Value);
	}

	public TypeSymbol ResolveKnownTypeSymbol(KnownTypeTag tag)
	{
		return _preludeReasoner.ResolveKnownTypeSymbol(tag);
	}

	public IEnumerable<Reasoner> GetAllReasoners()
	{
		yield return _preludeReasoner;

		foreach (var sourceFileState in _sourceFileToStateMap.Values)
		{
			yield return sourceFileState.Reasoner;
		}
	}

	private void InitializeSourceFile(SourceFile sourceFile)
	{
		var tree = Parser.ParseFile(sourceFile);
		var reasoner = Reasoner.Create(this, tree);

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

		var tree = Parser.ParseFile(sourceFile);
		var reasoner = Reasoner.Create(this, tree);
		state.SyntaxTree = tree;
		state.Reasoner = reasoner;

		var diags = reasoner.GetAllDiagnostics().ToImmutableArray();
		state.FileDiagnostics = new FileDiagnostics(sourceFile, diags);
	}

	private void LoadPrelude()
	{
		var preludeFilePath = Path.GetFullPath(Path.Combine(PathHelper.GetSrcFolderPath(), "lib/prelude.surab"));
		var preludeContent = File.ReadAllText(preludeFilePath);
		var preludeSourceFile = SourceFile.Create(preludeFilePath, preludeContent);
		var tree = Parser.ParseFile(preludeSourceFile);

		// Need to assing it before getting diags as that process might use this _preludeReasoner ref to resolve known types.
		_preludeReasoner = Reasoner.CreatePrelude(this, tree);
		var diags = _preludeReasoner.GetAllDiagnostics();

		if (diags.Any(x => x.Level >= DiagnosticLevel.Error))
		{
			throw new Exception("Errors in prelude.");
		}
	}

	private void InitializeSourceFiles()
	{
		foreach (var sourceFile in _sourceFiles)
		{
			InitializeSourceFile(sourceFile);
		}
	}

	private class SourceFileState
	{
		public bool IsAnalyzed { get; set; }

		public required SyntaxTree SyntaxTree { get; set; }

		public required Reasoner Reasoner { get; set; }

		public FileDiagnostics? FileDiagnostics { get; set; }
	}
}
