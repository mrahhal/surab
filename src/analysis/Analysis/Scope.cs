using System.Collections.Immutable;
using System.Diagnostics;

namespace Surab.Analysis;

/// <summary>
/// A scope is responsible for binding identifiers to symbols and exposing these symbols at certain scopes so lookups can be done on them.
/// </summary>
internal abstract class Scope
{
    public Scope? Parent { get; private set; }

    public SemanticModel SemanticModel { get; internal set; } = default!;

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

    protected static T? LookupIdentifierIn<T>(ImmutableArray<T> array, string identifier)
        where T : Symbol
    {
        // Move from bottom to top trying to match the identifier.
        for (var i = array.Length - 1; i >= 0; i--)
        {
            var item = array[i];
            if (item.Identifier != null && item.Identifier.Value == identifier)
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
    public ResolvedType ResolveType(SyntaxNode node, TypeNode? type)
    {
        if (type != null)
        {
            var identifier = type switch
            {
                NamedTypeNode namedTypeNode => namedTypeNode.Identifier,
                _ => null,
            };
            var resolvedType = type switch
            {
                NamedTypeNode namedTypeNode => LookupIdentifier(namedTypeNode.Identifier),
                FnTypeNode fnTypeNode => SemanticModel.LookupSymbol(fnTypeNode),
                _ => throw new UnreachableException(),
            };
            return ResolvedType.Typed(resolvedType as TypeSymbol ?? ErrorTypeSymbol.General);
        }
        else
        {
            var boundType = SemanticModel.InferTypeOfDeclaringNode(node);
            return ResolvedType.Inferred(boundType ?? ErrorTypeSymbol.General);
        }
    }

    public ResolvedType ResolveType(SyntaxNode node, ColonTypeNode? colonType) => ResolveType(node, colonType?.Type);
}

/// <summary>
/// Marks scopes that have locals.
/// </summary>
internal interface IScopeHasLocals
{
    /// <summary>
    /// The locals exposed by the scope.
    /// </summary>
    ImmutableArray<LocalSymbol> Locals { get; }
}

// TODO: preludeSemanticModel should become an array of models from imported modules.
internal class ModuleScope(ModuleNode module, SemanticModel? preludeSemanticModel) : Scope
{
    private ImmutableArray<Symbol> _decls;

    private readonly ModuleNode _module = module;
    private readonly SemanticModel? _preludeSemanticModel = preludeSemanticModel;

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
            _preludeSemanticModel?.LookupExportedSymbolByName(identifier);
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
            if (SemanticModel.TryGetDeclaredSymbol(decl, out var symbol))
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
        return LookupIdentifierIn(_symbol.Members, identifier);
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

internal class BlockExprScope(BlockExprNode block) : Scope, IScopeHasLocals
{
    private ImmutableArray<LocalSymbol> _locals;

    private readonly BlockExprNode _block = block;

    public ImmutableArray<LocalSymbol> Locals
    {
        get
        {
            if (_locals.IsDefault)
            {
                ImmutableInterlocked.InterlockedCompareExchange(ref _locals, BuildLocals(), default);
            }

            return _locals;
        }
    }

    public override Symbol? LookupIdentifierHere(string identifier)
    {
        return LookupIdentifierIn(Locals, identifier);
    }

    private ImmutableArray<LocalSymbol> BuildLocals()
    {
        var builder = ImmutableArray.CreateBuilder<LocalSymbol>();
        foreach (var decl in _block.Stmts)
        {
            if (SemanticModel.TryGetDeclaredSymbol(decl, out var symbol) && symbol is LocalSymbol localSymbol)
            {
                builder.Add(localSymbol);
            }
        }
        return builder.ToImmutable();
    }
}
