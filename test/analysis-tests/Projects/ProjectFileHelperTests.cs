namespace Surab.Analysis;

public class ProjectFileHelperTests
{
	[Theory]
	[InlineData("/project/file1.srb", "file1.srb", ".srb")]
	[InlineData("surab-project.json", "surab-project.json", ".json")]
	public void GetFileNameAndExt(string path, string expectedName, string expectedExt)
	{
		var (name, ext) = ProjectFileHelper.GetFileNameAndExt(path);
		name.Should().Be(expectedName);
		ext.Should().Be(expectedExt);
	}

	[Theory]
	[InlineData("/project/file1.srb", ProjectFileKind.Code)]
	[InlineData("/file2.srb", ProjectFileKind.Code)]
	[InlineData("file3.srb", ProjectFileKind.Code)]
	[InlineData("/project/surab-project.json", ProjectFileKind.ProjectConfig)]
	public void DetectFileKind(string path, ProjectFileKind expectedKind)
	{
		var kind = ProjectFileHelper.DetectFileKind(path);
		kind.Should().Be(expectedKind);
	}
}
