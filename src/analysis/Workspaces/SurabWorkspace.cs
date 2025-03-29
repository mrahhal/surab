using System.Diagnostics;
using Nito.AsyncEx;

namespace Surab.Analysis;

/// <summary>
/// Represents a Surab workspace that contains one or more Surab projects.
/// </summary>
public class SurabWorkspace
{
	private readonly AsyncReaderWriterLock _rwl = new();
	private readonly SymbolInterner _symbolInterner = new();
	private SurabProject? _stdProject;
	private List<SurabProject>? _projects;
	private List<SurabProject>? _workspaceProjects;
	private ModuleSymbol? _preludeModule;
	private readonly Dictionary<KnownTypeTag, TypeSymbol> _knownTypeTagToTypeSymbolMap = [];
	// This cancellation token will fire when a new change is being requested and just before a write lock is taken.
	private CancellationTokenSource _ctsOnChange = null!;

	private SurabWorkspace(string workspacePath)
	{
		WorkspacePath = workspacePath;
	}

	/// <summary>
	/// Gets the workspace path. Can be an empty string for in memory workspaces.
	/// </summary>
	public NormalizedPath WorkspacePath { get; }

	internal SymbolInterner SymbolInterner => _symbolInterner;

	/// <summary>
	/// Gets the std project. This project is always loaded and available whether it's a part of the workspace or not.
	/// </summary>
	public SurabProject StdProject => _stdProject!;

	public ModuleSymbol PreludeModule => _preludeModule!;

	/// <summary>
	/// Gets workspace projects plus out of workspace project dependencies if any (e.g. std).
	/// </summary>
	public IReadOnlyList<SurabProject> Projects => _projects!;

	/// <summary>
	/// Gets workspace projects. Will not include out of workspace project dependencies.
	/// </summary>
	public IReadOnlyList<SurabProject> WorkspaceProjects => _workspaceProjects!;

	public static SurabWorkspace Create(string workspacePath)
	{
		var workspace = new SurabWorkspace(workspacePath);
		workspace.Initialize();
		return workspace;
	}

	public static SurabWorkspace CreateInMemory(VFS vfs)
	{
		var workspace = new SurabWorkspace(string.Empty);
		workspace.InitializeInMemory(vfs);
		return workspace;
	}

	/// <summary>
	/// Creates a new Surab project from a single in memory code file. Mainly used in tests.
	/// </summary>
	public static SurabProject CreateInMemoryProject(string code)
	{
		var vfs = VFS.CreateSimple(code);
		return CreateInMemoryProject(vfs);
	}

	public static SurabProject CreateInMemoryProject(VFS vfs)
	{
		var workspace = CreateInMemory(vfs);
		return workspace.Projects.First(x => x.VFS == vfs);
	}

	/// <summary>
	/// Locks the workspace for read operations.
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
	/// Locks the workspace for read operations.
	/// </summary>
	/// <remarks>
	/// For convenience, read/write methods won't throw if you use them without locking. It's the responsibility of
	/// the consumer to make sure access is synchronized by locking for read or write when running in a possibly concurrent environment.
	/// Methods will be marked in their doc if they need a read or write lock.
	/// </remarks>
	public T LockForRead<T>(Func<T> func)
	{
		using (_rwl.ReaderLock())
		{
			return func();
		}
	}

	public async Task LockForReadAsync(Func<Task> func)
	{
		using (await _rwl.ReaderLockAsync())
		{
			await func();
		}
	}

	/// <summary>
	/// Locks the workspace for write operations.
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

		using (_rwl.WriterLock())
		{
			action();
			RunWritingHooks();
		}

