namespace Surab.Analysis;

public class DiagDef
{
	public DiagDef(
		string id,
		DiagLevel defaultLevel,
		string messageTemplate)
	{
		Id = id;
		DefaultLevel = defaultLevel;
		MessageTemplate = messageTemplate;
	}

	public string Id { get; set; }

	public DiagLevel DefaultLevel { get; }

	public string MessageTemplate { get; set; }
}
