using System.Diagnostics;
using Surab.Analysis;
using static Surab.Compilation.MonomorphizationCollection;

namespace Surab.Compilation;

public class MonomorphizationTests
{
	[Fact]
	public void Collect_Basic()
	{
		var collection = Collect("""
			fn f<T1, T2>() { }
			struct A { v: i32; }
			struct B<T> { v: T; }
			struct B2<T> { v: T; }
			struct B3<T> { v: T; }
			struct C<T1, T2> {
				v1: T1;
				v2: T2;

				c_f1() { }
				c_f2<T3>() { }
				fn c_f_static1<T4>() { }
			}
			struct D<T> { v: T; }
			struct E<T> { c: C<T, i32>; }
			struct E2<T> { b: B2<B3<T>>; }
			struct F { c: C<i32, i32>; }
			fn f2() { fn f3() { var c = C<i32, f64>.{ v1: 42, v2: 4.2 }; } }
			fn f4<T1, T2>() { var b: B<T1>; f<T1, T1>(); }

			fn main() {
				f<i32, i32>();
				f<i32, f64>();
				var a = A.{ v: 42 };
				var b = B<i32>.{ v: 42 };
				var b = B<i64>.{ v: 42i64 };
				var c = C<i32, i64>.{ v1: 42, v2: 42i64 };
				var c = C<i32, f64>.{ v1: 42, v2: 4.2 };
				var c = C<i32, f64>.{ v1: 42, v2: 4.2 }; // duplicate
				var c = C<f64, i32>.{ v1: 4.2, v2: 42 };
				c.c_f1();
				c.c_f2<i32>();
				c.c_f2<f64>();
				C<i32, f64>.c_f_static1<i32>();
				var e = E<f64>.{ c: C<f64, i32>.{ v1: 4.2, v2: 42 } };
				var e = E<bool>.{ c: C<bool, i32>.{ v1: false, v2: 42 } };
				var e = E2<f32>.{ b: B2<B3<f32>>.{ v: B3<f32>.{ v: 4.2f32 } } };
				f4<i32, f64>();
			}
			""");

		AssertUsageEntry(collection.GetUsageOrNull("B"), (["T"], []));
		AssertUsageEntry(collection.GetUsageOrNull("E"), (["T"], ["C<T, i32>"]));
		AssertUsageEntry(collection.GetUsageOrNull("E2"), (["T"], ["B2<B3<T>>"]));
		AssertUsageEntry(collection.GetUsageOrNull("F"));
		AssertUsageEntry(collection.GetUsageOrNull("f2"));
		AssertUsageEntry(collection.GetUsageOrNull("f3"));
		AssertUsageEntry(collection.GetUsageOrNull("f4"), (["T1"], ["B<T1>", "f<T1, T1>"]));

		// ---

		AssertEntry(collection.GetConcretesOrNull("f"),
			[
				[KnownTypeTag.i32, KnownTypeTag.i32],
				[KnownTypeTag.i32, KnownTypeTag.f64],
			]);
		AssertEntry(collection.GetConcretesOrNull("f4"),
			[
				[KnownTypeTag.i32, KnownTypeTag.f64],
			]);

		AssertEntry(collection.GetConcretesOrNull("A")); // not generic

		AssertEntry(collection.GetConcretesOrNull("B"),
			[
				[KnownTypeTag.i32],
				[KnownTypeTag.i64],
			]);
		var entryB2 = collection.GetConcretesOrNull("B2");
		entryB2.Should().NotBeNull();
		entryB2!.Should().HaveCount(1);
		entryB2![0].Types[0].Name.Should().Be("B3<f32>");
		AssertEntry(collection.GetConcretesOrNull("B3"),
			[
				[KnownTypeTag.f32],
			]);

		AssertEntry(collection.GetConcretesOrNull("C"),
			[
				[KnownTypeTag.i32, KnownTypeTag.i64],
				[KnownTypeTag.i32, KnownTypeTag.f64],
				[KnownTypeTag.f64, KnownTypeTag.i32],
				[KnownTypeTag.@bool, KnownTypeTag.i32],
			]);

		AssertEntry(collection.GetConcretesOrNull("c_f1"),
			[
				[KnownTypeTag.f64, KnownTypeTag.i32],
			]);

		AssertEntry(collection.GetConcretesOrNull("c_f2"),
			[
				[KnownTypeTag.f64, KnownTypeTag.i32, KnownTypeTag.i32],
				[KnownTypeTag.f64, KnownTypeTag.i32, KnownTypeTag.f64],
			]);

		AssertEntry(collection.GetConcretesOrNull("c_f_static1"),
			[
				[KnownTypeTag.i32, KnownTypeTag.f64, KnownTypeTag.i32],
			]);

		AssertEntry(collection.GetConcretesOrNull("D")); // unused

		AssertEntry(collection.GetConcretesOrNull("E"),
			[
				[KnownTypeTag.f64],
				[KnownTypeTag.@bool],
			]);
		AssertEntry(collection.GetConcretesOrNull("E2"),
			[
				[KnownTypeTag.f32],
			]);
	}

