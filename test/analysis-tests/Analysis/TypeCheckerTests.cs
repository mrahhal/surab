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

	private void Test(string markup) => AnalyzerTestUtils.Test(markup);
}

public static class AnalyzerTestUtils
{
	public static void Test(string annotatedCode)
	{
		var result = AnnotatedCodeUtil.Parse(annotatedCode, treatAnnotatedContentAsCode: true);
		var (_, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(result.Code);
		var actualDiags = reasoner.GetAllDiags().ToList();
		var expectedDiags = result.Annotations.Select(a => new ExpectedDiag(a.Hint, a.FileSpan));

		AnalysisAssert.Diags(result.Code, expectedDiags, actualDiags);
	}
}
