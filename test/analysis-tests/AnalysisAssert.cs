namespace Surab.Analysis;

internal static class AnalysisAssert
{
	public static void Diags(
		string code,
		IEnumerable<ExpectedDiag> expectedDiags,
		IEnumerable<Diag> actualDiags)
	{
		var remainingActualDiags = actualDiags.ToList();
		var failureMessages = new List<string>();

		foreach (var expectedDiag in expectedDiags)
		{
			var matchingDiags = remainingActualDiags
				.Where(diag => diag.Span.File.Start == expectedDiag.FileSpan.Start && diag.Span.File.End == expectedDiag.FileSpan.End)
				.Where(diag => diag.Id == expectedDiag.Id)
				.ToList();
			if (matchingDiags.Count == 0)
			{
				failureMessages.Add(
					$"Failed to find expected diag at:".NL().NL() + expectedDiag.Visualize(code));
			}
			foreach (var matchingDiag in matchingDiags)
			{
				remainingActualDiags.Remove(matchingDiag);
			}
		}

		foreach (var unexpectedDiag in remainingActualDiags)
		{
			failureMessages.Add(
				$"Found unexpected diag at:".NL().NL() + unexpectedDiag.Visualize(code));
		}

		if (failureMessages.Count > 0)
		{
			Assert.Fail($"{failureMessages.Count} diag assertion failure(s) listed below:".NL().NL() + string.Join("".NL().NL(), failureMessages.Select(x => "- " + x)));
		}
	}
}
