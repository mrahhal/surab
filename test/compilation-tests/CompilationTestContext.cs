using Surab.Analysis;
using Surab.Compilation.LLVM;

namespace Surab.Compilation;

public class CompilationTestContext
{
    public CompilationTestContext(CompilationContext context, SemanticModel semanticModel)
    {
        Context = context;
        SemanticModel = semanticModel;
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
        var semanticModel = SemanticModel.Create(tree);

        var llvm = LLVMCompilationContext.Create();
        var models = new List<SemanticModel> { Prelude.GetSemanticModel(), semanticModel };

        var target = new x86_64WindowsMSVCTarget();
        var context = new CompilationContext(models, target, llvm, CompilationOptions.CreateDefault());

        return new CompilationTestContext(context, semanticModel);
    }

    public CompilationContext Context { get; }

    public SemanticModel SemanticModel { get; }

    public StructTypeSymbol Struct1 { get; private set; } = null!;

    public StructTypeSymbol Struct2 { get; private set; } = null!;

    private void Fill()
    {
        var struct1Node = SemanticModel.Tree.Root.DescendantNodes().OfType<StructDeclNode>().First(x => x.Identifier.Value == "Struct1");
        Struct1 = (StructTypeSymbol)SemanticModel.GetDeclaredSymbol(struct1Node)!;

        var struct2Node = SemanticModel.Tree.Root.DescendantNodes().OfType<StructDeclNode>().First(x => x.Identifier.Value == "Struct2");
        Struct2 = (StructTypeSymbol)SemanticModel.GetDeclaredSymbol(struct2Node)!;
    }
}
