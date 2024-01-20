namespace Surab.Analysis;

public class ParserTests
{
	[Fact]
	public void File()
	{
		var src = "const a = 2;";
		var file = ParseFile(src);
		file.Should().NotBeNull();
		file.Imports.Should().BeEmpty();
		file.Decls.Should().HaveCount(1);
		file.Decls.ElementAt(0).Should().BeOfType<ConstDeclNode>();
	}

	[Fact]
	public void FileWithImports()
	{
		var src = """
			import "a.surab";
			import "b.surab";

			const a = 2;
			""";
		var file = ParseFile(src);
		file.Should().NotBeNull();
		file.Imports.Should().HaveCount(2);
		file.Decls.Should().HaveCount(1);
	}

	[Fact]
	public void Trivia()
	{
		var src = """
			// some
			/* c */ const /* c */ /* c */ a /* c */ = /* c */ 2 /* c */; // c
			""";
		var tree = Parser.ParseFile(src);
		var file = (FileNode)tree.Root;

		file.Should().NotBeNull();
		file.Decls.Should().HaveCount(1);
		file.Decls.ElementAt(0).Should().BeOfType<ConstDeclNode>();
		file.DescendantNodesAndSelf()
			.SelectMany(x => x.ChildTokens.SelectMany(x => x.LeadingTrivia.Concat(x.TrailingTrivia)))
			.Where(x => x.Kind is SyntaxTriviaKind.LineComment or SyntaxTriviaKind.BlockComment)
			.Should().HaveCount(8);
	}

	[Fact]
	public void Trivia_2()
	{
		var src = """
			// some
			alias Integer as i32;
			""";
		var tree = Parser.ParseFile(src);
		var file = (FileNode)tree.Root;

		file.Should().NotBeNull();
		file.Decls.Should().HaveCount(1);
		var aliasDecl = file.Decls.ElementAt(0).Should().BeOfType<AliasDeclNode>().Subject;
		aliasDecl.AliasToken.LeadingTrivia.Should().HaveCount(2); // line comment + whitespace
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
		var src = "fn f() {}";
		var node = ParseDecl(src).Should().BeOfType<FnDeclNode>().Subject;
		node.FnToken.IsKeyword.Should().BeTrue();
		node.FnToken.IsContextualKeyword.Should().BeFalse();
		node.Sig.ReturnType.Should().BeNull();
		node.Body.Should().NotBeNull();
		node.SemicolonToken.Should().BeNull();
	}

	[Fact]
	public void Decl_Fn_2()
	{
		var src = "fn f(number: u32) u32 { 2 }";
		var node = ParseDecl(src).Should().BeOfType<FnDeclNode>().Subject;
		node.Sig.ReturnType.Should().NotBeNull();
		node.Body.Should().NotBeNull();
		node.SemicolonToken.Should().BeNull();
	}

	[Fact]
	public void Decl_Fn_Faulty()
	{
		var src = "fn some()";
		// Semicolon and body can body be nulls so this won't have a parsing diag.
		ParseDecl(src, hasDiags: false).Should().NotBeNull();
	}

	[Fact]
	public void Decl_Fn_Faulty2()
	{
		var src = "fn some() /* some fn not complete */ fn main() {}";
		ParseFile(src, hasDiags: true).Should().NotBeNull();
	}

	[Fact]
	public void PtrType()
	{
		var src = """
			fn f() {
				var p1: *i32;
				var p2: *fn (i32) i32;
				var p3: *;
			}
			""";
		var node = ParseDecl(src);
		var varStmt1 = node.DescendantNodes().OfType<VarDeclNode>().ElementAt(0);
		var ptrTypeNode1 = varStmt1.Type!.Type.Should().BeOfType<PtrTypeNode>().Subject;
		ptrTypeNode1.InnerType.Should().BeOfType<NamedTypeNode>();
		var varStmt2 = node.DescendantNodes().OfType<VarDeclNode>().ElementAt(1);
		var ptrTypeNode2 = varStmt2.Type!.Type.Should().BeOfType<PtrTypeNode>().Subject;
		ptrTypeNode2.InnerType.Should().BeOfType<FnTypeNode>();
		var varStmt3 = node.DescendantNodes().OfType<VarDeclNode>().ElementAt(2);
		var ptrTypeNode3 = varStmt3.Type!.Type.Should().BeOfType<PtrTypeNode>().Subject;
		ptrTypeNode3.InnerType.Should().BeNull();
	}

