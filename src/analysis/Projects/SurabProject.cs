using System.Collections.Immutable;
using System.Diagnostics;

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
	private readonly ReaderWriterLockSlim _rwl = new();
	private readonly List<Action<IEnumerable<FileDiags>>> _fullDiagsReadyActions = [];
	private readonly Dictionary<ProjectFile, CodeFileState> _codeFileToStateMap = [];

	private readonly SymbolCache _symbolCache = new();
	private readonly List<VFSChange> _vfsChanges = [];

	// This cancellation token will fire when a new change is being requested and just before a write lock is taken.
	private CancellationTokenSource _ctsOnChange = null!;
	private Reasoner _preludeReasoner = default!;
	private ModuleSymbol? _rootModule;

	private SurabProject(VFS vfs)
	{
		VFS = vfs;

		LoadPrelude();
		CreateRootModule();
		Initialize();
	}

	// We start by loading a default config since there's always a possibility a config file doesn't exist.
	// If it exists, this config file will be overriden as the project is initializing files.
	public SurabProjectConfig Config { get; private set; } = SurabProjectConfig.CreateDefault();

	public VFS VFS { get; }

	public NormalizedPath ProjectPath => VFS.ProjectPath;

	internal SymbolCache SymbolCache => _symbolCache;

	internal Reasoner PreludeReasoner => _preludeReasoner;

	/// <summary>
	/// Creates a new Surab project.
	/// </summary>
	public static SurabProject Create(VFS vfs)
	{
		return new SurabProject(vfs);
	}

	/// <summary>
	/// Creates a new Surab project from a single in memory code file. Mainly used in tests.
	/// </summary>
	public static SurabProject CreateSimple(string code)
	{
		var vfs = VFS.CreateSimple(code);
		return Create(vfs);
	}

	public void SubscribeToFullDiagsReady(Action<IEnumerable<FileDiags>> action)
	{
		_fullDiagsReadyActions.Add(action);
	}

	/// <summary>
	/// Locks the project for read operations.
	/// </summary>
	/// <remarks>
	/// For convenience, read/write methods won't throw if you use them without locking. It's the responsibility of
	/// the consumer to make sure access is synchronized by locking for read or write when running in a possibly concurrent environment.
	/// Methods will be marked in their doc if they need a read or write lock.
	/// </remarks>
	public void LockForRead(Action action)
	{
		LockForRead<object?>(() =>
		{
			action();
			return null;
		});
	}

	/// <summary>
	/// Locks the project for read operations.
	/// </summary>
	/// <remarks>
	/// For convenience, read/write methods won't throw if you use them without locking. It's the responsibility of
	/// the consumer to make sure access is synchronized by locking for read or write when running in a possibly concurrent environment.
	/// Methods will be marked in their doc if they need a read or write lock.
	/// </remarks>
	public T LockForRead<T>(Func<T> func)
	{
		_rwl.EnterReadLock();

		try
		{
			return func();
		}
		finally
		{
			_rwl.ExitReadLock();
		}
	}

	/// <summary>
	/// Locks the project for write operations.
	/// </summary>
	/// <remarks>
	/// For convenience, read/write methods won't throw if you use them without locking. It's the responsibility of
	/// the consumer to make sure access is synchronized by locking for read or write when running in an asynchornous environment.
	/// Methods will be marked in their doc if theyre a read or a write operation.
	/// </remarks>
	public void LockForWrite(Action<VFS> action)
	{
		// Cancel in-flight read operations as they will mostly be unneeded now that a write lock is being requested.
		// If we don't cancel, acquiring a write lock will block until all current read operations finish running.
		// Full re-analysis also takes a read lock and this operation can take time, so this is a way to cancel that too.
		_ctsOnChange?.Cancel();
		_ctsOnChange = new();
		var ct = _ctsOnChange.Token;

		_rwl.EnterWriteLock();

		try
		{
			action(VFS);
			ResetAfterWrite();
			ProcessVFSChanges();
		}
		finally
		{
			_rwl.ExitWriteLock();
		}

		// We don't want to await anything here.
		_ = EnsureAnalyzedAsync(ct);
	}

	private void ResetAfterWrite()
	{
		// Reset cached/computed proj state that is stale on writes, e.g. modules

		// Or, just go ahead for now and create and set the root module (containing all the modules hierarchy).
		CreateRootModule();
	}

	private void CreateRootModule()
	{
		_rootModule = CreateModule(VFS.GetRootFolder(), Config.Name);
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

	/// <summary>
	/// Returns all <see cref="Reasoner"/>s in the project.
	/// </summary>
	/// <remarks>
	/// This method is a read operation.
	/// </remarks>
	public IEnumerable<Reasoner> GetAllReasoners()
	{
		yield return _preludeReasoner;

		foreach (var sourceFile in VFS.EnumerateCodeFiles())
		{
			yield return GetCodeFileState(sourceFile).Reasoner;
		}
	}

	/// <summary>
	/// Resolves a known <see cref="TypeSymbol"/> from its given name.
	/// </summary>
	public TypeSymbol? ResolveKnownTypeSymbolFromName(string? name)
	{
		var tag = KnownTypes.GetTag(name);
		if (tag == null)
			return null;

		return ResolveKnownTypeSymbol(tag.Value);
	}

	/// <summary>
	/// Resolves a known <see cref="TypeSymbol"/> from its given <see cref="KnownTypeTag"/>.
	/// </summary>
	public TypeSymbol ResolveKnownTypeSymbol(KnownTypeTag tag)
	{
		return _preludeReasoner.ResolveKnownTypeSymbol(tag);
	}

	public ModuleSymbol RootModule => _rootModule!;

	/// <summary>
	/// Resolves module of the file.
	/// </summary>
	public ModuleSymbol ResolveModule(ProjectFile file)
	{
		var folder = VFS.GetFolder(file);
		var module = FindModule(m => m.Folders.Contains(folder));
		Debug.Assert(module != null);
		return module;
	}

	/// <summary>
	/// Resolves module of the reasoner file.
	/// </summary>
	public ModuleSymbol ResolveModule(Reasoner reasoner) => ResolveModule(reasoner.File);

	/// <summary>
	/// Resolves the target module relative to the reasoner file.
	/// </summary>
	public ModuleSymbol? ResolveModule(Reasoner reasoner, IEnumerable<string> path)
	{
		var module = ResolveModule(reasoner);
		// TODO: Resolve from absolute, etc.
		var current = module;
		var found = true;
		foreach (var targetName in path)
		{
			var targetModule = current.Children.FirstOrDefault(m => m.IdentifierMatches(targetName));
			if (targetModule == null)
			{
				found = false;
				break;
			}
			current = targetModule;
		}
		if (!found)
		{
			return null;
		}
		return current;
	}

	private ModuleSymbol? FindModule(Func<ModuleSymbol, bool> func)
	{
		Debug.Assert(_rootModule != null);
		return FindModule(func, _rootModule);
	}

	private ModuleSymbol? FindModule(Func<ModuleSymbol, bool> func, ModuleSymbol module)
	{
		if (func(module)) return module;
		foreach (var childModule in module.Children)
		{
			var result = FindModule(func, childModule);
			if (result != null) return result;
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
			LockForRead(() =>
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
			});
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

	private void LoadPrelude()
	{
		var preludeFilePath = Path.GetFullPath(Path.Combine(PathHelper.GetSrcFolderPath(), "lib/prelude.surab"));
		var preludeContent = File.ReadAllText(preludeFilePath);
		var preludeSourceFile = ProjectFile.CreateCode(preludeContent);
		var tree = Parser.ParseFile(preludeSourceFile);

		// TODO: Prelude module?
		var module = new SrcModuleSymbol("_prelude_", [], this);
		// Need to assing it before getting diags as that process might use this _preludeReasoner ref to resolve known types.
		_preludeReasoner = Reasoner.CreatePrelude(this, module, tree);
		var diags = _preludeReasoner.GetAllDiags();

		if (diags.Any(x => x.Level >= DiagLevel.Error))
		{
			throw new Exception("Errors in prelude.");
		}
	}

	private void UpdateTreeAndReasoner(ProjectFile file)
	{
		var (tree, reasoner) = CreateNewTreeAndReasoner(file);

		if (!_codeFileToStateMap.TryGetValue(file, out var state))
		{
			// TODO: LogWarning
			return;
		}

		state.Tree = tree;
		state.Reasoner = reasoner;
	}

	private void Initialize()
	{
		VFS.SubscribeToChanges(_vfsChanges.Add);

		foreach (var file in VFS.EnumerateCodeFiles())
		{
			InitializeSourceFile(file);
		}
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
				{
					// Only load and set if the file is directly under the root folder.
					if (file.Folder.IsRoot)
					{
						Config = SurabProjectConfigLoader.Load(file);
					}
				}
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
