namespace Surab.Analyzer;

public class SemanticModelTests
{
    [Fact]
    public void CreatesSymbols()
    {
        var src = "const a = 2;";
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var declaration = tree.Root.DescendantNodes().OfType<ConstDeclarationNode>().Single();
        model.TryGetDeclaredSymbol(declaration, out var symbol).Should().BeTrue();
        symbol.Should().BeOfType<ConstSymbol>();
    }

    [Fact]
    public void CreatesBinders()
    {
        var src = "const a = 2;";
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        model.TryGetBinder(tree.Root, out var symbol).Should().BeTrue();
        symbol.Should().BeOfType<ModuleBinder>();
    }

    [Fact]
    public void GetSymbol()
    {
        var src = "const a = 2; const b = a;";
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var declaration1 = tree.Root.DescendantNodes().OfType<ConstDeclarationNode>().First();
        var declaration2 = tree.Root.DescendantNodes().OfType<ConstDeclarationNode>().Skip(1).Single();
        var valueSymbol = model.GetSymbol(declaration2.Value);
        model.TryGetDeclaredSymbol(declaration1, out var symbol).Should().BeTrue();

        valueSymbol.Should().NotBeNull().And.Be(symbol);
    }
}
