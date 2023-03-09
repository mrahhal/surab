using System.Text;
using MR.AttributeDI;

[AddToServices(Lifetime.Transient)]
public class AstVisitorsGenerator : GeneratorBase
{
    public AstVisitorsGenerator(
        AstDataProvider astDataProvider)
    {
        Data = astDataProvider.Data;
    }

    public AstProcessedData Data { get; }

    protected override Task GenerateCoreAsync()
    {
        GenerateHeader();
        new VisitorGenerator(_sb, Data, "IVisitor").Generate();

        return Task.CompletedTask;
    }

    private void GenerateHeader()
    {
        _sb.Append("""
            namespace Surab.Analyzer.Parsing;
            """.NL(2));
    }

    private class VisitorGenerator
    {
        private readonly StringBuilder _sb;

        public VisitorGenerator(
            StringBuilder sb,
            AstProcessedData data,
            string name)
        {
            _sb = sb;
            Data = data;
            Name = name;
        }

        public AstProcessedData Data { get; }
        public string Name { get; }

        public void Generate()
        {
            _sb.Append($$"""
                public partial interface {{Name}}
                {
                    {{T(Data.Nodes, n => $"{OVisit(n)}")}}
                }
                """.NL());
        }

        static string OVisit(SyntaxNode node)
        {
            var sb = new StringBuilder();

            var camelTypeName = node.TypeName.ToCamelCase();
            sb.Append($$"""
                {{INDENT}}public void Visit{{node.Name}}({{node.TypeName}} {{camelTypeName}})
                {{INDENT}}{
                {{INDENT}}{{INDENT}}Walk({{camelTypeName}});
                {{INDENT}}}
                """.NL());

            return sb.ToString();
        }
    }
}
