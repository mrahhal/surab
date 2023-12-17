using System.Text;
using MR.AttributeDI;

[AddToServices(Lifetime.Transient)]
public class AstVisitorsGenerator(
	AstDataProvider astDataProvider) : GeneratorBase
{
	public AstProcessedData Data { get; } = astDataProvider.Data;

	protected override Task GenerateCoreAsync()
	{
		GenerateHeader();
		new VisitorGenerator(_sb, Data, "SyntaxTreeVisitor").Generate();

		return Task.CompletedTask;
	}

	private void GenerateHeader()
	{
		_sb.Append("""
			namespace Surab.Analysis;
			""".NL(2));
	}

	private class VisitorGenerator(
		StringBuilder sb,
		AstProcessedData data,
		string name)
	{
		private readonly StringBuilder _sb = sb;

		public AstProcessedData Data { get; } = data;
		public string Name { get; } = name;

		public void Generate()
		{
			_sb.Append($$"""
				public abstract partial class {{Name}}
				{
					{{T(Data.Nodes, n => $"{OVisit(n)}")}}
				}
				""".NL());
		}

		static string OVisit(SyntaxNode node)
		{
			var sb = new StringBuilder();

			//var camelTypeName = node.TypeName.ToCamelCase();
			sb.Append($$"""
				{{INDENT}}public virtual void Visit{{node.Name}}({{node.TypeName}} node)
				{{INDENT}}{
				{{INDENT}}{{INDENT}}Walk(node);
				{{INDENT}}}
				""".NL());

			return sb.ToString();
		}
	}
}
