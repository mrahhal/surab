namespace Surab.Analysis;

public class ParserTests
{
	[Fact]
	public void File()
	{
		var code = "const a = 2;";
		var file = ParseFile(code);
		file.Should().NotBeNull();
		file.Imports.Should().BeEmpty();
		file.Decls.Should().HaveCount(1);
		file.Decls.ElementAt(0).Should().BeOfType<ConstDeclNode>();
	}

	[Fact]
	public void Imports()
	{
		var code = """
			import foo;
			import bar.baz as baz2;

			const a = 2;
			""";
		var file = ParseFile(code);
		file.Should().NotBeNull();
		file.Imports.Should().HaveCount(2);
		file.Imports.ElementAt(0).Target.Should().BeOfType<NameExprNode>();
		file.Imports.ElementAt(0).AsName.Should().BeNull();
		file.Imports.ElementAt(1).Target.Should().BeOfType<AccessExprNode>();
		file.Imports.ElementAt(1).AsName.Should().NotBeNull();
		file.Imports.ElementAt(1).AsName!.AsToken.IsMissing.Should().BeFalse();
		file.Imports.ElementAt(1).AsName!.Name.Identifier.Value.Should().Be("baz2");
		file.Decls.Should().HaveCount(1);
	}

	[Fact]
	public void Trivia()
	{
		var code = """
			// some
			/* c */ const /* c */ /* c */ a /* c */ = /* c */ 2 /* c */; // c
			""";
		var tree = Parser.ParseFile(code);
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
		var code = """
			// some
			alias i32 as Integer;
			""";
		var tree = Parser.ParseFile(code);
		var file = (FileNode)tree.Root;

		file.Should().NotBeNull();
		file.Decls.Should().HaveCount(1);
		var aliasDecl = file.Decls.ElementAt(0).Should().BeOfType<AliasDeclNode>().Subject;
		aliasDecl.AliasToken.LeadingTrivia.Should().HaveCount(2); // line comment + whitespace
	}

	[Fact]
	public void Decl_Const()
	{
		var code = "const a = 2;";
		ParseDecl(code).Should().BeOfType<ConstDeclNode>();
	}

	[Fact]
	public void Decl_Fn()
	{
		var code = "fn f() {}";
		var node = ParseDecl(code).Should().BeOfType<FnDeclNode>().Subject;
		node.FnToken.IsKeyword.Should().BeTrue();
		node.FnToken.IsContextualKeyword.Should().BeFalse();
		node.Sig.ReturnType.Should().BeNull();
		node.Body.Should().NotBeNull();
		node.SemicolonToken.Should().BeNull();
	}

	[Fact]
	public void Decl_Fn_2()
	{
		var code = "fn f(number: u32) u32 { 2 }";
		var node = ParseDecl(code).Should().BeOfType<FnDeclNode>().Subject;
		node.Sig.ReturnType.Should().NotBeNull();
		node.Body.Should().NotBeNull();
		node.SemicolonToken.Should().BeNull();
	}

	[Fact]
	public void Decl_Fn_Faulty()
	{
		var code = "fn some()";
		ParseDecl(code, hasDiags: true).Should().NotBeNull();
	}

	[Fact]
	public void Decl_Fn_Faulty2()
	{
		var code = "fn some() /* some fn not complete */ fn main() {}";
		ParseFile(code, hasDiags: true).Should().NotBeNull();
	}

	[Fact]
	public void PtrType()
	{
		var code = """
			fn f() {
				var p1: *i32;
				var p2: *fn (i32) i32;
				var p3: *;
			}
			""";
		var node = ParseDecl(code);
		var varStmt1 = node.DescendantNodes().OfType<VarDeclNode>().ElementAt(0);
		var ptrTypeNode1 = varStmt1.Type!.Type.Should().BeOfType<PtrTypeNode>().Subject;
		ptrTypeNode1.InnerType.Should().BeOfType<ExprTypeNode>().Subject.Expr.Should().BeOfType<NameExprNode>();
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
		var code = "var f: fn (i32, i32) i64;";
		var node = ParseDecl(code).Should().BeOfType<VarDeclNode>().Subject;
		var fnTypeNode = node.Type!.Type.Should().BeOfType<FnTypeNode>().Subject;
		var paramTypes = fnTypeNode.ParamTypes!;
		paramTypes.Should().HaveCount(2);
		paramTypes.ElementAt(0).Type.Should().BeOfType<ExprTypeNode>().Subject.Expr.Should().BeOfType<NameExprNode>();
		paramTypes.ElementAt(0).CommaToken.Should().NotBeNull();
		paramTypes.ElementAt(1).Type.Should().BeOfType<ExprTypeNode>().Subject.Expr.Should().BeOfType<NameExprNode>();
		paramTypes.ElementAt(1).CommaToken.Should().BeNull();
		fnTypeNode.ReturnType.Should().BeOfType<ExprTypeNode>().Subject.Expr.Should().BeOfType<NameExprNode>();
	}