	[Fact]
	public void FnType()
	{
		var src = "var f: fn (i32, i32) i64;";
		var node = ParseDecl(src).Should().BeOfType<VarDeclNode>().Subject;
		var fnTypeNode = node.Type!.Type.Should().BeOfType<FnTypeNode>().Subject;
		var paramTypes = fnTypeNode.ParamTypes!;
		paramTypes.Should().HaveCount(2);
		paramTypes.ElementAt(0).Type.Should().BeOfType<NamedTypeNode>();
		paramTypes.ElementAt(0).CommaToken.Should().NotBeNull();
		paramTypes.ElementAt(1).Type.Should().BeOfType<NamedTypeNode>();
		paramTypes.ElementAt(1).CommaToken.Should().BeNull();
		fnTypeNode.ReturnType.Should().BeOfType<NamedTypeNode>();
	}

	[Fact]
	public void FnTypeWithAttrs()
	{
		var src = "var f: @extern fn (i32, i32) i64;";
		var node = ParseDecl(src).Should().BeOfType<VarDeclNode>().Subject;
		var fnTypeNode = node.Type!.Type.Should().BeOfType<FnTypeNode>().Subject;
		fnTypeNode.ParamTypes!.Should().HaveCount(2);
		fnTypeNode.Attrs.Should().NotBeNull();
		fnTypeNode.Attrs!.Attrs.Should().HaveCount(1);
	}

	[Fact]
	public void Decl_Attrs()
	{
		var src = """
			@public @some(false)
			fn f() { }
			""";
		var node = ParseDecl(src).Should().BeOfType<FnDeclNode>().Subject;
		node.Attrs.Should().NotBeNull();
		var attrs = node.Attrs!.Attrs;
		attrs.Should().HaveCount(2);
		attrs.ElementAt(0).Identifier.Value.Should().Be("public");
		attrs.ElementAt(0).ArgsList.Should().BeNull();
		attrs.ElementAt(1).Identifier.Value.Should().Be("some");
		attrs.ElementAt(1).ArgsList.Should().NotBeNull();
		attrs.ElementAt(1).ArgsList!.Args.ElementAt(0).Expr.Should().BeOfType<LiteralExprNode>();
	}

	[Fact]
	public void Decl_Alias()
	{
		var src = "@public alias Foo as Bar;";
		var node = ParseDecl(src).Should().BeOfType<AliasDeclNode>().Subject;
		node.Attrs.Should().NotBeNull();
		node.Attrs!.Attrs.Should().HaveCount(1);
		node.AliasToken.Kind.Should().Be(SyntaxTokenKind.Simple);
		node.Identifier.Value.Should().Be("Foo");
		node.AsToken.SimpleTokenKind.Should().Be(SimpleTokenKind.AsKeyword);
		node.AsToken.IsKeyword.Should().BeTrue();
		node.AsToken.IsContextualKeyword.Should().BeTrue();
		node.Type.Should().BeOfType<NamedTypeNode>().Subject.Identifier.Value.Should().Be("Bar");
		node.SemicolonToken.Should().NotBeNull();
	}

