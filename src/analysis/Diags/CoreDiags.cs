namespace Surab.Analysis;

/// <summary>
/// Contains core diagnostics reported when parsing and analyzing code.
/// </summary>
public static class CoreDiags
{
	public static readonly DiagDef S0001 = new(
		nameof(S0001),
		DiagLevel.Error,
		"Skipped unparsed tokens.");
	public static readonly DiagDef S0002 = new(
		nameof(S0002),
		DiagLevel.Error,
		"Expected '{0}'.");
	public static readonly DiagDef S0003 = new(
		nameof(S0003),
		DiagLevel.Error,
		"Expected a type.");
	public static readonly DiagDef S0004 = new(
		nameof(S0004),
		DiagLevel.Error,
		"Expected an expression.");
	public static readonly DiagDef S0005 = new(
		nameof(S0005),
		DiagLevel.Error,
		"Expected an identifier.");
	public static readonly DiagDef S0006 = new(
		nameof(S0006),
		DiagLevel.Error,
		"'{0}' is not allowed as a type name.");

	public static readonly DiagDef S0100 = new(
		nameof(S0100),
		DiagLevel.Error,
		"Invalid syntax for import.");
	public static readonly DiagDef S0101 = new(
		nameof(S0101),
		DiagLevel.Error,
		"Failed to resolve module to import.");
}
