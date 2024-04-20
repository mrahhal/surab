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
		if (symbol.IdentifierMatches(identifier))
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
			if (item.IdentifierMatches(identifier))
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
			return ResolvedType.Inferred(boundType ?? UnknownTypeSymbol.Create());
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
			MissingTypeNode => UnknownTypeSymbol.Create(),
			_ => throw new UnreachableException(),
		};
		return ResolvedType.Typed(resolvedType.AsTypeSymbolOrUnknown());
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

/// <summary>
/// A scope that exposes file declarations, symbols in the current module, and imported symbols.
/// </summary>
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
			LookupInCurrentModule(identifier) ??
			LookupInImports(identifier);
	}

	private Symbol? LookupInCurrentModule(string identifier)
	{
		var module = _fileReasoner.ResolveCurrentModule();
		// All exported symbols of current module are exposed to files inside the module.
		return module.GetExportedSymbol(identifier);
	}

	private Symbol? LookupInImports(string identifier)
	{
		var imports = _file.Imports.ToList();
		for (var i = imports.Count - 1; i >= 0; i--)
		{
			var import = imports[i];
			var resolvedModule = _fileReasoner.ResolveImport(import);
			var result = resolvedModule.ExposedSymbols.FirstOrDefault(s => s.IdentifierMatches(identifier));

			if (result != null)
			{
				return result;
			}
		}

		// If nothing else works, try resolve from the prelude.
		return _preludeReasoner?.LookupExportedSymbolByName(identifier);
	}

	internal ModuleSymbol? LookupImportedModule(string identifier)
	{
		foreach (var import in _file.Imports)
		{
			var resolvedModule = _fileReasoner.ResolveImport(import);
			if (!resolvedModule.IsFailed && resolvedModule.Module.IdentifierMatches(identifier))
			{
				return resolvedModule.Module;
			}
		}
		return null;
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
/// A scope that exposes local declarations in the type, `this`, and generic params.
/// </summary>
internal sealed class NamedTypeDeclScope(NamedTypeSymbol symbol) : Scope
{
	private readonly NamedTypeSymbol _symbol = symbol;

	private ImmutableArray<Symbol> _localDecls;

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

		return
			LookupIdentifierIn(LocalDecls, identifier) ??
			LookupIdentifierInGenericParams(identifier);
	}

	private Symbol? LookupIdentifierInGenericParams(string identifier)
	{
		if (!_symbol.TryGetGenericParams(out var genericParams))
			return null;
		return LookupIdentifierIn(genericParams, identifier);
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
		// Need to filter out fields for example, because files are not in scope without `this`?
		return _symbol.Members.Where(x => x.Kind is SymbolKind.Type or SymbolKind.Fn).ToImmutableArray();
	}
}

/// <summary>
/// A scope that exposes the generic params in the alias type.
/// </summary>
internal sealed class AliasTypeDeclScope(AliasTypeSymbol symbol) : Scope
{
	private readonly AliasTypeSymbol _symbol = symbol;

	public override Symbol? LookupIdentifierHere(string identifier)
	{
		return LookupIdentifierInGenericParams(identifier);
	}

	private Symbol? LookupIdentifierInGenericParams(string identifier)
	{
		if (!_symbol.TryGetGenericParams(out var genericParams))
			return null;
		return LookupIdentifierIn(genericParams, identifier);
	}

	public override TypeSymbol? LookupTypeDeclHere()
	{
		return _symbol;
	}
}

/// <summary>
/// A scope that exposes a function's params and generic params.
/// </summary>
internal sealed class FnScope(FnSymbol symbol) : Scope
{
	private readonly FnSymbol _symbol = symbol;

	public override Symbol? LookupIdentifierHere(string identifier)
	{
		return
			LookupIdentifierIn(_symbol.Params, identifier) ??
			LookupIdentifierInGenericParams(identifier);
	}

	private Symbol? LookupIdentifierInGenericParams(string identifier)
	{
		if (!_symbol.TryGetGenericParams(out var genericParams))
			return null;
		return LookupIdentifierIn(genericParams, identifier);
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
		// We need to skip VarDeclNodes as those get their own LocalScopes and abide by a special ending scope.
		var declStmts = BlockExprNode.Stmts
			.OfType<DeclStmtNode>()
			.Where(declStmt => declStmt.Decl is not VarDeclNode);
		foreach (var declStmt in declStmts)
		{
			if (Reasoner.TryGetDeclaredSymbol(declStmt.Decl, out var symbol))
			{
				builder.Add(symbol);
			}
		}
		return builder.ToImmutable();
	}
}
