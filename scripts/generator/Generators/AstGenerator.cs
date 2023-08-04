using System.Text;
using MR.AttributeDI;

[AddToServices(Lifetime.Transient)]
public class AstGenerator : GeneratorBase
{
    public AstGenerator(
        AstDataProvider astDataProvider)
    {
        Data = astDataProvider.Data;
    }

    public AstProcessedData Data { get; }

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
        _sb.Append("""
            file static class Helper
            {
                public static TextSpan ComputeSpanningSpan(List<SyntaxNode?> nodes, List<SyntaxToken?> tokens)
                {
                    TextSpan? lo = null;
                    TextSpan? hi = null;

                    foreach (var x in nodes)
                    {
                        if (x != null) ProcessForSpanningSpan(x, ref lo, ref hi);
                    }
                    foreach (var x in tokens)
                    {
                        if (x != null) ProcessForSpanningSpan(x, ref lo, ref hi);
                    }

                    return lo.HasValue ?
                        TextSpan.FromTo(lo.Value, hi!.Value) :
                        new TextSpan();
                }

                public static void ProcessForSpanningSpan(
                    SyntaxEntity entity,
                    ref TextSpan? lo,
                    ref TextSpan? hi)
                {
                    var current = entity.Span;
                    if (!lo.HasValue)
                    {
                        lo = hi = current;
                        return;
                    }

                    if (lo.Value.File.Start > current.File.Start)
                    {
                        lo = current;
                    }
                    if (hi!.Value.File.End < current.File.End)
                    {
                        hi = current;
                    }
                }
            }
            """.NL(2));

        _sb.Append($$"""
            public enum SyntaxNodeKind
            {
                {{T(Data.Nodes, n => $"{INDENT}{n.Name},")}}
            }
            """.NL(2));

        for (var i = 0; i < Data.Nodes.Count; i++)
        {
            var node = Data.Nodes[i];
            var isLast = i == Data.Nodes.Count - 1;

            _sb.Append($$"""
                public partial class {{node.TypeName}} : {{node.Base}}
                {
                    public {{node.TypeName}}({{OCtorParams(node)}})
                        : base(SyntaxNodeKind.{{node.Name}})
                    {
                        {{T(SyntaxNodeMemberStoredFields.All, storedField => OSetStoredChildren(node, storedField))}}

                        Span = Helper.ComputeSpanningSpan(ChildNodes, ChildTokens);
                        SetChildrenParent();
                    }

                {{OMembers(node)}}
                    public override void Accept(Visitor visitor)
                    {
                        visitor.Visit{{node.Name}}(this);
                    }
                }
                """.NL(isLast ? 1 : 2));
        }

        // ---

        static string OCtorParams(SyntaxNode node)
        {
            var @params = string.Empty;

            for (int i = 0; i < node.Members.Count; i++)
            {
                var m = node.Members[i];
                var isLast = i == node.Members.Count - 1;

                var commaText = isLast ? "" : ", ";

                @params += $"{m.TypeFull} {m.Name}{commaText}";
            }

            return @params;
        }

        static string OSetStoredChildren(SyntaxNode node, SyntaxNodeMemberStoredField storedField)
        {
            var members = node.Members.Where(x => x.StoredField == storedField).ToList();

            var sb = new StringBuilder();
            sb.Append($"{INDENT}{INDENT}{storedField.Name} = ");

            if (!members.Any())
            {
                sb.Append("new()");
            }
            else if (members[0].List)
            {
                var m = members[0];
                sb.Append($"{m.Name}.Cast<{m.StoredField.TypeName}?>().ToList()");
            }
            else
            {
                sb.Append($"new() {{ {T(members, m => $"{m.Name}, ", false)} }}");
            }

            return sb.Append(';').ToString();
        }

        static string OMembers(SyntaxNode node)
        {
            var sb = new StringBuilder();

            foreach (var m in node.Members)
            {
                sb.Append(INDENT);
                var retText = m.List ?
                    $"{m.StoredField.Name}.Cast<{m.Type}>()" :
                    $"({m.TypeWithOptional}){m.StoredField.Name}[{m.Index}]!";
                sb.Append($"public {m.TypeFull} {m.NamePascal} => {retText};".NL());
            }

            return sb.ToString();
        }
    }
}
