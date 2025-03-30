using Surab.Analysis;
using Surab.Compilation.LLVM;

namespace Surab.Compilation;

internal class CompilationTestContext
{
	public CompilationTestContext(CompilationContext context, Reasoner reasoner, HirTree loweredTree)
	{
		Context = context;
		Reasoner = reasoner;
		LoweredTree = loweredTree;
		Fill();
	}

	public static CompilationTestContext Create()
	{
		var src = """
			// +-+-------------+---------------------+
			// |0|1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|
			// |a|      -      |b                    |
			// +-+-------------+---------------------+
			struct Struct1 {
				a: char;
				b: f64;
			}

			// +-+-------------+---------------------+--+--------------------+
			// |0|1|2|3|4|5|6|7|8|9|10|11|12|13|14|15|16|17|18|19|20|21|22|23|
			// |a|      -      |b                    |c |         -          |
			// +-+-------------+---------------------+--+--------------------+
			struct Struct2 {
				a: char;
				b: f64;
				c: char;
			}
			""";
		var (project, sourceFile) = AnalysisTestUtils.CreateProject(src);
		var reasoner = project.GetReasoner(sourceFile);
		var interner = new LoweringInterner();
		var loweredProject = HirLowerer.LowerProject(project, interner);
		var loweredTree = loweredProject.Trees.Single(x => x.SyntaxTree == reasoner.Tree);

		var llvm = LLVMCompilationContext.Create();

		var target = new x86_64WindowsMSVCTarget();
		var context = new CompilationContext(project, target, llvm, CompilationOptions.CreateDefault());

		return new CompilationTestContext(context, reasoner, loweredTree);
	}

	public CompilationContext Context { get; }

	public Reasoner Reasoner { get; }

	public HirTree LoweredTree { get; }

	public HirStructType Struct1 { get; private set; } = null!;

	public HirStructType Struct2 { get; private set; } = null!;

	private void Fill()
	{
		var struct1Node = LoweredTree.Root.DescendantNodes().OfType<HirStructDecl>().First(x => x.Type.Identifier == "Struct1");
		Struct1 = struct1Node.Type;

		var struct2Node = LoweredTree.Root.DescendantNodes().OfType<HirStructDecl>().First(x => x.Type.Identifier == "Struct2");
		Struct2 = struct2Node.Type;
	}
}
