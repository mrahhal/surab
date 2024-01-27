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
	/// Looks up `this` starting from this scope and walking up. In the case of structs, the returned type is the struct itself and not the pointer.
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
	/// Looks up an identifier that's exported in this scope. Currently only applies to <see cref="FileScope"/>.
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
	/// This method never returns `null`. If resolving fails a special <see cref="UnknownTypeSymbol"/> will be returned.
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
			return ResolvedType.Inferred(boundType ?? UnknownTypeSymbol.Create(Reasoner));
		}
	}

	public ResolvedType ResolveType(SyntaxNode node, ColonTypeNode? colonType) => ResolveType(node, colonType?.Type);

	/// <summary>
	/// Resolves a type, binding the type node to its type symbol.
	/// This method never returns `null`. If resolving fails a special <see cref="UnknownTypeSymbol"/> will be returned.
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
			ExprTypeNode exprTypeNode => Reasoner.LookupSymbol(exprTypeNode),
			FnTypeNode fnTypeNode => Reasoner.LookupSymbol(fnTypeNode),
			MissingTypeNode => UnknownTypeSymbol.Create(Reasoner),
			_ => throw new UnreachableException(),
		};
		return ResolvedType.Typed(resolvedType as TypeSymbol ?? UnknownTypeSymbol.Create(Reasoner));
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

internal sealed class FileScope(FileNode file, Reasoner fileReasoner, Reasoner? preludeReasoner) : Scope
{
	private ImmutableArray<Symbol> _decls;

	private readonly FileNode _file = file;
	private readonly Reasoner _fileReasoner = fileReasoner;

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
		var imports = _file.Imports.ToList();
		for (var i = imports.Count - 1; i >= 0; i--)
		{
			var import = imports[i];
			var targetReasoner = _fileReasoner.ResolveImportedFileReasoner(import);
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
		foreach (var decl in _file.Decls)
		{
			if (Reasoner.TryGetDeclaredSymbol(decl, out var symbol))
			{
				builder.Add(symbol);
			}
		}
		return builder.ToImmutable();
	}
}

/// <summary>
/// A scope that exposes declarations local in the type.
/// </summary>
internal sealed class NamedTypeDeclScope(
	DeclNode decl,
	NamedTypeSymbol symbol) : Scope
{
	private readonly NamedTypeSymbol _symbol = symbol;

	private ImmutableArray<Symbol> _localDecls;

	public DeclNode DeclNode { get; } = decl;

	public ImmutableArray<Symbol> LocalDecls
	{
		get
		{
			if (_localDecls.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _localDecls, BuildLocalDecls(), default);
			}

			return _localDecls;
		}
	}

	public override Symbol? LookupIdentifierHere(string identifier)
	{
		// Type members (fields and methods) are accessed through `this`, so they're not visible from the scope.
		// Local decls don't use `this`, so here we only need to lookup using LocalDecls.

		return LookupIdentifierIn(LocalDecls, identifier);
	}

	public override TypeSymbol? LookupThisHere()
	{
		return _symbol;
	}

	public override TypeSymbol? LookupTypeDeclHere()
	{
		return _symbol;
	}

	private ImmutableArray<Symbol> BuildLocalDecls()
	{
		var nestedDecls = DeclNode switch
		{
			StructDeclNode structDeclNode => structDeclNode.DeclsList.Decls,
			ClassDeclNode classDeclNode => classDeclNode.DeclsList.Decls,
			_ => throw new UnreachableException(),
		};

		var builder = ImmutableArray.CreateBuilder<Symbol>();
		foreach (var localDecl in nestedDecls.Where(x => x is StructDeclNode or ClassDeclNode or FnDeclNode))
		{
			if (Reasoner.TryGetDeclaredSymbol(localDecl, out var symbol))
			{
				builder.Add(symbol);
			}
		}
		return builder.ToImmutable();
	}
}

internal sealed class FnScope(
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
internal sealed class LocalScope(LocalSymbol symbol) : Scope
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

/// <summary>
/// A scope that exposes declarations local to the block to the entire block.
/// This means declaring a struct anywhere inside the block makes it available to any statement inside the whole block.
/// </summary>
internal sealed class BlockScope(BlockExprNode blockExprNode) : Scope
{
	private ImmutableArray<Symbol> _localDecls;

	public BlockExprNode BlockExprNode { get; } = blockExprNode;

	public ImmutableArray<Symbol> LocalDecls
	{
		get
		{
			if (_localDecls.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _localDecls, BuildLocalDecls(), default);
			}

			return _localDecls;
		}
	}

	public override Symbol? LookupIdentifierHere(string identifier)
	{
		return LookupIdentifierIn(LocalDecls, identifier);
	}

	private ImmutableArray<Symbol> BuildLocalDecls()
	{
		var builder = ImmutableArray.CreateBuilder<Symbol>();
		foreach (var declStmt in BlockExprNode.Stmts.OfType<DeclStmtNode>())
		{
			if (Reasoner.TryGetDeclaredSymbol(declStmt.Decl, out var symbol))
			{
				builder.Add(symbol);
			}
		}
		return builder.ToImmutable();
	}
}
