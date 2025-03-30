namespace Surab.Analysis;

public abstract class ConstSymbol : Symbol, ISymbolHasType
{
	protected ConstSymbol() : base(SymbolKind.Const)
	{
	}

	public abstract TypeSymbol Type { get; }

	public bool IsTypeInferred => false;
}

public sealed class SrcConstSymbol : ConstSymbol
{
	private readonly ConstDeclNode _node;
	private ResolvedType? _type;

	internal SrcConstSymbol(ConstDeclNode node)
	{
		_node = node;
	}

	public override Symbol OriginalDef => this;

	public override ConstDeclNode DeclaringNode => _node;

	internal override IdentifierToken IdentifierToken => _node.Identifier;

	public override TypeSymbol Type => EnsureResolveType().Type;

	private ResolvedType EnsureResolveType()
	{
		return _type ??= Reasoner.ResolveType(_node, _node.Type);
	}
}
