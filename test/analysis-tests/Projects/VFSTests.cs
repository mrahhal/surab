namespace Surab.Analysis;

public class VFSTests
{
	[Fact]
	public void Folders()
	{
		var file3 = SourceFile.Create("/project/mod1/file3.surab", "");
		var file4 = SourceFile.Create("/project/mod2/mod3/file4.surab", "");
		var vfs = VFS.Create("/project", [
			SourceFile.Create("/project/file1.surab", ""),
			SourceFile.Create("/project/file2.surab", ""),
			file3,
			file4,
		]);

		var rootFolder = vfs.GetRootFolder();
		rootFolder.Path.Should().Be((NormalizedPath)"/project");
		rootFolder.ParentFolder.Should().BeNull();
		rootFolder.SubFolders.Should().HaveCount(2);
		rootFolder.SourceFiles.Should().HaveCount(2);

		var mod1Folder = vfs.GetFolder(file3);
		mod1Folder.Path.Should().Be((NormalizedPath)"/project/mod1");
		mod1Folder.ParentFolder.Should().Be(rootFolder);
		mod1Folder.SubFolders.Should().BeEmpty();
		mod1Folder.SourceFiles.Should().HaveCount(1);

		var mod3Folder = vfs.GetFolder(file4);
		mod3Folder.Path.Should().Be((NormalizedPath)"/project/mod2/mod3");
		var mod2Folder = mod3Folder.ParentFolder!;
		mod2Folder.Should().NotBeNull();
		mod2Folder.Path.Should().Be((NormalizedPath)"/project/mod2");
		mod2Folder.SubFolders.Should().HaveCount(1);
		mod2Folder.SourceFiles.Should().BeEmpty();
		mod3Folder.SubFolders.Should().BeEmpty();
		mod3Folder.SourceFiles.Should().HaveCount(1);
	}

	[Fact]
	public void GetSourceFile()
	{
		var file1 = SourceFile.Create("/project/file1.surab", "");
		var file2 = SourceFile.Create("/project/file2.surab", "");
		var file3 = SourceFile.Create("/project/mod1/file3.surab", "");
		var file4 = SourceFile.Create("/project/mod2/mod3/file4.surab", "");
		var vfs = VFS.Create("/project", [
			file1,
			file2,
			file3,
			file4,
		]);

		vfs.GetSourceFile("/project/nonexistingfile.surab").Should().BeNull();

		vfs.GetSourceFile("/project/file1.surab").Should().Be(file1);
		vfs.GetSourceFile("/project/file2.surab").Should().Be(file2);
		vfs.GetSourceFile("/project/mod1/file3.surab").Should().Be(file3);
		vfs.GetSourceFile("/project/mod2/mod3/file4.surab").Should().Be(file4);
	}

	[Fact]
	public void SourceFileOutsideOfBaseProject()
	{
		Assert.ThrowsAny<Exception>(() => VFS.Create("/project/", [
			SourceFile.Create("/project/file1.surab", ""),
			SourceFile.Create("/out/file2.surab", ""),
		]));
	}
}
