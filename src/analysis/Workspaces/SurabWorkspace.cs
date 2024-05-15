using System.Diagnostics;

namespace Surab.Analysis;

/// <summary>
/// Represents a Surab workspace that contains one or more Surab projects.
/// </summary>
public class SurabWorkspace
{
	private SurabProject? _stdProject;
	private List<SurabProject>? _projects;
	private ModuleSymbol? _preludeModule;

	private readonly Dictionary<KnownTypeTag, TypeSymbol> _knownTypeTagToTypeSymbolMap = [];

	private SurabWorkspace(string workspacePath)
	{
		WorkspacePath = workspacePath;
	}

	/// <summary>
	/// Gets the workspace path. Can be an empty string for in memory workspaces.
	/// </summary>
	public NormalizedPath WorkspacePath { get; }

	/// <summary>
	/// Gets the std project. This project is always loaded and available whether it's a part of the workspace or not.
	/// </summary>
	public SurabProject StdProject => _stdProject!;

	public ModuleSymbol PreludeModule => _preludeModule!;

	/// <summary>
	/// Gets all projects in the workspace, including std.
	/// </summary>
	public IReadOnlyList<SurabProject> Projects => _projects!;

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

	public ProjectFile GetFile(NormalizedPath filePath)
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
		// REVIEW:
		throw new UnreachableException();
	}

	public Task EnsureAnalyzedAsync(CancellationToken ct = default)
	{
		var tasks = Projects.Select(p => p.EnsureAnalyzedAsync(ct));
		return Task.WhenAll(tasks);
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

	// ---

	private void Initialize()
	{
		Debug.Assert(!string.IsNullOrEmpty(WorkspacePath.Value));

		var projectPaths = FindProjects(WorkspacePath);
		ValidateProjectPathsDoNotOverlap(projectPaths);
		var stdPath = (NormalizedPath)GetStdPath();
		var projects = projectPaths
			.Where(path => path != stdPath)
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
				if (p1 != p2 && p1.Value.Contains(p2.Value) || p2.Value.Contains(p1.Value))
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

	private void InitializeProjects(List<SurabProject> projects)
	{
		_stdProject = LoadStdProject();
		Debug.Assert(!projects.Any(p => p.ProjectPath == _stdProject.ProjectPath));
		_projects = [_stdProject, .. projects];

		_preludeModule = StdProject.RootModule.Children.First(x => x.Name == "_prelude");
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
