using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace Surab.Analysis;

/// <summary>
/// Represents a Surab project.
/// Multiple projects will communicate with each other and cooperate when analyzing a workspace.
/// </summary>
public class SurabProject
{
	private readonly List<Action<SurabProject>> _fullAnalysisDoneActions = [];
	private readonly Dictionary<ProjectFile, CodeFileState> _codeFileToStateMap = [];

	private readonly List<SurabProject> _projectDependencies = [];
	private readonly List<SurabProject> _projectDependents = [];
	private readonly List<VFSChange> _vfsChanges = [];

	private ModuleSymbol _rootModule = null!;
	private bool _dirty = true;
	private Task? _fullAnalysisTask;
	private readonly ConcurrentDictionary<Symbol, List<SyntaxNode>> _symbolToUsagesMap = new(); // filled when type checking during full analysis
	private readonly ConcurrentDictionary<Symbol, string?> _symbolToDocumentation = [];
	private IReadOnlyList<FileDiags>? _projectDiags = null;

	private SurabProject(
		SurabWorkspace workspace,
		VFS vfs)
	{
		Workspace = workspace;
		VFS = vfs;
	}

	// We start by loading a default config since there's always a possibility a config file doesn't exist.
	// If it exists, this config file will be overriden as the project is initializing files.
	public SurabProjectConfig Config { get; private set; } = SurabProjectConfig.CreateDefault();

	public SurabWorkspace Workspace { get; set; }

	// TODO: Transitive?

	public IReadOnlyList<SurabProject> ProjectDependencies => _projectDependencies;

	public IReadOnlyList<SurabProject> ProjectDependents => _projectDependents;

	public VFS VFS { get; }

	public NormalizedPath ProjectPath => VFS.ProjectPath;

	internal SymbolInterner SymbolInterner => Workspace.SymbolInterner;

	public ModuleSymbol RootModule => _rootModule!;

	public bool IsStdProject => this == Workspace.StdProject;

	internal static SurabProject Create(
		SurabWorkspace workspace,
		VFS vfs)
	{
		return new SurabProject(workspace, vfs);
	}

	public void SubscribeToFullAnalysisDone(Action<SurabProject> action)
	{
		_fullAnalysisDoneActions.Add(action);
	}

	private ModuleSymbol CreateModule(ProjectFolder folder, string? name = null)
	{
		var moduleName = name ?? folder.Name;
		var module = new SrcModuleSymbol(moduleName, this, [folder]);

		foreach (var subFolder in folder.SubFolders)
		{
			var childModule = CreateModule(subFolder);
			childModule.SetParent(module);
		}

		return module;
	}

	/// <summary>
	/// Gets the <see cref="SyntaxTree"/> for a given <see cref="SourceFile"/>.
	/// </summary>
	/// <remarks>
	/// This method is a read operation.
	/// </remarks>
	public SyntaxTree GetSyntaxTree(ProjectFile file)
	{
		return GetCodeFileState(file).Tree;
	}

	/// <summary>
	/// Gets the <see cref="Reasoner"/> for a given <see cref="SourceFile"/>.
	/// Ensures the reasoner is built.
	/// </summary>
	/// <remarks>
	/// This method is a read operation.
	/// </remarks>
	public Reasoner GetReasoner(ProjectFile file)
	{
		var reasoner = GetCodeFileState(file).Reasoner;
		reasoner.EnsureBuild();
		return reasoner;
	}

	public IEnumerable<SurabProject> GetInvolvedProjects() => [.. ProjectDependencies, this];

	public SurabProject? GetFileProject(NormalizedPath filePath)
	{
		foreach (var project in GetInvolvedProjects())
		{
			if (project.IsFileInProject(filePath))
			{
				return project;
			}
		}
		return null;
	}

	public ProjectFile? GetFile(NormalizedPath filePath)
	{
		Debug.Assert(IsFileInProject(filePath));
		return VFS.GetFile(filePath);
	}

	public bool IsFileInProject(NormalizedPath filePath)
	{
		return filePath.Value.StartsWith(ProjectPath.Value);
	}

	/// <summary>
	/// Returns all <see cref="Reasoner"/>s in this project and project dependencies.
	/// </summary>
	/// <remarks>
	/// This method is a read operation.
	/// </remarks>
	public IEnumerable<Reasoner> GetAllReasoners()
	{
		foreach (var project in GetInvolvedProjects())
		{
			foreach (var reasoner in project.GetProjectReasoners())
			{
				yield return reasoner;
			}
		}
	}