	[Fact]
	public void FnTypeWithAttrs()
	{
		var code = "var f: @extern fn (i32, i32) i64;";
		var node = ParseDecl(code).Should().BeOfType<VarDeclNode>().Subject;
		var fnTypeNode = node.Type!.Type.Should().BeOfType<FnTypeNode>().Subject;
		fnTypeNode.ParamTypes!.Should().HaveCount(2);
		fnTypeNode.Attrs.Should().NotBeNull();
		fnTypeNode.Attrs!.Attrs.Should().HaveCount(1);
	}

	[Fact]
	public void Decl_Attrs()
	{
		var code = """
			@public @some(false)
			fn f() { }
			""";
		var node = ParseDecl(code).Should().BeOfType<FnDeclNode>().Subject;
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
		var code = "@public alias Foo as Bar;";
		var node = ParseDecl(code).Should().BeOfType<AliasDeclNode>().Subject;
		node.Attrs.Should().NotBeNull();
		node.Attrs!.Attrs.Should().HaveCount(1);
		node.AliasToken.Kind.Should().Be(SyntaxTokenKind.Simple);
		node.AsName.Name.Identifier.Value.Should().Be("Bar");
		node.AsName.AsToken.IsMissing.Should().BeFalse();
		node.AsName.AsToken.SimpleTokenKind.Should().Be(SimpleTokenKind.AsKeyword);
		node.AsName.AsToken.IsKeyword.Should().BeTrue();
		node.AsName.AsToken.IsContextualKeyword.Should().BeTrue();
		node.TargetType.Should().BeOfType<ExprTypeNode>().Subject.Expr.Should().BeOfType<NameExprNode>().Subject.Name.Identifier.Value.Should().Be("Foo");
		node.SemicolonToken.Should().NotBeNull();
	}

	[Fact]
	public void Decl_Alias_Fn()
	{
		var code = "alias @extern fn (S2) i32 as Delegate;";
		var node = ParseDecl(code).Should().BeOfType<AliasDeclNode>().Subject;
		node.AliasToken.Kind.Should().Be(SyntaxTokenKind.Simple);
		node.AsName.Name.Identifier.Value.Should().Be("Delegate");
		node.AsName.AsToken.IsMissing.Should().BeFalse();
		var fnType = node.TargetType.Should().BeOfType<FnTypeNode>().Subject;
		fnType.Attrs.Should().NotBeNull();
		fnType.ReturnType.Should().NotBeNull();
		node.SemicolonToken.Should().NotBeNull();
	}

	[Fact]
	public void Decl_Alias_Fn2()
	{
		var code = "alias @extern fn (S2) as Delegate;";
		var node = ParseDecl(code).Should().BeOfType<AliasDeclNode>().Subject;
		node.AliasToken.Kind.Should().Be(SyntaxTokenKind.Simple);
		node.AsName.Name.Identifier.Value.Should().Be("Delegate");
		node.AsName.AsToken.IsMissing.Should().BeFalse();
		var fnType = node.TargetType.Should().BeOfType<FnTypeNode>().Subject;
		fnType.Attrs.Should().NotBeNull();
		fnType.ReturnType.Should().BeNull();
		node.SemicolonToken.Should().NotBeNull();
	}

	[Fact]
	public void Decl_Struct()
	{
		var code = """
			struct S1 {
				// field
				a: u32 = 2;

				// initializer
				() { }

				// operator
				+() { }

				// method
				m() { }

				// fn (static)
				fn f2() { }
			}
			""";
		var node = ParseDecl(code).Should().BeOfType<StructDeclNode>().Subject;
		node.Members.Should().HaveCount(5);
		node.Members.ElementAt(0).Should().BeOfType<FieldDeclNode>();
		node.Members.ElementAt(1).Should().BeOfType<MethodDeclNode>().Subject
			.IsInitializer.Should().BeTrue();
		node.Members.ElementAt(2).Should().BeOfType<MethodDeclNode>().Subject
			.IsMethodOp.Should().BeTrue();
		node.Members.ElementAt(3).Should().BeOfType<MethodDeclNode>().Subject
			.IsNormallyCallable.Should().BeTrue();
		node.Members.ElementAt(4).Should().BeOfType<FnDeclNode>();
	}

