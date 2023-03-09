namespace Surab.Analyzer;

public class ParserTests
{
    [Fact]
    public void Declaration_Const()
    {
        var src = "const a = 2;";
        ParseDeclaration(src).Should().BeOfType<ConstDeclarationNode>();
    }

    [Fact]
    public void Declaration_Function()
    {
        var src = "function foo() {}";
        var node = ParseDeclaration(src).Should().BeOfType<FunctionDeclarationNode>().Subject;
        node.PublicToken.Should().BeNull();
    }

    [Fact]
    public void Declaration_Function2()
    {
        var src = "public function foo(number: Number): Number { 2 }";
        var node = ParseDeclaration(src).Should().BeOfType<FunctionDeclarationNode>().Subject;
        node.PublicToken.Should().NotBeNull();
    }

    [Fact]
    public void Declaration_Class()
    {
        var src = "class Foo { a = 2; foo() {} }";
        var node = ParseDeclaration(src).Should().BeOfType<ClassDeclarationNode>().Subject;
        node.PublicToken.Should().BeNull();
        node.Members.Should().HaveCount(2);
    }

    [Fact]
    public void Statement_Let_1()
    {
        var src = "let a = 2;";
        var node = ParseStatement(src).Should().BeOfType<LetStatementNode>().Subject;
        node.Ty.Should().BeNull();
    }

    [Fact]
    public void Statement_Let_2()
    {
        var src = "let a: Number = 2;";
        var node = ParseStatement(src).Should().BeOfType<LetStatementNode>().Subject;
        node.Ty.Should().NotBeNull();
    }

    [Fact]
    public void Statement_Return()
    {
        var src = "return 2;";
        var node = ParseStatement(src).Should().BeOfType<ReturnStatementNode>().Subject;
        node.Expression.Should().BeOfType<LiteralExpressionNode>();
    }

    [Fact]
    public void Statement_Expression()
    {
        var src = "a();";
        var node = ParseStatement(src).Should().BeOfType<ExpressionStatementNode>().Subject;
        node.Expression.Should().NotBeNull();
    }

    [Fact]
    public void Statement_BareExpression()
    {
        var src = "a()";
        var node = ParseStatement(src).Should().BeOfType<BareExpressionStatementNode>().Subject;
        node.Expression.Should().NotBeNull();
    }

