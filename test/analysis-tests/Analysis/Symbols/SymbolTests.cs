namespace Surab.Analysis;

public class SymbolTests
{
	[Fact]
	public void SymbolModule()
	{
		var vfs = VFS.Create();
		var file1 = vfs.AddCodeFile("/file1.srb", "struct Foo { a: i32; }");
		var file2 = vfs.AddCodeFile("/mod1/mod2/file1.srb", "struct Baz { a: i32; struct Bar { a: i32; } }");
		var project = SurabWorkspace.CreateInMemoryProject(vfs);

		var rootModule = project.RootModule;
		var mod1Module = project.RootModule.Children.Single();
		var mod2Module = mod1Module.Children.Single();

		var tree1 = project.GetSyntaxTree(file1);
		var fooStructDecl = tree1.Root.DescendantNodes().OfType<StructDeclNode>().Single();
		var reasoner1 = project.GetReasoner(file1);
		var fooSymbol = reasoner1.GetDeclaredSymbol(fooStructDecl)!;
		fooSymbol.Module.Should().Be(rootModule);
		fooSymbol.FullName.Should().Be("main::Foo");

		var tree2 = project.GetSyntaxTree(file2);
		var barStructDecl = tree2.Root.DescendantNodes().OfType<StructDeclNode>().Where(x => x.Name.Identifier.Value == "Bar").Single();
		var reasoner2 = project.GetReasoner(file2);
		var barSymbol = reasoner2.GetDeclaredSymbol(barStructDecl)!;
		barSymbol.Module.Should().Be(mod2Module);
		barSymbol.FullName.Should().Be("main.mod1.mod2::Baz.Bar");
	}
}
