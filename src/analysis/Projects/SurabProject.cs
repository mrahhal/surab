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
	private readonly List<Action<IEnumerable<FileDiagnostics>>> _fullDiagnosticsReadyActions = [];
	private readonly Dictionary<SourceFile, SourceFileState> _sourceFileToStateMap = [];

	private readonly VFS _vfs;
	private readonly ReasoningContext _reasoningContext = new();

	// This cancellation token will fire when a new change is being requested and just before a write lock is taken.
	private CancellationTokenSource _ctsOnChange = null!;
	private Reasoner _preludeReasoner = default!;
	private ModuleSymbol? _rootModule;

	private SurabProject(
		NormalizedPath projectPath,
		SurabProjectConfig config,
		IEnumerable<SourceFile> sourceFiles)
	{
		ProjectPath = projectPath;
		Config = config;
		_vfs = VFS.Create(projectPath, sourceFiles);
	}

	public NormalizedPath ProjectPath { get; }

	public SurabProjectConfig Config { get; }

	internal ReasoningContext ReasoningContext => _reasoningContext;

	internal Reasoner PreludeReasoner => _preludeReasoner;

	internal VFS VFS => _vfs;

	public static SurabProject Create(IEnumerable<SourceFile> sourceFiles)
	{
		return Create(string.Empty, sourceFiles);
	}

	public static SurabProject Create(
		string projectPath,
		IEnumerable<SourceFile> sourceFiles)
	{
		var normalizedProjectPath = (NormalizedPath)projectPath;
		var config = SurabProjectConfigLoader.Load(normalizedProjectPath);
		var project = new SurabProject(normalizedProjectPath, config, sourceFiles);
		project.LoadPrelude();
		project.InitializeSourceFiles();
		project.CreateRootModule();
		return project;
	}

	public SourceFile? GetSourceFile(string path)
	{
		return VFS.GetSourceFile(path);
	}

	public void SubscribeToFullDiagnosticsReady(Action<IEnumerable<FileDiagnostics>> action)
	{
		_fullDiagnosticsReadyActions.Add(action);
	}

	/// <summary>
	/// Locks the project for read operations.
	/// </summary>
	/// <remarks>
	/// For convenience, read/write methods won't throw if you use them without locking. It's the responsibility of
	/// the consumer to make sure access is synchronized by locking for read or write when running in an asynchornous environment.
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
	/// the consumer to make sure access is synchronized by locking for read or write when running in an asynchornous environment.
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
	public void LockForWrite(Action action)
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
			action();
			ResetAfterWrite();
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

	private ModuleSymbol CreateModule(VFSFolder folder, string? name = null)
	{
		var moduleName = name ?? folder.Name;
		var module = new ModuleSymbol(moduleName, [folder]);
		var moduleScope = new ModuleScope(this, folder.SourceFiles);
		module.SetScope(moduleScope);

		foreach (var subFolder in folder.SubFolders)
		{
			var childModule = CreateModule(subFolder);
			childModule.SetParent(module);
		}

		return module;
	}

	/// <summary>
	/// Adds a <see cref="SourceFile"/> to the project.
	/// </summary>
	/// <remarks>
	/// This method is a write operation.
	/// </remarks>
	public void AddSourceFile(SourceFile sourceFile)
	{
		VFS.AddSourceFile(sourceFile);
		InitializeSourceFile(sourceFile);
	}

	/// <summary>
	/// Updates a <see cref="SourceFile"/> with new content.
	/// </summary>
	/// <remarks>
	/// This method is a write operation.
	/// </remarks>
	public void UpdateSourceFile(SourceFile sourceFile, string text)
	{
		VFS.UpdateSourceFile(sourceFile, text);
		UpdateTreeAndReasoner(sourceFile);
	}

	/// <summary>
	/// Removes a <see cref="SourceFile"/> from the project.
	/// </summary>
	/// <remarks>
	/// This method is a write operation.
	/// </remarks>
	public void RemoveSourceFile(SourceFile sourceFile)
	{
		VFS.RemoveSourceFile(sourceFile);
		_sourceFileToStateMap.Remove(sourceFile);
	}

	/// <summary>
	/// Gets the <see cref="SyntaxTree"/> for a given <see cref="SourceFile"/>.
	/// </summary>
	/// <remarks>
	/// This method is a read operation.
	/// </remarks>
	public SyntaxTree GetSyntaxTree(SourceFile sourceFile)
	{
		return GetSourceFileState(sourceFile).Tree;
	}

	/// <summary>
	/// Gets the <see cref="Reasoner"/> for a given <see cref="SourceFile"/>.
	/// </summary>
	/// <remarks>
	/// This method is a read operation.
	/// </remarks>
	public Reasoner GetReasoner(SourceFile sourceFile)
	{
		return GetSourceFileState(sourceFile).Reasoner;
	}

	/// <summary>
	/// Resolves module of the reasoner file.
	/// </summary>
	public ModuleSymbol ResolveModule(Reasoner reasoner)
	{
		var folder = VFS.GetFolder(reasoner.SourceFile);
		var module = FindModule(m => m.Folders.Contains(folder));
		Debug.Assert(module != null);
		return module;
	}

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
			var targetModule = current.Children.FirstOrDefault(m => m.Name == targetName);
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

	internal IEnumerable<FileDiagnostics> GetProjectDiagnostics()
	{
		foreach (var sourceFile in VFS.EnumerateSourceFiles())
		{
			var fileDiagnostics = GetSourceFileState(sourceFile).FileDiagnostics;
			if (fileDiagnostics != null)
				yield return fileDiagnostics;
		}
	}

	/// <summary>
	/// Resolves a known <see cref="TypeSymbol"/> from its given name.
	/// </summary>
	/// <remarks>
	/// This method is a read operation.
	/// </remarks>
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
	/// <remarks>
	/// This method is a read operation.
	/// </remarks>
	public TypeSymbol ResolveKnownTypeSymbol(KnownTypeTag tag)
	{
		return _preludeReasoner.ResolveKnownTypeSymbol(tag);
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

		foreach (var sourceFile in VFS.EnumerateSourceFiles())
		{
			yield return GetSourceFileState(sourceFile).Reasoner;
		}
	}

	internal (SyntaxTree, Reasoner) CreateNewTreeAndReasoner(SourceFile sourceFile)
	{
		var tree = Parser.ParseFile(sourceFile);
		var reasoner = Reasoner.Create(this, tree);
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
					foreach (var sourceFile in VFS.EnumerateSourceFiles())
					{
						AnalyzeSourceFile(sourceFile, ct);
					}

					ct.ThrowIfCancellationRequested();

					// REVIEW: Should this be here inside the lock?
					RaiseFullDiagnosticsReady();
				}
				catch (OperationCanceledException)
				{
				}
			});
		}, ct);
	}

	private void RaiseFullDiagnosticsReady()
	{
		var diags = GetProjectDiagnostics();
		foreach (var action in _fullDiagnosticsReadyActions)
		{
			action(diags);
		}
	}

	private void AnalyzeSourceFile(SourceFile sourceFile, CancellationToken ct)
	{
		// Later?:
		// reasoner.GetDependentSourceFiles() => each => EnsureAnalyzeSourceFile()

		var state = GetSourceFileState(sourceFile);
		var reasoner = state.Reasoner;
		var diags = reasoner.GetAllDiagnostics().ToImmutableArray();
		var highestLevel = diags.Any() ? (DiagnosticLevel?)diags.Select(x => (int)x.Level).Max() : null;
		state.FileDiagnostics = new FileDiagnostics(sourceFile, diags, highestLevel);
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

	private void UpdateTreeAndReasoner(SourceFile sourceFile)
	{
		var (tree, reasoner) = CreateNewTreeAndReasoner(sourceFile);

		if (!_sourceFileToStateMap.TryGetValue(sourceFile, out var state))
		{
			// TODO: LogWarning
			return;
		}

		state.Tree = tree;
		state.Reasoner = reasoner;
	}

	private void InitializeSourceFiles()
	{
		foreach (var file in VFS.EnumerateSourceFiles())
		{
			InitializeSourceFile(file);
		}
	}

	private void InitializeSourceFile(SourceFile sourceFile)
	{
		var (tree, reasoner) = CreateNewTreeAndReasoner(sourceFile);

		_sourceFileToStateMap[sourceFile] = new SourceFileState
		{
			Tree = tree,
			Reasoner = reasoner,
		};
	}

	private SourceFileState GetSourceFileState(SourceFile sourceFile)
	{
		// TODO: LogWarning instead?
		Debug.Assert(_sourceFileToStateMap.ContainsKey(sourceFile));

		return _sourceFileToStateMap[sourceFile];
	}

	public class SourceFileState
	{
		public required SyntaxTree Tree { get; set; }

		public required Reasoner Reasoner { get; set; }

		public FileDiagnostics? FileDiagnostics { get; set; }
	}
}