	[Fact]
	public void Decl_Struct_MissingName()
	{
		var code = """
			struct {
				a: u32;
			}
			""";
		ParseDecl(code, hasDiags: true).Should().BeOfType<StructDeclNode>();
	}

	[Fact]
	public void Decl_Struct_NotAllowedAsTypeName()
	{
		var code = """
			struct as {
				a: u32;
			}
			""";
		ParseDecl(code, hasDiags: true).Should().BeOfType<StructDeclNode>();
	}

	[Fact]
	public void Decl_Struct_2()
	{
		var code = "struct S1 { a: u32; f() {} }";
		var node = ParseDecl(code).Should().BeOfType<StructDeclNode>().Subject;
		node.Members.Should().HaveCount(2);
	}

	[Fact]
	public void Decl_Impl()
	{
		var code = """
			struct S1 {
				a: u32;

				impl Some {
					foo() {}
				}
			}
			""";
		var node = ParseDecl(code).Should().BeOfType<StructDeclNode>().Subject;
		node.Members.Should().HaveCount(2);
		var implDecl = node.Members.ElementAt(1).Should().BeOfType<ImplDeclNode>().Subject;
		implDecl.Members.Should().HaveCount(1);
		implDecl.Members.ElementAt(0).Should().BeOfType<MethodDeclNode>();
	}

	[Fact]
	public void Stmt_Var_1()
	{
		var code = "var a = 2;";
		var node = ParseDecl(code).Should().BeOfType<VarDeclNode>().Subject;
		node.Type.Should().BeNull();
	}

	[Fact]
	public void Stmt_Var_2()
	{
		var code = "var a: u32 = 2;";
		var node = ParseDecl(code).Should().BeOfType<VarDeclNode>().Subject;
		node.Type.Should().NotBeNull();
	}

	[Fact]
	public void Stmt_Var_MissingInitializer()
	{
		var code = "var a: u32;";
		var node = ParseDecl(code).Should().BeOfType<VarDeclNode>().Subject;
		node.Type.Should().NotBeNull();
		node.Initializer.Should().BeNull();
	}

	[Fact]
	public void Stmt_Var_MissingTyAndInitializer()
	{
		var code = "var a;";
		var node = ParseDecl(code).Should().BeOfType<VarDeclNode>().Subject;
		node.Type.Should().BeNull();
		node.Initializer.Should().BeNull();
	}

	[Fact]
	public void Stmt_Return()
	{
		var code = "return 2;";
		var node = ParseStmt(code).Should().BeOfType<ReturnStmtNode>().Subject;
		node.Expr.Should().BeOfType<LiteralExprNode>();
	}

	[Fact]
	public void Stmt_Expr()
	{
		var code = "a();";
		var node = ParseStmt(code).Should().BeOfType<ExprStmtNode>().Subject;
		node.Expr.Should().NotBeNull();
	}

	[Fact]
	public void Stmt_BareExpr()
	{
		var code = "a()";
		var node = ParseStmt(code).Should().BeOfType<BareExprStmtNode>().Subject;
		node.Expr.Should().NotBeNull();
	}

	[Fact]
	public void EmptyStmt()
	{
		var code = """
			fn f() {
				{ 2; };
				;
			}
			""";
		var fnBody = ParseDecl(code).Should().BeOfType<FnDeclNode>().Subject.Body!.Stmts;
		fnBody.Should().HaveCount(3);
		fnBody.ElementAt(0).Should().BeOfType<BareExprStmtNode>();
		fnBody.ElementAt(1).Should().BeOfType<EmptyStmtNode>();
		fnBody.ElementAt(2).Should().BeOfType<EmptyStmtNode>();
	}

