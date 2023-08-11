using Surab.Analysis;

namespace Surab.Compilation;

public class LowererTests
{
    [Fact]
    public void BareExpressionStatementToReturnStatement()
    {
        var src = """
            fn foo(): Number {
                2
            }
            """;
        var f = LowerFn(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body).ToList();
        body.Should().HaveCount(1);
        body[0].Should().BeOfType<LoweredReturnStatement>().Subject
            .Expression.Should().BeOfType<LoweredLiteralExpression>();
    }

    [Fact]
    public void EmptyFn()
    {
        var src = """
            fn foo() { }
            """;
        var f = LowerFn(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body).ToList();
        body.Should().HaveCount(0);
    }

    [Fact]
    public void ExpressionRewriting_1()
    {
        var src = """
            fn foo() {
                let a = 2;
            }
            """;
        var f = LowerFn(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body).ToList();
        body.Should().HaveCount(2);
        body[0].Should().BeOfType<LoweredVariableStatement>().Subject.Symbol.Name.Should().Be("a");
        var store1 = body[1].Should().BeOfType<LoweredVariableStoreStatement>().Subject;
        store1.Variable.Name.Should().Be("a");
        store1.Expression.Should().BeOfType<LoweredLiteralExpression>();
    }

    [Fact]
    public void ExpressionRewriting_2()
    {
        var src = """
            fn foo() {
                let b: Number;
                let a = b = 2;
            }
            """;
        var f = LowerFn(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body).ToList();
        body.Should().HaveCount(4);
        body[0].Should().BeOfType<LoweredVariableStatement>().Subject.Symbol.Name.Should().Be("b");
        body[1].Should().BeOfType<LoweredVariableStatement>().Subject.Symbol.Name.Should().Be("a");
        var store1 = body[2].Should().BeOfType<LoweredVariableStoreStatement>().Subject;
        store1.Variable.Name.Should().Be("b");
        store1.Expression.Should().BeOfType<LoweredLiteralExpression>();
        var store2 = body[3].Should().BeOfType<LoweredVariableStoreStatement>().Subject;
        store2.Variable.Name.Should().Be("a");
        store2.Expression.Should().BeOfType<LoweredVariableLoadExpression>();
    }

    [Fact]
    public void ExpressionRewriting_3()
    {
        var src = """
            fn foo() {
                let a = { 2 };
            }
            """;
        var f = LowerFn(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body).ToList();
        body.Should().HaveCount(2);
        body[0].Should().BeOfType<LoweredVariableStatement>().Subject.Symbol.Name.Should().Be("a");
        var store1 = body[1].Should().BeOfType<LoweredVariableStoreStatement>().Subject;
        store1.Variable.Name.Should().Be("a");
        store1.Expression.Should().BeOfType<LoweredLiteralExpression>();
    }

    [Fact]
    public void ExpressionRewriting_4()
    {
        var src = """
            fn foo() {
                let a = { let b = 2 + 1; b };
            }
            """;
        var f = LowerFn(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body).ToList();
        body.Should().HaveCount(4);
        body[0].Should().BeOfType<LoweredVariableStatement>().Subject.Symbol.Name.Should().Be("a");
        body[1].Should().BeOfType<LoweredVariableStatement>().Subject.Symbol.Name.Should().Be("b");
        var store1 = body[2].Should().BeOfType<LoweredVariableStoreStatement>().Subject;
        store1.Variable.Name.Should().Be("b");
        store1.Expression.Should().BeOfType<LoweredBinaryExpression>();
        var store2 = body[3].Should().BeOfType<LoweredVariableStoreStatement>().Subject;
        store2.Variable.Name.Should().Be("a");
        store2.Expression.Should().BeOfType<LoweredVariableLoadExpression>().Subject.Variable.Name.Should().Be("b");
    }

    [Fact]
    public void ExpressionRewriting_5()
    {
        var src = """
            fn foo() {
                let a: Number;
                a = 2;
            }
            """;
        var f = LowerFn(src);
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body).ToList();
        body.Should().HaveCount(2);
        body[0].Should().BeOfType<LoweredVariableStatement>().Subject.Symbol.Name.Should().Be("a");
        var store1 = body[1].Should().BeOfType<LoweredVariableStoreStatement>().Subject;
        store1.Variable.Name.Should().Be("a");
        store1.Expression.Should().BeOfType<LoweredLiteralExpression>();
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
        var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body).ToList();
        body.Should().HaveCount(0);
    }

    private static LoweredFnDeclaration LowerFn(string src)
    {
        var module = LowerModule(src);
        return module.Declarations.ElementAt(0).Should().BeOfType<LoweredFnDeclaration>().Subject;
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
