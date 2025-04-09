using System.Collections.Immutable;
using System.Diagnostics;

namespace Surab.Analysis;

/// <summary>
/// A named type. Can be any of: struct, trait.
/// </summary>
public abstract class NamedTypeSymbol : TypeSymbol, ISymbolHasExternAttr, IPossiblyGenericSymbol
{
	private ImmutableArray<Symbol> _members;
	private ImmutableArray<Symbol> _accessibleMembers;
	private InitializerMethodWrapper? _initializerMethodWrapper = default;
	private ImmutableArray<TypeCreationMemberSymbol> _creationMemberSymbols;
	private string? _name;

	public override string Name
	{
		get
		{
			return _name ??= GenericHelper.CreateName(this, Identifier);
		}
	}

	public override abstract NamedTypeSymbol OriginalDef { get; }

	public abstract bool IsExtern { get; }

	public abstract AttrsList AttrsList { get; }

	public abstract ImmutableArray<GenericParamTypeSymbol> GenericParams { get; }

	public abstract ImmutableArray<TypeSymbol> GenericArgs { get; }

	public override ImmutableArray<Symbol> Members
	{
		get
		{
			if (_members.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _members, BuildMembers(), default);
			}
			return _members;
		}
	}

	private ImmutableArray<Symbol> BuildMembers()
	{
		var builder = ImmutableArray.CreateBuilder<Symbol>();

		foreach (var child in Children)
		{
			if (IsTypeMember(child))
			{
				builder.Add(child);
			}
			else if (child is ImplSymbol impl)
			{
				foreach (var implMember in impl.Members)
				{
					if (IsTypeMember(implMember))
					{
						builder.Add(implMember);
					}
				}
			}
		}

		return builder.ToImmutable();

		static bool IsTypeMember(Symbol symbol)
		{
			// Only need to account for decl symbols.
			return !(symbol.Kind is SymbolKind.Impl);
		}
	}

	public override ImmutableArray<Symbol> AccessibleMembers
	{
		get
		{
			if (_accessibleMembers.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _accessibleMembers, BuildAccessibleMembers(), default);
			}
			return _accessibleMembers;
		}
	}

	private ImmutableArray<Symbol> BuildAccessibleMembers()
	{
		var builder = ImmutableArray.CreateBuilder<Symbol>();

		foreach (var member in Members)
		{
			if (member.IdentifierToken != null &&
				!member.IdentifierToken.IsMissing &&
				!member.IdentifierToken.IsOp())
			{
				builder.Add(member);
			}
		}

		return builder.ToImmutable();
	}

	public FnSymbol? InitializerMethod
	{
		get
		{
			_initializerMethodWrapper ??= BuildInitializerMethod();
			return _initializerMethodWrapper.Value.Fn;
		}
	}

	/// <summary>
	/// Gets the members that can be set in a type creation expr. Can include both field and param symbols (from the initializer).
	/// </summary>
	public ImmutableArray<TypeCreationMemberSymbol> CreationMemberSymbols
	{
		get
		{
			if (_creationMemberSymbols.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _creationMemberSymbols, BuildCreationMemberSymbols(), default);
			}
			return _creationMemberSymbols;
		}
	}

	public TypeCreationMemberSymbol? GetCreationMemberSymbol(IdentifierToken identifierToken) => GetCreationMemberSymbol(identifierToken.Value);

	public TypeCreationMemberSymbol? GetCreationMemberSymbol(string identifier)
	{
		for (var i = CreationMemberSymbols.Length - 1; i >= 0; i--)
		{
			var item = CreationMemberSymbols[i];
			if (!item.IsConflicting && item.Symbol.IdentifierMatches(identifier))
			{
				return item;
			}
		}
		return null;
	}

	private InitializerMethodWrapper BuildInitializerMethod()
	{
		foreach (var fn in Members.OfType<FnSymbol>())
		{
			if (fn.IsMethod && fn.IsInitializerMethod)
			{
				// We process only the first init found. Multiple inits will produce an error.
				return new InitializerMethodWrapper { Fn = fn };
			}
		}
		return new InitializerMethodWrapper { Fn = null };
	}

	private ImmutableArray<TypeCreationMemberSymbol> BuildCreationMemberSymbols()
	{
		var symbols = new List<Symbol>();

		// Fields
		foreach (var field in Members.OfType<FieldSymbol>().Where(f => !f.HasInitializer))
		{
			symbols.Add(field);
		}

		// Initializer params
		if (InitializerMethod != null)
		{
			foreach (var param in InitializerMethod.Params)
			{
				symbols.Add(param);
			}
		}

		var fields = symbols.OfType<FieldSymbol>().Select(x => new TypeCreationMemberSymbol(x, false));
		var @params = symbols.OfType<ParamSymbol>().Select(x => new TypeCreationMemberSymbol(x, isConflicting: fields.Any(f => f.Symbol.IdentifierMatches(x))));

		return [.. fields, .. @params];
	}

	internal override NamedTypeSymbol SubstituteCore(SubstitutionMap smap)
	{
		return new SubstitutedNamedTypeSymbol(this, [], smap);
	}

	Symbol IPossiblyGenericSymbol.SubstituteConstructedGeneric(SubstitutionMap smap, IReadOnlyList<TypeSymbol> genericArgs)
	{
		return new SubstitutedNamedTypeSymbol(this, genericArgs, smap);
	}

	private readonly struct InitializerMethodWrapper
	{
		public FnSymbol? Fn { get; init; }
	}
}

