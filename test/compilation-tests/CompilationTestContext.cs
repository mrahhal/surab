using Surab.Analysis;
using Surab.Compilation.LLVM;

namespace Surab.Compilation;

public class CompilationTestContext
{
    public CompilationTestContext(CompilationContext context, Reasoner reasoner)
    {
        Context = context;
        Reasoner = reasoner;
        Fill();
    }

    public static CompilationTestContext Create()
    {
        var src = """
            // 0|1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|
            // a|      -      |b                    |
            struct Struct1 {
                a: char;
                b: f64;
            }

            // 0|1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|16|17|18|19|20|21|22|23|
            // a|      -      |b                    |c |         -          |
            struct Struct2 {
                a: char;
                b: f64;
                c: char;
            }
            """;

        var tree = Parser.ParseModule(src);
        var reasoner = Reasoner.Create(tree);

        var llvm = LLVMCompilationContext.Create();
        var reasoners = new List<Reasoner> { Prelude.GetReasoner(), reasoner };

        var target = new x86_64WindowsMSVCTarget();
        var context = new CompilationContext(reasoners, target, llvm, CompilationOptions.CreateDefault());

        return new CompilationTestContext(context, reasoner);
    }

    public CompilationContext Context { get; }

    public Reasoner Reasoner { get; }

    public StructTypeSymbol Struct1 { get; private set; } = null!;

    public StructTypeSymbol Struct2 { get; private set; } = null!;

    private void Fill()
    {
        var struct1Node = Reasoner.Tree.Root.DescendantNodes().OfType<StructDeclNode>().First(x => x.Identifier.Value == "Struct1");
        Struct1 = (StructTypeSymbol)Reasoner.GetDeclaredSymbol(struct1Node)!;

        var struct2Node = Reasoner.Tree.Root.DescendantNodes().OfType<StructDeclNode>().First(x => x.Identifier.Value == "Struct2");
        Struct2 = (StructTypeSymbol)Reasoner.GetDeclaredSymbol(struct2Node)!;
    }
}
