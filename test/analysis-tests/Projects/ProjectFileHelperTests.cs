namespace Surab.Analysis;

public class ProjectFileHelperTests
{
	[Theory]
	[InlineData("/project/file1.surab", "file1.surab", ".surab")]
	[InlineData("surab-project.json", "surab-project.json", ".json")]
	public void GetFileNameAndExt(string path, string expectedName, string expectedExt)
	{
		var (name, ext) = ProjectFileHelper.GetFileNameAndExt(path);
		name.Should().Be(expectedName);
		ext.Should().Be(expectedExt);
	}

	[Theory]
	[InlineData("/project/file1.surab", ProjectFileKind.Code)]
	[InlineData("/file2.surab", ProjectFileKind.Code)]
	[InlineData("file3.surab", ProjectFileKind.Code)]
	[InlineData("/project/surab-project.json", ProjectFileKind.ProjectConfig)]
	public void DetectFileKind(string path, ProjectFileKind expectedKind)
	{
		var kind = ProjectFileHelper.DetectFileKind(path);
		kind.Should().Be(expectedKind);
	}
}
