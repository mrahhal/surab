using System.Collections.Immutable;

namespace Surab.Analysis;

/// <summary>
/// A binder binds identifiers to symbols and exposes these symbols at certain scopes.
/// </summary>
internal abstract class Binder
{
    public Binder? Parent { get; private set; }

    public SemanticModel SemanticModel { get; internal set; } = default!;

    public void SetParent(Binder? parent)
    {
        Parent = parent;
    }

    /// <summary>
    /// Looks up an identifier starting from the given binder's scope and walking up.
    /// </summary>
    public Symbol? LookupIdentifier(IdentifierToken identifier)
    {
        return LookupWalkingUp(b => b.LookupIdentifierHere(identifier));
    }

    /// <summary>
    /// Looks up `this` starting from the given binder's scope and walking up.
    /// </summary>
    public TypeSymbol? LookupThis()
    {
        return LookupWalkingUp(b => b.LookupThisHere());
    }

    /// <summary>
    /// Looks up any type decl starting from the given binder's scope and walking up.
    /// </summary>
    public TypeSymbol? LookupTypeDecl()
    {
        return LookupWalkingUp(b => b.LookupTypeDeclHere());
    }

    private T? LookupWalkingUp<T>(Func<Binder, T> func)
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
    /// Looks up an identifier in this binder's scope.
    /// </summary>
    public virtual Symbol? LookupIdentifierHere(IdentifierToken identifier) => null;

    /// <summary>
    /// Looks up `this` in this binder's scope.
    /// </summary>
    public virtual TypeSymbol? LookupThisHere() => null;

    /// <summary>
    /// Looks up any type decl in this binder's scope.
    /// </summary>
    public virtual TypeSymbol? LookupTypeDeclHere() => null;

    protected static T? LookupIdentifierIn<T>(ImmutableArray<T> array, IdentifierToken identifier)
        where T : Symbol
    {
        // Move from bottom to top trying to match the identifier.
        for (var i = array.Length - 1; i >= 0; i--)
        {
            var item = array[i];
            if (item.Identifier != null && item.Identifier.Value == identifier.Value)
            {
                return item;
            }
        }
        return null;
    }

    /// <summary>
    /// Binds a type node to its symbol.
    /// This method will never return null. If binding fails a special <see cref="ErrorTypeSymbol"/> will be returned.
    /// </summary>
    public ResolvedType BindType(SyntaxNode parentNode, ColonTypeNode? colonType)
    {
        var type = colonType?.Type;

        ResolvedType? resolvedType = null;
        if (type != null)
        {
            // TODO: Not nice calling SemanticModel.LookupBuiltIn() here.
            // TODO: Feels like there are two apis doing similar things (SemanticModel's lookup type, and binder's lookup type)
            // leading to both places having to call SemanticModel.LookupBuiltIn() after trying to lookup the identifier.
            var boundType = LookupIdentifier(type.Identifier) as TypeSymbol ?? SemanticModel.LookupBuiltIn(type.Identifier);
            if (boundType != null)
                resolvedType = ResolvedType.Typed(boundType);
        }
        else
        {
            var boundType = SemanticModel.InferTypeOfDeclaringNode(parentNode);
            if (boundType != null)
                resolvedType = ResolvedType.Inferred(boundType);
        }

        return resolvedType ?? ResolvedType.Typed(new ErrorTypeSymbol(type?.Identifier));
    }
}

/// <summary>
/// Marks binders that have locals.
/// </summary>
internal interface IBinderHasLocals
{
    /// <summary>
    /// The locals introduced by the binder.
    /// </summary>
    ImmutableArray<LocalSymbol> Locals { get; }
}

internal class ModuleBinder(ModuleNode module) : Binder
{
    private ImmutableArray<Symbol> _decls;

    private readonly ModuleNode _module = module;

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

    public override Symbol? LookupIdentifierHere(IdentifierToken identifier)
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

internal class TypeDeclBinder(NamedTypeSymbol symbol) : Binder
{
    protected readonly NamedTypeSymbol _symbol = symbol;

    public override Symbol? LookupIdentifierHere(IdentifierToken identifier)
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

internal class StructDeclBinder(
    StructDeclNode structDecl,
    StructTypeSymbol symbol) : TypeDeclBinder(symbol)
{
    private readonly StructDeclNode _structDecl = structDecl;
}

internal class ClassDeclBinder(
    ClassDeclNode classDecl,
    NamedTypeSymbol symbol) : TypeDeclBinder(symbol)
{
    private readonly ClassDeclNode _classDecl = classDecl;
}

internal class FnBinder(
    FnSigNode fnSig,
    FnSymbol fnSymbol) : Binder
{
    private readonly FnSigNode _fnSig = fnSig;
    private readonly FnSymbol _fnSymbol = fnSymbol;

    public override Symbol? LookupIdentifierHere(IdentifierToken identifier)
    {
        return LookupIdentifierIn(_fnSymbol.Params, identifier);
    }
}

internal class BlockExprBinder(BlockExprNode block) : Binder, IBinderHasLocals
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

    public override Symbol? LookupIdentifierHere(IdentifierToken identifier)
    {
        for (var i = Locals.Length - 1; i >= 0; i--)
        {
            var local = Locals[i];
            if (local.Identifier != null && local.Identifier.Value == identifier.Value)
            {
                return local;
            }
        }
        return null;
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