	[Fact]
	public void Decl_Alias_Fn()
	{
		var src = "alias Delegate as @extern fn (S2);";
		var node = ParseDecl(src).Should().BeOfType<AliasDeclNode>().Subject;
		node.AliasToken.Kind.Should().Be(SyntaxTokenKind.Simple);
		node.Identifier.Value.Should().Be("Delegate");
		node.AsToken.SimpleTokenKind.Should().Be(SimpleTokenKind.AsKeyword);
		node.AsToken.IsKeyword.Should().BeTrue();
		node.AsToken.IsContextualKeyword.Should().BeTrue();
		node.Type.Should().BeOfType<FnTypeNode>().Subject.Attrs.Should().NotBeNull();
		node.SemicolonToken.Should().NotBeNull();
	}

	[Fact]
	public void Decl_Struct()
	{
		var src = """
			struct S1 {
				// field
				a: u32 = 2;

				// initializer
				() { }

				// operator
				+() { }

				// method
				f() { }
			}
			""";
		var node = ParseDecl(src).Should().BeOfType<StructDeclNode>().Subject;
		node.Members.Should().HaveCount(4);
		node.Members.ElementAt(0).Should().BeOfType<FieldDeclNode>();
		node.Members.ElementAt(1).Should().BeOfType<MethodDeclNode>().Subject
			.IsInitializer.Should().BeTrue();
		node.Members.ElementAt(2).Should().BeOfType<MethodDeclNode>().Subject
			.IsMethodOp.Should().BeTrue();
		node.Members.ElementAt(3).Should().BeOfType<MethodDeclNode>().Subject
			.IsNormallyCallable.Should().BeTrue();
	}

	[Fact]
	public void Decl_Struct_2()
	{
		var src = "struct S1 { a: u32; f() {} }";
		var node = ParseDecl(src).Should().BeOfType<StructDeclNode>().Subject;
		node.Members.Should().HaveCount(2);
	}

	[Fact]
	public void Decl_Class()
	{
		var src = "class S1 { a = 2; f() {} }";
		var node = ParseDecl(src).Should().BeOfType<ClassDeclNode>().Subject;
		node.Members.Should().HaveCount(2);
	}

	[Fact]
	public void Stmt_Var_1()
	{
		var src = "var a = 2;";
		var node = ParseDecl(src).Should().BeOfType<VarDeclNode>().Subject;
		node.Type.Should().BeNull();
	}

	[Fact]
	public void Stmt_Var_2()
	{
		var src = "var a: u32 = 2;";
		var node = ParseDecl(src).Should().BeOfType<VarDeclNode>().Subject;
		node.Type.Should().NotBeNull();
	}

	[Fact]
	public void Stmt_Var_MissingInitializer()
	{
		var src = "var a: u32;";
		var node = ParseDecl(src).Should().BeOfType<VarDeclNode>().Subject;
		node.Type.Should().NotBeNull();
		node.Initializer.Should().BeNull();
	}

	[Fact]
	public void Stmt_Var_MissingTyAndInitializer()
	{
		var src = "var a;";
		var node = ParseDecl(src).Should().BeOfType<VarDeclNode>().Subject;
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

	[Fact]
	public void BareExprs_Blocks()
	{
		var src = "{ 2 }";
		var node = ParseStmt(src).Should().BeOfType<BareExprStmtNode>().Subject;
		node.IsTreatedAsStmt.Should().BeFalse();
		var blockNode = node.Expr.Should().BeOfType<BlockExprNode>().Subject;
		blockNode.ReturnsExpr.Should().BeTrue();
	}

	[Fact]
	public void BareExprs_Blocks2()
	{
		var src = "{ var a = 2; }";
		var node = ParseStmt(src).Should().BeOfType<BareExprStmtNode>().Subject;
		node.IsTreatedAsStmt.Should().BeTrue();
		var blockNode = node.Expr.Should().BeOfType<BlockExprNode>().Subject;
		blockNode.ReturnsExpr.Should().BeFalse();
	}

	[Fact]
	public void BareExprs_Blocks3()
	{
		var src = "{ { 2 } }";
		var node = ParseStmt(src).Should().BeOfType<BareExprStmtNode>().Subject;
		node.IsTreatedAsStmt.Should().BeFalse();
		var blockNode = node.Expr.Should().BeOfType<BlockExprNode>().Subject;
		blockNode.ReturnsExpr.Should().BeTrue();
	}

	[Fact]
	public void BareExprs_Blocks4()
	{
		var src = "{ { var a = 2; } }";
		var node = ParseStmt(src).Should().BeOfType<BareExprStmtNode>().Subject;
		node.IsTreatedAsStmt.Should().BeTrue();
		var blockNode = node.Expr.Should().BeOfType<BlockExprNode>().Subject;
		blockNode.ReturnsExpr.Should().BeFalse();
	}

	[Theory]
	[InlineData("0", 0)]
	[InlineData("12", 12)]
	[InlineData("49999", 49999)]
	public void Expr_NumericInteger(string src, ulong expected)
	{
		var node = ParseExpr(src).Should().BeOfType<LiteralExprNode>().Subject;
		node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.NumericInteger);
		node.Literal.TryGetNumericIntegerValue(out var value).Should().BeTrue();
		value.Should().Be(expected);
		node.Literal.NumericType.Should().BeNull();
	}

