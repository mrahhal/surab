using MR.AttributeDI;

[AddToServices(Lifetime.Transient)]
public class DiagsGenerator(
	DiagsDataProvider diagsDataProvider) : GeneratorBase
{
	public DiagsProcessedData Data { get; } = diagsDataProvider.Data;

	protected override Task GenerateCoreAsync()
	{
		GenerateHeader();
		GenerateMain();

		return Task.CompletedTask;
	}

	private void GenerateHeader()
	{
		_sb.Append("""
			namespace Surab.Analysis;
			""".NL(2));
	}

	private void GenerateMain()
	{
		_sb.Append($$"""
			/// <summary>
			/// Contains core diagnostics reported when parsing and analyzing code.
			/// </summary>
			public static class CoreDiags
			{
			""".NL(2));

		foreach (var diag in Data.Diags)
		{
			_sb.Append($$"""
					/// <summary>
					/// {{diag.Message}}
					/// </summary>
					public static readonly DiagDef {{diag.Name}} = new(
						"{{diag.Id}}",
						DiagLevel.{{diag.Level}},
						"{{diag.Message}}");
				""".NL(2));
		}

		_sb.Append($$"""
			}
			""".NL());
	}
}
