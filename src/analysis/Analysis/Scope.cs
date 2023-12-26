using System.Collections.Immutable;
using System.Diagnostics;

namespace Surab.Analysis;

/// <summary>
/// A scope is responsible for binding identifiers to symbols and exposing these symbols at certain scopes so lookups can be done on them.
/// </summary>
internal abstract class Scope
{
	public Scope? Parent { get; private set; }

	public Reasoner Reasoner { get; internal set; } = default!;

	/// <summary>
	/// Sets the parent scope of this scope.
	/// </summary>
	public void SetParent(Scope? parent)
	{
		Parent = parent;
	}

	/// <summary>
	/// Looks up an identifier starting from this scope and walking up.
	/// </summary>
	public Symbol? LookupIdentifier(IdentifierToken identifier)
	{
		return LookupWalkingUp(b => b.LookupIdentifierHere(identifier));
	}

	/// <summary>
	/// Looks up `this` starting from this scope and walking up.
	/// </summary>
	public TypeSymbol? LookupThis()
	{
		return LookupWalkingUp(b => b.LookupThisHere());
	}

	/// <summary>
	/// Looks up any type decl starting from this scope and walking up.
	/// </summary>
	public TypeSymbol? LookupTypeDecl()
	{
		return LookupWalkingUp(b => b.LookupTypeDeclHere());
	}

	private T? LookupWalkingUp<T>(Func<Scope, T> func)
	{
		var current = this;
		while (current != null)
		{
			var result = func(current);
			if (result != null)
			{
				return result;
			}

			current = current.Parent;
		}

		return default;
	}

	// TODO: Seperate this into LookupIdentifierForType and LookupIdentifierForInstance?
	/// <summary>
	/// Looks up an identifier in this scope.
	/// </summary>
	public virtual Symbol? LookupIdentifierHere(string identifier) => null;

	/// <summary>
	/// Looks up an identifier that's exported in this scope. Currently only applies to <see cref="ModuleScope"/>.
	/// </summary>
	public virtual Symbol? LookupExportedIdentifier(string identifier) => null;

	public Symbol? LookupIdentifierHere(IdentifierToken identifier) => LookupIdentifierHere(identifier.Value);

	/// <summary>
	/// Looks up `this` in this scope.
	/// </summary>
	public virtual TypeSymbol? LookupThisHere() => null;

	/// <summary>
	/// Looks up any type decl in this scope.
	/// </summary>
	public virtual TypeSymbol? LookupTypeDeclHere() => null;

	protected static T? LookupIdentifierIn<T>(T symbol, string identifier)
		where T : Symbol
	{
		if (symbol.Identifier?.Value == identifier)
		{
			return symbol;
		}
		return null;
	}

	protected static T? LookupIdentifierIn<T>(ImmutableArray<T> array, string identifier)
		where T : Symbol
	{
		// Move from bottom to top trying to match the identifier.
		for (var i = array.Length - 1; i >= 0; i--)
		{
			var item = array[i];
			if (item.Identifier?.Value == identifier)
			{
				return item;
			}
		}
		return null;
	}

	/// <summary>
	/// Resolves a type, binding the type node to its type symbol.
	/// This method never returns `null`. If resolving fails a special <see cref="ErrorTypeSymbol"/> will be returned.
	/// </summary>
	public ResolvedType ResolveType(SyntaxNode node, TypeNode? typeNode)
	{
		if (typeNode != null)
		{
			return ResolveType(typeNode);
		}
		else
		{
			var boundType = Reasoner.InferTypeOfDeclaringNode(node);
			return ResolvedType.Inferred(boundType ?? ErrorTypeSymbol.Create());
		}
	}

	public ResolvedType ResolveType(SyntaxNode node, ColonTypeNode? colonType) => ResolveType(node, colonType?.Type);

	/// <summary>
	/// Resolves a type, binding the type node to its type symbol.
	/// This method never returns `null`. If resolving fails a special <see cref="ErrorTypeSymbol"/> will be returned.
	/// </summary>
	public ResolvedType ResolveType(TypeNode typeNode)
	{
		//var identifier = typeNode switch
		//{
		//    NamedTypeNode namedTypeNode => namedTypeNode.Identifier,
		//    _ => null,
		//};
		var resolvedType = typeNode switch
		{
			PtrTypeNode ptrTypeNode => Reasoner.LookupSymbol(ptrTypeNode),
			NamedTypeNode namedTypeNode => LookupIdentifier(namedTypeNode.Identifier),
			FnTypeNode fnTypeNode => Reasoner.LookupSymbol(fnTypeNode),
			MissingTypeNode => ErrorTypeSymbol.Create(),
			_ => throw new UnreachableException(),
		};
		return ResolvedType.Typed(resolvedType as TypeSymbol ?? ErrorTypeSymbol.Create());
	}

