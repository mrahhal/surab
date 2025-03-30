using System.Diagnostics;
using System.Text.Json;

namespace Surab.Analysis;

public class SurabProjectConfig
{
	public string Name { get; init; } = "main";

	public bool IsInvalid { get; init; }

	public static SurabProjectConfig CreateDefault() => new();

	public static SurabProjectConfig CreateDefaultInvalid() => new() { IsInvalid = true };
}

public static class SurabProjectConfigLoader
{
	private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

	public static SurabProjectConfig Load(ProjectFile? projectConfigFile)
	{
		if (projectConfigFile == null)
		{
			return SurabProjectConfig.CreateDefault();
		}

		Debug.Assert(projectConfigFile.Kind is ProjectFileKind.ProjectConfig);

		try
		{
			var config = JsonSerializer.Deserialize<SurabProjectConfig>(
				projectConfigFile.Text,
				_jsonOptions);
			return config ?? SurabProjectConfig.CreateDefault();
		}
		catch (Exception)
		{
			return SurabProjectConfig.CreateDefaultInvalid();
		}
	}
}
