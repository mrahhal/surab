using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Surab.Analysis;

public readonly struct ResolvedImport
{
	private ResolvedImport(
		ModuleSymbol? module,
		IEnumerable<Symbol> exposedSymbols)
	{
		Module = module;
		ExposedSymbols = exposedSymbols.ToImmutableArray();
	}

	/// <summary>
	/// The module this resolved import is tied to.
	/// </summary>
	public ModuleSymbol? Module { get; }

	/// <summary>
	/// The symbols this resolved import exposes. Can be a single module, or several exposed in file symbols.
	/// </summary>
	public ImmutableArray<Symbol> ExposedSymbols { get; }

	[MemberNotNullWhen(returnValue: false, nameof(Module))]
	public bool IsFailed => Module == null;

	internal static ResolvedImport Resolved(
		ModuleSymbol module,
		IEnumerable<Symbol> exposedSymbols)
	{
		return new ResolvedImport(module, exposedSymbols);
	}

	internal static ResolvedImport Failed()
	{
		return new ResolvedImport(null, []);
	}
}
