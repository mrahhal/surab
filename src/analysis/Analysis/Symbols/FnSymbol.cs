using System.Collections.Immutable;

namespace Surab.Analysis;

public abstract class FnSymbol : Symbol, ISymbolHasType, ISymbolHasExternAttr, IPossiblyGenericSymbol
{
	private TypeSymbol? _methodReceiverType;
	private FnTypeSymbol? _typeWithoutReceiver;
	private string? _name;
	private IEnumerable<Symbol>? _children;

	protected FnSymbol() : base(SymbolKind.Fn)
	{
	}

	public override string Name
	{
		get
		{
			return _name ??= GenericHelper.CreateName(this, Identifier);
		}
	}

	public override IEnumerable<Symbol> Children
	{
		get
		{
			if (_children == null)
			{
				Interlocked.CompareExchange(ref _children, FnChildrenComputer.Compute(this), default);
			}
			return _children;
		}
	}

	// TODO: Remove?
	public abstract FnSigNode SigNode { get; }

	public abstract bool HasBody { get; }

	/// <summary>
	/// Gets whether a function is an outer function. i.e. not a member of another symbol but directly under the module.
	/// </summary>
	public bool IsOuterFn => Parent?.Kind is SymbolKind.Module;

	public abstract bool IsMethod { get; }

	public bool IsMain => IsOuterFn && Name == "main";

	public abstract bool IsInitializerMethod { get; }

	public abstract bool IsStatic { get; }

	public abstract bool IsExtern { get; }

	public abstract AttrsList AttrsList { get; }

	public abstract ImmutableArray<GenericParamTypeSymbol> GenericParams { get; }

	public abstract ImmutableArray<TypeSymbol> GenericArgs { get; }

	/// <summary>
	/// The type symbol of the function.
	/// </summary>
	public abstract FnTypeSymbol Type { get; }

	TypeSymbol ISymbolHasType.Type => Type;

	public bool IsTypeInferred => false;

	/// <summary>
	/// The type symbol of the function without the receiver arg for non static methods.
	/// For functions or static methods, the same normal fn type will be returned.
	/// This is useful when analysis is done on a method being accessed through an instance (`a.b()`).
	/// </summary>
	public FnTypeSymbol TypeWithoutReceiver
	{
		get
		{
			if (_typeWithoutReceiver != null)
				return _typeWithoutReceiver;

			var normalType = Type;
			if (!IsMethod || IsStatic)
				return normalType;

			var key = new FnTypeKey(
				normalType.IsExtern,
				normalType.ReturnType,
				[.. normalType.ParamTypes[1..]]);
			return _typeWithoutReceiver = SymbolInterner.GetFnType(key);
		}
	}

	/// <summary>
	/// The type symbol of the receiver type (struct/class) if this is a non static method.
	/// </summary>
	public TypeSymbol? MethodReceiverType
	{
		get
		{
			if (!IsMethod || IsStatic)
				return null;

			// TODO: Account for planned @receiver_by_val.
			return _methodReceiverType ??= SymbolInterner.GetPtrType(EnclosingType ?? UnknownTypeSymbol.Create());
		}
	}

	/// <summary>
	/// The return type of the function.
	/// </summary>
	public TypeSymbol ReturnType => Type.ReturnType;

	/// <summary>
	/// Gets the fn's params.
	/// </summary>
	/// <remarks>
	/// This won't include the receiver for methods. Use <see cref="MethodReceiverType"/> to access the receiver type.
	/// </remarks>
	public abstract ImmutableArray<ParamSymbol> Params { get; }

	public override string ToString()
	{
		return $"{Name}({string.Join(", ", Params.Select(p => $"{p.Name}: {p.Type.Name}"))}) {ReturnType.Name}";
	}

	internal override Symbol SubstituteCore(SubstitutionMap smap)
	{
		return new SubstitutedFnSymbol(this, [], smap);
	}

	Symbol IPossiblyGenericSymbol.SubstituteConstructedGeneric(SubstitutionMap smap, IReadOnlyList<TypeSymbol> genericArgs)
	{
		return new SubstitutedFnSymbol(this, genericArgs, smap);
	}
}

public sealed class SrcFnSymbol : FnSymbol
{
	private readonly FnSigNode _node;
	private ImmutableArray<GenericParamTypeSymbol> _genericParams;
	private FnTypeSymbol? _type;
	private ImmutableArray<ParamSymbol> _params;
	private AttrsList? _attrsList;

	internal SrcFnSymbol(FnSigNode node)
	{
		_node = node;
	}

	public override Symbol OriginalDef => this;

	// Parent will be either a FunctionDeclNode or a MethodDeclNode.
	public override SyntaxNode DeclaringNode => _node.Parent!;

	public override FnSigNode SigNode => _node;

	internal override IdentifierToken IdentifierToken => _node.Name.Identifier;

	public override bool HasBody => _node.FnHasBody;

	public override bool IsMethod => _node.Parent is MethodDeclNode;

	public override bool IsInitializerMethod => IsMethod && ((MethodDeclNode)_node.Parent!).IsInitializer;

	public override bool IsStatic => _node.Parent is FnDeclNode;

	public override bool IsExtern => AttrsList.HasExtern;

	public override AttrsList AttrsList => _attrsList ??= Reasoner.GetAttrs(_node.Attrs);

