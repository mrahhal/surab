namespace Surab.Analysis;

public abstract class LocalSymbol : Symbol, ISymbolHasType
{
	protected LocalSymbol() : base(SymbolKind.Local)
	{
	}

	public abstract TypeSymbol Type { get; }

	public abstract bool IsTypeInferred { get; }
}

public sealed class SrcLocalSymbol : LocalSymbol
{
	private readonly VarDeclNode _node;
	private ResolvedType? _type;

	internal SrcLocalSymbol(VarDeclNode node)
	{
		_node = node;
	}

	public override Symbol OriginalDef => this;

	public override VarDeclNode DeclaringNode => _node;

	internal override IdentifierToken? IdentifierToken => _node.Identifier;

	public override TypeSymbol Type => EnsureResolveType().Type;

	public override bool IsTypeInferred => EnsureResolveType().IsInferred;

	private ResolvedType EnsureResolveType()
	{
		return _type ??= Reasoner.ResolveType(_node, _node.Type);
	}
}
