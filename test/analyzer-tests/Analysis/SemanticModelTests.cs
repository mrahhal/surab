namespace Surab.Analyzer;

public class SemanticModelTests
{
    [Fact]
    public void CreatesSymbols()
    {
        var src = """
            const a = 2;
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var declaration = tree.Root.DescendantNodes().OfType<ConstDeclarationNode>().Single();
        model.TryGetDeclaredSymbol(declaration, out var symbol).Should().BeTrue();
        symbol.Should().BeOfType<ConstSymbol>();
    }

    [Fact]
    public void CreatesBinders()
    {
        var src = """
            const a = 2;
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        model.TryGetBinder(tree.Root, out var symbol).Should().BeTrue();
        symbol.Should().BeOfType<ModuleBinder>();
    }

    [Fact]
    public void GetSymbol()
    {
        var src = """
            const a = 2;
            const b = a;
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var declaration1 = tree.Root.DescendantNodes().OfType<ConstDeclarationNode>().First();
        var declaration2 = tree.Root.DescendantNodes().OfType<ConstDeclarationNode>().Skip(1).Single();
        var valueSymbol = model.GetSymbol(declaration2.Value);
        model.TryGetDeclaredSymbol(declaration1, out var symbol).Should().BeTrue();

        valueSymbol.Should().NotBeNull().And.Be(symbol);
    }

    [Fact]
    public void GetTypeSymbol()
    {
        var src = """
            const a = 2;
            const c = '2';
            const b = "2";
            const d = true;
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var declaration1 = tree.Root.DescendantNodes().OfType<ConstDeclarationNode>().First();
        var declaration2 = tree.Root.DescendantNodes().OfType<ConstDeclarationNode>().Skip(1).First();
        var declaration3 = tree.Root.DescendantNodes().OfType<ConstDeclarationNode>().Skip(2).First();
        var declaration4 = tree.Root.DescendantNodes().OfType<ConstDeclarationNode>().Skip(3).Single();

        model.GetTypeSymbol(declaration1.Value).Should().Be(BuiltInTypeSystem.Number);
        model.GetTypeSymbol(declaration2.Value).Should().Be(BuiltInTypeSystem.Char);
        model.GetTypeSymbol(declaration3.Value).Should().Be(BuiltInTypeSystem.String);
        model.GetTypeSymbol(declaration4.Value).Should().Be(BuiltInTypeSystem.Bool);
    }

    [Fact]
    public void LocalSymbolType_Null()
    {
        var src = """
            function f() {
                let a = { 2 };
            }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var letStatement = tree.Root.DescendantNodes().OfType<LetStatementNode>().Single();
        model.TryGetDeclaredSymbol(letStatement, out var symbol).Should().BeTrue();
        var localSymbol = symbol.Should().BeOfType<LocalSymbol>().Subject;
        localSymbol.Type.Should().BeNull();
    }

    [Fact]
    public void GetTypeSymbol_Block()
    {
        var src = """
            function f() {
                let a = { 2 };
            }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var letStatement = tree.Root.DescendantNodes().OfType<LetStatementNode>().Single();
        var initializerSymbol = model.GetTypeSymbol(letStatement.Initializer);
        initializerSymbol.Should().Be(BuiltInTypeSystem.Number);
    }
}
