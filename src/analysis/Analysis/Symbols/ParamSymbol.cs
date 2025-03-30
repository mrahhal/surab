namespace Surab.Analysis;

public abstract class ParamSymbol : Symbol, ISymbolHasType
{
	protected ParamSymbol() : base(SymbolKind.Param)
	{
	}

	public FnSymbol Fn => (FnSymbol)Parent!;

	public abstract ExprNode? InitializerExprNode { get; }

	public bool HasInitializer => InitializerExprNode != null;

	public abstract TypeSymbol Type { get; }

	// Param type is always typed.
	public bool IsTypeInferred => false;

	internal override ParamSymbol SubstituteCore(SubstitutionMap smap)
	{
		return new SubstitutedParamSymbol(this, smap);
	}
}

public sealed class SrcParamSymbol : ParamSymbol
{
	private readonly ParamNode _node;
	private TypeSymbol? _type;

	internal SrcParamSymbol(ParamNode node)
	{
		_node = node;
	}

	public override Symbol OriginalDef => this;

	public override ParamNode DeclaringNode => _node;

	internal override IdentifierToken? IdentifierToken => _node.Identifier;

	public override ExprNode? InitializerExprNode => _node.Initializer?.Expr;

	public override TypeSymbol Type
	{
		get
		{
			return _type ??= Reasoner.ResolveType(_node, _node.Type).Type; // Will never be inferred.
		}
	}
}

internal sealed class SubstitutedParamSymbol : ParamSymbol
{
	private readonly ParamSymbol _original;
	private readonly SubstitutionMap _smap;

	private TypeSymbol? _type;

	public SubstitutedParamSymbol(
		ParamSymbol original,
		SubstitutionMap smap)
	{
		_original = original;
		_smap = smap;
	}

	public override Symbol OriginalDef => _original.OriginalDef;

	public override SyntaxNode? DeclaringNode => _original.DeclaringNode;

	internal override IdentifierToken? IdentifierToken => _original.IdentifierToken;

	public override ExprNode? InitializerExprNode => _original.InitializerExprNode;

	public override TypeSymbol Type
	{
		get
		{
			return _type ??= (TypeSymbol)_original.Type.SubstituteWithHierarchyPatching(_smap);
		}
	}
}