	public IEnumerable<Reasoner> GetProjectReasoners()
	{
		foreach (var sourceFile in VFS.EnumerateCodeFiles())
		{
			yield return GetReasoner(sourceFile);
		}
	}

	/// <summary>
	/// Looks up the declaration symbol.
	/// Type member (e.g. method) -> the declaration in the trait of the member.
	/// </summary>
	public Symbol? LookupDeclaration(Symbol? symbol)
	{
		if (symbol == null) return null;

		if (symbol.EnclosingImpl != null)
		{
			var implMember = symbol.EnclosingImpl.ProcessedMembers.FirstOrDefault(m => m.ImplSymbol == symbol);
			if (implMember != null)
			{
				return implMember.MatchingTraitSymbol;
			}
		}

		return null;
	}

	internal ModuleSymbol ResolveModule(ProjectFile file)
	{
		Debug.Assert(IsFileInProject(file.Path));
		var folder = VFS.GetFolder(file);
		var module = FindModule(m => m.Folders.Contains(folder));
		Debug.Assert(module != null);
		return module;
	}

	internal ModuleSymbol? FindModule(Func<ModuleSymbol, bool> func)
	{
		Debug.Assert(_rootModule != null);
		return FindModule(func, _rootModule);
	}

	private static ModuleSymbol? FindModule(Func<ModuleSymbol, bool> func, ModuleSymbol module)
	{
		if (func(module))
			return module;
		foreach (var childModule in module.Children)
		{
			var result = FindModule(func, childModule);
			if (result != null)
				return result;
		}
		return null;
	}

	/// <summary>
	/// Gets the project diags.
	/// Only available after full analysis.
	/// </summary>
	public IEnumerable<FileDiags> GetProjectDiags()
	{
		if (_projectDiags != null)
			return _projectDiags;

		// Thread safe, doesn't matter much if we execute this more than once.
		return _projectDiags = [.. VFS.EnumerateCodeFiles().Select(file =>
		{
			var fileDiags = GetCodeFileState(file).FileDiags;
			Debug.Assert(fileDiags != null);
			return fileDiags;
		})];
	}

	/// <summary>
	/// Gets usages of the given symbol in the project.
	/// Only availabe after full analysis.
	/// </summary>
	public IReadOnlyList<SyntaxNode> GetUsagesOfSymbolInProject(Symbol symbol)
	{
		if (_symbolToUsagesMap.TryGetValue(symbol, out var usages))
		{
			return usages;
		}
		return [];
	}

	// So far, only nodes that are associated with a symbol usage:
	// - NameNode
	// - ObjectInitializerItemNode
	// This should be reflected in the type checker.
	internal void StoreSymbolUsage(Symbol symbol, SyntaxNode usage)
	{
		// Always store the original def symbol. Never store a closed generic.
		symbol = symbol.OriginalDef;
		_symbolToUsagesMap.AddOrUpdate(symbol, s =>
		{
			return [usage];
		}, (s, usages) =>
		{
			// REVIEW: Is this lock needed?
			lock (usages)
			{
				usages.Add(usage);
			}
			return usages;
		});
	}

	public bool IsSymbolInProject(Symbol symbol)
	{
		if (symbol.DeclaringFile == null)
			return false;
		return VFS.Contains(symbol.DeclaringFile);
	}

	public string? GetDocumentationOfSymbol(Symbol? symbol)
	{
		if (symbol == null)
			return null;

		return _symbolToDocumentation.GetOrAdd(symbol, ComputeDocumentation);
	}