	[Fact]
	public void Collect_Aliases()
	{
		var collection = Collect("""
			struct C<T1, T2> {
				v1: T1;
				v2: T2;
			}
			alias C<i32, i32> as CAlias1;
			alias C<T, f64> as CAlias2<T>;
			alias C<T1, T2> as CAlias3<T1, T2>;

			fn main() {
				var c = CAlias1.{ v1: 42, v2: 42 };
				var c = CAlias2<f64>.{ v1: 4.2, v2: 4.2 };
				var c = CAlias3<f64, i32>.{ v1: 4.2, v2: 42 };
				var c = C<f64, i32>.{ v1: 4.2, v2: 42 };
			}
			""");

		AssertEntry(collection.GetConcretesOrNull("C"),
			[
				[KnownTypeTag.i32, KnownTypeTag.i32],
				[KnownTypeTag.f64, KnownTypeTag.f64],
				[KnownTypeTag.f64, KnownTypeTag.i32],
			]);
	}

	[Fact]
	public void Collect_FnPtrs()
	{
		var collection = Collect("""
			fn f<T1, T2>() { }

			fn main() {
				var f1 = f<i32, i32>;
				var f2 = f<i32, f64>;
				f<i32, i32>();
				f<i32, i64>();
			}
			""");

		AssertEntry(collection.GetConcretesOrNull("f"),
			[
				[KnownTypeTag.i32, KnownTypeTag.i32],
				[KnownTypeTag.i32, KnownTypeTag.f64],
				[KnownTypeTag.i32, KnownTypeTag.i64],
			]);
	}

	[Fact(Skip = "Not yet")]
	public void Collect_Cyclic()
	{
		var collection = Collect("""
			fn f<T1, T2>() { }
			fn f2<T1, T2>() { }

			struct S1<T> { v: S2<T>; }
			struct S2<T> { v: S1<T>; }

			fn main() {
				var s1: S1<i32>;
			}
			""");

		AssertEntry(collection.GetConcretesOrNull("S1"),
			[
				[KnownTypeTag.i32],
			]);
		AssertEntry(collection.GetConcretesOrNull("S2"),
			[
				[KnownTypeTag.i32],
			]);
	}

	[Fact]
	public void Monomorph_Basic()
	{
		var tree = Monomorph("""
			fn f<T>(v: T) T { v }

			fn main() {
				var v = f<i32>(42);
				print_i32(v);
				var v = f<bool>(true);
				print_bool(v);
			}
			""");

		var fnDecls = tree.Root.DescendantNodes().OfType<HirFnDecl>().ToList();
		fnDecls.Where(x => x.FnValue.Identifier == "f").Should().HaveCount(2);
		var main_fn = fnDecls.Single(x => x.FnValue.Identifier == "main");
		var main_fn_body = main_fn.Body!.Stmts.ToList();
		var i = 0;
		main_fn_body[i++].Should().BeOfType<HirLocalDeclareStmt>();
		main_fn_body[i++].Should().BeOfType<HirLocalDeclareStmt>();
		main_fn_body[i++].Should().BeOfType<HirValueStoreStmt>().Subject
			.Expr.Should().BeOfType<HirCallExpr>().Subject
			.Callee.Should().BeOfType<HirValueLoadExpr>().Subject.Value.Should().BeOfType<HirClosedGenericFnValue>().Subject.GetIsFullyConcrete().Should().BeTrue();
		main_fn_body[i++].Should().BeOfType<HirExprStmt>().Subject.Expr.Should().BeOfType<HirCallExpr>();
		main_fn_body[i++].Should().BeOfType<HirValueStoreStmt>().Subject
			.Expr.Should().BeOfType<HirCallExpr>().Subject
			.Callee.Should().BeOfType<HirValueLoadExpr>().Subject.Value.Should().BeOfType<HirClosedGenericFnValue>().Subject.GetIsFullyConcrete().Should().BeTrue();
		main_fn_body[i++].Should().BeOfType<HirExprStmt>().Subject.Expr.Should().BeOfType<HirCallExpr>();
	}

