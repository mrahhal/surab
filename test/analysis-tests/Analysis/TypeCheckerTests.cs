using System.Text;

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
	public static void Test(string markup)
	{
		var codeAndDiags = Parse(markup);
		var (_, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(codeAndDiags.Code);
		var actualDiags = reasoner.GetAllDiags().ToList();

		AnalysisAssert.Diags(codeAndDiags.Code, codeAndDiags.ExpectedDiags, actualDiags);
	}

	private static CodeAndDiags Parse(string markup)
	{
		var codeSB = new StringBuilder();
		var expectedDiags = new List<ExpectedDiag>();

		var offset = 0;
		while (offset < markup.Length)
		{
			if (Check("[|"))
			{
				offset += 2;

				var innerMarkupStart = offset;
				var diagStart = codeSB.Length;
				while (!Check("|]"))
				{
					if (offset >= markup.Length)
					{
						throw new Exception("Could not found closing markup '|]'.");
					}

					offset++;
				}

				var slice = markup[innerMarkupStart..offset];
				offset += 2;
				codeSB.Append(slice);
				var diagEnd = codeSB.Length;
				expectedDiags.Add(new ExpectedDiag(null, new FileSpan(diagStart, diagEnd)));
			}

			if (Check("{|"))
			{
				offset += 2;

				var innerMarkupStart = offset;
				var diagStart = codeSB.Length;
				while (!Check(":"))
				{
					if (offset >= markup.Length)
					{
						throw new Exception("Could not found markup ':'.");
					}

					offset++;
				}
				var id = markup[innerMarkupStart..offset];
				offset += 1;

				innerMarkupStart = offset;
				while (!Check("|}"))
				{
					if (offset >= markup.Length)
					{
						throw new Exception("Could not found closing markup '|}'.");
					}

					offset++;
				}
				var slice = markup[innerMarkupStart..offset];
				offset += 2;
				codeSB.Append(slice);
				var diagEnd = codeSB.Length;
				expectedDiags.Add(new ExpectedDiag(id, new FileSpan(diagStart, diagEnd)));
			}

			codeSB.Append(markup[offset]);
			offset++;
		}

		return new CodeAndDiags { Code = codeSB.ToString(), ExpectedDiags = expectedDiags };

		bool Check(string part)
		{
			if (markup[offset..].StartsWith(part))
			{
				return true;
			}
			return false;
		}
	}

	private class CodeAndDiags
	{
		public required string Code { get; set; }

		public required List<ExpectedDiag> ExpectedDiags { get; set; }
	}
}