	/// <summary>
	/// Returns whether or not this scope should end with the given node.
	/// </summary>
	/// <remarks>
	/// Some scopes (such as a local scope) should be extended to live beyond their associated node.
	///
	/// The default implementation always returns `true` so that the scope ends with its associated node.
	/// This is overriden in a local scope for example to make it live longer.
	/// </remarks>
	internal virtual bool ShouldEndScope(SyntaxNode node)
	{
		return true;
	}
}

internal class ModuleScope(ModuleNode module, Reasoner moduleReasoner, Reasoner? preludeReasoner) : Scope
{
	private ImmutableArray<Symbol> _decls;

	private readonly ModuleNode _module = module;
	private readonly Reasoner _moduleReasoner = moduleReasoner;

	private readonly Reasoner? _preludeReasoner = preludeReasoner;

	public ImmutableArray<Symbol> Decls
	{
		get
		{
			if (_decls.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _decls, BuildDecls(), default);
			}

			return _decls;
		}
	}

	public override Symbol? LookupIdentifierHere(string identifier)
	{
		return
			LookupIdentifierIn(Decls, identifier) ??
			LookupInImports(identifier);
	}

	private Symbol? LookupInImports(string identifier)
	{
		var imports = _module.Imports.ToList();
		for (var i = imports.Count - 1; i >= 0; i--)
		{
			var import = imports[i];
			var targetReasoner = _moduleReasoner.ResolveImportedFileReasoner(import);
			var result = targetReasoner?.LookupExportedSymbolByName(identifier);

			if (result != null)
			{
				return result;
			}
		}

		// If nothing else works, try resolve from the prelude.
		return _preludeReasoner?.LookupExportedSymbolByName(identifier);
	}

	public override Symbol? LookupExportedIdentifier(string identifier)
	{
		return LookupIdentifierIn(Decls, identifier);
	}

	private ImmutableArray<Symbol> BuildDecls()
	{
		var builder = ImmutableArray.CreateBuilder<Symbol>();
		foreach (var decl in _module.Decls)
		{
			if (Reasoner.TryGetDeclaredSymbol(decl, out var symbol))
			{
				builder.Add(symbol);
			}
		}
		return builder.ToImmutable();
	}
}

internal class TypeDeclScope(NamedTypeSymbol symbol) : Scope
{
	protected readonly NamedTypeSymbol _symbol = symbol;

	public override Symbol? LookupIdentifierHere(string identifier)
	{
		// Type members are accessed through `this`, so they're not visible from the scope.
		return null;
		//return LookupIdentifierIn(_symbol.Members, identifier);
	}

	public override TypeSymbol? LookupThisHere()
	{
		return _symbol;
	}

	public override TypeSymbol? LookupTypeDeclHere()
	{
		return _symbol;
	}
}

internal class StructDeclScope(
	StructDeclNode structDecl,
	StructTypeSymbol symbol) : TypeDeclScope(symbol)
{
	private readonly StructDeclNode _structDecl = structDecl;
}

internal class ClassDeclScope(
	ClassDeclNode classDecl,
	NamedTypeSymbol symbol) : TypeDeclScope(symbol)
{
	private readonly ClassDeclNode _classDecl = classDecl;
}

internal class FnScope(
	FnSigNode fnSig,
	FnSymbol fnSymbol) : Scope
{
	private readonly FnSigNode _fnSig = fnSig;
	private readonly FnSymbol _fnSymbol = fnSymbol;

	public override Symbol? LookupIdentifierHere(string identifier)
	{
		return LookupIdentifierIn(_fnSymbol.Params, identifier);
	}
}

/// <summary>
/// A scope that exposes a local symbol.
/// </summary>
/// <remarks>
/// This scope gets extended until the end of a block node. This scope replaces explicit block scopes to allow var shadowing.
/// </remarks>
internal class LocalScope(LocalSymbol symbol) : Scope
{
	private readonly LocalSymbol _symbol = symbol;

	public override Symbol? LookupIdentifierHere(string identifier)
	{
		return LookupIdentifierIn(_symbol, identifier);
	}

	internal override bool ShouldEndScope(SyntaxNode node)
	{
		// A local scope lives until its containing block ends.
		return node is BlockExprNode;
	}
}
