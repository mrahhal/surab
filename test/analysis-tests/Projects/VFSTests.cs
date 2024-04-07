namespace Surab.Analysis;

// Important: Paths for folders should never end with a separator in the tests below. Will cause problems otherwise.

public class VFSTests
{
	[Fact]
	public void Folders()
	{
		var result = CreateDefaultVFS();
		var vfs = result.VFS;

		var rootFolder = vfs.GetRootFolder();
		rootFolder.Path.Should().Be((NormalizedPath)"/project");
		rootFolder.ParentFolder.Should().BeNull();
		rootFolder.SubFolders.Should().HaveCount(2);
		rootFolder.Files.Should().HaveCount(2);

		var mod1Folder = vfs.GetFolder(result.File3);
		mod1Folder.Path.Should().Be((NormalizedPath)"/project/mod1");
		mod1Folder.ParentFolder.Should().Be(rootFolder);
		mod1Folder.SubFolders.Should().BeEmpty();
		mod1Folder.Files.Should().HaveCount(1);

		var mod3Folder = vfs.GetFolder(result.File4);
		mod3Folder.Path.Should().Be((NormalizedPath)"/project/mod2/mod3");
		var mod2Folder = mod3Folder.ParentFolder!;
		mod2Folder.Should().NotBeNull();
		mod2Folder.Path.Should().Be((NormalizedPath)"/project/mod2");
		mod2Folder.SubFolders.Should().HaveCount(1);
		mod2Folder.Files.Should().BeEmpty();
		mod3Folder.SubFolders.Should().BeEmpty();
		mod3Folder.Files.Should().HaveCount(1);
	}

	[Fact]
	public void GetFile()
	{
		var result = CreateDefaultVFS();
		var vfs = result.VFS;

		vfs.GetFile("/project/nonexistingfile.surab").Should().BeNull();

		vfs.GetFile("/project/file1.surab").Should().Be(result.File1);
		vfs.GetFile("/project/file2.surab").Should().Be(result.File2);
		vfs.GetFile("/project/mod1/file3.surab").Should().Be(result.File3);
		vfs.GetFile("/project/mod2/mod3/file4.surab").Should().Be(result.File4);
	}

	[Fact]
	public void EmptyProjectPath()
	{
		var vfs = VFS.Create();
		var file1 = vfs.AddCodeFile("/file1.surab", "");
		var file2 = vfs.AddCodeFile("/mod1/file2.surab", "");

		file1.Folder.Should().NotBeNull();
		file1.Folder.Name.Should().BeEmpty();
		vfs.GetFile("/file1.surab").Should().Be(file1);
		file2.Folder.Should().NotBeNull();
		file2.Folder.Name.Should().Be("mod1");
		vfs.GetFile("/mod1/file2.surab").Should().Be(file2);
	}

	[Fact]
	public void FileOutsideOfBaseProject_Throws()
	{
		var vfs = VFS.Create("/project");
		Assert.ThrowsAny<Exception>(() =>
		{
			vfs.AddCodeFile("/project/file1.surab", "");
			vfs.AddCodeFile("/out/file2.surab", "");
		});
	}

	private static Result CreateDefaultVFS()
	{
		var vfs = VFS.Create("/project");
		var file1 = vfs.AddCodeFile("/project/file1.surab", "");
		var file2 = vfs.AddCodeFile("/project/file2.surab", "");
		var file3 = vfs.AddCodeFile("/project/mod1/file3.surab", "");
		var file4 = vfs.AddCodeFile("/project/mod2/mod3/file4.surab", "");

		return new Result
		{
			VFS = vfs,
			File1 = file1,
			File2 = file2,
			File3 = file3,
			File4 = file4,
		};
	}

	private class Result
	{
		public required VFS VFS { get; init; }
		public required ProjectFile File1 { get; init; }
		public required ProjectFile File2 { get; init; }
		public required ProjectFile File3 { get; init; }
		public required ProjectFile File4 { get; init; }
	}
}