    [Theory]
    [InlineData("12", 12.0)]
    [InlineData("12.0", 12.0)]
    [InlineData("12.1", 12.1)]
    [InlineData("0.01", 0.01)]
    public void Expression_Number(string src, double expected)
    {
        var node = ParseExpression(src).Should().BeOfType<LiteralExpressionNode>().Subject;
        node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.Number);
        node.Literal.TryGetNumberValue(out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("'a'", 'a')]
    [InlineData("'勉'", '勉')]
    public void Expression_Char(string src, char expected)
    {
        var node = ParseExpression(src).Should().BeOfType<LiteralExpressionNode>().Subject;
        node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.Char);
        node.Literal.TryGetCharValue(out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("'a")]
    [InlineData("'勉")]
    public void Expression_Char_Invalid(string src)
    {
        var node = ParseExpression(src).Should().BeOfType<LiteralExpressionNode>().Subject;
        node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.Char);
        node.Literal.TryGetCharValue(out var value).Should().BeTrue();
        value.Should().BeNull();
    }

    [Theory]
    [InlineData("\"a\"", "a")]
    [InlineData("\"勉\"", "勉")]
    public void Expression_String(string src, string expected)
    {
        var node = ParseExpression(src).Should().BeOfType<LiteralExpressionNode>().Subject;
        node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.String);
        node.Literal.TryGetStringValue(out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("\"a")]
    [InlineData("\"勉")]
    public void Expression_String_Invalid(string src)
    {
        var node = ParseExpression(src).Should().BeOfType<LiteralExpressionNode>().Subject;
        node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.String);
        node.Literal.TryGetStringValue(out var value).Should().BeTrue();
        value.Should().BeNull();
    }

    [Fact]
    public void Expression_Binary_1()
    {
        var src = "2 + 3";
        var node = ParseExpression(src).Should().BeOfType<BinaryExpressionNode>().Subject;
        node.Op.BinaryOperatorTokenKind.Should().Be(BinaryOperatorTokenKind.Add);
        node.Left.Should().BeOfType<LiteralExpressionNode>();
        node.Right.Should().BeOfType<LiteralExpressionNode>();
    }

    [Fact]
    public void Expression_Binary_2()
    {
        var src = "2 * 3 + 4";
        var node = ParseExpression(src).Should().BeOfType<BinaryExpressionNode>().Subject;
        node.Op.BinaryOperatorTokenKind.Should().Be(BinaryOperatorTokenKind.Add);
        {
            var left = node.Left.Should().BeOfType<BinaryExpressionNode>().Subject;
            left.Op.BinaryOperatorTokenKind.Should().Be(BinaryOperatorTokenKind.Mul);
            left.Left.Should().BeOfType<LiteralExpressionNode>();
            left.Right.Should().BeOfType<LiteralExpressionNode>();
        }
        node.Right.Should().BeOfType<LiteralExpressionNode>();
    }

    [Fact]
    public void Expression_Binary_3()
    {
        var src = "2 ^^ 3 ^^ 4";
        var node = ParseExpression(src).Should().BeOfType<BinaryExpressionNode>().Subject;
        node.Op.BinaryOperatorTokenKind.Should().Be(BinaryOperatorTokenKind.Pow);
        node.Left.Should().BeOfType<LiteralExpressionNode>();
        {
            var right = node.Right.Should().BeOfType<BinaryExpressionNode>().Subject;
            right.Op.BinaryOperatorTokenKind.Should().Be(BinaryOperatorTokenKind.Pow);
            right.Left.Should().BeOfType<LiteralExpressionNode>();
            right.Right.Should().BeOfType<LiteralExpressionNode>();
        }
    }

    [Fact]
    public void Expression_Binary_4()
    {
        var src = "2 + -3";
        var node = ParseExpression(src).Should().BeOfType<BinaryExpressionNode>().Subject;
        node.Op.BinaryOperatorTokenKind.Should().Be(BinaryOperatorTokenKind.Add);
        node.Left.Should().BeOfType<LiteralExpressionNode>();
        {
            var right = node.Right.Should().BeOfType<UnaryExpressionNode>().Subject;
            right.Op.UnaryOperatorTokenKind.Should().Be(UnaryOperatorTokenKind.Neg);
            right.Expression.Should().BeOfType<LiteralExpressionNode>();
        }
    }

    [Fact]
    public void BlockAsExpression()
    {
        var src = "let a = { 2 };";
        var node = ParseStatement(src).Should().BeOfType<LetStatementNode>().Subject;
        var block = node.Initializer.Expression.Should().BeOfType<BlockExpressionNode>().Subject;
        block.Statements.Should().HaveCount(1);
        var bareExpression = block.Statements.ElementAt(0).Should().BeOfType<BareExpressionStatementNode>().Subject;
        bareExpression.Expression.Should().BeOfType<LiteralExpressionNode>();
    }

    [Fact]
    public void BlockAsStatement()
    {
        var src = "{ }";
        var node = ParseStatement(src).Should().BeOfType<BareExpressionStatementNode>().Subject;
        node.Expression.Should().BeOfType<BlockExpressionNode>();
    }

    [Fact]
    public void SkippedTokens()
    {
        var src = "saunk ;";
        var node = ParseDeclaration(src).Should().BeOfType<SkippedTokensDeclarationNode>().Subject;
        node.Skipped.Tokens.Should().HaveCount(2);
    }

    [Fact]
    public void WritesDiagnostics()
    {
        // Should write a diag for the missing equal.
        var src = "const a /* = */ 2;";
        var tree = Parser.ParseDeclaration(src);
        tree.Diagnostics.Diagnostics.Should().HaveCount(1);
    }

    // ---

    private static DeclarationNode ParseDeclaration(string src)
    {
        var tree = Parser.ParseDeclaration(src);
        return (DeclarationNode)tree.Root;
    }

    private static StatementNode ParseStatement(string src)
    {
        var tree = Parser.ParseStatement(src);
        return (StatementNode)tree.Root;
    }

    private static ExpressionNode ParseExpression(string src)
    {
        var tree = Parser.ParseExpression(src);
        return (ExpressionNode)tree.Root;
    }
}
