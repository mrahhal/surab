namespace Surab.Analysis;

public class TypeCheckerTests
{
	[Fact]
	public void Import_InvalidSyntax()
	{
		Test("""
			import {|S0100:2|};
			""");

		Test("""
			import {|S0100:"asd"|};
			""");
	}

	[Fact]
	public void Import_FailedToResolve()
	{
		Test("""
			import {|S0101:a|};
			""");

		Test("""
			import {|S0101:a.b|};
			""");
	}

	[Fact]
	public void UnknownName()
	{
		Test("""
			fn f() {
				print_i32([|a|]);
				var a = 2;
			}
			""");
	}

	[Fact]
	public void Generic()
	{
		Test("""
			struct S<T> { v: T; }
			struct C<T1, T2> {
				v1: T1;
				v2: T2;
			}
			struct E<T> { c: C<T, i32>; }

			fn f() {
				var s = S<i32>.{ v: 42 };
				var s = S<bool>.{ v: false };
				var c = C<f64, i32>.{ v1: 4.2, v2: 42 };
				var e = E<f64>.{ c: C<f64, i32>.{ v1: 4.2, v2: 42 } };
				var e = E<bool>.{ c: C<bool, i32>.{ v1: false, v2: 42 } };
			}
			""");
	}

	[Fact]
	public void GenericClosing_NonGeneric()
	{
		Test("""
			struct S { }
			fn f() {
				var s1 = [|S<i32>|].{ };
				var s2: [|S<i32>|];
			}
			""");
	}

	[Fact]
	public void GenericClosing_WrongNumOfParams()
	{
		Test("""
			struct S<T1> { }
			fn f() {
				var s1 = [|S<i32, i32>|].{ };
				var s2: [|S<i32, i32>|];
			}
			""");
	}

	[Fact]
	public void FunctionBody()
	{
		Test("""
			fn [|f1|]();

			@extern
			fn f2() { }

			@extern
			fn f3();

			@intrinsic("f4")
			fn f4();
			""");
	}

	[Fact]
	public void ThisIsNotValidHere()
	{
		Test("""
			struct S {
				a: i32;

				m() i32 {
					this.a
				}

				fn f() i32 {
					[|this|].a
				}
			}
			""");
	}

	private void Test(string markup) => AnalyzerTestUtils.Test(markup);
}

public static class AnalyzerTestUtils
{
	public static void Test(string annotatedCode)
	{
		var result = AnnotatedCodeUtil.Parse(annotatedCode, treatAnnotatedContentAsCode: true);
		var (_, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(result.Code);
		var actualDiags = reasoner.GetAllDiags().ToList();
		var expectedDiags = result.Annotations.Select(a => new ExpectedDiag(a.Name, a.FileSpan));

		AnalysisAssert.Diags(result.Code, expectedDiags, actualDiags);
	}
}
