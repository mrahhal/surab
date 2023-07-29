namespace Surab.Analysis;

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
    public void LookupSymbol()
    {
        var src = """
            const a = 2;
            const b = a;
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var declaration1 = tree.Root.DescendantNodes().OfType<ConstDeclarationNode>().First();
        var declaration2 = tree.Root.DescendantNodes().OfType<ConstDeclarationNode>().Skip(1).Single();
        var valueSymbol = model.LookupSymbol(declaration2.Value);
        model.TryGetDeclaredSymbol(declaration1, out var symbol).Should().BeTrue();

        valueSymbol.Should().NotBeNull().And.Be(symbol);
    }

    [Fact]
    public void LookupType()
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

        model.LookupType(declaration1.Value).Should().Be(BuiltInTypeSystem.Number);
        model.LookupType(declaration2.Value).Should().Be(BuiltInTypeSystem.Char);
        model.LookupType(declaration3.Value).Should().Be(BuiltInTypeSystem.String);
        model.LookupType(declaration4.Value).Should().Be(BuiltInTypeSystem.Bool);
    }

    [Fact]
    public void LookupType_Block()
    {
        var src = """
            function f() {
                let a = { 2 };
            }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var letStatement = tree.Root.DescendantNodes().OfType<LetStatementNode>().Single();
        var initializerSymbol = model.LookupType(letStatement.Initializer!);
        initializerSymbol.Should().Be(BuiltInTypeSystem.Number);
    }

    [Fact]
    public void Symbol_ErrorType()
    {
        var src = """
            function f() {
                let a: Unknown = 2;
            }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var letStatement = tree.Root.DescendantNodes().OfType<LetStatementNode>().Single();
        model.TryGetDeclaredSymbol(letStatement, out var symbol).Should().BeTrue();
        var localSymbol = (LocalSymbol)symbol!;
        localSymbol.Type.Should().BeOfType<ErrorTypeSymbol>().Subject.Name?.Identifier.Should().Be("Unknown");
    }

    [Fact]
    public void Symbol_InferType()
    {
        var src = """
            function f() {
                let a = 2;
            }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var letStatement = tree.Root.DescendantNodes().OfType<LetStatementNode>().Single();
        model.TryGetDeclaredSymbol(letStatement, out var symbol).Should().BeTrue();
        var localSymbol = (LocalSymbol)symbol!;
        localSymbol.Type.Should().Be(BuiltInTypeSystem.Number);
    }

    [Fact]
    public void Symbol_InferType_2()
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
        localSymbol.Type.Should().Be(BuiltInTypeSystem.Number);
    }
}
