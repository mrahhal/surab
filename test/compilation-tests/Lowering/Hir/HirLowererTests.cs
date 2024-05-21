using System.Collections.Immutable;
using System.Diagnostics;
using Surab.Analysis;

namespace Surab.Compilation;

public class HirLowererTests
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
		body[i++].Should().BeOfType<HirLocalDeclareStmt>();
		body[i++].Should().BeOfType<HirLocalDeclareStmt>();
		body[i++].Should().BeOfType<HirValueStoreStmt>();
		body[i++].Should().BeOfType<HirExprStmt>().Subject.Expr.Should().BeOfType<HirCallExpr>();
		body[i++].Should().BeOfType<HirValueStoreStmt>();
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
		var decls = LowerFile(src).Decls;
		decls.Should().HaveCount(5);
		var i = 0;
		var names = new[]
		{
			decls[i++].Should().BeOfType<HirFnDecl>().Subject.FnValue.Name,
			decls[i++].Should().BeOfType<HirFnDecl>().Subject.FnValue.Name,
			decls[i++].Should().BeOfType<HirFnDecl>().Subject.FnValue.Name,
			decls[i++].Should().BeOfType<HirStructDecl>().Subject.Type.Name,
			decls[i++].Should().BeOfType<HirStructDecl>().Subject.Type.Name,
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
	public void DeclHoisting_FnHoistingDoesNotMessWithVars()
	{
		// Used to happen because of a bug. f2 would have the var declare instead of f.
		var src = """
			fn f() {
				var a = 2.0;
				fn f2() { }
			}
			""";
		var file = LowerFile(src);
		var decls = file.Decls;
		decls.Should().HaveCount(2);
		var fStmts = file.GetFnBody("f");
		var f2Stmts = file.GetFnBody("f2");
		f2Stmts.Should().BeEmpty();
		fStmts[0].Should().BeOfType<HirLocalDeclareStmt>();
		fStmts[1].Should().BeOfType<HirValueStoreStmt>();
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
		body[0].Should().BeOfType<HirReturnStmt>().Subject
			.Expr.Should().BeOfType<HirLiteralExpr>();
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
		body.Should().HaveCount(1);
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
				var s = S.{ a = 42 };
			}
			""";
		var file = LowerFile(src);
		var body = file.GetFnBody("f");
		body.Should().HaveCount(3);
		body[0].Should().BeOfType<HirLocalDeclareStmt>();
		body[1].Should().BeOfType<HirLocalDeclareStmt>(); // alloc
		var storeStmt = body[2].Should().BeOfType<HirValueStoreStmt>().Subject;
		var storeExprNodes = storeStmt.Expr.EnumerateChildren().ToList();

		storeExprNodes[0].Should().BeOfType<HirFieldStoreStmt>();
		storeExprNodes[1].Should().BeOfType<HirExprStmt>().Subject
			.Expr.Should().BeOfType<HirCallExpr>();
		storeExprNodes[2].Should().BeOfType<HirValueLoadExpr>();
	}

	[Fact]
	public void StructFieldLoad()
	{
		var src = """
			struct S { a: i32; }
			fn f() {
				var s = S.{ a = 42 };
				var a = s.a;
			}
			""";
		var file = LowerFile(src);
		var body = file.GetFnBody("f");
		body.Should().HaveCount(5);
		var variableStore = body.OfType<HirValueStoreStmt>().Last();
		var fieldStore = variableStore.Expr.Should().BeOfType<HirFieldLoadExpr>().Subject;
		fieldStore.FieldValue.Name.Should().Be("a");
	}

	[Fact]
	public void StructFieldStore()
	{
		var src = """
			struct S { a: i32; }
			fn f() {
				var s = S.{ a = 42 };
				s.a = 43;
			}
			""";
		var file = LowerFile(src);
		var body = file.GetFnBody("f");
		body.Should().HaveCount(4);
		var storeExpr = body[3].Should().BeOfType<HirExprStmt>().Subject
			.Expr.Should().BeOfType<HirBlockExpr>().Subject;
		storeExpr.Stmts.Should().HaveCount(1);
		storeExpr.Stmts[0].Should().BeOfType<HirFieldStoreStmt>();
		var fieldLoadExpr = storeExpr.BlockReturnExpr.Should().BeOfType<HirFieldLoadExpr>().Subject;
		fieldLoadExpr.FieldValue.Name.Should().Be("a");
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
		body[2].Should().BeOfType<HirExprStmt>().Subject
			.Expr.Should().BeOfType<HirValueLoadExpr>(); // Deref(Address) disappears
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
				var s = S.{ a = 42 };
				var s_ptr = &s;
				var s_ptr_ptr = &s_ptr;
				print_i32(s_ptr.a);
				print_i32(s_ptr_ptr.a);
				s_ptr.mutate();
			}
			""";
		var file = LowerFile(src);
		var body = file.GetFnBody("f");
		var callExpr1 = body.OfType<HirExprStmt>().ElementAt(0).Expr.Should().BeOfType<HirCallExpr>().Subject;
		{
			var fieldLoadExpr = callExpr1.Args[0].Should().BeOfType<HirFieldLoadExpr>().Subject;
			var deref1 = fieldLoadExpr.Target.Should().BeOfType<HirUnaryExpr>().Subject;
			deref1.Op.Should().Be(UnaryOperatorTokenKind.Deref);
			deref1.Type.Should().BeAssignableTo<HirStructType>();
			deref1.Expr.Should().BeOfType<HirValueLoadExpr>();
		}
		var callExpr2 = body.OfType<HirExprStmt>().ElementAt(1).Expr.Should().BeOfType<HirCallExpr>().Subject;
		{
			var fieldLoadExpr = callExpr2.Args[0].Should().BeOfType<HirFieldLoadExpr>().Subject;
			var deref1 = fieldLoadExpr.Target.Should().BeOfType<HirUnaryExpr>().Subject;
			deref1.Op.Should().Be(UnaryOperatorTokenKind.Deref);
			deref1.Type.Should().BeAssignableTo<HirStructType>();
			var deref2 = deref1.Expr.Should().BeOfType<HirUnaryExpr>().Subject;
			deref2.Op.Should().Be(UnaryOperatorTokenKind.Deref);
			deref2.Type.Should().BeOfType<HirPtrType>();
			deref2.Expr.Should().BeOfType<HirValueLoadExpr>();
		}
		var callExpr3 = body.OfType<HirExprStmt>().ElementAt(2).Expr.Should().BeOfType<HirCallExpr>().Subject;
		{
			callExpr3.Args[0].Should().BeOfType<HirValueLoadExpr>();
		}
	}

	[Fact]
	public void AutoDeref_Unneeded()
	{
		var src = """
			struct S { a: i32; }
			fn f() i32 {
				var s = S.{ a = 42 };
				print_i32(s.a);
			}
			""";
		var file = LowerFile(src);
		var body = file.GetFnBody("f");
		var callExpr = body.OfType<HirExprStmt>().Single().Expr.Should().BeOfType<HirCallExpr>().Subject;
		var fieldLoadExpr = callExpr.Args[0].Should().BeOfType<HirFieldLoadExpr>().Subject;
		fieldLoadExpr.Target.Should().BeOfType<HirValueLoadExpr>();
	}

	[Fact]
	public void GenericTypes()
	{
		var src = """
			struct S<T1> { a: T1; struct M<T2> { b: T2; do<T3>() { } } }
			fn f() {
				var m = S<i32>.M<f64>.{ b = 4.2 };
				m.do<i64>();
			}
			""";
		var file = LowerFile(src);
		var declM = (HirStructDecl)file.DescendantNodes().Single(d => d is HirStructDecl loweredType && loweredType.Type.Identifier == "M");
		var declDo = (HirFnDecl)file.DescendantNodes().Single(d => d is HirFnDecl loweredFn && loweredFn.FnValue.Identifier == "do");

		// Temp stuff. Later will test these explicit generics in lowered values/types.
		Debug.Assert(
			declDo.FnValue.TryGetGenericParams(out var declDoAllGenericParams));
		declDoAllGenericParams.Should().HaveCount(3);
		declDoAllGenericParams[0].Identifier.Should().Be("T1");
		declDoAllGenericParams[1].Identifier.Should().Be("T2");
		declDoAllGenericParams[2].Identifier.Should().Be("T3");

		var body = file.GetFnBody("f");
		var allocStmt = (HirLocalDeclareStmt)body[1];
		Debug.Assert(
			allocStmt.Value.Type.TryGetGenericArgs(out var allocStmtAllGenericArgs));
		allocStmtAllGenericArgs.Should().HaveCount(2);
		allocStmtAllGenericArgs[1].KnownTypeTag.Should().Be(KnownTypeTag.f64);
		allocStmtAllGenericArgs[0].KnownTypeTag.Should().Be(KnownTypeTag.i32);

		var callExpr = body.SelectMany(x => x.DescendantNodes()).OfType<HirCallExpr>().Single();
		Debug.Assert(
			((HirValueLoadExpr)callExpr.Callee).Value.TryGetGenericArgs(out var callExprAllGenericArgs));
		callExprAllGenericArgs.Should().HaveCount(3);
		callExprAllGenericArgs[0].KnownTypeTag.Should().Be(KnownTypeTag.i32);
		callExprAllGenericArgs[1].KnownTypeTag.Should().Be(KnownTypeTag.f64);
		callExprAllGenericArgs[2].KnownTypeTag.Should().Be(KnownTypeTag.i64);
	}

	private static HirFnDecl LowerFn(string src)
	{
		var file = LowerFile(src);
		return file.Decls.ElementAt(0).Should().BeOfType<HirFnDecl>().Subject;
	}

	private static HirFile LowerFile(string src)
	{
		var tree = Lower(src);
		return tree.Root.Should().BeOfType<HirFile>().Subject;
	}

	private static HirTree Lower(string src)
	{
		var (project, tree, _) = AnalysisTestUtils.CreateProjectAndTreeAndReasoner(src, typeCheckAndAssertNoErrors: true);
		var interner = new LoweringInterner();
		var loweredProject = HirLowerer.LowerProject(project, interner);
		return loweredProject.Trees.Single(t => t.SyntaxTree == tree);
	}
}

internal static class LowererTestsExtensions
{
	public static ImmutableArray<HirStmt> GetFnBody(this HirNode node, string? identifier = null)
	{
		HirFnDecl fnDecl;

		if (node is HirFnDecl selfFnDecl)
		{
			if (identifier != null)
			{
				throw new Exception("Function name was specified but the given node is itself a function.");
			}
			fnDecl = selfFnDecl;
		}
		else
		{
			var fns = node.DescendantNodes().OfType<HirFnDecl>().ToList();
			if (fns.Count > 1 && identifier == null)
				throw new Exception("Specifying a name is required when there are more than one function.");

			if (identifier == null)
			{
				fnDecl = fns.Single();
			}
			else
			{
				fnDecl = fns.Single(x => x.FnValue.Identifier == identifier);
			}
		}

		return fnDecl.Body!.Stmts;
	}
}
