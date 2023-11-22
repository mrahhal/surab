using Surab.Analysis;

namespace Surab.Compilation;

public class LowererTests
{
    [Fact]
    public void BareExprStmtToReturnStmt()
    {
        var src = """
            fn foo(): Number {
                2
            }
            """;
        var f = LowerFn(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body!).ToList();
        body.Should().HaveCount(1);
        body[0].Should().BeOfType<LoweredReturnStmt>().Subject
            .Expr.Should().BeOfType<LoweredLiteralExpr>();
    }

    [Fact]
    public void EmptyFn()
    {
        var src = """
            fn foo() { }
            """;
        var f = LowerFn(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body!).ToList();
        body.Should().HaveCount(0);
    }

    [Fact]
    public void ExprRewriting_1()
    {
        var src = """
            fn foo() {
                var a = 2;
            }
            """;
        var f = LowerFn(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body!).ToList();
        body.Should().HaveCount(2);
        body[0].Should().BeOfType<LoweredVariableStmt>().Subject.Symbol.Name.Should().Be("a");
        var store1 = body[1].Should().BeOfType<LoweredVariableStoreStmt>().Subject;
        store1.Variable.Name.Should().Be("a");
        store1.Expr.Should().BeOfType<LoweredLiteralExpr>();
    }

    [Fact]
    public void ExprRewriting_2()
    {
        var src = """
            fn foo() {
                var b: Number;
                var a = b = 2;
            }
            """;
        var f = LowerFn(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body!).ToList();
        body.Should().HaveCount(4);
        body[0].Should().BeOfType<LoweredVariableStmt>().Subject.Symbol.Name.Should().Be("b");
        body[1].Should().BeOfType<LoweredVariableStmt>().Subject.Symbol.Name.Should().Be("a");
        var store1 = body[2].Should().BeOfType<LoweredVariableStoreStmt>().Subject;
        store1.Variable.Name.Should().Be("b");
        store1.Expr.Should().BeOfType<LoweredLiteralExpr>();
        var store2 = body[3].Should().BeOfType<LoweredVariableStoreStmt>().Subject;
        store2.Variable.Name.Should().Be("a");
        store2.Expr.Should().BeOfType<LoweredVariableLoadExpr>();
    }

    [Fact]
    public void ExprRewriting_3()
    {
        var src = """
            fn foo() {
                var a = { 2 };
            }
            """;
        var f = LowerFn(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body!).ToList();
        body.Should().HaveCount(2);
        body[0].Should().BeOfType<LoweredVariableStmt>().Subject.Symbol.Name.Should().Be("a");
        var store1 = body[1].Should().BeOfType<LoweredVariableStoreStmt>().Subject;
        store1.Variable.Name.Should().Be("a");
        store1.Expr.Should().BeOfType<LoweredLiteralExpr>();
    }

    [Fact]
    public void ExprRewriting_4()
    {
        var src = """
            fn foo() {
                var a = { var b = 2 + 1; b };
            }
            """;
        var f = LowerFn(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body!).ToList();
        body.Should().HaveCount(4);
        body[0].Should().BeOfType<LoweredVariableStmt>().Subject.Symbol.Name.Should().Be("a");
        body[1].Should().BeOfType<LoweredVariableStmt>().Subject.Symbol.Name.Should().Be("b");
        var store1 = body[2].Should().BeOfType<LoweredVariableStoreStmt>().Subject;
        store1.Variable.Name.Should().Be("b");
        store1.Expr.Should().BeOfType<LoweredBinaryExpr>();
        var store2 = body[3].Should().BeOfType<LoweredVariableStoreStmt>().Subject;
        store2.Variable.Name.Should().Be("a");
        store2.Expr.Should().BeOfType<LoweredVariableLoadExpr>().Subject.Variable.Name.Should().Be("b");
    }

    [Fact]
    public void ExprRewriting_5()
    {
        var src = """
            fn foo() {
                var a: Number;
                a = 2;
            }
            """;
        var f = LowerFn(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body!).ToList();
        body.Should().HaveCount(2);
        body[0].Should().BeOfType<LoweredVariableStmt>().Subject.Symbol.Name.Should().Be("a");
        var store1 = body[1].Should().BeOfType<LoweredVariableStoreStmt>().Subject;
        store1.Variable.Name.Should().Be("a");
        store1.Expr.Should().BeOfType<LoweredLiteralExpr>();
    }

    [Fact]
    public void LastStandaloneBlocks()
    {
        var src = """
            fn foo() {
                { }
            }
            """;
        var f = LowerFn(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body!).ToList();
        body.Should().HaveCount(0);
    }

    [Fact]
    public void StructCreationExpr()
    {
        var src = """
            struct Foo { a: Number; }
            fn foo() {
                var foo = Foo { a = 42 };
            }
            """;
        var module = LowerModule(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(module.Decls[1]).ToList();
        body.Should().HaveCount(2);
        var store = body.OfType<LoweredVariableStoreStmt>().Single();
        store.Variable.Name.Should().Be("foo");
        store.Expr.Should().BeOfType<LoweredStructCreationExpr>();
    }

    private static LoweredFnDecl LowerFn(string src)
    {
        var module = LowerModule(src);
        return module.Decls.ElementAt(0).Should().BeOfType<LoweredFnDecl>().Subject;
    }

    private static LoweredModule LowerModule(string src)
    {
        var tree = Lower(src);
        return tree.Root.Should().BeOfType<LoweredModule>().Subject;
    }

    private static LoweredTree Lower(string src)
    {
        var tree = Parser.ParseModule(src);
        var model = SemanticModel.Create(tree);
        return Lowerer.Lower(model);
    }
}
