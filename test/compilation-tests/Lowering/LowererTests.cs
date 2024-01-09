using Surab.Analysis;

namespace Surab.Compilation;

public class LowererTests
{
	[Fact]
	public void VarHoisting()
	{
		var src = """
			fn f() {
				var a = 2;
				print_i32(a);
				var b = 3;
			}
			""";
		var f = LowerFn(src);
		var body = f.GetFnBody();
		body.Should().HaveCount(5);
		var i = 0;
		body[i++].Should().BeOfType<LoweredVariableDeclareStmt>();
		body[i++].Should().BeOfType<LoweredVariableDeclareStmt>();
		body[i++].Should().BeOfType<LoweredVariableStoreStmt>();
		body[i++].Should().BeOfType<LoweredExprStmt>().Subject.Expr.Should().BeOfType<LoweredCallExpr>();
		body[i++].Should().BeOfType<LoweredVariableStoreStmt>();
	}

	[Fact]
	public void DeclHoisting()
	{
		var src = """
			fn f() {
				fn f2() {
					fn f3() { }
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
		var decls = LowerFile(src).UnwrapContainers().ToList();
		decls.Should().HaveCount(5);
		var i = 0;
		var names = new[]
		{
			decls[i++].Should().BeOfType<LoweredFnDecl>().Subject.FnSymbol.Name,
			decls[i++].Should().BeOfType<LoweredFnDecl>().Subject.FnSymbol.Name,
			decls[i++].Should().BeOfType<LoweredFnDecl>().Subject.FnSymbol.Name,
			decls[i++].Should().BeOfType<LoweredTypeDecl>().Subject.TypeSymbol.Name,
			decls[i++].Should().BeOfType<LoweredTypeDecl>().Subject.TypeSymbol.Name,
		};
		names.Should().BeEquivalentTo([
			"f",
			"f3",
			"f2",
			"S2",
			"S",
		], o => o.WithStrictOrdering());
	}

	[Fact]
	public void BareExprStmtToReturnStmt()
	{
		var src = """
			fn f() i32 {
				2
			}
			""";
		var f = LowerFn(src);
		var body = f.GetFnBody();
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
		var body = f.GetFnBody();
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
		var body = f.GetFnBody();
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
		var file = LowerFile(src);
		var body = file.GetFnBody("f");
		body.Should().HaveCount(2);
		body[0].Should().BeOfType<LoweredVariableDeclareStmt>();
		var storeStmt = body[1].Should().BeOfType<LoweredVariableStoreStmt>().Subject;
		var storeExprNodes = storeStmt.Expr.UnwrapContainers().ToList();

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
		var file = LowerFile(src);
		var body = file.GetFnBody("f");
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
		var file = LowerFile(src);
		var body = file.GetFnBody("f");
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
		var body = f.GetFnBody();
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
		var file = LowerFile(src);
		var body = file.GetFnBody("f");
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
		var body = file.GetFnBody("f");
		var callExpr = body.OfType<LoweredExprStmt>().Single().Expr.Should().BeOfType<LoweredCallExpr>().Subject;
		var fieldLoadExpr = callExpr.Args[0].Should().BeOfType<LoweredFieldLoadExpr>().Subject;
		fieldLoadExpr.Target.Should().BeOfType<LoweredVariableLoadExpr>();
	}

	private static LoweredFnDecl LowerFn(string src)
	{
		var file = LowerFile(src);
		return file.UnwrapContainers().ElementAt(0).Should().BeOfType<LoweredFnDecl>().Subject;
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

internal static class LowererTestsExtensions
{
	public static List<LoweredStmt> GetFnBody(this LoweredNode node, string? name = null)
	{
		LoweredFnDecl fnDecl;

		if (node is LoweredFnDecl selfFnDecl)
		{
			if (name != null)
			{
				throw new Exception("Function name was specified but the given node is itself a function.");
			}
			fnDecl = selfFnDecl;
		}
		else
		{
			var fns = node.DescendantNodes().OfType<LoweredFnDecl>().ToList();
			if (fns.Count > 1 && name == null)
				throw new Exception("Specifying a name is required when there are more than one function.");

			if (name == null)
			{
				fnDecl = fns.Single();
			}
			else
			{
				fnDecl = fns.Single(x => x.FnSymbol.Name == name);
			}
		}

		return fnDecl.Body!
			.UnwrapContainers()
			.Cast<LoweredStmt>()
			.ToList();
	}
}
