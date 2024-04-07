using Surab.Analysis;

namespace Surab;

// Will ensure all samples under /samples can compile and run without errors.

public class SamplesTests
{
	[Fact]
	public void SamplesSucceed()
	{
		var samplesFolders = Directory.GetDirectories(PathHelper.GetSamplesFolderPath());
		foreach (var sampleFolder in samplesFolders)
		{
			var project = SurabProjectFSLoader.CreateProjectAt(sampleFolder);
			IntegrationRunner.Run(project);
		}
	}
}
