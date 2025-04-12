using System.Diagnostics;

namespace Surab.Analysis;

public enum ProjectFileKind
{
	Code, // *.srb
	ProjectConfig, // surab-project.json
}

internal static class ProjectFileHelper
{
	public static ProjectFileKind? DetectFileKind(NormalizedPath path)
	{
		var (name, ext) = GetFileNameAndExt(path);

		if (name == Constants.ProjectConfigFileName)
		{
			return ProjectFileKind.ProjectConfig;
		}

		if (ext == Constants.CodeFileExt)
		{
			return ProjectFileKind.Code;
		}

		return null;
	}

	public static (string Name, string Ext) GetFileNameAndExt(NormalizedPath path)
	{
		var name = Path.GetFileName(path.Value);
		var ext = Path.GetExtension(path.Value);
		return (name, ext);
	}
}

/// <summary>
/// A file in the project. Can be code, or any other important file (e.g. project config).
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public class ProjectFile
{
	public ProjectFile(ProjectFileKind kind, NormalizedPath path, string name, string text, ProjectFolder folder)
	{
		Kind = kind;
		Path = path;
		Name = name;
		Text = text;
		Folder = folder;
	}

	/// <summary>
	/// The kind of the file.
	/// </summary>
	public ProjectFileKind Kind { get; }

	// TODO: Should this be a uri instead?
	/// <summary>
	/// The path of the file. Usually absolute.
	/// </summary>
	public NormalizedPath Path { get; }

	/// <summary>
	/// The name of the file.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// The text content of the file.
	/// </summary>
	public string Text { get; private set; }

	/// <summary>
	/// The folder this file is in.
	/// </summary>
	public ProjectFolder Folder { get; }

	/// <summary>
	/// Don't call this directly from other places. Supposed to be called only from VFS.
	/// </summary>
	internal void UpdateText(string newText)
	{
		Text = newText;
	}

	public static ProjectFile Create(ProjectFileKind kind, NormalizedPath path, string name, string text, ProjectFolder folder)
	{
		return new ProjectFile(kind, path, name, text, folder);
	}

	/// <summary>
	/// Creates a code file with only text. To be used mainly in tests.
	/// </summary>
	public static ProjectFile CreateCode(string code)
	{
		return Create(ProjectFileKind.Code, string.Empty, string.Empty, code, ProjectFolder.GenericRootFolder);
	}

	public override string ToString()
	{
		return $"Kind={Kind} Name=\"{Name}\" Path=\"{Path.Value}\"";
	}
}

/// <summary>
/// A folder in the project.
/// </summary>
[DebuggerDisplay("{ToString(),nq}")]
public class ProjectFolder
{
	public static readonly ProjectFolder GenericRootFolder = new() { Path = string.Empty, Name = string.Empty, IsRoot = true };

	private readonly List<ProjectFile> _files = [];
	private readonly List<ProjectFolder> _subFolders = [];

	public required NormalizedPath Path { get; init; }

	public required string Name { get; init; }

	public required bool IsRoot { get; set; }

	public ProjectFolder? ParentFolder { get; private set; }

	public IReadOnlyList<ProjectFile> Files => _files;

	public IReadOnlyList<ProjectFolder> SubFolders => _subFolders;

	internal void SetParent(ProjectFolder parent)
	{
		parent._subFolders.Add(this);
		ParentFolder = parent;
	}

	internal void AddFile(ProjectFile file)
	{
		_files.Add(file);
	}

	internal void RemoveFile(ProjectFile file)
	{
		_files.Remove(file);
	}

	public override string ToString()
	{
		return $"Name=\"{Name}\" Path=\"{Path.Value}\"";
	}
}

public class VFSChange
{
	public IEnumerable<ProjectFile> AddedFiles { get; init; } = [];

	public IEnumerable<ProjectFile> UpdatedFiles { get; init; } = [];

	public IEnumerable<ProjectFile> RemovedFiles { get; init; } = [];

