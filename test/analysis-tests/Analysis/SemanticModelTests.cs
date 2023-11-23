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

        var decl = tree.Root.DescendantNodes().OfType<ConstDeclNode>().Single();
        model.TryGetDeclaredSymbol(decl, out var symbol).Should().BeTrue();
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

        var decl1 = tree.Root.DescendantNodes().OfType<ConstDeclNode>().First();
        var decl2 = tree.Root.DescendantNodes().OfType<ConstDeclNode>().Skip(1).Single();
        var valueSymbol = model.LookupSymbol(decl2.Value);
        model.TryGetDeclaredSymbol(decl1, out var symbol).Should().BeTrue();

        valueSymbol.Should().NotBeNull().And.Be(symbol);
    }

    [Fact]
    public void LookupSymbol_Field()
    {
        var src = """
            struct S1 { a: Number = 42; }
            fn f() { var s = S1 { }; var a = s.a; }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var varStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Skip(1).Single();
        var accessExpr = varStmt.Initializer!.Expr;
        var fieldSymbol = model.LookupSymbol(accessExpr);

        fieldSymbol.Should().NotBeNull().And.BeAssignableTo<FieldSymbol>();
    }

    [Fact]
    public void LookupSymbol_LookupType_Field_InsideMapInitializer()
    {
        var src = """
            struct S1 { a: Number; }
            fn f() { var s = S1 { a = 42 }; }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var varStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Single();
        var structCreationExpr = (StructCreationExprNode)varStmt.Initializer!.Expr;
        var mapInitializer = structCreationExpr.MapInitializer;

        var fieldSymbol = model.LookupSymbol(mapInitializer.WellFormedExprs.ElementAt(0).Lhs);
        var fieldType = model.LookupType(mapInitializer.WellFormedExprs.ElementAt(0).Lhs);

        fieldSymbol.Should().NotBeNull().And.BeAssignableTo<FieldSymbol>();
        fieldType.Should().NotBeNull().And.Be(BuiltInTypeSystem.Number);
    }

    [Fact]
    public void LookupType()
    {
        var src = """
            const a = 2;
            const c = '2';
            //const b = "2";
            const d = true;
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var decl1 = tree.Root.DescendantNodes().OfType<ConstDeclNode>().First();
        var decl2 = tree.Root.DescendantNodes().OfType<ConstDeclNode>().Skip(1).First();
        //var decl3 = tree.Root.DescendantNodes().OfType<ConstDeclNode>().Skip(2).First();
        var decl4 = tree.Root.DescendantNodes().OfType<ConstDeclNode>().Skip(2).Single();

        model.LookupType(decl1.Value).Should().Be(BuiltInTypeSystem.Number);
        model.LookupType(decl2.Value).Should().Be(BuiltInTypeSystem.Char);
        //model.LookupType(decl3.Value).Should().Be(BuiltInTypeSystem.String);
        model.LookupType(decl4.Value).Should().Be(BuiltInTypeSystem.Bool);
    }

    [Fact]
    public void LookupType_Block()
    {
        var src = """
            fn f() {
                var a = { 2 };
            }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var letStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Single();
        var initializerSymbol = model.LookupType(letStmt.Initializer!);
        initializerSymbol.Should().Be(BuiltInTypeSystem.Number);
    }

    [Fact]
    public void LookupType_Field()
    {
        var src = """
            struct S1 { a: Number = 42; }
            fn f() { var s = S1 { }; var a = s.a; }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var varStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Skip(1).Single();
        var accessExpr = varStmt.Initializer!.Expr;
        var fieldType = model.LookupType(accessExpr);
        fieldType.Should().NotBeNull().And.Be(BuiltInTypeSystem.Number);
    }

    [Fact]
    public void Symbol_ErrorType()
    {
        var src = """
            fn f() {
                var a: Unknown = 2;
            }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var letStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Single();
        model.TryGetDeclaredSymbol(letStmt, out var symbol).Should().BeTrue();
        var localSymbol = (LocalSymbol)symbol!;
        localSymbol.Type.Should().BeOfType<ErrorTypeSymbol>().Subject.Name.Should().Be("Unknown");
        localSymbol.IsTypeInferred.Should().BeFalse();
    }

    [Fact]
    public void Symbol_InferType()
    {
        var src = """
            fn f() {
                var a = 2;
            }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var letStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Single();
        model.TryGetDeclaredSymbol(letStmt, out var symbol).Should().BeTrue();
        var localSymbol = (LocalSymbol)symbol!;
        localSymbol.Type.Should().Be(BuiltInTypeSystem.Number);
        localSymbol.IsTypeInferred.Should().BeTrue();
    }

    [Fact]
    public void Symbol_InferType_2()
    {
        var src = """
            fn f() {
                var a = { 2 };
            }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var letStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Single();
        model.TryGetDeclaredSymbol(letStmt, out var symbol).Should().BeTrue();
        var localSymbol = symbol.Should().BeOfType<LocalSymbol>().Subject;
        localSymbol.Type.Should().Be(BuiltInTypeSystem.Number);
    }

    [Fact]
    public void Symbol_InferType_Field()
    {
        var src = """
            struct S1 {
                a = true;
            }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var fieldDecl = tree.Root.DescendantNodes().OfType<FieldDeclNode>().Single();
        model.TryGetDeclaredSymbol(fieldDecl, out var symbol).Should().BeTrue();
        var fieldSymbol = (FieldSymbol)symbol!;
        fieldSymbol.Type.Should().Be(BuiltInTypeSystem.Bool);
        fieldSymbol.IsTypeInferred.Should().BeTrue();
    }

    [Fact]
    public void FnReturnTypeAsNoneIfNotSet()
    {
        var src = """
            fn f() { }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var fnDecl = tree.Root.DescendantNodes().OfType<FnDeclNode>().Single();
        model.TryGetDeclaredSymbol(fnDecl, out var symbol).Should().BeTrue();
        var fnSymbol = symbol.Should().BeOfType<FnSymbol>().Subject;
        fnSymbol.ReturnType.Should().Be(BuiltInTypeSystem.None);
    }
}
