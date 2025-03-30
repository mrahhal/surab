using MR.AttributeDI;
using Newtonsoft.Json;

public class DiagsProcessedData
{
	public required List<Diag> Diags { get; set; }
}

public class Diag
{
	public required string Id { get; set; }

	public required string Level { get; set; }

	public required string Message { get; set; }

	public required string Slug { get; set; }

	public string Name => $"{Id}_{Slug}";
}

[AddToServices(Lifetime.Singleton)]
public class DiagsDataProvider
{
	public class Defined
	{
		public required Dictionary<string, DefinedDiag> Root { get; set; }
	}

	public class DefinedDiag
	{
		public required string Level { get; set; }
		public required string Message { get; set; }
	}

	public DiagsDataProvider()
	{
		var json = File.ReadAllText("Data/diags.jsonc");
		var root = JsonConvert.DeserializeObject<Dictionary<string, DefinedDiag>>(json)!;
		Data = Process(new Defined { Root = root });
	}

	public DiagsProcessedData Data { get; }

	private DiagsProcessedData Process(Defined defined)
	{
		var diags = defined.Root.Select(p => new Diag
		{
			Id = p.Key,
			Level = p.Value.Level,
			Message = p.Value.Message,
			Slug = CreateSlug(p.Value.Message),
		}).ToList();

		return new DiagsProcessedData
		{
			Diags = diags,
		};
	}

	private string CreateSlug(string message)
	{
		message = message.ToPascalCase();
		foreach (var item in new[] { ".", "'", ":" })
		{
			message = message.Replace(item, string.Empty);
		}
		foreach (var item in new[] { "{", "}" })
		{
			message = message.Replace(item, "_");
		}
		message = message.Replace(";", "Semicolon");
		return message;
	}
}
