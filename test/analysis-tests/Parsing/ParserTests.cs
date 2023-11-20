namespace Surab.Analysis;

public class ParserTests
{
    [Fact]
    public void Module()
    {
        var src = "const a = 2;";
        var module = ParseModule(src);
        module.Should().NotBeNull();
        module.Decls.Should().HaveCount(1);
        module.Decls.ElementAt(0).Should().BeOfType<ConstDeclNode>();
    }

    [Fact]
    public void Decl_Const()
    {
        var src = "const a = 2;";
        ParseDecl(src).Should().BeOfType<ConstDeclNode>();
    }

    [Fact]
    public void Decl_Fn()
    {
        var src = "fn foo() {}";
        var node = ParseDecl(src).Should().BeOfType<FnDeclNode>().Subject;
        node.PublicToken.Should().BeNull();
        node.Body.Should().NotBeNull();
        node.SemicolonToken.Should().BeNull();
    }

    [Fact]
    public void Decl_Fn2()
    {
        var src = "public fn foo(number: Number): Number { 2 }";
        var node = ParseDecl(src).Should().BeOfType<FnDeclNode>().Subject;
        node.PublicToken.Should().NotBeNull();
        node.Body.Should().NotBeNull();
        node.SemicolonToken.Should().BeNull();
    }

    [Fact]
    public void Decl_ExternFn()
    {
        var src = "extern fn foo(number: Number);";
        var node = ParseDecl(src).Should().BeOfType<FnDeclNode>().Subject;
        node.Body.Should().BeNull();
        node.SemicolonToken.Should().NotBeNull();
    }

    [Fact]
    public void Decl_Struct()
    {
        var src = "struct Foo { a: Number = 2; foo() {} }";
        var node = ParseDecl(src).Should().BeOfType<StructDeclNode>().Subject;
        node.PublicToken.Should().BeNull();
        node.ExternToken.Should().BeNull();
        node.Members.Should().HaveCount(2);
        node.Members.ElementAt(0).Should().BeOfType<FieldDeclNode>();
        node.Members.ElementAt(1).Should().BeOfType<FnDeclNode>();
    }

    [Fact]
    public void Decl_Struct2()
    {
        var src = "struct Foo { a: Number; foo() {} }";
        var node = ParseDecl(src).Should().BeOfType<StructDeclNode>().Subject;
        node.PublicToken.Should().BeNull();
        node.ExternToken.Should().BeNull();
        node.Members.Should().HaveCount(2);
    }

    [Fact]
    public void Decl_ExternStruct()
    {
        var src = "extern struct Foo { a = 2; foo() {} }";
        var node = ParseDecl(src).Should().BeOfType<StructDeclNode>().Subject;
        node.PublicToken.Should().BeNull();
        node.ExternToken.Should().NotBeNull();
        node.Members.Should().HaveCount(2);
    }

    [Fact]
    public void Decl_Class()
    {
        var src = "class Foo { a = 2; foo() {} }";
        var node = ParseDecl(src).Should().BeOfType<ClassDeclNode>().Subject;
        node.PublicToken.Should().BeNull();
        node.Members.Should().HaveCount(2);
    }

    [Fact]
    public void Stmt_Let_1()
    {
        var src = "let a = 2;";
        var node = ParseStmt(src).Should().BeOfType<LetStmtNode>().Subject;
        node.Type.Should().BeNull();
    }

    [Fact]
    public void Stmt_Let_2()
    {
        var src = "let a: Number = 2;";
        var node = ParseStmt(src).Should().BeOfType<LetStmtNode>().Subject;
        node.Type.Should().NotBeNull();
    }

    [Fact]
    public void Stmt_Let_MissingInitializer()
    {
        var src = "let a: Number;";
        var node = ParseStmt(src).Should().BeOfType<LetStmtNode>().Subject;
        node.Type.Should().NotBeNull();
        node.Initializer.Should().BeNull();
    }

