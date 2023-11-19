using System.Collections.Immutable;
using System.Diagnostics;

namespace Surab.Analysis;

public enum SymbolKind
{
    Const,
    Fn,
    Field,
    Param,
    Local,

    NamedType, // Class, Struct
    ArrayType, // [Type]
}

[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
public abstract class Symbol
{
    public abstract SymbolKind Kind { get; }

    public Symbol? Parent { get; private set; }

    internal Binder Context { get; private set; } = default!;

    /// <summary>
    /// The node in the tree representing this symbol.
    /// This can be `null` when the declaration isn't in the src (built-in type, synthesized, external package, etc).
    /// </summary>
    public virtual SyntaxNode? DeclaringNode => null;

    public virtual IdentifierToken? Identifier => null;

    public string? Name => Identifier?.Value;

    internal void SetParent(Symbol? parent)
    {
        Parent = parent;
    }

    internal void SetContextBinder(Binder context)
    {
        Context = context;
    }

    public override string ToString() => Name ?? string.Empty;
}

public sealed class ConstSymbol : Symbol
{
    private readonly ConstDeclNode _node;
    private ResolvedType? _type;

    internal ConstSymbol(ConstDeclNode node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.Const;

    public override SyntaxNode? DeclaringNode => _node;

    public override IdentifierToken? Identifier => _node.Identifier;

    /// <summary>
    /// The type of the const.
    /// </summary>
    public TypeSymbol Type => EnsureResolveType().Type;

    public bool IsTypeInferred => EnsureResolveType().IsInferred;

    private ResolvedType EnsureResolveType()
    {
        return _type ??= Context.BindType(_node, _node.Type);
    }
}

public sealed class FnSymbol : Symbol
{
    private readonly FnSigNode _node;
    private TypeSymbol? _returnType;
    private ImmutableArray<ParamSymbol> _params;

    internal FnSymbol(FnSigNode node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.Fn;

    // Parent will be either a FunctionDeclNode or a MethodDeclNode.
    public override SyntaxNode? DeclaringNode => _node.Parent;

    public override IdentifierToken? Identifier => _node.Identifier;

    /// <summary>
    /// The return type of the function.
    /// </summary>
    public TypeSymbol ReturnType
    {
        get
        {
            return _returnType ??= _node.ReturnTy?.Type == null ?
                BuiltInTypeSystem.None :
                Context.BindType(_node, _node.ReturnTy).Type; // Will never be inferred.
        }
    }

    public ImmutableArray<ParamSymbol> Params
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
        foreach (var paramNode in _node.Params.Params)
        {
            if (Context.SemanticModel.TryGetDeclaredSymbol(paramNode, out var symbol))
            {
                builder.Add((ParamSymbol)symbol);
            }
        }
        return builder.ToImmutable();
    }
}

public abstract class FieldSymbol : Symbol
{
    public override SymbolKind Kind => SymbolKind.Field;

    /// <summary>
    /// The type of the field.
    /// </summary>
    public abstract TypeSymbol Type { get; }
}

internal sealed class FieldSymbolImpl : FieldSymbol
{
    private readonly FieldDeclNode _node;
    private ResolvedType? _type;

    internal FieldSymbolImpl(FieldDeclNode node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.Field;

    public override SyntaxNode? DeclaringNode => _node;

    public override IdentifierToken? Identifier => _node.Identifier;

    /// <summary>
    /// The type of the field.
    /// </summary>
    public override TypeSymbol Type => EnsureResolveType().Type;

    public bool IsTypeInferred => EnsureResolveType().IsInferred;

    private ResolvedType EnsureResolveType()
    {
        return _type ??= Context.BindType(_node, _node.Type);
    }
}

public sealed class ParamSymbol : Symbol
{
    private readonly ParamNode _node;
    private TypeSymbol? _type;

    internal ParamSymbol(ParamNode node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.Param;

    public override SyntaxNode? DeclaringNode => _node;

    public override IdentifierToken? Identifier => _node.Identifier;

    /// <summary>
    /// The type of the param.
    /// </summary>
    public TypeSymbol Type
    {
        get
        {
            return _type ??= Context.BindType(_node, _node.Type).Type; // Will never be inferred.
        }
    }
}

public sealed class LocalSymbol : Symbol
{
    private readonly LetStmtNode _node;
    private ResolvedType? _type;

    internal LocalSymbol(LetStmtNode node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.Local;

    public override SyntaxNode? DeclaringNode => _node;

    public override IdentifierToken? Identifier => _node.Identifier;

    /// <summary>
    /// The type of the local.
    /// </summary>
    public TypeSymbol Type => EnsureResolveType().Type;

    public bool IsTypeInferred => EnsureResolveType().IsInferred;

    private ResolvedType EnsureResolveType()
    {
        return _type ??= Context.BindType(_node, _node.Type);
    }
}

public abstract class TypeSymbol : Symbol
{
    public bool IsBuiltInType => this is BuiltInStructTypeSymbol;

    public bool IsScalar =>
        this == BuiltInTypeSystem.None ||
        this == BuiltInTypeSystem.Number ||
        this == BuiltInTypeSystem.Char ||
        this == BuiltInTypeSystem.Bool;
}

public abstract class NamedTypeSymbol : TypeSymbol
{
    private readonly DeclNode? _node;
    private ImmutableArray<Symbol> _members;

    internal NamedTypeSymbol(DeclNode? node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.NamedType;

    public override DeclNode? DeclaringNode => _node;

    public override IdentifierToken? Identifier => _node switch
    {
        ClassDeclNode classDecl => classDecl.Identifier,
        StructDeclNode structDecl => structDecl.Identifier,
        _ => null,
    };

    public bool IsError => this is ErrorTypeSymbol;

    public virtual ImmutableArray<Symbol> Members
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
        foreach (var node in EnumerateMembers())
        {
            if (Context.SemanticModel.TryGetDeclaredSymbol(node, out var symbol))
            {
                builder.Add(symbol);
            }
        }
        return builder.ToImmutable();
    }

    protected abstract IEnumerable<SyntaxNode> EnumerateMembers();
}

public class StructTypeSymbol : NamedTypeSymbol
{
    internal StructTypeSymbol(StructDeclNode? node) : base(node)
    {
    }

    public new StructDeclNode? DeclaringNode => (StructDeclNode?)base.DeclaringNode;

    protected override IEnumerable<SyntaxNode> EnumerateMembers()
    {
        Debug.Assert(DeclaringNode != null, "Unreachable in built in types.");
        foreach (var member in DeclaringNode.Members)
        {
            yield return member;
        }
    }
}

public class ClassTypeSymbol : NamedTypeSymbol
{
    internal ClassTypeSymbol(ClassDeclNode? node) : base(node)
    {
    }

    public new ClassDeclNode? DeclaringNode => (ClassDeclNode?)base.DeclaringNode;

    protected override IEnumerable<SyntaxNode> EnumerateMembers()
    {
        Debug.Assert(DeclaringNode != null, "Unreachable in built in types.");
        foreach (var member in DeclaringNode.Members)
        {
            yield return member;
        }
    }
}

/// <summary>
/// Represents a type symbol that failed to bind.
/// </summary>
public sealed class ErrorTypeSymbol : NamedTypeSymbol
{
    private readonly IdentifierToken? _identifier;

    internal ErrorTypeSymbol(IdentifierToken? identifier) : base(null)
    {
        _identifier = identifier;
    }

    public override IdentifierToken? Identifier => _identifier;

    protected override IEnumerable<SyntaxNode> EnumerateMembers()
    {
        yield break;
    }
}

public sealed class BuiltInStructTypeSymbol : StructTypeSymbol
{
    private readonly IdentifierToken _name;
    private readonly ImmutableArray<Symbol> _members;

    internal BuiltInStructTypeSymbol(
        IdentifierToken name,
        IEnumerable<Symbol> members) : base(null)
    {
        _name = name;
        _members = members.ToImmutableArray();
    }

    public override IdentifierToken? Identifier => _name;

    public override ImmutableArray<Symbol> Members => _members;
}

//public sealed class ArrayTypeSymbol : TypeSymbol
//{
//    public ArrayTypeSymbol(TypeSymbol elementType)
//    {
//        ElementType = elementType;
//    }

//    public override SymbolKind Kind => SymbolKind.ArrayType;

//    /// <summary>
//    /// The type symbol representing the element of the array. (i.e the `T` in `[T]`)
//    /// </summary>
//    public ResolvedType ElementType { get; }
//}

public static class SymbolCollectionExtensions
{
    public static IEnumerable<FieldSymbol> Fields<TSymbol>(this IEnumerable<TSymbol> collection)
        where TSymbol : Symbol
    {
        return collection.OfType<FieldSymbol>();
    }
}