	[Fact]
	public void Monomorph_Nested()
	{
		var tree = Monomorph("""
			struct S<T> { v: T; }
			struct N<T> { v: T; }
			struct M<T> { v: N<T>; }

			fn main() {
				var s = S<i32>.{ v: 42 };
				print_i32(s.v);
				var s = S<N<i32>>.{ v: N<i32>.{ v: 42 } };
				print_i32(s.v.v);
				var m = M<f64>.{ v: N<f64>.{ v: 4.2 } };
			}
			""");

		var structDecls = tree.Root.DescendantNodes().OfType<HirStructDecl>().ToList();
		structDecls.Where(x => x.Type.Identifier == "S").Should().HaveCount(2);
		structDecls.Where(x => x.Type.Identifier == "N").Should().HaveCount(2);
		structDecls.Where(x => x.Type.Identifier == "M").Should().HaveCount(1);
	}

	private static void AssertEntry(
		IReadOnlyList<Concrete>? actual,
		List<KnownTypeTag[]>? expected = null)
	{
		if (expected == null)
		{
			actual.Should().BeNull();
			return;
		}
		actual.Should().NotBeNull();
		Debug.Assert(actual != null);
		actual.Should().HaveCount(expected.Count);
		foreach (var perm in expected)
		{
			actual.Where(actualPerm =>
			{
				if (actualPerm.Types.Count != perm.Length)
					return false;
				for (var i = 0; i < actualPerm.Types.Count; i++)
				{
					if (actualPerm.Types[i].KnownTypeTag != perm[i])
						return false;
				}
				return true;
			}).Should().ContainSingle($"Could not find [{string.Join(", ", perm)}]");
		}
	}

	private static void AssertUsageEntry(
		Usage? actual,
		(List<string> expectedGenericParamTypesUsed, List<string> expectedDependents)? expected = null)
	{
		if (expected == null)
		{
			actual.Should().BeNull();
			return;
		}
		actual.Should().NotBeNull();
		Debug.Assert(actual != null);
		var (expectedGenericParamTypesUsed, expectedDependents) = expected.Value;
		actual.GenericParamTypesUsed.Should().HaveCount(expectedGenericParamTypesUsed.Count);
		actual.Dependents.Should().HaveCount(expectedDependents.Count);
		foreach (var used in expectedGenericParamTypesUsed)
		{
			actual.GenericParamTypesUsed.Where(actualUsed => actualUsed.Identifier == used)
				.Should().ContainSingle($"Could not find used generic type param: {string.Join(", ", used)}");
		}
		foreach (var dependent in expectedDependents)
		{
			actual.Dependents.Where(actualDependent => actualDependent.Name == dependent)
				.Should().ContainSingle($"Could not find dependent: {string.Join(", ", dependent)}");
		}
	}

	private static MonomorphizationCollection Collect(string src)
	{
		var (project, _, _) = AnalysisTestUtils.CreateProjectAndTreeAndReasoner(src, typeCheckAndAssertNoErrors: true);
		var interner = new LoweringInterner();
		var loweredProject = HirLowerer.LowerProject(project, interner);
		return Monomorphization.Collect(loweredProject, interner);
	}

	private static HirTree Monomorph(string src)
	{
		var (project, tree, _) = AnalysisTestUtils.CreateProjectAndTreeAndReasoner(src, typeCheckAndAssertNoErrors: true);
		var interner = new LoweringInterner();
		var loweredProject = HirLowerer.LowerProject(project, interner);
		var trees = Monomorphization.Monomorph(loweredProject, interner);
		return trees.First(x => x.SyntaxTree == tree);
	}
}
