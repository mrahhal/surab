namespace Surab.Analysis;

public abstract class FieldSymbol : Symbol, ISymbolHasType
{
	protected FieldSymbol() : base(SymbolKind.Field)
	{
	}

	/// <summary>
	/// The type symbol of the enclosing type.
	/// </summary>
	public TypeSymbol EnclosingType => (TypeSymbol)Parent!;

	/// <summary>
	/// The type of the field.
	/// </summary>
	public abstract TypeSymbol Type { get; }

	public abstract bool IsTypeInferred { get; }

	public bool HasInitializer => InitializerExprNode != null;

	public abstract ExprNode? InitializerExprNode { get; }

	/// <summary>
	/// The field index in the fields array of its containing type.
	/// </summary>
	public abstract int FieldIndex { get; }

	internal override Symbol SubstituteCore(SubstitutionMap smap)
	{
		return new SubstitutedFieldSymbol(this, smap);
	}
}

internal sealed class SrcFieldSymbol : FieldSymbol
{
	private readonly FieldDeclNode _node;
	private ResolvedType? _type;

	internal SrcFieldSymbol(FieldDeclNode node, int fieldIndex)
	{
		_node = node;
		FieldIndex = fieldIndex;
	}

	public override Symbol OriginalDef => this;

	public override FieldDeclNode DeclaringNode => _node;

	internal override IdentifierToken? IdentifierToken => _node.Identifier;

	public override TypeSymbol Type => EnsureResolveType().Type;

	public override bool IsTypeInferred => EnsureResolveType().IsInferred;

	public override ExprNode? InitializerExprNode => _node.Initializer?.Expr;

	public override int FieldIndex { get; }

	private ResolvedType EnsureResolveType()
	{
		return _type ??= Reasoner.ResolveType(_node, _node.Type);
	}
}

internal sealed class SubstitutedFieldSymbol : FieldSymbol
{
	private readonly FieldSymbol _original;
	private readonly SubstitutionMap _smap;

	private TypeSymbol? _type;

	public SubstitutedFieldSymbol(
		FieldSymbol original,
		SubstitutionMap smap)
	{
		_original = original;
		_smap = smap;
	}

	public override Symbol OriginalDef => _original.OriginalDef;

	public override SyntaxNode? DeclaringNode => _original.DeclaringNode;

	internal override IdentifierToken? IdentifierToken => _original.IdentifierToken;

	public override TypeSymbol Type
	{
		get
		{
			return _type ??= (TypeSymbol)_original.Type.SubstituteWithHierarchyPatching(_smap);
		}
	}

	public override bool IsTypeInferred => _original.IsTypeInferred;

	// But doesn't this need to be replaced too somehow? But this a syntax node...
	// Do we have to check against filled out generics and replace on the fly in LookupSymbol and such?
	// Or maybe we don't need to?
	public override ExprNode? InitializerExprNode => _original.InitializerExprNode;

	public override int FieldIndex => _original.FieldIndex;
}