	public bool AddedOrRemovedAnyFiles => AddedFiles.Any() || UpdatedFiles.Any();

	public bool UpdatedAnyFiles => UpdatedFiles.Any();
}

/// <summary>
/// A VFS to keep track of source files organized into virtual folders.
/// No access to the physical file system is done in this class.
/// </summary>
public class VFS
{
	private readonly FolderInfo _rootFolderInfo;
	private readonly Dictionary<string, ProjectFolder> _folderKeyToFolderMap = [];

	private readonly List<Action<VFSChange>> _changeSubscribers = [];

	private VFS(NormalizedPath projectPath)
	{
		ProjectPath = projectPath;

		var rootFolderKey = projectPath.IsEmpty ? "." : Path.GetRelativePath(projectPath.Value, projectPath.Value);
		_rootFolderInfo = new FolderInfo
		{
			Key = rootFolderKey,
			Path = projectPath,
			Name = projectPath.IsEmpty ? string.Empty : Path.GetFileName(projectPath.Value),
		};
		_folderKeyToFolderMap[rootFolderKey] = new ProjectFolder { Name = Path.GetFileName(projectPath.Value), Path = projectPath, IsRoot = true };
	}

	public NormalizedPath ProjectPath { get; }

	public static VFS Create()
	{
		return Create(string.Empty);
	}

	public static VFS Create(NormalizedPath projectPath)
	{
		var vfs = new VFS(projectPath);
		return vfs;
	}

	public static VFS CreateSimple(string code)
	{
		var vfs = Create();
		vfs.AddCodeFile(code);
		return vfs;
	}

	public Action SubscribeToChanges(Action<VFSChange> action)
	{
		_changeSubscribers.Add(action);
		return () => _changeSubscribers.Remove(action);
	}

	public ProjectFile AddFile(ProjectFileKind kind, NormalizedPath path, string text)
	{
		ValidateNewFileIsInProject(path);
		var (name, _) = ProjectFileHelper.GetFileNameAndExt(path);
		var folder = GetOrAddFolder(path);
		var file = ProjectFile.Create(kind, path, name, text, folder);
		folder.AddFile(file);
		EmitChange(new VFSChange { AddedFiles = [file] });
		return file;
	}

	public ProjectFile AddCodeFile(NormalizedPath path, string text) => AddFile(ProjectFileKind.Code, path, text);

	public ProjectFile AddCodeFile(string code) => AddCodeFile(string.Empty, code);

	public ProjectFile AddFile(NormalizedPath path, string text)
	{
		var kind = ProjectFileHelper.DetectFileKind(path) ?? throw new Exception("Unknown file being added to VFS.");
		return AddFile(kind, path, text);
	}

	/// <summary>
	/// Gets the first project config file that is found under the root folder.
	/// In the case of more than one project config file, an error should be shown somewhere else,
	/// and the first file found should be treated as the effective one.
	/// </summary>
	public ProjectFile? GetProjectConfigFile()
	{
		return GetRootFolder().Files.FirstOrDefault(f => f.Kind is ProjectFileKind.ProjectConfig);
	}

	public bool Contains(ProjectFile file) => Contains(file.Path);

	public bool Contains(NormalizedPath filePath)
	{
		if (ProjectPath.IsEmpty)
		{
			// Probably in a test or an in memory proj.
			// For now go with bruteforce.
			return EnumerateFiles().Any(f => f.Path == filePath);
		}

		// TODO: Improve.
		return !Path.GetRelativePath(ProjectPath.Value, filePath.Value).StartsWith("..");
	}

	private void ValidateNewFileIsInProject(NormalizedPath filePath)
	{
		if (ProjectPath.IsEmpty)
		{
			// Test or in memory. Don't bother.
			return;
		}

		if (!Contains(filePath))
		{
			throw new Exception("File not in project.");
		}
	}

	public void UpdateFile(ProjectFile file, string text)
	{
		file.UpdateText(text);
		EmitChange(new VFSChange { UpdatedFiles = [file] });
	}

