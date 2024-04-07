using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Surab.Analysis;

namespace Surab.LSPServer;

public static class ConversionHelper
{
	public static OmniSharp.Extensions.LanguageServer.Protocol.Models.Range FromTo(TextSpan span)
	{
		return FromTo(span.Line);
	}

	public static OmniSharp.Extensions.LanguageServer.Protocol.Models.Range FromTo(LineSpan span)
	{
		return new(new(span.Start.Line, span.Start.Char), new(span.End.Line, span.End.Char));
	}

	public static Position FromTo(LinePosition position)
	{
		return new(position.Line, position.Char);
	}

	public static LinePosition FromTo(Position position)
	{
		return new(position.Line, position.Character);
	}

	public static DiagnosticSeverity? FromTo(DiagLevel level)
	{
		return level switch
		{
			DiagLevel.Error => DiagnosticSeverity.Error,
			DiagLevel.Warning => DiagnosticSeverity.Warning,
			DiagLevel.Suggestion => DiagnosticSeverity.Hint,
			_ => null,
		};
	}
}
