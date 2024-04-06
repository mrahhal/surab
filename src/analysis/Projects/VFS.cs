using System.Diagnostics;

namespace Surab.Analysis;

public class VFSFolder
{
	public required string Name { get; set; }

	public required NormalizedPath Path { get; set; }

	public VFSFolder? ParentFolder { get; set; }

	public List<SourceFile> SourceFiles { get; set; } = [];

	public List<VFSFolder> SubFolders { get; set; } = [];
}

/// <summary>
/// A VFS to keep track of source files organized into virtual folders.
/// No access to the physical file system is done in this class.
/// </summary>
public class VFS
{
	private readonly NormalizedPath _projectPath;
	private readonly string _rootFolderKey;
	private readonly Dictionary<string, VFSFolder> _folderKeyToFolderMap = [];

	private VFS(NormalizedPath projectPath)
	{
		_projectPath = projectPath;
		// TODO: In memory projects?
		_rootFolderKey = projectPath.IsEmpty ? "." : Path.GetRelativePath(projectPath.Value, projectPath.Value);
		_folderKeyToFolderMap[_rootFolderKey] = new VFSFolder { Name = projectPath.IsEmpty ? "inmem" : Path.GetFileName(projectPath.Value), Path = projectPath };
	}

	public static VFS Create(NormalizedPath projectPath, IEnumerable<SourceFile> sourceFiles)
	{
		var vfs = new VFS(projectPath);
		foreach (var sourceFile in sourceFiles)
		{
			vfs.AddSourceFile(sourceFile);
		}
		return vfs;
	}

	public void AddSourceFile(SourceFile sourceFile)
	{
		ValidateIsInProject(sourceFile);
		var folder = EnsureGetFolder(sourceFile);
		folder.SourceFiles.Add(sourceFile);
	}

	private void ValidateIsInProject(SourceFile sourceFile)
	{
		// TODO:
		if (!sourceFile.Path.IsEmpty && Path.GetRelativePath(_projectPath.Value, sourceFile.Path.Value).StartsWith(".."))
		{
			throw new Exception("Source file not in project.");
		}
	}

	public void UpdateSourceFile(SourceFile sourceFile, string text)
	{
		sourceFile.UpdateText(text);
	}

	public void RemoveSourceFile(SourceFile sourceFile)
	{
		var folder = EnsureGetFolder(sourceFile);
		folder.SourceFiles.Remove(sourceFile);
	}

	public IEnumerable<SourceFile> EnumerateSourceFiles()
	{
		foreach (var folder in _folderKeyToFolderMap.Values)
		{
			foreach (var sourceFile in folder.SourceFiles)
			{
				yield return sourceFile;
			}
		}
	}

	public SourceFile? GetSourceFile(NormalizedPath path)
	{
		var folderInfo = GetFolderInfo(path);
		if (_folderKeyToFolderMap.TryGetValue(folderInfo.Key, out var folder))
		{
			return folder.SourceFiles.FirstOrDefault(x => x.Path.Value == path.Value);
		}
		return null;
	}

	public VFSFolder GetRootFolder()
	{
		Debug.Assert(_folderKeyToFolderMap.ContainsKey(_rootFolderKey));
		return _folderKeyToFolderMap[_rootFolderKey];
	}

	public VFSFolder GetFolder(SourceFile sourceFile)
	{
		if (sourceFile.Path.IsEmpty)
		{
			return GetRootFolder();
		}

		var folderKey = GetFolderInfo(sourceFile).Key;
		Debug.Assert(_folderKeyToFolderMap.ContainsKey(folderKey));
		return _folderKeyToFolderMap[folderKey];
	}

	private VFSFolder EnsureGetFolder(SourceFile sourceFile) => sourceFile.Path.IsEmpty ? GetRootFolder() : EnsureGetFolder(sourceFile.Path);

	private VFSFolder EnsureGetFolder(NormalizedPath filePath)
	{
		var folderInfo = GetFolderInfo(filePath);
		if (folderInfo.Key == _rootFolderKey)
		{
			// Exit early without trying to create missing folders in between since we're on project path.
			return _folderKeyToFolderMap[_rootFolderKey];
		}

		// REVIEW: Any need to handle concurrency in VFS in general?
		if (!_folderKeyToFolderMap.TryGetValue(folderInfo.Key, out var folder))
		{
			folder = new VFSFolder { Name = folderInfo.Name, Path = folderInfo.Path };
			_folderKeyToFolderMap[folderInfo.Key] = folder;
		}

		// Recurse to fill any missing in between folders and set Parent/SubFolders.
		var dirName = Path.GetDirectoryName(filePath.Value);
		if (!string.IsNullOrEmpty(dirName))
		{
			var parentFolder = EnsureGetFolder(dirName);
			folder.ParentFolder = parentFolder;
			parentFolder.SubFolders.Add(folder);
		}

		return folder;
	}

	private FolderInfo GetFolderInfo(SourceFile sourceFile) => sourceFile.Path.IsEmpty ? GetFolderInfo(_projectPath) : GetFolderInfo(sourceFile.Path);

	private FolderInfo GetFolderInfo(NormalizedPath filePath)
	{
		var folderPath = Path.GetDirectoryName(filePath.Value);
		Debug.Assert(folderPath != null);
		var folderName = Path.GetFileName(folderPath);
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
		return Path.GetRelativePath(_projectPath.Value, folderPath.Value);
	}

	private struct FolderInfo
	{
		public string Key { get; set; }

		public string Path { get; set; }

		public string Name { get; set; }
	}
}
