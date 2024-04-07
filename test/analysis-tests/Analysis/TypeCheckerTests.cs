using System.Text;

namespace Surab.Analysis;

public class TypeCheckerTests
{
}

public static class AnalyzerTestUtils
{
	public static void Test(string markup)
	{
		var codeAndDiags = Parse(markup);
		var (tree, reasoner) = AnalysisTestUtils.CreateTreeAndReasoner(codeAndDiags.Code);
		var actualDiags = reasoner.GetAllDiagnostics().ToList();
		var failureMessages = new List<string>();

		foreach (var expectedDiag in codeAndDiags.ExpectedDiags)
		{
			if (expectedDiag.Id != null)
			{
				throw new NotImplementedException();
			}

			var matchingDiags = actualDiags
				.Where(diag => diag.Span.File.Start == expectedDiag.Span.Start && diag.Span.File.End == expectedDiag.Span.End)
				.ToList();
			if (matchingDiags.Count == 0)
			{
				failureMessages.Add(
					$"Failed to find expected diag at:".NL().NL() + DiagnosticHelper.Visualize(codeAndDiags.Code, expectedDiag.Span, null, null, null));
			}
			foreach (var matchingDiag in matchingDiags)
			{
				actualDiags.Remove(matchingDiag);
			}
		}

		foreach (var unexpectedDiag in actualDiags)
		{
			failureMessages.Add(
				$"Found unexpected diag at:".NL().NL() + unexpectedDiag.Visualize(codeAndDiags.Code));
		}

		if (failureMessages.Count > 0)
		{
			Assert.Fail($"{failureMessages.Count} assertion failure(s) listed below:".NL().NL() + string.Join("".NL().NL(), failureMessages.Select(x => "- " + x)));
		}
	}

	private static CodeAndDiags Parse(string markup)
	{
		var codeSB = new StringBuilder();
		var expectedDiags = new List<CodeAndDiagsDiag>();

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
				expectedDiags.Add(new CodeAndDiagsDiag { Id = null, Span = new FileSpan(diagStart, diagEnd) });
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
				expectedDiags.Add(new CodeAndDiagsDiag { Id = id, Span = new FileSpan(diagStart, diagEnd) });
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

		public required List<CodeAndDiagsDiag> ExpectedDiags { get; set; }
	}

	private class CodeAndDiagsDiag
	{
		public required string? Id { get; set; }

		public required FileSpan Span { get; set; }
	}
}
