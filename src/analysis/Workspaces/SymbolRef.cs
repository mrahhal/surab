namespace Surab.Analysis;

public enum SymbolRefKind
{
	Text,
	Decl,
	Read,
	Write,
}

public record class SymbolRef(ProjectFile File, TextSpan Span, SymbolRefKind Kind);
