using System.Collections.Immutable;
using System.Diagnostics;

namespace Surab.Analysis;

/// <summary>
/// Represents a Surab project.
/// Multiple projects will communicate with each other and cooperate when analyzing a workspace.
/// </summary>
/// <remarks>
/// This isn't thread safe in general.
/// Initial plan is for concurrent queries to be thread safe, but updates can't occur at the same time (will need to use a reader/writer lock).
/// </remarks>
public class SurabProject
{
	private readonly List<Action<IEnumerable<FileDiags>>> _fullDiagsReadyActions = [];
	private readonly Dictionary<ProjectFile, CodeFileState> _codeFileToStateMap = [];

	private readonly List<SurabProject> _projectDependencies = [];
	private readonly List<VFSChange> _vfsChanges = [];

	private ModuleSymbol _rootModule = null!;

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

	public void SubscribeToFullDiagsReady(Action<IEnumerable<FileDiags>> action)
	{
		_fullDiagsReadyActions.Add(action);
	}

	private ModuleSymbol CreateModule(ProjectFolder folder, string? name = null)
	{
		var moduleName = name ?? folder.Name;
		var module = new SrcModuleSymbol(moduleName, [folder], this);

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
	/// </summary>
	/// <remarks>
	/// This method is a read operation.
	/// </remarks>
	public Reasoner GetReasoner(ProjectFile file)
	{
		return GetCodeFileState(file).Reasoner;
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
			yield return GetCodeFileState(sourceFile).Reasoner;
		}
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

	internal IEnumerable<FileDiags> GetProjectDiags()
	{
		foreach (var file in VFS.EnumerateCodeFiles())
		{
			var fileDiags = GetCodeFileState(file).FileDiags;
			if (fileDiags != null)
			{
				yield return fileDiags;
			}
		}
	}

	internal (SyntaxTree, Reasoner) CreateNewTreeAndReasoner(ProjectFile file)
	{
		var module = ResolveModule(file);
		var tree = Parser.ParseFile(file);
		var reasoner = Reasoner.Create(this, module, tree);
		return (tree, reasoner);
	}

	/// <summary>
	/// Analyze files that aren't analyzed.
	/// This method acquires a read lock and returns a task that completes when all files are fully analyzed.
	/// </summary>
	public Task EnsureAnalyzedAsync(CancellationToken ct = default)
	{
		return Task.Run(() =>
		{
			try
			{
				foreach (var file in VFS.EnumerateCodeFiles())
				{
					AnalyzeSourceFile(file, ct);
				}

				ct.ThrowIfCancellationRequested();

				// REVIEW: Should this be here inside the lock?
				RaiseFullDiagsReady();
			}
			catch (OperationCanceledException)
			{
			}
		}, ct);
	}

	private void RaiseFullDiagsReady()
	{
		var diags = GetProjectDiags();
		foreach (var action in _fullDiagsReadyActions)
		{
			action(diags);
		}
	}

	private void AnalyzeSourceFile(ProjectFile file, CancellationToken ct)
	{
		// Later?:
		// reasoner.GetDependentSourceFiles() => each => EnsureAnalyzeSourceFile()

		var state = GetCodeFileState(file);
		var reasoner = state.Reasoner;
		var diags = reasoner.GetAllDiags().ToImmutableArray();
		var highestLevel = diags.Any() ? (DiagLevel?)diags.Select(x => (int)x.Level).Max() : null;
		state.FileDiags = new FileDiags(file.Path, diags, highestLevel);
	}

	private void UpdateTreeAndReasoner(ProjectFile file)
	{
		var (tree, reasoner) = CreateNewTreeAndReasoner(file);

		if (!_codeFileToStateMap.TryGetValue(file, out var state))
		{
			// TODO: LogWarning
			return;
		}

		// TODO: SymbolInterner.CleanReasoner(state.Reasoner)

		state.Tree = tree;
		state.Reasoner = reasoner;
	}

	internal void Initialize()
	{
		PrepProjectDependencies();
		InitializeProjectConfig();
		CreateRootModule();

		VFS.SubscribeToChanges(_vfsChanges.Add);
		foreach (var file in VFS.EnumerateCodeFiles())
		{
			InitializeSourceFile(file);
		}
	}

	private void PrepProjectDependencies()
	{
		if (this != Workspace.StdProject)
		{
			_projectDependencies.Add(Workspace.StdProject);
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
		ResetAfterWrite();
		ProcessVFSChanges();
	}

	private void ResetAfterWrite()
	{
		// Reset cached/computed proj state that is stale on writes, e.g. modules

		// Or, just go ahead for now and create and set the root module (containing all the modules hierarchy).
		CreateRootModule();
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
	}

	private void InitializeSourceFile(ProjectFile file)
	{
		switch (file.Kind)
		{
			case ProjectFileKind.Code:
				{
					var (tree, reasoner) = CreateNewTreeAndReasoner(file);

					_codeFileToStateMap[file] = new CodeFileState
					{
						Tree = tree,
						Reasoner = reasoner,
					};
				}
				break;

			case ProjectFileKind.ProjectConfig:
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
		public required SyntaxTree Tree { get; set; }

		public required Reasoner Reasoner { get; set; }

		public FileDiags? FileDiags { get; set; }
	}
}