	public void RemoveFile(ProjectFile file)
	{
		var folder = GetOrAddFolder(file);
		folder.RemoveFile(file);
		EmitChange(new VFSChange { RemovedFiles = [file] });
	}

	public IEnumerable<ProjectFile> EnumerateFiles()
	{
		foreach (var folder in _folderKeyToFolderMap.Values)
		{
			foreach (var file in folder.Files)
			{
				yield return file;
			}
		}
	}

	public IEnumerable<ProjectFile> EnumerateCodeFiles()
	{
		return EnumerateFiles().Where(f => f.Kind is ProjectFileKind.Code);
	}

	public ProjectFile? GetFile(NormalizedPath path)
	{
		var folder = GetFolder(path);
		var folderInfo = GetFolderInfo(path);
		return folder.Files.FirstOrDefault(x => x.Path.Value == path.Value);
	}

	public ProjectFolder GetRootFolder()
	{
		Debug.Assert(_folderKeyToFolderMap.ContainsKey(_rootFolderInfo.Key));
		return _folderKeyToFolderMap[_rootFolderInfo.Key];
	}

	public ProjectFolder GetFolder(ProjectFile file) => GetFolder(file.Path);

	public ProjectFolder GetFolder(NormalizedPath filePath)
	{
		if (filePath.IsEmpty)
		{
			return GetRootFolder();
		}

		var folderKey = GetFolderInfo(filePath).Key;
		return _folderKeyToFolderMap[folderKey];
	}

	private ProjectFolder GetOrAddFolder(ProjectFile file) => file.Path.IsEmpty ? GetRootFolder() : GetOrAddFolder(file.Path);

	private ProjectFolder GetOrAddFolder(NormalizedPath filePath)
	{
		var folderInfo = GetFolderInfo(filePath);
		if (folderInfo.Key == _rootFolderInfo.Key)
		{
			// Exit early without trying to create missing folders in between since we're on project path.
			return GetRootFolder();
		}

		// REVIEW: Any need to handle concurrency in VFS in general?
		if (!_folderKeyToFolderMap.TryGetValue(folderInfo.Key, out var folder))
		{
			folder = new ProjectFolder { Name = folderInfo.Name, Path = folderInfo.Path, IsRoot = false };
			_folderKeyToFolderMap[folderInfo.Key] = folder;

			// Recurse to fill any missing in between folders and set Parent/SubFolders.
			var dirName = Path.GetDirectoryName(filePath.Value);
			if (!string.IsNullOrEmpty(dirName))
			{
				var parentFolder = GetOrAddFolder(dirName);
				folder.SetParent(parentFolder);
			}
		}

		return folder;
	}

	private FolderInfo GetFolderInfo(NormalizedPath filePath)
	{
		if (filePath.IsEmpty)
		{
			return _rootFolderInfo;
		}

		var folderPath = Path.GetDirectoryName(filePath.Value);
		Debug.Assert(folderPath != null);
		var folderName = Path.GetFileName(folderPath);
		if (string.IsNullOrEmpty(folderName))
		{
			// This can happen in tests mostly, when we have a "/file.srb" path given. Just treat it as a file in the root.
			return _rootFolderInfo;
		}

		var key = GetFolderKey(folderPath);
		return new FolderInfo
		{
			Key = key,
			Path = folderPath,
			Name = folderName,
		};
	}

	private string GetFolderKey(NormalizedPath folderPath)
	{
		// GetRelativePath throws if project path is empty.
		var projectPath = ProjectPath.IsEmpty ? (NormalizedPath)"/" : ProjectPath.Value;
		return Path.GetRelativePath(projectPath.Value, folderPath.Value);
	}

	private void EmitChange(VFSChange change)
	{
		foreach (var subscriber in _changeSubscribers)
		{
			subscriber(change);
		}
	}

	private struct FolderInfo
	{
		public string Key { get; set; }

		public NormalizedPath Path { get; set; }

		public string Name { get; set; }
	}
}
