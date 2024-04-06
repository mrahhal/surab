using System.Text.Json;

namespace Surab.Analysis;

public class SurabProjectConfig
{
	public static readonly SurabProjectConfig Default = new();

	public static readonly string ConfigFileName = "surab-project.json";

	public string Name { get; init; } = "main";
}

public static class SurabProjectConfigLoader
{
	public static SurabProjectConfig Load(NormalizedPath projectPath)
	{
		var config = SurabProjectConfig.Default;
		if (!projectPath.IsEmpty)
		{
			// Try to find surab-project.json and parse it for config.
			var configFilePath = Path.Combine(projectPath.Value, SurabProjectConfig.ConfigFileName);
			if (File.Exists(configFilePath))
			{
				// TODO: This is using fs when project path is not empty. Do it better.
				var json = File.ReadAllText(configFilePath);
				config = JsonSerializer.Deserialize<SurabProjectConfig>(json);
			}
		}

		// TODO: Need to account for invalid config file and report that properly.
		return config ?? SurabProjectConfig.Default;
	}
}