	private string? ComputeDocumentation(Symbol symbol)
	{
		var node = symbol.DeclaringNode;
		if (node == null)
			return null;

		var firstToken = node.GetTokenAtWithTrivia(node.Span.Line.Start);
		if (firstToken == null)
			return null;

		var leadingTrivia = firstToken.LeadingTrivia.ToList();
		if (leadingTrivia.Count == 0)
			return null;

		// Documentation starts with exactly: `///`
		// Module documentation starts with exactly: `//!` and should be located in the main module file (same name as module folder).

		if (symbol is ModuleSymbol)
		{
			// TODO
		}

		var indexStart = -1;
		// Walk up until we hit a documentation line. Mark as started. Keep walking up until we hit
		// a non documentation line.
		// Walk up until we hit a non documentation line storing the index of lines to be added, until we hit a non document line.
		for (var i = leadingTrivia.Count - 1; i >= 0; i--)
		{
			var trivia = leadingTrivia[i];
			if (indexStart == -1)
			{
				if (trivia.Kind is SyntaxTriviaKind.Whitespace)
				{
					continue;
				}
				else if (IsDocumentationTrivia(trivia))
				{
					indexStart = i;
					continue;
				}
				else
				{
					break;
				}
			}
			else
			{
				if (IsDocumentationTrivia(trivia) || IsWhitespaceWithOneNewLine(trivia))
				{
					indexStart = i;
				}
				else
				{
					break;
				}
			}
		}

		if (indexStart < 0)
			return null;

		var sb = new StringBuilder();

		for (var i = indexStart; i < leadingTrivia.Count; i++)
		{
			var trivia = leadingTrivia[i];
			if (trivia.Kind is SyntaxTriviaKind.LineComment)
			{
				sb.Append(GetLexeme(trivia)[3..].Trim());
			}
			else if (trivia.Kind is SyntaxTriviaKind.Whitespace)
			{
				sb.AppendLine();
			}
		}

		return sb.ToString();

		static bool IsDocumentationTrivia(SyntaxTrivia trivia) =>
			trivia.Kind is SyntaxTriviaKind.LineComment && GetLexeme(trivia).StartsWith("///");

		static ReadOnlySpan<char> GetLexeme(SyntaxTrivia trivia) =>
			trivia.Token.Tree.File.Text.AsSpan()[trivia.Span.File.GetRange()];

		static bool IsWhitespaceWithOneNewLine(SyntaxTrivia trivia) =>
			trivia.Kind is SyntaxTriviaKind.Whitespace && GetLexeme(trivia).Count('\n') == 1;
	}

	private (SyntaxTree, Reasoner) CreateNewTreeAndReasoner(ProjectFile file)
	{
		var module = ResolveModule(file);
		var tree = Parser.ParseFile(file);
		var reasoner = new Reasoner(this, module, tree);
		return (tree, reasoner);
	}

	private void UpdateTreeAndReasoner(ProjectFile file)
	{
		var module = ResolveModule(file);
		var tree = Parser.ParseFile(file);

		if (!_codeFileToStateMap.TryGetValue(file, out var state))
		{
			// TODO: Will this ever happen? Log.
			Debug.Assert(false);
			return;
		}

		// TODO: SymbolInterner.CleanReasoner(state.Reasoner)

		state.Version = Workspace.Version;
		state.Tree = tree;
		state.Reasoner.Reset(module, tree);
	}

	/// <summary>
	/// Analyze files that aren't analyzed.
	/// Returns a task that completes when all files are fully analyzed.
	/// Note: You can also call this method to wait for full analysis to complete if you depend on that.
	/// </summary>
	public Task EnsureAnalyzedAsync(CancellationToken ct = default)
	{
		if (!_dirty)
			return Task.CompletedTask;

		if (_fullAnalysisTask != null)
			return _fullAnalysisTask;

		lock (this)
		{
			if (_fullAnalysisTask != null)
				return _fullAnalysisTask;

			_fullAnalysisTask = Task.Run(() =>
			{
				foreach (var file in VFS.EnumerateCodeFiles())
				{
					if (ct.IsCancellationRequested)
						return;
					AnalyzeSourceFile(file, ct);
				}

				if (ct.IsCancellationRequested)
					return;

				RaiseFullAnalysisDone();
			}, ct);
		}

		return _fullAnalysisTask;
	}

	private void RaiseFullAnalysisDone()
	{
		foreach (var action in _fullAnalysisDoneActions)
		{
			action(this);
		}
	}

	private void AnalyzeSourceFile(ProjectFile file, CancellationToken ct)
	{
		var state = GetCodeFileState(file);
		var reasoner = state.Reasoner;
		reasoner.EnsureBuild();
		var diags = reasoner.EnsureTypeCheckAndGetAllDiags().ToImmutableArray();
		var highestLevel = diags.Any() ? (DiagLevel?)diags.Select(x => (int)x.Level).Max() : null;
		state.FileDiags = new FileDiags(file.Path, diags, highestLevel);
	}

	internal void Initialize()
	{
		PrepProjectDependenciesAndDependents();
		InitializeProjectConfig();
		CreateRootModule();

		VFS.SubscribeToChanges(_vfsChanges.Add);
		foreach (var file in VFS.EnumerateCodeFiles())
		{
			InitializeSourceFile(file);
		}
	}

	private void PrepProjectDependenciesAndDependents()
	{
		// TODO: This is temporary.
		if (this != Workspace.StdProject)
		{
			_projectDependencies.Add(Workspace.StdProject);
		}
		else
		{
			_projectDependents.Add(this);
		}
	}