	[Theory]
	[InlineData("0", 0)]
	[InlineData("12", 12)]
	[InlineData("49999", 49999)]
	public void Expr_NumericInteger(string code, ulong expected)
	{
		var node = ParseExpr(code).Should().BeOfType<LiteralExprNode>().Subject;
		node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.NumericInteger);
		node.Literal.TryGetNumericIntegerValue(out var value).Should().BeTrue();
		value.Should().Be(expected);
		node.Literal.NumericType.Should().BeNull();
	}

	[Theory]
	[InlineData("12.0", 12.0)]
	[InlineData("12.1", 12.1)]
	[InlineData("0.01", 0.01)]
	public void Expr_NumericFloat(string code, double expected)
	{
		var node = ParseExpr(code).Should().BeOfType<LiteralExprNode>().Subject;
		node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.NumericFloat);
		node.Literal.TryGetNumericFloatValue(out var value).Should().BeTrue();
		value.Should().Be(expected);
		node.Literal.NumericType.Should().BeNull();
	}

	[Theory]
	[InlineData("12u64", 12, "u64")]
	[InlineData("42f32", 42, "f32")]
	public void Expr_NumericIntegerWithType(string code, ulong expected, string expectedType)
	{
		var node = ParseExpr(code).Should().BeOfType<LiteralExprNode>().Subject;
		node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.NumericInteger);
		node.Literal.TryGetNumericIntegerValue(out var value).Should().BeTrue();
		value.Should().Be(expected);
		node.Literal.NumericType.Should().Be(expectedType);
	}

	[Theory]
	[InlineData("12.0u64", 12.0, "u64")]
	[InlineData("12.1f32", 12.1, "f32")]
	public void Expr_NumericFloatWithType(string code, double expected, string expectedType)
	{
		var node = ParseExpr(code).Should().BeOfType<LiteralExprNode>().Subject;
		node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.NumericFloat);
		node.Literal.TryGetNumericFloatValue(out var value).Should().BeTrue();
		value.Should().Be(expected);
		node.Literal.NumericType.Should().Be(expectedType);
	}

	[Theory]
	[InlineData("'a'", 'a')]
	[InlineData("'勉'", '勉')]
	public void Expr_Char(string code, char expected)
	{
		var node = ParseExpr(code).Should().BeOfType<LiteralExprNode>().Subject;
		node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.Char);
		node.Literal.TryGetCharValue(out var value).Should().BeTrue();
		value.Should().Be(expected);
	}

	[Theory]
	[InlineData("'a")]
	[InlineData("'勉")]
	public void Expr_Char_Invalid(string code)
	{
		var node = ParseExpr(code, hasDiags: true).Should().BeOfType<LiteralExprNode>().Subject;
		node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.Char);
		node.Literal.TryGetCharValue(out var value).Should().BeTrue();
		value.Should().BeNull();
	}

	[Theory]
	[InlineData("\"a\"", "a")]
	[InlineData("\"勉\"", "勉")]
	public void Expr_String(string code, string expected)
	{
		var node = ParseExpr(code).Should().BeOfType<LiteralExprNode>().Subject;
		node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.String);
		node.Literal.TryGetStringValue(out var value).Should().BeTrue();
		value.Should().Be(expected);
	}

	[Theory]
	[InlineData("\"a")]
	[InlineData("\"勉")]
	public void Expr_String_Invalid(string code)
	{
		var node = ParseExpr(code, hasDiags: true).Should().BeOfType<LiteralExprNode>().Subject;
		node.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.String);
		node.Literal.TryGetStringValue(out var value).Should().BeTrue();
		value.Should().BeNull();
	}

	[Fact]
	public void Expr_Binary_1()
	{
		var code = "2 + 3";
		var node = ParseExpr(code).Should().BeOfType<BinaryExprNode>().Subject;
		node.Op.BinaryOperatorTokenKind.Should().Be(BinaryOperatorTokenKind.Add);
		node.Left.Should().BeOfType<LiteralExprNode>();
		node.Right.Should().BeOfType<LiteralExprNode>();
	}

	[Fact]
	public void Expr_Binary_2()
	{
		var code = "2 * 3 + 4";
		var node = ParseExpr(code).Should().BeOfType<BinaryExprNode>().Subject;
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
		var code = "2 ^^ 3 ^^ 4";
		var node = ParseExpr(code).Should().BeOfType<BinaryExprNode>().Subject;
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
		var code = "2 + -3";
		var node = ParseExpr(code).Should().BeOfType<BinaryExprNode>().Subject;
		node.Op.BinaryOperatorTokenKind.Should().Be(BinaryOperatorTokenKind.Add);
		node.Left.Should().BeOfType<LiteralExprNode>();
		{
			var right = node.Right.Should().BeOfType<UnaryExprNode>().Subject;
			right.Op.UnaryOperatorTokenKind.Should().Be(UnaryOperatorTokenKind.Neg);
			right.Expr.Should().BeOfType<LiteralExprNode>();
		}
	}

	[Fact]
	public void Expr_Binary_5()
	{
		var code = "4 < 2";
		var node = ParseExpr(code).Should().BeOfType<BinaryExprNode>().Subject;
		node.Op.BinaryOperatorTokenKind.Should().Be(BinaryOperatorTokenKind.Lt);
		node.Left.Should().BeOfType<LiteralExprNode>();
		node.Right.Should().BeOfType<LiteralExprNode>();
	}

	[Fact]
	public void Expr_Access()
	{
		var code = "a.b";
		var tree = Parser.ParseExpr(code);
		var accessExpr = tree.Root.Should().BeOfType<AccessExprNode>().Subject;
		accessExpr.Name.Identifier.Value.Should().Be("b");
		accessExpr.Target.Should().BeOfType<NameExprNode>().Subject.Name.Identifier.Value.Should().Be("a");
	}

	[Fact]
	public void Expr_ModuleAccess()
	{
		var code = "a::b";
		var tree = Parser.ParseExpr(code);
		var accessExpr = tree.Root.Should().BeOfType<ModuleAccessExprNode>().Subject;
		accessExpr.Name.Identifier.Value.Should().Be("b");
		accessExpr.Target.Should().BeOfType<NameExprNode>().Subject.Name.Identifier.Value.Should().Be("a");
	}

	[Fact]
	public void Expr_TypeCreation()
	{
		var code = "Foo.{ a: 42, b: false }";
		var creationExpr = ParseExpr(code).Should().BeOfType<TypeCreationExprNode>().Subject;
		creationExpr.TypeExpr.Should().BeOfType<NameExprNode>().Subject.Name.Identifier.Value.Should().Be("Foo");
		creationExpr.ObjectInitializer.Items.Should().HaveCount(2);
		creationExpr.ObjectInitializer.Items.ElementAt(0).Identifier.Value.Should().Be("a");
		creationExpr.ObjectInitializer.Items.ElementAt(0).ColonToken.IsMissing.Should().BeFalse();
		creationExpr.ObjectInitializer.Items.ElementAt(0).Expr.Should().BeOfType<LiteralExprNode>()
			.Which.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.NumericInteger);
		creationExpr.ObjectInitializer.Items.ElementAt(1).Identifier.Value.Should().Be("b");
		creationExpr.ObjectInitializer.Items.ElementAt(1).ColonToken.IsMissing.Should().BeFalse();
		creationExpr.ObjectInitializer.Items.ElementAt(1).Expr.Should().BeOfType<LiteralExprNode>()
			.Which.Literal.LiteralTokenKind.Should().Be(LiteralTokenKind.Bool);
	}

	[Fact]
	public void Expr_TypeCreation_EmptyInitializer()
	{
		var code = "Foo.{ }";
		var creationExpr = ParseExpr(code).Should().BeOfType<TypeCreationExprNode>().Subject;
		creationExpr.TypeExpr.Should().BeOfType<NameExprNode>().Subject.Name.Identifier.Value.Should().Be("Foo");
		creationExpr.ObjectInitializer.Items.Should().HaveCount(0);
	}

	[Fact]
	public void Expr_TypeCreation_NestedAccess()
	{
		var code = "S.SNested.{ a: 42 }";
		var tree = Parser.ParseExpr(code);
		var creationExpr = tree.Root.Should().BeOfType<TypeCreationExprNode>().Subject;
		var accessExpr = creationExpr.TypeExpr.Should().BeOfType<AccessExprNode>().Subject;
		accessExpr.Name.Identifier.Value.Should().Be("SNested");
		accessExpr.Target.Should().BeOfType<NameExprNode>().Subject.Name.Identifier.Value.Should().Be("S");
	}

	[Fact]
	public void Expr_TypeCreation_ModuleAccess()
	{
		var code = "a::B.{ a: 42 }";
		var tree = Parser.ParseExpr(code);
		var creationExpr = tree.Root.Should().BeOfType<TypeCreationExprNode>().Subject;
		var accessExpr = creationExpr.TypeExpr.Should().BeOfType<ModuleAccessExprNode>().Subject;
		accessExpr.Name.Identifier.Value.Should().Be("B");
		accessExpr.Target.Should().BeOfType<NameExprNode>().Subject.Name.Identifier.Value.Should().Be("a");
	}

	[Fact]
	public void Expr_If()
	{
		var code = "if b { c }";
		var node = ParseStmt(code).Should().BeOfType<BareExprStmtNode>().Subject;
		var ifExpr = node.Expr.Should().BeOfType<IfExprNode>().Subject;
		ifExpr.ThenBlock.Should().BeOfType<BlockExprNode>();
	}

	[Fact]
	public void Expr_If2()
	{
		var code = "var a = if b { c } else { d };";
		var node = ParseDecl(code).Should().BeOfType<VarDeclNode>().Subject;
		var ifExpr = node.Initializer!.Expr.Should().BeOfType<IfExprNode>().Subject;
		ifExpr.ElseClause.Should().NotBeNull();
		ifExpr.ElseClause!.Expr.Should().BeOfType<BlockExprNode>();
	}

	[Fact]
	public void Expr_If3()
	{
		var code = "if b { c } else if d { e } else { f }";
		var node = ParseStmt(code).Should().BeOfType<BareExprStmtNode>().Subject;
		var ifExpr = node.Expr.Should().BeOfType<IfExprNode>().Subject;
		ifExpr.ThenBlock.Should().BeOfType<BlockExprNode>();
		ifExpr.ElseClause.Should().NotBeNull();
		var elseIfExpr = ifExpr.ElseClause!.Expr.Should().BeOfType<IfExprNode>().Subject;
		elseIfExpr.ElseClause.Should().NotBeNull();
		elseIfExpr.ElseClause!.Expr.Should().BeOfType<BlockExprNode>();
	}

	[Fact]
	public void Params_NoType()
	{
		// Syntax error, but should still lead to a proper FnDeclNode.
		var code = "fn a(b /* : number */) {}";
		var node = ParseDecl(code).Should().BeOfType<FnDeclNode>().Subject;
		node.Sig.Params.Should().HaveCount(1);
		node.Sig.Params.ElementAt(0).Type.Should().BeNull();
	}

	[Fact]
	public void Params_1()
	{
		var code = "fn a() {}";
		var node = ParseDecl(code).Should().BeOfType<FnDeclNode>().Subject;
		node.Sig.Params.Should().HaveCount(0);
	}

	[Fact]
	public void Params_2()
	{
		var code = "fn a(b: number) {}";
		var node = ParseDecl(code).Should().BeOfType<FnDeclNode>().Subject;
		node.Sig.Params.Should().HaveCount(1);
	}

	[Fact]
	public void Params_3()
	{
		var code = "fn a(b: number, c: number) {}";
		var node = ParseDecl(code).Should().BeOfType<FnDeclNode>().Subject;
		node.Sig.Params.Should().HaveCount(2);
	}

	[Fact]
	public void Args_1()
	{
		var code = "a()";
		var node = ParseExpr(code).Should().BeOfType<CallExprNode>().Subject;
		node.Args.Should().HaveCount(0);
	}

	[Fact]
	public void Args_2()
	{
		var code = "a(b)";
		var node = ParseExpr(code).Should().BeOfType<CallExprNode>().Subject;
		node.Args.Should().HaveCount(1);
	}

	[Fact]
	public void Args_3()
	{
		var code = "a(b, c)";
		var node = ParseExpr(code).Should().BeOfType<CallExprNode>().Subject;
		node.Args.Should().HaveCount(2);
	}

	[Fact]
	public void ExprInArg_1()
	{
		var code = "a(41 + 1)";
		var node = ParseExpr(code).Should().BeOfType<CallExprNode>().Subject;
		node.Args.Should().HaveCount(1);
		node.Args.ElementAt(0).Expr.Should().BeOfType<BinaryExprNode>();
	}

	[Fact]
	public void ExprInArg_2()
	{
		var code = "a(b = 2 + 1)";
		var node = ParseExpr(code).Should().BeOfType<CallExprNode>().Subject;
		node.Args.Should().HaveCount(1);
		node.Args.ElementAt(0).Expr.Should().BeOfType<AssignmentExprNode>();
	}

	[Fact]
	public void ExprInArg_3()
	{
		var code = "a(b = c = d)";
		var node = ParseExpr(code).Should().BeOfType<CallExprNode>().Subject;
		node.Args.Should().HaveCount(1);
		var assignment1 = node.Args.ElementAt(0).Expr.Should().BeOfType<AssignmentExprNode>().Subject;
		assignment1.Lhs.Should().BeOfType<NameExprNode>().Subject.Name.Identifier.Value.Should().Be("b");
		var assignment2 = assignment1.Rhs.Should().BeOfType<AssignmentExprNode>().Subject;
		assignment2.Lhs.Should().BeOfType<NameExprNode>().Subject.Name.Identifier.Value.Should().Be("c");
		assignment2.Rhs.Should().BeOfType<NameExprNode>().Subject.Name.Identifier.Value.Should().Be("d");
	}

	[Fact]
	public void ExprInArg_4()
	{
		var code = "a(b())";
		var node = ParseExpr(code).Should().BeOfType<CallExprNode>().Subject;
		node.Args.Should().HaveCount(1);
		node.Args.ElementAt(0).Expr.Should().BeOfType<CallExprNode>();
	}

	[Fact]
	public void Generics_NoGenericParams()
	{
		var code = "struct S { }";
		var node = ParseDecl(code).Should().BeOfType<StructDeclNode>().Subject;
		node.Name.Identifier.Value.Should().Be("S");
		node.Name.GenericParamsList.Should().BeNull();
	}

	[Fact]
	public void GenericParamTypes_1()
	{
		var code = "struct S<T> { }";
		var node = ParseDecl(code).Should().BeOfType<StructDeclNode>().Subject;
		node.Name.Identifier.Value.Should().Be("S");
		node.Name.GenericParamsList.Should().NotBeNull();
		node.Name.GenericParamsList!.GenericParams.Should().HaveCount(1);
		node.Name.GenericParamsList!.GenericParams.ElementAt(0).Identifier.Value.Should().Be("T");
	}

	[Fact]
	public void GenericParamTypes_2()
	{
		var code = "struct S<T, T2> { }";
		var node = ParseDecl(code).Should().BeOfType<StructDeclNode>().Subject;
		node.Name.GenericParamsList.Should().NotBeNull();
		node.Name.GenericParamsList!.GenericParams.Should().HaveCount(2);
	}

	[Fact]
	public void GenericParamTypes_3()
	{
		var code = "var a = S<T, T2>.a;";
		var node = ParseDecl(code).Should().BeOfType<VarDeclNode>().Subject;
		var accessExpr = node.Initializer!.Expr.Should().BeOfType<AccessExprNode>().Subject;
		var nameExpr = accessExpr.Target.Should().BeOfType<NameExprNode>().Subject;
		nameExpr.Name.Identifier.Value.Should().Be("S");
		nameExpr.Name.GenericArgsList.Should().NotBeNull();
		nameExpr.Name.GenericArgsList!.GenericArgs.Should().HaveCount(2);
		nameExpr.Name.GenericArgsList!.GenericArgs.ElementAt(0).Type.Should().BeOfType<ExprTypeNode>().Subject
			.Expr.Should().BeOfType<NameExprNode>().Subject
			.Name.Identifier.Value.Should().Be("T");
	}

	[Fact]
	public void GenericMethod_1()
	{
		var code = """
			struct S<T1> {
				f<T2, T3>() { }
			}
			""";
		var node = ParseDecl(code).Should().BeOfType<StructDeclNode>().Subject;
		node.Name.Identifier.Value.Should().Be("S");
		node.Name.GenericParamsList.Should().NotBeNull();
		node.Name.GenericParamsList!.GenericParams.Should().HaveCount(1);
		node.Name.GenericParamsList!.GenericParams.ElementAt(0).Identifier.Value.Should().Be("T1");
		node.Members.Should().HaveCount(1);
		var fMethodNode = node.Members.ElementAt(0).Should().BeOfType<MethodDeclNode>().Subject;
		fMethodNode.Sig.Name.GenericParamsList.Should().NotBeNull();
		fMethodNode.Sig.Name.GenericParamsList!.GenericParams.Should().HaveCount(2);
		fMethodNode.Sig.Name.GenericParamsList!.GenericParams.ElementAt(0).Identifier.Value.Should().Be("T2");
		fMethodNode.Sig.Name.GenericParamsList!.GenericParams.ElementAt(1).Identifier.Value.Should().Be("T3");
	}

	[Fact]
	public void LessThan()
	{
		var code = "var a = S < T;";
		var node = ParseDecl(code).Should().BeOfType<VarDeclNode>().Subject;
		node.Initializer!.Expr.Should().BeOfType<BinaryExprNode>();
	}

	[Fact]
	public void Assignment()
	{
		var code = "b = c = d";
		var assignment1 = ParseExpr(code).Should().BeOfType<AssignmentExprNode>().Subject;
		assignment1.Lhs.Should().BeOfType<NameExprNode>().Subject.Name.Identifier.Value.Should().Be("b");
		var assignment2 = assignment1.Rhs.Should().BeOfType<AssignmentExprNode>().Subject;
		assignment2.Lhs.Should().BeOfType<NameExprNode>().Subject.Name.Identifier.Value.Should().Be("c");
		assignment2.Rhs.Should().BeOfType<NameExprNode>().Subject.Name.Identifier.Value.Should().Be("d");
	}

	[Fact]
	public void BlockAsExpr()
	{
		var code = "var a = { 2 };";
		var node = ParseDecl(code).Should().BeOfType<VarDeclNode>().Subject;
		var block = node.Initializer!.Expr.Should().BeOfType<BlockExprNode>().Subject;
		block.Stmts.Should().HaveCount(1);
		var bareExpr = block.Stmts.ElementAt(0).Should().BeOfType<BareExprStmtNode>().Subject;
		bareExpr.Expr.Should().BeOfType<LiteralExprNode>();
	}

	[Fact]
	public void BlockAsStmt()
	{
		var code = "{ }";
		var node = ParseStmt(code).Should().BeOfType<BareExprStmtNode>().Subject;
		node.Expr.Should().BeOfType<BlockExprNode>();
	}

	[Fact]
	public void BlockAsStmt_2()
	{
		var code = """
			fn f() u32 {
				{ 1 }
				{ 2; }
			}
			""";
		var node = ParseDecl(code).Should().BeOfType<FnDeclNode>().Subject;
		var body = node.Body!;
		body.Should().NotBeNull();
		body.Stmts.Should().HaveCount(2);
		body.Stmts.ElementAt(0).Should().BeOfType<BareExprStmtNode>();
		body.Stmts.ElementAt(1).Should().BeOfType<BareExprStmtNode>();
	}

	[Fact]
	public void SkippedTokens()
	{
		var code = "gomi";
		var node = ParseDecl(code, hasDiags: true).Should().BeOfType<SkippedTokensDeclNode>().Subject;
		node.Skipped.Should().HaveCount(1);
	}

	[Fact]
	public void WritesDiags()
	{
		// Should write a diag for the missing equal.
		var code = "const a /* = */ 2;";
		var tree = Parser.ParseDecl(code);
		tree.Diags.Diags.Should().HaveCount(1);
	}

	[Fact]
	public void RandomIssue_1()
	{
		// Used to cause an infinite loop when parsing due to not properly checking EOF.
		var code = """
			fn main() {
				d(
			}
			""";
		var tree = Parser.ParseFile(code);
		tree.Diags.Diags.Should().NotBeEmpty();
	}

	[Fact]
	public void LocalDecls()
	{
		var code = """
			fn f() {
				fn f2() S.S2 {
					fn f3() { }
					return S.S2.{ a: 42 };
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
		var tree = Parser.ParseFile(code);
		tree.Diags.Diags.Should().BeNullOrEmpty();
		var fFnDecl = ((FileNode)tree.Root).Decls.ElementAt(0).Should().BeOfType<FnDeclNode>().Subject;
		var fStmts = fFnDecl.Body!.Stmts;
		fStmts.Should().HaveCount(3);
		var f2FnDecl = fStmts.ElementAt(0).Should().BeOfType<DeclStmtNode>().Subject.Decl.Should().BeOfType<FnDeclNode>().Subject;
		var f2Stmts = f2FnDecl.Body!.Stmts;
		f2Stmts.Should().HaveCount(2);
		f2Stmts.ElementAt(0).Should().BeOfType<DeclStmtNode>().Subject.Decl.Should().BeOfType<FnDeclNode>();
		var sStructDecl = fStmts.ElementAt(2).Should().BeOfType<DeclStmtNode>().Subject.Decl.Should().BeOfType<StructDeclNode>().Subject;
		var sMembers = sStructDecl.Members;
		sMembers.Should().HaveCount(2);
		sMembers.ElementAt(0).Should().BeOfType<FieldDeclNode>();
		sMembers.ElementAt(1).Should().BeOfType<StructDeclNode>();
	}

	// ---

	private static FileNode ParseFile(string code, bool? hasDiags = false)
	{
		var tree = Parser.ParseFile(code);
		ValidateDiags(tree, hasDiags);
		return ValidateNode((FileNode)tree.Root);
	}

	private static DeclNode ParseDecl(string code, bool? hasDiags = false)
	{
		var tree = Parser.ParseDecl(code);
		ValidateDiags(tree, hasDiags);
		return ValidateNode((DeclNode)tree.Root);
	}

	private static StmtNode ParseStmt(string code, bool? hasDiags = false)
	{
		var tree = Parser.ParseStmt(code);
		ValidateDiags(tree, hasDiags);
		return ValidateNode((StmtNode)tree.Root);
	}

	private static ExprNode ParseExpr(string code, bool? hasDiags = false)
	{
		var tree = Parser.ParseExpr(code);
		ValidateDiags(tree, hasDiags);
		return ValidateNode((ExprNode)tree.Root);
	}

	private static void ValidateDiags(SyntaxTree tree, bool? hasDiags)
	{
		if (hasDiags == false && tree.Diags.Diags.Count > 0)
		{
			AnalysisAssert.Diags(tree.File.Text, [], tree.Diags.Diags);
		}
		else if (hasDiags == true && tree.Diags.Diags.Count == 0)
		{
			tree.Diags.Diags.Should().NotBeEmpty();
		}
	}

	private static T ValidateNode<T>(T node)
		where T : SyntaxNode
	{
		// Nothing to validate for now? Tree being set doesn't need to be validated anymore since it's required in the ctor.
		return node;
	}
}
