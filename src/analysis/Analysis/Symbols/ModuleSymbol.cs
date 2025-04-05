using System.Collections.Immutable;
using System.Diagnostics;

namespace Surab.Analysis;

/// <summary>
/// A symbol that represents a Surab module.
/// e.g. main, std.io, etc
/// </summary>
public abstract class ModuleSymbol : Symbol
{
	private readonly List<ModuleSymbol> _childModules = [];

	protected ModuleSymbol() : base(SymbolKind.Module)
	{
	}

	/// <summary>
	/// The parent <see cref="ModuleSymbol"/> if this is a nested module.
	/// </summary>
	public new ModuleSymbol? Parent => (ModuleSymbol?)base.Parent;

	internal override void SetParent(Symbol? parent)
	{
		Debug.Assert(parent is ModuleSymbol);
		var moduleParent = (ModuleSymbol)parent;
		moduleParent._childModules.Add(this);
		base.SetParent(parent);
	}

	/// <summary>
	/// The child <see cref="ModuleSymbol"/>s under this symbol.
	/// </summary>
	public override IEnumerable<ModuleSymbol> Children => _childModules;

	internal override IdentifierToken? IdentifierToken => null;

	public override string Name => Identifier;

	public override SyntaxNode? DeclaringNode => null;

	public override Symbol OriginalDef => this;

	public abstract SurabProject Project { get; }

	/// <summary>
	/// The project folders that make up this module.
	/// </summary>
	public abstract ImmutableArray<ProjectFolder> Folders { get; }

	internal IEnumerable<ProjectFile> CodeFiles => Folders.SelectMany(folder => folder.Files).Where(f => f.Kind is ProjectFileKind.Code);

	/// <summary>
	/// The exported symbols from this module. Anything other than private will be here.
	/// </summary>
	public abstract ImmutableArray<Symbol> ExportedSymbols { get; }

	/// <summary>
	/// Returns an exported symbol that matches the given identifier.
	/// </summary>
	public Symbol? GetExportedSymbol(IdentifierToken identifier) => GetExportedSymbol(identifier.Value);

	/// <summary>
	/// Returns an exported symbol that matches the given identifier.
	/// </summary>
	public Symbol? GetExportedSymbol(string identifier)
	{
		// TODO (cache): Might be a hot path, and this might be a big array.
		return ExportedSymbols.FirstOrDefault(s => s.IdentifierMatches(identifier));
	}
}

public sealed class SrcModuleSymbol : ModuleSymbol
{
	private readonly string _identifier;
	private ImmutableArray<Symbol> _exportedSymbols;

	internal SrcModuleSymbol(
		string name,
		SurabProject project,
		IEnumerable<ProjectFolder> folders)
	{
		_identifier = name;
		Project = project;
		Folders = [.. folders];
	}

	public override string Identifier => _identifier;

	public override SurabProject Project { get; }

	public override ImmutableArray<ProjectFolder> Folders { get; }

	public override ImmutableArray<Symbol> ExportedSymbols
	{
		get
		{
			if (_exportedSymbols.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _exportedSymbols, BuildExportedSymbols(), default);
			}
			return _exportedSymbols;
		}
	}

	private ImmutableArray<Symbol> BuildExportedSymbols()
	{
		var builder = ImmutableArray.CreateBuilder<Symbol>();
		foreach (var file in CodeFiles)
		{
			var reasoner = Project.GetReasoner(file);
			foreach (var symbol in reasoner.GetExportedSymbols())
			{
				builder.Add(symbol);
			}
		}
		return builder.ToImmutable();
	}
}

public sealed class AliasedModuleSymbol : ModuleSymbol
{
	private readonly string _alias;

	internal AliasedModuleSymbol(ModuleSymbol original, string alias)
	{
		_alias = alias;
		Original = original;
	}

	public ModuleSymbol Original { get; }

	public override string Identifier => _alias;

	public override SurabProject Project => Original.Project;

	public override ImmutableArray<ProjectFolder> Folders => Original.Folders;

	public override ImmutableArray<Symbol> ExportedSymbols => Original.ExportedSymbols;
}