	private void InitializeProjectConfig()
	{
		var projectConfigFile = VFS.GetProjectConfigFile();
		if (projectConfigFile != null)
		{
			Config = SurabProjectConfigLoader.Load(projectConfigFile);
		}
	}

	private void CreateRootModule()
	{
		_rootModule = CreateModule(VFS.GetRootFolder(), Config.Name);

		if (IsStdProject)
		{
			Workspace.SetPreludeModule(_rootModule.Children.First(x => x.Name == "_prelude"));
		}
	}

	internal void RunWritingHook()
	{
		// 1. Recreate modules if needed.

		var addedOrRemovedAnyFiles = _vfsChanges.Any(x => x.AddedOrRemovedAnyFiles);
		var updatedAnyFiles = _vfsChanges.Any(x => x.UpdatedAnyFiles);

		if (addedOrRemovedAnyFiles)
		{
			// TODO: Optimize by only changing the affected part of the module hierarchy.
			CreateRootModule();
		}

		if (!addedOrRemovedAnyFiles && !updatedAnyFiles)
		{
			return;
		}

		// 2. Process for tree/reasoner.

		ProcessVFSChanges();

		RunForCurrentAndDependentProjects(static p =>
		{
			p.MarkAsDirtyAndClearCache();
			p.ResetAllReasoners();
			p.EagerlyBuildNeededReasoners();
		});
	}

	private void ProcessVFSChanges()
	{
		foreach (var change in _vfsChanges)
		{
			foreach (var file in change.AddedFiles)
			{
				InitializeSourceFile(file);
			}

			foreach (var file in change.UpdatedFiles)
			{
				UpdateTreeAndReasoner(file);
			}

			foreach (var file in change.RemovedFiles)
			{
				_codeFileToStateMap.Remove(file);
			}
		}
		_vfsChanges.Clear();
	}

	private void ResetAllReasoners()
	{
		foreach (var file in VFS.EnumerateCodeFiles())
		{
			var state = GetCodeFileState(file);
			if (state.Version != Workspace.Version)
			{
				state.Version = Workspace.Version;
				// TODO: No need to resolve module if no modules changed.
				//var module = changedModules ? ResolveModule(file) : state.Reasoner.Module;
				var module = ResolveModule(file);
				state.Reasoner.Reset(module, state.Reasoner.Tree);
			}
		}
	}

	/// <summary>
	/// Builds important reasoners eagerly without waiting for analysis (i.e. prelude module).
	/// </summary>
	private void EagerlyBuildNeededReasoners()
	{
		if (!IsStdProject)
			return;
		foreach (var preludeCodeFile in Workspace.PreludeModule.CodeFiles)
		{
			// Builds it
			_ = GetReasoner(preludeCodeFile);
		}
	}

	private void RunForCurrentAndDependentProjects(Action<SurabProject> action)
	{
		action(this);
		foreach (var dependent in ProjectDependents)
		{
			action(dependent);
		}
	}

	private void MarkAsDirtyAndClearCache()
	{
		_dirty = true;

		// Clear cache
		_symbolToUsagesMap.Clear();
		_symbolToDocumentation.Clear();
		_fullAnalysisTask = null;
		_projectDiags = null;
		if (IsStdProject)
		{
			Workspace.ResetKnownTypeSymbols();
		}
	}

	private void InitializeSourceFile(ProjectFile file)
	{
		switch (file.Kind)
		{
			case ProjectFileKind.Code:
				{
					var (tree, reasoner) = CreateNewTreeAndReasoner(file);

					if (IsStdProject && reasoner.Module == Workspace.PreludeModule)
					{
						reasoner.EnsureBuild();
					}

					_codeFileToStateMap[file] = new CodeFileState
					{
						Version = Workspace.Version,
						Tree = tree,
						Reasoner = reasoner,
					};
				}
				break;

			case ProjectFileKind.ProjectConfig:
				// TODO?
				// Processed separately.
				break;

			default:
				throw new NotImplementedException();
		}
	}

	private CodeFileState GetCodeFileState(ProjectFile file)
	{
		Debug.Assert(file.Kind is ProjectFileKind.Code);
		// TODO: LogWarning instead?
		Debug.Assert(_codeFileToStateMap.ContainsKey(file));

		return _codeFileToStateMap[file];
	}

	public class CodeFileState
	{
		public required uint Version { get; set; }

		public required SyntaxTree Tree { get; set; }

		public required Reasoner Reasoner { get; set; }

		public FileDiags? FileDiags { get; set; }
	}
}