		// We don't want to await anything here.
		_ = EnsureAnalyzedAsync(ct);
	}

	private void RunWritingHooks()
	{
		foreach (var project in Projects)
		{
			project.RunWritingHook();
		}
	}

	public SurabProject GetFileProject(NormalizedPath filePath)
	{
		foreach (var project in Projects)
		{
			if (project.IsFileInProject(filePath))
			{
				return project;
			}
		}
		// REVIEW:
		throw new UnreachableException();
	}

	public ProjectFile? GetFile(NormalizedPath filePath)
	{
		foreach (var project in Projects)
		{
			if (project.IsFileInProject(filePath.Value))
			{
				var file = project.GetFile(filePath.Value);
				if (file != null)
					return file;
			}
		}

		return null;
	}

	public async Task EnsureAnalyzedAsync(CancellationToken ct = default)
	{
		await LockForReadAsync(async () =>
		{
			var tasks = Projects.Select(p => p.EnsureAnalyzedAsync(ct));
			await Task.WhenAll(tasks);
		});
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
		if (_knownTypeTagToTypeSymbolMap.TryGetValue(tag, out var typeSymbol))
		{
			return typeSymbol;
		}
		throw new UnreachableException();
	}

	internal void AddKnownTypeSymbol(KnownTypeTag tag, TypeSymbol type)
	{
		_knownTypeTagToTypeSymbolMap.Add(tag, type);
	}

	/// <summary>
	/// Resolves module of the reasoner file.
	/// </summary>
	public ModuleSymbol ResolveModule(Reasoner reasoner) => ResolveModule(reasoner.File);

	/// <summary>
	/// Resolves module of the file.
	/// </summary>
	public ModuleSymbol ResolveModule(ProjectFile file)
	{
		var project = Projects.First(p => p.IsFileInProject(file.Path));
		return project.ResolveModule(file);
	}

	/// <summary>
	/// Resolves the target module relative to the reasoner file.
	/// </summary>
	public ModuleSymbol? ResolveModule(Reasoner reasoner, IEnumerable<string> path)
	{
		var module = ResolveModule(reasoner);
		var current = module;
		var found = true;
		var pathIndex = 0;
		foreach (var targetName in path)
		{
			// Resolve relative.
			var targetModule = current.Children.FirstOrDefault(m => m.IdentifierMatches(targetName));
			if (targetModule == null && pathIndex == 0)
			{
				// Resolve absolute from project dependencies.
				targetModule = reasoner.Project.ProjectDependencies.Select(p => p.RootModule).FirstOrDefault(m => m.IdentifierMatches(targetName));
			}
			if (targetModule == null)
			{
				found = false;
				break;
			}
			current = targetModule;
			pathIndex++;
		}
		if (!found)
		{
			return null;
		}
		return current;
	}

	// ---

	private void Initialize()
	{
		Debug.Assert(!string.IsNullOrEmpty(WorkspacePath.Value));

		var projectPaths = FindProjects(WorkspacePath);
		ValidateProjectPathsDoNotOverlap(projectPaths);
		var projects = projectPaths
			.Select(path => CreateProjectAt(this, path))
			.ToList();
		InitializeProjects(projects);
	}

	private void ValidateProjectPathsDoNotOverlap(IEnumerable<NormalizedPath> projectPaths)
	{
		foreach (var p1 in projectPaths)
		{
			foreach (var p2 in projectPaths)
			{
				if (p1 != p2 && (p1.Value.Contains(p2.Value) || p2.Value.Contains(p1.Value)))
				{
					throw new Exception($"Invalid overlap of projects: '{p1.Value}' and '{p2.Value}'");
				}
			}
		}
	}

	private void InitializeInMemory(VFS vfs)
	{
		Debug.Assert(string.IsNullOrEmpty(WorkspacePath.Value));

		var project = SurabProject.Create(this, vfs);
		InitializeProjects([project]);
	}

	private void InitializeProjects(List<SurabProject> workspaceProjects)
	{
		var projects = workspaceProjects;
		var stdPath = (NormalizedPath)GetStdPath();
		var stdProject = workspaceProjects.FirstOrDefault(p => p.ProjectPath == stdPath);
		if (stdProject == null)
		{
			stdProject = LoadStdProject();
			projects = [stdProject, .. workspaceProjects];
		}
		else
		{
			// Ensure std project is first.
			workspaceProjects.Sort((x, y) => x == stdProject ? 1 : (y == stdProject ? -1 : 0));
		}

		_stdProject = stdProject;
		_projects = projects;
		_workspaceProjects = workspaceProjects;

		foreach (var project in projects)
		{
			project.Initialize();
		}
	}

	internal void SetPreludeModule(ModuleSymbol preludeModule)
	{
		_preludeModule = preludeModule;
	}

	private SurabProject LoadStdProject()
	{
		return CreateProjectAt(this, GetStdPath());
	}

	private static string GetStdPath()
	{
		return Path.GetFullPath(Path.Combine(PathHelper.GetSrcFolderPath(), "lib/std"));
	}

	private static IEnumerable<NormalizedPath> FindProjects(NormalizedPath workspacePath)
	{
		var projectConfigFiles = GetProjectConfigFilePaths(workspacePath);
		var projects = projectConfigFiles
			.Select(Path.GetDirectoryName)
			.Where(x => x != null)
			.Select(x => (NormalizedPath)x!);
		return projects!;
	}

	private static SurabProject CreateProjectAt(SurabWorkspace workspace, NormalizedPath projectPath)
	{
		var vfs = CreateVFSAt(projectPath);
		return SurabProject.Create(workspace, vfs);
	}

	private static VFS CreateVFSAt(NormalizedPath projectPath)
	{
		var vfs = VFS.Create(projectPath);

		var projectConfigFiles = GetProjectConfigFilePaths(projectPath);
		var codeFiles = Directory
			.GetFiles(projectPath.Value, Constants.CodeFilesGlob, SearchOption.AllDirectories);

		foreach (var filePath in projectConfigFiles.Concat(codeFiles))
		{
			vfs.AddFile(filePath, File.ReadAllText(filePath));
		}

		return vfs;
	}

	private static IEnumerable<string> GetProjectConfigFilePaths(NormalizedPath @base)
	{
		return Directory.GetFiles(@base.Value, Constants.ProjectConfigFileName, SearchOption.AllDirectories);
	}
}
