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
	private readonly ReaderWriterLockSlim _rwl = new();
	// This cancellation token will fire when a new change is being requested and just before a write lock is taken.
	private CancellationTokenSource _ctsOnChange = null!;
	private readonly List<Action<IEnumerable<FileDiagnostics>>> _fullDiagnosticsReadyActions = [];

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
		}
		finally
		{
			_rwl.ExitWriteLock();
		}

		// We don't want to await anything here.
		_ = EnsureAnalyzedAsync(ct);
	}

	/// <summary>
	/// Adds a <see cref="SourceFile"/> to the project.
	/// </summary>
	/// <remarks>
	/// This method is a write operation.
	/// </remarks>
	public void AddSourceFile(SourceFile sourceFile)
	{
		_sourceFiles.Add(sourceFile);
		InitializeSourceFile(sourceFile);
		MarkChangedDependencies(sourceFile);
	}

	/// <summary>
	/// Updates a <see cref="SourceFile"/> with new content.
	/// </summary>
	/// <remarks>
	/// This method is a write operation.
	/// </remarks>
	public void UpdateSourceFile(SourceFile sourceFile, string text)
	{
		sourceFile.UpdateText(text);
		UpdateTreeAndReasoner(sourceFile);
		MarkChangedDependencies(sourceFile);
	}

	/// <summary>
	/// Removes a <see cref="SourceFile"/> from the project.
	/// </summary>
	/// <remarks>
	/// This method is a write operation.
	/// </remarks>
	public void RemoveSourceFile(SourceFile sourceFile)
	{
		_sourceFiles.Remove(sourceFile);
		_sourceFileToStateMap.Remove(sourceFile);
		MarkChangedDependencies(sourceFile);
	}

	/// <summary>
	/// Gets the <see cref="SyntaxTree"/> for a given <see cref="SourceFile"/>.
	/// </summary>
	/// <remarks>
	/// This method is a read operation.
	/// </remarks>
	public SyntaxTree GetSyntaxTree(SourceFile sourceFile)
	{
		return _sourceFileToStateMap[sourceFile].Tree;
	}

	/// <summary>
	/// Gets the <see cref="Reasoner"/> for a given <see cref="SourceFile"/>.
	/// </summary>
	/// <remarks>
	/// This method is a read operation.
	/// </remarks>
	public Reasoner GetReasoner(SourceFile sourceFile)
	{
		return _sourceFileToStateMap[sourceFile].Reasoner;
	}

	internal IEnumerable<FileDiagnostics> GetProjectDiagnostics()
	{
		foreach (var sourceFileState in _sourceFileToStateMap.Values)
		{
			if (sourceFileState.FileDiagnostics != null)
				yield return sourceFileState.FileDiagnostics;
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

		foreach (var sourceFileState in _sourceFileToStateMap.Values)
		{
			yield return sourceFileState.Reasoner;
		}
	}

	private (SyntaxTree, Reasoner) CreateNewTreeAndReasoner(SourceFile sourceFile)
	{
		var tree = Parser.ParseFile(sourceFile);
		var reasoner = Reasoner.Create(this, tree);
		return (tree, reasoner);
	}

	private void InitializeSourceFile(SourceFile sourceFile)
	{
		var (tree, reasoner) = CreateNewTreeAndReasoner(sourceFile);

		_sourceFileToStateMap[sourceFile] = new SourceFileState
		{
			IsAnalyzed = false,
			Tree = tree,
			Reasoner = reasoner,
		};
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

	private void MarkChangedDependencies(SourceFile sourceFile)
	{
		// TODO: Do this properly.
		// If the dependency only uses exported symbols that didn't change (or if exported symbols didn't change in the first place), don't mark it as changed.

		// souceFile is the file that changed. Go to all files that have a dependency on this file and mark them to be re-analyzed.
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
					foreach (var sourceFile in _sourceFiles)
					{
						EnsureAnalyzeSourceFile(sourceFile, ct);
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

	private void EnsureAnalyzeSourceFile(SourceFile sourceFile, CancellationToken ct)
	{
		// Can call AnalyzeSourceFile on other dependent files.

		if (!_sourceFileToStateMap.TryGetValue(sourceFile, out var state))
		{
			// TODO: LogWarning
			return;
		}

		// Can't tell if the changed file affected others so for now we're reanalysing all files.
		//if (state.IsAnalyzed)
		//	return;

		AnalyzeSourceFile(sourceFile, state, ct);
	}

	private void AnalyzeSourceFile(SourceFile sourceFile, SourceFileState state, CancellationToken ct)
	{
		// Later:
		// reasoner.GetDependentSourceFiles() => each => EnsureAnalyzeSourceFile()

		var reasoner = GetReasoner(sourceFile);
		var diags = reasoner.GetAllDiagnostics().ToImmutableArray();
		var highestLevel = diags.Any() ? (DiagnosticLevel?)diags.Select(x => (int)x.Level).Max() : null;
		state.FileDiagnostics = new FileDiagnostics(sourceFile, diags, highestLevel);

		state.IsAnalyzed = true;
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

		public required SyntaxTree Tree { get; set; }

		public required Reasoner Reasoner { get; set; }

		public FileDiagnostics? FileDiagnostics { get; set; }
	}
}