	public override ImmutableArray<GenericParamTypeSymbol> GenericParams
	{
		get
		{
			if (_genericParams.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _genericParams, BuildGenericParams(), default);
			}
			return _genericParams;
		}
	}

	public override ImmutableArray<TypeSymbol> GenericArgs => [];

	private ImmutableArray<GenericParamTypeSymbol> BuildGenericParams()
	{
		return GetGenericParamsList()?
			.GenericParams
			.Select(Reasoner.GetDeclaredSymbol)
			.ToImmutableArray() ?? [];
	}

	private GenericParamsListNode? GetGenericParamsList() => _node.Name.GenericParamsList;

	public override FnTypeSymbol Type
	{
		get
		{
			return _type ??= Reasoner.GetFnType(_node.Parent!);
		}
	}

	public override ImmutableArray<ParamSymbol> Params
	{
		get
		{
			if (_params.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _params, BuildParams(), default);
			}
			return _params;
		}
	}

	private ImmutableArray<ParamSymbol> BuildParams()
	{
		var builder = ImmutableArray.CreateBuilder<ParamSymbol>();
		foreach (var paramNode in _node.Params)
		{
			if (Reasoner.TryGetDeclaredSymbol(paramNode, out var symbol))
			{
				builder.Add((ParamSymbol)symbol);
			}
		}
		return builder.ToImmutable();
	}
}

internal sealed class SubstitutedFnSymbol : FnSymbol
{
	private readonly FnSymbol _original;
	private readonly SubstitutionMap _smap;

	private FnTypeSymbol? _type;
	private ImmutableArray<ParamSymbol>? _params;

	public SubstitutedFnSymbol(
		FnSymbol original,
		IEnumerable<TypeSymbol> genericArgs,
		SubstitutionMap smap)
	{
		_original = original;
		SetParent(original.Parent);
		GenericArgs = genericArgs.ToImmutableArray();
		_smap = smap;

		SymbolInterner = original.OriginalDef.SymbolInterner;
	}

	public override Symbol OriginalDef => _original.OriginalDef;

	public override SyntaxNode? DeclaringNode => _original.DeclaringNode;

	internal override IdentifierToken? IdentifierToken => _original.IdentifierToken;

	public override FnSigNode SigNode => _original.SigNode;

	public override bool HasBody => _original.HasBody;

	public override bool IsMethod => _original.IsMethod;

	public override bool IsInitializerMethod => _original.IsInitializerMethod;

	public override bool IsStatic => _original.IsStatic;

	public override bool IsExtern => _original.IsExtern;

	public override AttrsList AttrsList => _original.AttrsList;

	public override ImmutableArray<GenericParamTypeSymbol> GenericParams => _original.GenericParams;

	public override ImmutableArray<TypeSymbol> GenericArgs { get; }

	public override FnTypeSymbol Type
	{
		get
		{
			return _type ??= (FnTypeSymbol)_original.Type.SubstituteWithHierarchyPatching(_smap);
		}
	}

	public override ImmutableArray<ParamSymbol> Params
	{
		get
		{
			return _params ??= _original.Params
				.Select(p => (ParamSymbol)p.SubstituteAndSetParent(this, _smap))
				.ToImmutableArray();
		}
	}
}

internal sealed class ReplicatedFnSymbol : FnSymbol
{
	private readonly FnSymbol _target;

	public ReplicatedFnSymbol(FnSymbol target, Symbol parent)
	{
		_target = target;

		SetParent(parent);
		SymbolInterner = target.SymbolInterner;
	}

	public override FnSigNode SigNode => _target.SigNode;

	public override bool HasBody => _target.HasBody;

	public override bool IsMethod => _target.IsMethod;

	public override bool IsInitializerMethod => _target.IsInitializerMethod;

	public override bool IsStatic => _target.IsStatic;

	public override bool IsExtern => _target.IsExtern;

	public override AttrsList AttrsList => _target.AttrsList;

	public override ImmutableArray<GenericParamTypeSymbol> GenericParams => _target.GenericParams;

	public override ImmutableArray<TypeSymbol> GenericArgs => _target.GenericArgs;

	public override FnTypeSymbol Type => _target.Type;

	public override ImmutableArray<ParamSymbol> Params => _target.Params;

	public override Symbol OriginalDef => _target.OriginalDef;

	public override SyntaxNode? DeclaringNode => _target.DeclaringNode;

	internal override IdentifierToken? IdentifierToken => _target.IdentifierToken;
}

file class FnChildrenComputer(Reasoner reasoner) : SyntaxTreeVisitor
{
	private readonly List<Symbol> _children = [];

	public static IEnumerable<Symbol> Compute(FnSymbol fn)
	{
		var body = fn.DeclaringNode switch
		{
			FnDeclNode fnDecl => fnDecl.Body,
			MethodDeclNode methodDecl => methodDecl.Body,
			_ => null,
		};
		if (body == null)
		{
			return [];
		}

		var visitor = new FnChildrenComputer(fn.Reasoner);
		visitor.Visit(body);
		return visitor._children;
	}

	public override void Visit(SyntaxNode node)
	{
		if (node.DeclaresSymbol() && reasoner.GetDeclaredSymbol(node) is { } symbol)
		{
			_children.Add(symbol);
			return;
		}

		base.Visit(node);
	}
}
