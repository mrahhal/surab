using System.Diagnostics;
using MR.AttributeDI;
using Newtonsoft.Json;

public class SyntaxNode
{
    public required string Name { get; set; }
    public required string TypeName { get; set; }
    public required string Base { get; set; }
    public required string? Comment { get; set; }
    public required List<SyntaxNodeMember> Members { get; set; }
}

[DebuggerDisplay("{Name}, {Type} (Optional = {Optional}, List = {List})")]
public class SyntaxNodeMember
{
    public required string Name { get; set; }
    public string NamePascal => Name.ToPascalCase();
    public required string Type { get; set; }
    public string TypeWithOptional => Optional ? $"{Type}?" : Type;
    public string TypeFull => List ? $"IEnumerable<{TypeWithOptional}>" : TypeWithOptional;
    public required bool Optional { get; set; }
    public required bool List { get; set; }
    public required string? Comment { get; set; }

    public required int Index { get; set; }
    public required SyntaxNodeMemberStoredField StoredField { get; set; }
}

public record SyntaxNodeMemberStoredField(string Name, string TypeName);

public static class SyntaxNodeMemberStoredFields
{
    public static readonly SyntaxNodeMemberStoredField Tokens = new("ChildTokens", "SyntaxToken");
    public static readonly SyntaxNodeMemberStoredField Nodes = new("ChildNodes", "SyntaxNode");

    public static IEnumerable<SyntaxNodeMemberStoredField> All => new[] { Tokens, Nodes };
}

public class AstProcessedData
{
    public required List<SyntaxNode> Nodes { get; set; }
}

[AddToServices(Lifetime.Singleton)]
public class AstDataProvider
{
    public class Defined
    {
        public required List<SyntaxNode> Nodes { get; set; }
    }

    public AstDataProvider()
    {
        var json = File.ReadAllText("Data/ast.json");
        var defined = JsonConvert.DeserializeObject<Defined>(json)!;
        Data = Process(defined);
    }

    public AstProcessedData Data { get; }

    private static AstProcessedData Process(Defined defined)
    {
        var nodes = defined.Nodes;

        foreach (var node in nodes)
        {
            node.Base ??= "SyntaxNode";
            node.TypeName = $"{node.Name}Node";
            node.Members ??= [];
            foreach (var m in node.Members)
            {
                m.StoredField = m.Type.EndsWith("Node") ?
                    SyntaxNodeMemberStoredFields.Nodes :
                    SyntaxNodeMemberStoredFields.Tokens;
            }
            foreach (var g in node.Members.GroupBy(x => x.StoredField))
            {
                var counter = 0;
                foreach (var item in g)
                {
                    item.Index = counter++;
                }
            }
        }

        return new()
        {
            Nodes = nodes,
        };
    }
}
