namespace Surab.Analysis;

internal static class TextSpanHelper
{
	public static (TextSpan, TextSpan) ComputeSpanningSpan(IEnumerable<SyntaxNode?> nodes, IEnumerable<SyntaxToken?> tokens)
	{
		TextSpan? lo = null;
		TextSpan? hi = null;
		TextSpan? lo_with_trivia = null;
		TextSpan? hi_with_trivia = null;

		foreach (var x in nodes)
		{
			if (x != null)
			{
				AdjustLoHiToInclude(x.Span, ref lo, ref hi);
				AdjustLoHiToInclude(x.SpanWithTrivia, ref lo_with_trivia, ref hi_with_trivia);
			}
		}
		foreach (var x in tokens)
		{
			if (x != null)
			{
				AdjustLoHiToInclude(x.Span, ref lo, ref hi);
				AdjustLoHiToInclude(x.SpanWithTrivia, ref lo_with_trivia, ref hi_with_trivia);
			}
		}

		var span = lo.HasValue ?
			TextSpan.FromTo(lo.Value, hi!.Value) :
			new TextSpan();
		var spanWithTrivia = lo_with_trivia.HasValue ?
			TextSpan.FromTo(lo_with_trivia.Value, hi_with_trivia!.Value) :
			new TextSpan();

		return (span, spanWithTrivia);
	}

	public static (TextSpan, TextSpan) ComputeSpanningSpan(SyntaxToken token, TextSpan span)
	{
		TextSpan? lo = null;
		TextSpan? hi = null;

		AdjustLoHiToInclude(span, ref lo, ref hi);
		foreach (var trivia in token.LeadingTrivia)
		{
			AdjustLoHiToInclude(trivia.Span, ref lo, ref hi);
		}
		foreach (var trivia in token.TrailingTrivia)
		{
			AdjustLoHiToInclude(trivia.Span, ref lo, ref hi);
		}

		return (span, TextSpan.FromTo(lo!.Value, hi!.Value));
	}

	public static void AdjustLoHiToInclude(
		TextSpan current,
		ref TextSpan? lo,
		ref TextSpan? hi)
	{
		if (!lo.HasValue)
		{
			lo = hi = current;
			return;
		}

		if (lo.Value.File.Start > current.File.Start)
		{
			lo = current;
		}
		if (hi!.Value.File.End < current.File.End)
		{
			hi = current;
		}
	}
}
