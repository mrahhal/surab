using Surab.Analysis;

namespace Surab.Compilation;

public class LowererTests
{
	[Fact]
	public void BareExprStmtToReturnStmt()
	{
		var src = """
			fn f() i32 {
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
			fn f() { }
			""";
		var f = LowerFn(src);
		var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body!).ToList();
		body.Should().HaveCount(0);
	}

	[Fact]
	public void ExprRewriting_1()
	{
		var src = """
			fn f() {
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
			fn f() {
				var b: i32;
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
			fn f() {
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
			fn f() {
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
			fn f() {
				var a: i32;
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
			fn f() {
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
			struct S1 { a: i32; }
			fn f() {
				var s = S1 { a = 42 };
			}
			""";
		var module = LowerModule(src);
		var body = LoweringUtils.EnumerateUnwrappedDescendants(module.Decls[1]).ToList();
		body.Should().HaveCount(2);
		var store = body.OfType<LoweredVariableStoreStmt>().Single();
		store.Variable.Name.Should().Be("s");
		store.Expr.Should().BeOfType<LoweredStructCreationExpr>();
	}

	[Fact]
	public void StructFieldLoad()
	{
		var src = """
			struct S1 { a: i32; }
			fn f() {
				var s = S1 { a = 42 };
				var a = s.a;
			}
			""";
		var module = LowerModule(src);
		var body = LoweringUtils.EnumerateUnwrappedDescendants(module.Decls[1]).ToList();
		body.Should().HaveCount(4);
		var variableStore = body.OfType<LoweredVariableStoreStmt>().Last();
		var fieldStore = variableStore.Expr.Should().BeOfType<LoweredFieldLoadExpr>().Subject;
		fieldStore.Field.Name.Should().Be("a");
	}

	[Fact]
	public void StructFieldStore()
	{
		var src = """
			struct S1 { a: i32; }
			fn f() {
				var s = S1 { a = 42 };
				s.a = 43;
			}
			""";
		var module = LowerModule(src);
		var body = LoweringUtils.EnumerateUnwrappedDescendants(module.Decls[1]).ToList();
		body.Should().HaveCount(3);
		var fieldStore = body.OfType<LoweredFieldStoreStmt>().Last();
		fieldStore.Field.Name.Should().Be("a");
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
		var reasoner = Reasoner.Create(tree);
		return Lowerer.Lower(reasoner);
	}
}
