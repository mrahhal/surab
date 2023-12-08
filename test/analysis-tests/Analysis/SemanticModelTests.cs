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
    public void CreatesScopes()
    {
        var src = """
            const a = 2;
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        model.TryGetScope(tree.Root, out var symbol).Should().BeTrue();
        symbol.Should().BeOfType<ModuleScope>();
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
            struct S1 { a: i32 = 42; }
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
            struct S1 { a: i32; }
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
        fieldType.Should().NotBeNull().And.Be(BuiltinTypeSystem.i32);
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

        model.LookupType(decl1.Value).Should().Be(BuiltinTypeSystem.i32);
        model.LookupType(decl2.Value).Should().Be(BuiltinTypeSystem.@char);
        //model.LookupType(decl3.Value).Should().Be(BuiltInTypeSystem.String);
        model.LookupType(decl4.Value).Should().Be(BuiltinTypeSystem.@bool);
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
        initializerSymbol.Should().Be(BuiltinTypeSystem.i32);
    }

    [Fact]
    public void Symbol_Alias()
    {
        var src = """
            alias Integer i32;
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var aliasDecl = tree.Root.DescendantNodes().OfType<AliasDeclNode>().Single();
        model.TryGetDeclaredSymbol(aliasDecl, out var symbol).Should().BeTrue();
        symbol.Should().BeOfType<AliasTypeSymbol>();
    }

    [Fact]
    public void Symbol_Alias_UnwrapAliases()
    {
        var src = """
            alias Integer i32;
            alias AnotherInteger Integer;
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var anotherIntegerAliasDecl = tree.Root.DescendantNodes().OfType<AliasDeclNode>().ElementAt(1);
        var symbol = (AliasTypeSymbol)model.GetDeclaredSymbol(anotherIntegerAliasDecl)!;
        symbol.UnwrapAliases().Should().Be(BuiltinTypeSystem.i32);
    }

    [Fact]
    public void Symbol_IsAssignableFrom()
    {
        var src = """
            alias Integer i32;
            alias AnotherInteger Integer;
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var anotherIntegerAliasDecl = tree.Root.DescendantNodes().OfType<AliasDeclNode>().ElementAt(1);
        var symbol = (AliasTypeSymbol)model.GetDeclaredSymbol(anotherIntegerAliasDecl)!;
        symbol.IsAssignableFrom(BuiltinTypeSystem.i32);
        BuiltinTypeSystem.i32.IsAssignableFrom(symbol);
    }

    [Fact]
    public void Symbol_IsAssignableFrom2()
    {
        var src = """
            alias Delegate1 fn (i32) f64;
            alias Delegate2 fn (i32) f64;
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var aliasDecl1 = tree.Root.DescendantNodes().OfType<AliasDeclNode>().ElementAt(0);
        var aliasDecl2 = tree.Root.DescendantNodes().OfType<AliasDeclNode>().ElementAt(1);
        var symbol1 = (AliasTypeSymbol)model.GetDeclaredSymbol(aliasDecl1)!;
        var symbol2 = (AliasTypeSymbol)model.GetDeclaredSymbol(aliasDecl2)!;
        symbol1.IsAssignableFrom(symbol2);
    }

    [Fact]
    public void LookupType_Call()
    {
        var src = """
            fn f1() i32 { return 2; }

            fn f2() {
                f1();
            }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var callExpr = tree.Root.DescendantNodes().OfType<CallExprNode>().Single();
        var callType = model.LookupType(callExpr);
        callType.Should().Be(BuiltinTypeSystem.i32);
    }

    [Fact]
    public void LookupType_Call_FnPtr()
    {
        var src = """
            fn f1() i32 { return 2; }

            fn f2() {
                var f = f1;
                f();
            }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var callExpr = tree.Root.DescendantNodes().OfType<CallExprNode>().Single();
        var callType = model.LookupType(callExpr);
        callType.Should().Be(BuiltinTypeSystem.i32);
    }

    [Fact]
    public void LookupType_Field()
    {
        var src = """
            struct S1 { a: i32 = 42; }
            fn f() { var s = S1 { }; var a = s.a; }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var varStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Skip(1).Single();
        var accessExpr = varStmt.Initializer!.Expr;
        var fieldType = model.LookupType(accessExpr);
        fieldType.Should().NotBeNull().And.Be(BuiltinTypeSystem.i32);
    }

    [Fact]
    public void LookupType_NumericExplicitType()
    {
        var src = """
            fn f() { var n = 32f32; }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var varStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Single();
        var literalExpr = varStmt.Initializer!.Expr;
        var numericType = model.LookupType(literalExpr);
        numericType.Should().Be(BuiltinTypeSystem.f32);
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
        localSymbol.Type.Should().BeOfType<ErrorTypeSymbol>();
        localSymbol.IsTypeInferred.Should().BeFalse();
    }

    [Fact]
    public void Symbol_InferType_NumericInteger_i32()
    {
        ExpectLocalInferredTypeFromExpr("2", BuiltinTypeSystem.i32);
    }

    [Fact]
    public void Symbol_InferType_NumericFloat_f64()
    {
        ExpectLocalInferredTypeFromExpr("2.0", BuiltinTypeSystem.f64);
    }

    [Fact]
    public void Symbol_InferType_Block()
    {
        ExpectLocalInferredTypeFromExpr("{ 2 }", BuiltinTypeSystem.i32);
    }

    private void ExpectLocalInferredTypeFromExpr(string expr, TypeSymbol expectedType)
    {
        var src = $$"""
            fn f() {
                var a = {{expr}};
            }
            """;
        ExpectLocalInferredType(src, expectedType);
    }

    private void ExpectLocalInferredType(string src, TypeSymbol expectedType)
    {
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var letStmt = tree.Root.DescendantNodes().OfType<VarStmtNode>().Single();
        model.TryGetDeclaredSymbol(letStmt, out var symbol).Should().BeTrue();
        var localSymbol = symbol.Should().BeOfType<LocalSymbol>().Subject;
        localSymbol.Type.Should().Be(expectedType);
        localSymbol.IsTypeInferred.Should().BeTrue();
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
        fieldSymbol.Type.Should().Be(BuiltinTypeSystem.@bool);
        fieldSymbol.IsTypeInferred.Should().BeTrue();
    }

    [Fact]
    public void FnSymbol_ReturnTypeAsNoneIfNotSet()
    {
        var src = """
            fn f() { }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var fnDecl = tree.Root.DescendantNodes().OfType<FnDeclNode>().Single();
        model.TryGetDeclaredSymbol(fnDecl, out var symbol).Should().BeTrue();
        var fnSymbol = symbol.Should().BeOfType<FnSymbol>().Subject;
        fnSymbol.ReturnType.Should().Be(BuiltinTypeSystem.None);
    }

    [Fact]
    public void FnTypesForCompatibleFnSigsAreEqual()
    {
        var src = """
            fn f1(a: i32, b: i32) i64 { return 1.0; }
            fn f2(a: i32, b: i32) i64 { return 2.0; }
            fn f3(a: i32, b: i32) { }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var f1Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(0);
        var f1Symbol = (FnSymbol)model.GetDeclaredSymbol(f1Node)!;
        var f2Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(1);
        var f2Symbol = (FnSymbol)model.GetDeclaredSymbol(f2Node)!;
        var f3Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(2);
        var f3Symbol = (FnSymbol)model.GetDeclaredSymbol(f3Node)!;

        f1Symbol.Type.Equals(f2Symbol.Type).Should().BeTrue();
        f1Symbol.Type.Equals(f3Symbol.Type).Should().BeFalse();
    }

    [Fact]
    public void FnTypesWithDifferringExternAreNotEqual()
    {
        var src = """
            @extern fn f1(a: i32, b: i32) i64 { return 1.0; }
            @extern fn f2(a: i32, b: i32) i64 { return 2.0; }
            fn f3(a: i32, b: i32) i64 { return 3.0; }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var f1Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(0);
        var f1Symbol = (FnSymbol)model.GetDeclaredSymbol(f1Node)!;
        var f2Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(1);
        var f2Symbol = (FnSymbol)model.GetDeclaredSymbol(f2Node)!;
        var f3Node = tree.Root.DescendantNodes().OfType<FnDeclNode>().ElementAt(2);
        var f3Symbol = (FnSymbol)model.GetDeclaredSymbol(f3Node)!;

        f1Symbol.Type.Equals(f2Symbol.Type).Should().BeTrue();
        f1Symbol.Type.Equals(f3Symbol.Type).Should().BeFalse();
    }

    [Fact]
    public void FnTypesForCompatibleFnTypesAreEqual()
    {
        var src = """
            fn f() {
                var f1: fn (i32, i32) i64;
                var f2: fn (i32, i32) i64;
                var f3: fn (i32, i32);
            }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var f1Node = tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(0);
        var f1Symbol = (LocalSymbol)model.GetDeclaredSymbol(f1Node)!;
        var f2Node = tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(1);
        var f2Symbol = (LocalSymbol)model.GetDeclaredSymbol(f2Node)!;
        var f3Node = tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(2);
        var f3Symbol = (LocalSymbol)model.GetDeclaredSymbol(f3Node)!;

        f1Symbol.Type.Should().BeOfType<FnTypeSymbol>();
        f1Symbol.Type.Equals(f2Symbol.Type).Should().BeTrue();
        f1Symbol.Type.Equals(f3Symbol.Type).Should().BeFalse();
    }

    [Fact]
    public void FnTypesUsingTypeAliases()
    {
        var src = """
            alias Integer1 i32;
            alias Integer2 i32;
            alias Float f64;

            fn f() {
                var f1: fn (Integer1, i32, Integer1) f64;
                var f2: fn (i32, Integer2, Integer2) Float;
            }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var f1Node = tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(0);
        var f1Symbol = (LocalSymbol)model.GetDeclaredSymbol(f1Node)!;
        var f2Node = tree.Root.DescendantNodes().OfType<VarStmtNode>().ElementAt(1);
        var f2Symbol = (LocalSymbol)model.GetDeclaredSymbol(f2Node)!;

        f1Symbol.Type.IsAssignableFrom(f2Symbol.Type).Should().BeTrue();
        f2Symbol.Type.IsAssignableFrom(f1Symbol.Type).Should().BeTrue();
    }

    [Fact]
    public void FnSymbol_IsExtern()
    {
        var src = """
            @extern fn f();
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var fnDecl = tree.Root.DescendantNodes().OfType<FnDeclNode>().Single();
        model.TryGetDeclaredSymbol(fnDecl, out var symbol).Should().BeTrue();
        var fnSymbol = symbol.Should().BeOfType<FnSymbol>().Subject;
        fnSymbol.IsExtern.Should().BeTrue();
    }

    [Fact]
    public void FnSymbol_OuterFn()
    {
        var src = """
            struct S1 { }
            fn f() { }
            """;
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);

        var fnDecl = tree.Root.DescendantNodes().OfType<FnDeclNode>().Single();
        model.TryGetDeclaredSymbol(fnDecl, out var symbol).Should().BeTrue();
        var fnSymbol = symbol.Should().BeOfType<FnSymbol>().Subject;
        fnSymbol.IsOuterFn.Should().BeTrue();
    }
}