	[Theory]
	[InlineData("12.0", 12.0)]
	[InlineData("12.1", 12.1)]
	[InlineData("0.01", 0.01)]
	public void Expr_NumericFloat(string src, double expected)
	{
		var node = ParseExpr(src).Should().BeOfType<LiteralExprNode>().Subject;
		node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.NumericFloat);
		node.Literal.TryGetNumericFloatValue(out var value).Should().BeTrue();
		value.Should().Be(expected);
		node.Literal.NumericType.Should().BeNull();
	}

	[Theory]
	[InlineData("12u64", 12, "u64")]
	[InlineData("42f32", 42, "f32")]
	public void Expr_NumericIntegerWithType(string src, ulong expected, string expectedType)
	{
		var node = ParseExpr(src).Should().BeOfType<LiteralExprNode>().Subject;
		node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.NumericInteger);
		node.Literal.TryGetNumericIntegerValue(out var value).Should().BeTrue();
		value.Should().Be(expected);
		node.Literal.NumericType.Should().Be(expectedType);
	}

	[Theory]
	[InlineData("12.0u64", 12.0, "u64")]
	[InlineData("12.1f32", 12.1, "f32")]
	public void Expr_NumericFloatWithType(string src, double expected, string expectedType)
	{
		var node = ParseExpr(src).Should().BeOfType<LiteralExprNode>().Subject;
		node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.NumericFloat);
		node.Literal.TryGetNumericFloatValue(out var value).Should().BeTrue();
		value.Should().Be(expected);
		node.Literal.NumericType.Should().Be(expectedType);
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
	public void Expr_TypeCreation()
	{
		var src = "Foo { a = 42, b = false }";
		var creationExpr = ParseExpr(src).Should().BeOfType<TypeCreationExprNode>().Subject;
		creationExpr.TypeExpr.Should().BeOfType<IdentifierExprNode>().Subject.Identifier.Value.Should().Be("Foo");
		creationExpr.MapInitializer.Items.Should().HaveCount(2);
		creationExpr.MapInitializer.WellFormedExprs.ElementAt(0).Lhs.Should().BeOfType<IdentifierExprNode>()
			.Which.Identifier.Value.Should().Be("a");
		creationExpr.MapInitializer.WellFormedExprs.ElementAt(0).Rhs.Should().BeOfType<LiteralExprNode>()
			.Which.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.NumericInteger);
		creationExpr.MapInitializer.WellFormedExprs.ElementAt(1).Lhs.Should().BeOfType<IdentifierExprNode>()
			.Which.Identifier.Value.Should().Be("b");
		creationExpr.MapInitializer.WellFormedExprs.ElementAt(1).Rhs.Should().BeOfType<LiteralExprNode>()
			.Which.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.Bool);
	}

	[Fact]
	public void Expr_TypeCreation_EmptyInitializer()
	{
		var src = "Foo { }";
		var creationExpr = ParseExpr(src).Should().BeOfType<TypeCreationExprNode>().Subject;
		creationExpr.TypeExpr.Should().BeOfType<IdentifierExprNode>().Subject.Identifier.Value.Should().Be("Foo");
		creationExpr.MapInitializer.WellFormedExprs.Should().HaveCount(0);
	}

	[Fact]
	public void Expr_TypeCreation_NestedAccess()
	{
		var src = "S.SNested { a = 42 }";
		var tree = Parser.ParseExpr(src);
		var creationExpr = tree.Root.Should().BeOfType<TypeCreationExprNode>().Subject;
		var accessExpr = creationExpr.TypeExpr.Should().BeOfType<AccessExprNode>().Subject;
		accessExpr.Identifier.Value.Should().Be("SNested");
		accessExpr.Target.Should().BeOfType<IdentifierExprNode>().Subject.Identifier.Value.Should().Be("S");
	}

	[Fact]
	public void Params_NoType()
	{
		// Syntax error, but should still lead to a proper FnDeclNode.
		var src = "fn a(b /* : number */) {}";
		var node = ParseDecl(src).Should().BeOfType<FnDeclNode>().Subject;
		node.Sig.Params.Should().HaveCount(1);
		node.Sig.Params.ElementAt(0).Type.Should().BeNull();
	}

	[Fact]
	public void Params_1()
	{
		var src = "fn a() {}";
		var node = ParseDecl(src).Should().BeOfType<FnDeclNode>().Subject;
		node.Sig.Params.Should().HaveCount(0);
	}

	[Fact]
	public void Params_2()
	{
		var src = "fn a(b: number) {}";
		var node = ParseDecl(src).Should().BeOfType<FnDeclNode>().Subject;
		node.Sig.Params.Should().HaveCount(1);
	}

	[Fact]
	public void Params_3()
	{
		var src = "fn a(b: number, c: number) {}";
		var node = ParseDecl(src).Should().BeOfType<FnDeclNode>().Subject;
		node.Sig.Params.Should().HaveCount(2);
	}

	[Fact]
	public void Args_1()
	{
		var src = "a()";
		var node = ParseExpr(src).Should().BeOfType<CallExprNode>().Subject;
		node.Args.Should().HaveCount(0);
	}

	[Fact]
	public void Args_2()
	{
		var src = "a(b)";
		var node = ParseExpr(src).Should().BeOfType<CallExprNode>().Subject;
		node.Args.Should().HaveCount(1);
	}

	[Fact]
	public void Args_3()
	{
		var src = "a(b, c)";
		var node = ParseExpr(src).Should().BeOfType<CallExprNode>().Subject;
		node.Args.Should().HaveCount(2);
	}

	[Fact]
	public void ExprInArg_1()
	{
		var src = "a(41 + 1)";
		var node = ParseExpr(src).Should().BeOfType<CallExprNode>().Subject;
		node.Args.Should().HaveCount(1);
		node.Args.ElementAt(0).Expr.Should().BeOfType<BinaryExprNode>();
	}

	[Fact]
	public void ExprInArg_2()
	{
		var src = "a(b = 2 + 1)";
		var node = ParseExpr(src).Should().BeOfType<CallExprNode>().Subject;
		node.Args.Should().HaveCount(1);
		node.Args.ElementAt(0).Expr.Should().BeOfType<AssignmentExprNode>();
	}

	[Fact]
	public void ExprInArg_3()
	{
		var src = "a(b = c = d)";
		var node = ParseExpr(src).Should().BeOfType<CallExprNode>().Subject;
		node.Args.Should().HaveCount(1);
		var assignment1 = node.Args.ElementAt(0).Expr.Should().BeOfType<AssignmentExprNode>().Subject;
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
		node.Args.Should().HaveCount(1);
		node.Args.ElementAt(0).Expr.Should().BeOfType<CallExprNode>();
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
		var src = "var a = { 2 };";
		var node = ParseDecl(src).Should().BeOfType<VarDeclNode>().Subject;
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
			fn f() u32 {
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
		var node = ParseDecl(src, hasDiags: true).Should().BeOfType<SkippedTokensDeclNode>().Subject;
		node.Skipped.Should().HaveCount(2);
	}

	[Fact]
	public void WritesDiagnostics()
	{
		// Should write a diag for the missing equal.
		var src = "const a /* = */ 2;";
		var tree = Parser.ParseDecl(src);
		tree.Diagnostics.Diagnostics.Should().HaveCount(1);
	}

	[Fact]
	public void RandomIssue_1()
	{
		// Used to cause an infinite loop when parsing due to not properly checking EOF.
		var src = """
			fn main() {
				d(
			}
			""";
		var tree = Parser.ParseFile(src);
		tree.Diagnostics.Diagnostics.Should().NotBeEmpty();
	}

	[Fact]
	public void LocalDecls()
	{
		var src = """
			fn f() {
				fn f2() S.S2 {
					fn f3() { }
					return S.S2 { a = 42 };
				}
				var a = 2;
				struct S {
					a: i32;
					struct S2 {
						a: i32;
					}
				}
			}
			""";
		var tree = Parser.ParseFile(src);
		tree.Diagnostics.Diagnostics.Should().BeNullOrEmpty();
		var fFnDecl = ((FileNode)tree.Root).Decls.ElementAt(0).Should().BeOfType<FnDeclNode>().Subject;
		var fStmts = fFnDecl.Body!.Stmts;
		fStmts.Should().HaveCount(3);
		var f2FnDecl = fStmts.ElementAt(0).Should().BeOfType<DeclStmtNode>().Subject.Decl.Should().BeOfType<FnDeclNode>().Subject;
		var f2Stmts = f2FnDecl.Body!.Stmts;
		f2Stmts.Should().HaveCount(2);
		f2Stmts.ElementAt(0).Should().BeOfType<DeclStmtNode>().Subject.Decl.Should().BeOfType<FnDeclNode>();
		var sStructDecl = fStmts.ElementAt(2).Should().BeOfType<DeclStmtNode>().Subject.Decl.Should().BeOfType<StructDeclNode>().Subject;
		var sDecls = sStructDecl.DeclsList.Decls;
		sDecls.Should().HaveCount(2);
		sDecls.ElementAt(0).Should().BeOfType<FieldDeclNode>();
		sDecls.ElementAt(1).Should().BeOfType<StructDeclNode>();
	}

	// ---

	private static FileNode ParseFile(string src, bool? hasDiags = false)
	{
		var tree = Parser.ParseFile(src);
		ValidateDiags(tree, hasDiags);
		return ValidateNode((FileNode)tree.Root);
	}

	private static DeclNode ParseDecl(string src, bool? hasDiags = false)
	{
		var tree = Parser.ParseDecl(src);
		ValidateDiags(tree, hasDiags);
		return ValidateNode((DeclNode)tree.Root);
	}

	private static StmtNode ParseStmt(string src, bool? hasDiags = false)
	{
		var tree = Parser.ParseStmt(src);
		ValidateDiags(tree, hasDiags);
		return ValidateNode((StmtNode)tree.Root);
	}

	private static ExprNode ParseExpr(string src, bool? hasDiags = false)
	{
		var tree = Parser.ParseExpr(src);
		ValidateDiags(tree, hasDiags);
		return ValidateNode((ExprNode)tree.Root);
	}

	private static void ValidateDiags(SyntaxTree tree, bool? hasDiags)
	{
		if (hasDiags == false)
		{
			tree.Diagnostics.Diagnostics.Should().BeEmpty();
		}
		else if (hasDiags == true)
		{
			tree.Diagnostics.Diagnostics.Should().NotBeEmpty();
		}
	}

	private static T ValidateNode<T>(T node)
		where T : SyntaxNode
	{
		// Nothing to validate for now? Tree being set doesn't need to be validated anymore since it's required in the ctor.
		return node;
	}
}