    [Fact]
    public void Stmt_Let_MissingTyAndInitializer()
    {
        var src = "let a;";
        var node = ParseStmt(src).Should().BeOfType<LetStmtNode>().Subject;
        node.Type.Should().BeNull();
        node.Initializer.Should().BeNull();
    }

    [Fact]
    public void Stmt_Return()
    {
        var src = "return 2;";
        var node = ParseStmt(src).Should().BeOfType<ReturnStmtNode>().Subject;
        node.Expr.Should().BeOfType<LiteralExprNode>();
    }

    [Fact]
    public void Stmt_Expr()
    {
        var src = "a();";
        var node = ParseStmt(src).Should().BeOfType<ExprStmtNode>().Subject;
        node.Expr.Should().NotBeNull();
    }

    [Fact]
    public void Stmt_BareExpr()
    {
        var src = "a()";
        var node = ParseStmt(src).Should().BeOfType<BareExprStmtNode>().Subject;
        node.Expr.Should().NotBeNull();
    }

    [Theory]
    [InlineData("12", 12.0)]
    [InlineData("12.0", 12.0)]
    [InlineData("12.1", 12.1)]
    [InlineData("0.01", 0.01)]
    public void Expr_Number(string src, double expected)
    {
        var node = ParseExpr(src).Should().BeOfType<LiteralExprNode>().Subject;
        node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.Number);
        node.Literal.TryGetNumberValue(out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("'a'", 'a')]
    [InlineData("'勉'", '勉')]
    public void Expr_Char(string src, char expected)
    {
        var node = ParseExpr(src).Should().BeOfType<LiteralExprNode>().Subject;
        node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.Char);
        node.Literal.TryGetCharValue(out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("'a")]
    [InlineData("'勉")]
    public void Expr_Char_Invalid(string src)
    {
        var node = ParseExpr(src).Should().BeOfType<LiteralExprNode>().Subject;
        node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.Char);
        node.Literal.TryGetCharValue(out var value).Should().BeTrue();
        value.Should().BeNull();
    }

    [Theory]
    [InlineData("\"a\"", "a")]
    [InlineData("\"勉\"", "勉")]
    public void Expr_String(string src, string expected)
    {
        var node = ParseExpr(src).Should().BeOfType<LiteralExprNode>().Subject;
        node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.String);
        node.Literal.TryGetStringValue(out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Theory]
    [InlineData("\"a")]
    [InlineData("\"勉")]
    public void Expr_String_Invalid(string src)
    {
        var node = ParseExpr(src).Should().BeOfType<LiteralExprNode>().Subject;
        node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.String);
        node.Literal.TryGetStringValue(out var value).Should().BeTrue();
        value.Should().BeNull();
    }

    [Fact]
    public void Expr_Binary_1()
    {
        var src = "2 + 3";
        var node = ParseExpr(src).Should().BeOfType<BinaryExprNode>().Subject;
        node.Op.BinaryOperatorTokenKind.Should().Be(BinaryOperatorTokenKind.Add);
        node.Left.Should().BeOfType<LiteralExprNode>();
        node.Right.Should().BeOfType<LiteralExprNode>();
    }

    [Fact]
    public void Expr_Binary_2()
    {
        var src = "2 * 3 + 4";
        var node = ParseExpr(src).Should().BeOfType<BinaryExprNode>().Subject;
        node.Op.BinaryOperatorTokenKind.Should().Be(BinaryOperatorTokenKind.Add);
        {
            var left = node.Left.Should().BeOfType<BinaryExprNode>().Subject;
            left.Op.BinaryOperatorTokenKind.Should().Be(BinaryOperatorTokenKind.Mul);
            left.Left.Should().BeOfType<LiteralExprNode>();
            left.Right.Should().BeOfType<LiteralExprNode>();
        }
        node.Right.Should().BeOfType<LiteralExprNode>();
    }

    [Fact]
    public void Expr_Binary_3()
    {
        var src = "2 ^^ 3 ^^ 4";
        var node = ParseExpr(src).Should().BeOfType<BinaryExprNode>().Subject;
        node.Op.BinaryOperatorTokenKind.Should().Be(BinaryOperatorTokenKind.Pow);
        node.Left.Should().BeOfType<LiteralExprNode>();
        {
            var right = node.Right.Should().BeOfType<BinaryExprNode>().Subject;
            right.Op.BinaryOperatorTokenKind.Should().Be(BinaryOperatorTokenKind.Pow);
            right.Left.Should().BeOfType<LiteralExprNode>();
            right.Right.Should().BeOfType<LiteralExprNode>();
        }
    }

    [Fact]
    public void Expr_Binary_4()
    {
        var src = "2 + -3";
        var node = ParseExpr(src).Should().BeOfType<BinaryExprNode>().Subject;
        node.Op.BinaryOperatorTokenKind.Should().Be(BinaryOperatorTokenKind.Add);
        node.Left.Should().BeOfType<LiteralExprNode>();
        {
            var right = node.Right.Should().BeOfType<UnaryExprNode>().Subject;
            right.Op.UnaryOperatorTokenKind.Should().Be(UnaryOperatorTokenKind.Neg);
            right.Expr.Should().BeOfType<LiteralExprNode>();
        }
    }

    [Fact]
    public void Params_NoType()
    {
        // Syntax error, but should still lead to a proper FnDeclNode.
        var src = "fn a(b /* : number */) {}";
        var node = ParseDecl(src).Should().BeOfType<FnDeclNode>().Subject;
        node.Sig.Params.Params.Should().HaveCount(1);
        node.Sig.Params.Params.ElementAt(0).Type.Should().BeNull();
    }

    [Fact]
    public void Params_1()
    {
        var src = "fn a() {}";
        var node = ParseDecl(src).Should().BeOfType<FnDeclNode>().Subject;
        node.Sig.Params.Params.Should().HaveCount(0);
    }

    [Fact]
    public void Params_2()
    {
        var src = "fn a(b: number) {}";
        var node = ParseDecl(src).Should().BeOfType<FnDeclNode>().Subject;
        node.Sig.Params.Params.Should().HaveCount(1);
    }

    [Fact]
    public void Params_3()
    {
        var src = "fn a(b: number, c: number) {}";
        var node = ParseDecl(src).Should().BeOfType<FnDeclNode>().Subject;
        node.Sig.Params.Params.Should().HaveCount(2);
    }

    [Fact]
    public void Args_1()
    {
        var src = "a()";
        var node = ParseExpr(src).Should().BeOfType<CallExprNode>().Subject;
        node.Args.Args.Should().HaveCount(0);
    }

    [Fact]
    public void Args_2()
    {
        var src = "a(b)";
        var node = ParseExpr(src).Should().BeOfType<CallExprNode>().Subject;
        node.Args.Args.Should().HaveCount(1);
    }

    [Fact]
    public void Args_3()
    {
        var src = "a(b, c)";
        var node = ParseExpr(src).Should().BeOfType<CallExprNode>().Subject;
        node.Args.Args.Should().HaveCount(2);
    }

    [Fact]
    public void ExprInArg_1()
    {
        var src = "a(41 + 1)";
        var node = ParseExpr(src).Should().BeOfType<CallExprNode>().Subject;
        node.Args.Args.Should().HaveCount(1);
        node.Args.Args.ElementAt(0).Expr.Should().BeOfType<BinaryExprNode>();
    }

    [Fact]
    public void ExprInArg_2()
    {
        var src = "a(b = 2 + 1)";
        var node = ParseExpr(src).Should().BeOfType<CallExprNode>().Subject;
        node.Args.Args.Should().HaveCount(1);
        node.Args.Args.ElementAt(0).Expr.Should().BeOfType<AssignmentExprNode>();
    }

    [Fact]
    public void ExprInArg_3()
    {
        var src = "a(b = c = d)";
        var node = ParseExpr(src).Should().BeOfType<CallExprNode>().Subject;
        node.Args.Args.Should().HaveCount(1);
        var assignment1 = node.Args.Args.ElementAt(0).Expr.Should().BeOfType<AssignmentExprNode>().Subject;
        assignment1.Lhs.Should().BeOfType<IdentifierExprNode>().Subject.Identifier.Value.Should().Be("b");
        var assignment2 = assignment1.Rhs.Should().BeOfType<AssignmentExprNode>().Subject;
        assignment2.Lhs.Should().BeOfType<IdentifierExprNode>().Subject.Identifier.Value.Should().Be("c");
        assignment2.Rhs.Should().BeOfType<IdentifierExprNode>().Subject.Identifier.Value.Should().Be("d");
    }

    [Fact]
    public void ExprInArg_4()
    {
        var src = "a(b())";
        var node = ParseExpr(src).Should().BeOfType<CallExprNode>().Subject;
        node.Args.Args.Should().HaveCount(1);
        node.Args.Args.ElementAt(0).Expr.Should().BeOfType<CallExprNode>();
    }

    [Fact]
    public void Assignment()
    {
        var src = "b = c = d";
        var assignment1 = ParseExpr(src).Should().BeOfType<AssignmentExprNode>().Subject;
        assignment1.Lhs.Should().BeOfType<IdentifierExprNode>().Subject.Identifier.Value.Should().Be("b");
        var assignment2 = assignment1.Rhs.Should().BeOfType<AssignmentExprNode>().Subject;
        assignment2.Lhs.Should().BeOfType<IdentifierExprNode>().Subject.Identifier.Value.Should().Be("c");
        assignment2.Rhs.Should().BeOfType<IdentifierExprNode>().Subject.Identifier.Value.Should().Be("d");
    }

    [Fact]
    public void BlockAsExpr()
    {
        var src = "let a = { 2 };";
        var node = ParseStmt(src).Should().BeOfType<LetStmtNode>().Subject;
        var block = node.Initializer!.Expr.Should().BeOfType<BlockExprNode>().Subject;
        block.Stmts.Should().HaveCount(1);
        var bareExpr = block.Stmts.ElementAt(0).Should().BeOfType<BareExprStmtNode>().Subject;
        bareExpr.Expr.Should().BeOfType<LiteralExprNode>();
    }

    [Fact]
    public void BlockAsStmt()
    {
        var src = "{ }";
        var node = ParseStmt(src).Should().BeOfType<BareExprStmtNode>().Subject;
        node.Expr.Should().BeOfType<BlockExprNode>();
    }

    [Fact]
    public void BlockAsStmt_2()
    {
        var src = """
            fn foo(): Number {
                { 1 }
                { 2; }
            }
            """;
        var node = ParseDecl(src).Should().BeOfType<FnDeclNode>().Subject;
        var body = node.Body!;
        body.Should().NotBeNull();
        body.Stmts.Should().HaveCount(2);
        body.Stmts.ElementAt(0).Should().BeOfType<BareExprStmtNode>();
        body.Stmts.ElementAt(1).Should().BeOfType<BareExprStmtNode>();
    }

    [Fact]
    public void SkippedTokens()
    {
        var src = "saunk ;";
        var node = ParseDecl(src).Should().BeOfType<SkippedTokensDeclNode>().Subject;
        node.Skipped.Tokens.Should().HaveCount(2);
    }

    [Fact]
    public void WritesDiagnostics()
    {
        // Should write a diag for the missing equal.
        var src = "const a /* = */ 2;";
        var tree = Parser.ParseDecl(src);
        tree.Diagnostics.Diagnostics.Should().HaveCount(1);
    }

    // ---

    private static ModuleNode ParseModule(string src)
    {
        var tree = Parser.ParseModule(src);
        return (ModuleNode)tree.Root;
    }

    private static DeclNode ParseDecl(string src)
    {
        var tree = Parser.ParseDecl(src);
        return (DeclNode)tree.Root;
    }

    private static StmtNode ParseStmt(string src)
    {
        var tree = Parser.ParseStmt(src);
        return (StmtNode)tree.Root;
    }

    private static ExprNode ParseExpr(string src)
    {
        var tree = Parser.ParseExpr(src);
        return (ExprNode)tree.Root;
    }
}
