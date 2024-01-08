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
			struct S {
				a: i32;
				() { }
			}
			fn f() {
				var s = S { a = 42 };
			}
			""";
		var module = LowerFile(src);
		var body = LoweringUtils.EnumerateUnwrappedDescendants(module.Decls[1]).ToList();
		body.Should().HaveCount(2);
		body[0].Should().BeOfType<LoweredVariableDeclareStmt>();
		var storeStmt = body[1].Should().BeOfType<LoweredVariableStoreStmt>().Subject;
		var storeExprNodes = LoweringUtils.EnumerateUnwrappedDescendants(storeStmt.Expr).ToList();

		storeExprNodes[0].Should().BeOfType<LoweredObjectAllocStmt>();
		storeExprNodes[1].Should().BeOfType<LoweredFieldStoreStmt>();
		storeExprNodes[2].Should().BeOfType<LoweredExprStmt>().Subject
			.Expr.Should().BeOfType<LoweredCallExpr>();
		storeExprNodes[3].Should().BeOfType<LoweredSymbolLoadExpr>();
	}

	[Fact]
	public void StructFieldLoad()
	{
		var src = """
			struct S { a: i32; }
			fn f() {
				var s = S { a = 42 };
				var a = s.a;
			}
			""";
		var module = LowerFile(src);
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
			struct S { a: i32; }
			fn f() {
				var s = S { a = 42 };
				s.a = 43;
			}
			""";
		var module = LowerFile(src);
		var body = LoweringUtils.EnumerateUnwrappedDescendants(module.Decls[1]).ToList();
		body.Should().HaveCount(3);
		var storeExpr = body[2].Should().BeOfType<LoweredExprStmt>().Subject
			.Expr.Should().BeOfType<LoweredStmtListEndingWithExpr>().Subject;
		storeExpr.Stmts.Should().HaveCount(1);
		storeExpr.Stmts[0].Should().BeOfType<LoweredFieldStoreStmt>();
		var fieldLoadExpr = storeExpr.EndingExpr.Should().BeOfType<LoweredFieldLoadExpr>().Subject;
		fieldLoadExpr.Field.Name.Should().Be("a");
	}

	[Fact]
	public void AddressInsideDerefCancelEachOther()
	{
		var src = """
			fn f() i32 {
				var n = 42;
				*&n;
			}
			""";
		var f = LowerFn(src);
		var body = LoweringUtils.EnumerateUnwrappedDescendants(f.Body!).ToList();
		body.Should().HaveCount(3);
		body[2].Should().BeOfType<LoweredExprStmt>().Subject
			.Expr.Should().BeOfType<LoweredVariableLoadExpr>(); // Deref(Address) disappears
	}

	[Fact]
	public void AutoDeref()
	{
		var src = """
			struct S {
				a: i32;

				mutate() {
					this.a = this.a + 1;
				}
			}
			fn f() i32 {
				var s = S { a = 42 };
				var s_ptr = &s;
				var s_ptr_ptr = &s_ptr;
				print_i32(s_ptr.a);
				print_i32(s_ptr_ptr.a);
				s_ptr.mutate();
			}
			""";
		var module = LowerFile(src);
		var body = LoweringUtils.EnumerateUnwrappedDescendants(module.Decls[1]).ToList();
		var callExpr1 = body.OfType<LoweredExprStmt>().ElementAt(0).Expr.Should().BeOfType<LoweredCallExpr>().Subject;
		{
			var fieldLoadExpr = callExpr1.Args[0].Should().BeOfType<LoweredFieldLoadExpr>().Subject;
			var deref1 = fieldLoadExpr.Target.Should().BeOfType<LoweredUnaryExpr>().Subject;
			deref1.Op.Should().Be(UnaryOperatorTokenKind.Deref);
			deref1.Type.Should().BeOfType<StructTypeSymbol>();
			deref1.Expr.Should().BeOfType<LoweredVariableLoadExpr>();
		}
		var callExpr2 = body.OfType<LoweredExprStmt>().ElementAt(1).Expr.Should().BeOfType<LoweredCallExpr>().Subject;
		{
			var fieldLoadExpr = callExpr2.Args[0].Should().BeOfType<LoweredFieldLoadExpr>().Subject;
			var deref1 = fieldLoadExpr.Target.Should().BeOfType<LoweredUnaryExpr>().Subject;
			deref1.Op.Should().Be(UnaryOperatorTokenKind.Deref);
			deref1.Type.Should().BeOfType<StructTypeSymbol>();
			var deref2 = deref1.Expr.Should().BeOfType<LoweredUnaryExpr>().Subject;
			deref2.Op.Should().Be(UnaryOperatorTokenKind.Deref);
			deref2.Type.Should().BeOfType<PtrTypeSymbol>();
			deref2.Expr.Should().BeOfType<LoweredVariableLoadExpr>();
		}
		var callExpr3 = body.OfType<LoweredExprStmt>().ElementAt(2).Expr.Should().BeOfType<LoweredCallExpr>().Subject;
		{
			callExpr3.Args[0].Should().BeOfType<LoweredVariableLoadExpr>();
		}
	}

	[Fact]
	public void AutoDeref_Unneeded()
	{
		var src = """
			struct S { a: i32; }
			fn f() i32 {
				var s = S { a = 42 };
				print_i32(s.a);
			}
			""";
		var file = LowerFile(src);
		var body = LoweringUtils.EnumerateUnwrappedDescendants(file.Decls[1]).ToList();
		var callExpr = body.OfType<LoweredExprStmt>().Single().Expr.Should().BeOfType<LoweredCallExpr>().Subject;
		var fieldLoadExpr = callExpr.Args[0].Should().BeOfType<LoweredFieldLoadExpr>().Subject;
		fieldLoadExpr.Target.Should().BeOfType<LoweredVariableLoadExpr>();
	}

	private static LoweredFnDecl LowerFn(string src)
	{
		var file = LowerFile(src);
		return file.Decls.ElementAt(0).Should().BeOfType<LoweredFnDecl>().Subject;
	}

	private static LoweredFile LowerFile(string src)
	{
		var tree = Lower(src);
		return tree.Root.Should().BeOfType<LoweredFile>().Subject;
	}

	private static LoweredTree Lower(string src)
	{
		var (_, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(src);
		return Lowerer.Lower(reasoner);
	}
}
