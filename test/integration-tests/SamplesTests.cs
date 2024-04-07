using Surab.Analysis;

namespace Surab;

// Will ensure all samples under /samples can compile and run without errors.

public class SamplesTests
{
	[Theory, ClassData(typeof(Samples))]
	public void SamplesSucceed(string sample)
	{
		var sampleFolderPath = Path.Combine(PathHelper.GetSamplesFolderPath(), sample);
		var project = SurabProjectFSLoader.CreateProjectAt(sampleFolderPath);
		IntegrationRunner.Run(project);
	}

	private class Samples : IEnumerable<object[]>
	{
		public IEnumerator<object[]> GetEnumerator()
		{
			return Directory.GetDirectories(PathHelper.GetSamplesFolderPath())
				.Select(x => new object[] { Path.GetFileName(x) })
				.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
