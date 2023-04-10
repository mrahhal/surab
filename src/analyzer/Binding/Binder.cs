using System.Collections.Immutable;

namespace Surab.Analyzer;

/// <summary>
/// A binder binds identifiers to symbols and exposes these symbpls at certain scopes.
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
        var current = this;
        while (current != null)
        {
            var result = current.LookupIdentifierHere(identifier);
            if (result != null)
            {
                return result;
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// Looks up `this` starting from the given binder's scope and walking up.
    /// </summary>
    public Symbol? LookupThis()
    {
        var current = this;
        while (current != null)
        {
            var result = current.LookupThisHere();
            if (result != null)
            {
                return result;
            }

            current = current.Parent;
        }

        return null;
    }

    // TODO: Seperate this into LookupIdentifierForType and LookupIdentifierForInstance?
    /// <summary>
    /// Looks up an identifier in this binder's scope.
    /// </summary>
    public virtual Symbol? LookupIdentifierHere(IdentifierToken identifier) => null;

    /// <summary>
    /// Looks up `this` in this binder's scope.
    /// </summary>
    public virtual Symbol? LookupThisHere() => null;

    protected static T? LookupIdentifierIn<T>(ImmutableArray<T> array, IdentifierToken identifier)
        where T : Symbol
    {
        // Move from bottom to top trying to match the identifier.
        for (var i = array.Length - 1; i >= 0; i--)
        {
            var item = array[i];
            if (item.Name != null && item.Name.Identifier == identifier.Identifier)
            {
                return item;
            }
        }
        return null;
    }

    public TypeSymbol? BindType(ColonTyNode? ty) => BindType(ty?.Ty);

    public TypeSymbol? BindType(TyNode? ty)
    {
        if (ty == null) return null;

        return LookupIdentifier(ty.Identifier) as TypeSymbol;
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

internal class ModuleBinder : Binder
{
    private ImmutableArray<Symbol> _declarations;

    private readonly ModuleNode _module;

    public ModuleBinder(ModuleNode module)
    {
        _module = module;
    }

    public ImmutableArray<Symbol> Declarations
    {
        get
        {
            if (_declarations.IsDefault)
            {
                ImmutableInterlocked.InterlockedCompareExchange(ref _declarations, BuildDeclarations(), default);
            }

            return _declarations;
        }
    }

    public override Symbol? LookupIdentifierHere(IdentifierToken identifier)
    {
        return LookupIdentifierIn(Declarations, identifier);
    }

    private ImmutableArray<Symbol> BuildDeclarations()
    {
        var builder = ImmutableArray.CreateBuilder<Symbol>();
        foreach (var declaration in _module.Declarations)
        {
            if (SemanticModel.TryGetSymbol(declaration, out var symbol))
            {
                builder.Add(symbol);
            }
        }
        return builder.ToImmutable();
    }
}

internal class ClassDeclarationBinder : Binder
{
    private readonly ClassDeclarationNode _classDeclaration;
    private readonly NamedTypeSymbol _symbol;

    public ClassDeclarationBinder(
        ClassDeclarationNode classDeclaration,
        NamedTypeSymbol symbol)
    {
        _classDeclaration = classDeclaration;
        _symbol = symbol;
    }

    public override Symbol? LookupIdentifierHere(IdentifierToken identifier)
    {
        return LookupIdentifierIn(_symbol.Members, identifier);
    }
}

internal class FunctionSignatureBinder : Binder
{
    private readonly FunctionSignatureNode _functionSignature;
    private readonly FunctionSymbol _functionSymbol;

    public FunctionSignatureBinder(
        FunctionSignatureNode functionSignature,
        FunctionSymbol functionSymbol)
    {
        _functionSignature = functionSignature;
        _functionSymbol = functionSymbol;
    }

    public override Symbol? LookupIdentifierHere(IdentifierToken identifier)
    {
        return LookupIdentifierIn(_functionSymbol.Parameters, identifier);
    }
}

internal class BlockExpressionBinder : Binder, IBinderHasLocals
{
    private ImmutableArray<LocalSymbol> _locals;

    private readonly BlockExpressionNode _block;

    public BlockExpressionBinder(BlockExpressionNode block)
    {
        _block = block;
    }

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
            if (local.Name != null && local.Name.Identifier == identifier.Identifier)
            {
                return local;
            }
        }
        return null;
    }

    private ImmutableArray<LocalSymbol> BuildLocals()
    {
        var builder = ImmutableArray.CreateBuilder<LocalSymbol>();
        foreach (var declaration in _block.Statements)
        {
            if (SemanticModel.TryGetSymbol(declaration, out var symbol) && symbol is LocalSymbol localSymbol)
            {
                builder.Add(localSymbol);
            }
        }
        return builder.ToImmutable();
    }
}