public class SrcNamedTypeSymbol : NamedTypeSymbol
{
	private readonly SyntaxNode _node;
	private ImmutableArray<Symbol> _children;
	private ImmutableArray<GenericParamTypeSymbol> _genericParams;
	private AttrsList? _attrsList;

	public SrcNamedTypeSymbol(SyntaxNode node)
	{
		_node = node;
		IdentifierToken = node switch
		{
			StructDeclNode structDecl => structDecl.Name.Identifier,
			TraitDeclNode traitDecl => traitDecl.Name.Identifier,
			_ => throw new UnreachableException(),
		};
		TypeKind = node switch
		{
			StructDeclNode => TypeSymbolKind.Struct,
			TraitDeclNode => TypeSymbolKind.Trait,
			_ => throw new UnreachableException(),
		};
	}

	public override TypeSymbolKind TypeKind { get; }

	public override SyntaxNode DeclaringNode => _node;

	internal override IdentifierToken? IdentifierToken { get; }

	public override NamedTypeSymbol OriginalDef => this;

	private AttrsListNode? AttrsListNode => _node switch
	{
		StructDeclNode structDecl => structDecl.Attrs,
		TraitDeclNode traitDecl => traitDecl.Attrs,
		_ => throw new UnreachableException(),
	};

	public override bool IsExtern => AttrsList.HasExtern;

	public override AttrsList AttrsList => _attrsList ??= Reasoner.GetAttrs(AttrsListNode);

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

	private GenericParamsListNode? GetGenericParamsList() => _node switch
	{
		StructDeclNode structDecl => structDecl.Name.GenericParamsList,
		TraitDeclNode traitDecl => traitDecl.Name.GenericParamsList,
		_ => throw new UnreachableException(),
	};

	public override IEnumerable<Symbol> Children
	{
		get
		{
			if (_children.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _children, BuildChildren(), default);
			}
			return _children;
		}
	}

	private ImmutableArray<Symbol> BuildChildren()
	{
		var builder = ImmutableArray.CreateBuilder<Symbol>();
		foreach (var node in EnumerateChildrenDecls())
		{
			if (Reasoner.TryGetDeclaredSymbol(node, out var symbol))
			{
				builder.Add(symbol);
			}
		}
		return builder.ToImmutable();
	}

	private IEnumerable<SyntaxNode> EnumerateChildrenDecls()
	{
		return _node switch
		{
			StructDeclNode structDecl => structDecl.Decls,
			TraitDeclNode traitDecl => traitDecl.Decls,
			_ => throw new UnreachableException(),
		};
	}
}

internal class SubstitutedNamedTypeSymbol : NamedTypeSymbol
{
	private readonly NamedTypeSymbol _original;
	private readonly SubstitutionMap _smap;
	private ImmutableArray<Symbol> _children;

	public SubstitutedNamedTypeSymbol(
		NamedTypeSymbol original,
		IEnumerable<TypeSymbol> genericArgs,
		SubstitutionMap smap)
	{
		_original = original;
		SetParent(original.Parent);
		GenericArgs = genericArgs.ToImmutableArray();
		_smap = smap.CreateSub();
	}

	public override NamedTypeSymbol OriginalDef => _original.OriginalDef;

	public override TypeSymbolKind TypeKind => _original.TypeKind;

	public override SyntaxNode? DeclaringNode => _original.DeclaringNode;

	internal override IdentifierToken? IdentifierToken => _original.IdentifierToken;

	public override bool IsExtern => _original.IsExtern;

	public override AttrsList AttrsList => _original.AttrsList;

	public override ImmutableArray<GenericParamTypeSymbol> GenericParams => _original.GenericParams;

	public override ImmutableArray<TypeSymbol> GenericArgs { get; }

	public override IEnumerable<Symbol> Children
	{
		get
		{
			if (_children.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _children, BuildChildren(), default);
			}
			return _children;
		}
	}

	private ImmutableArray<Symbol> BuildChildren()
	{
		return _original.Children.Select(x => x.SubstituteAndSetParent(this, _smap)).ToImmutableArray();
	}
}
