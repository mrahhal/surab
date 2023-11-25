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

    internal Scope Scope { get; private set; } = default!;

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

    internal void SetContextScope(Scope scope)
    {
        Scope = scope;
    }

    public override string ToString() => Name ?? string.Empty;
}

/// <summary>
/// Marks symbols that have a type.
/// </summary>
public interface ISymbolHasType
{
    /// <summary>
    /// Gets the type of the symbol.
    /// </summary>
    public TypeSymbol Type { get; }

    /// <summary>
    /// Gets whether the type of the symbol was inferred rather than typed.
    /// </summary>
    public bool IsTypeInferred { get; }
}

public sealed class ConstSymbol : Symbol, ISymbolHasType
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

    public TypeSymbol Type => EnsureResolveType().Type;

    public bool IsTypeInferred => EnsureResolveType().IsInferred;

    private ResolvedType EnsureResolveType()
    {
        return _type ??= Scope.ResolveType(_node, _node.Type);
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

    public bool IsPublic => _node.Parent is FnDeclNode fnDeclNode && fnDeclNode.Attrs.HasAttr(KnownAttrs.Public);

    public bool IsExtern => _node.Parent is FnDeclNode fnDeclNode && fnDeclNode.Attrs.HasAttr(KnownAttrs.Extern);

    /// <summary>
    /// The return type of the function.
    /// </summary>
    public TypeSymbol ReturnType
    {
        get
        {
            return _returnType ??= _node.ReturnTy?.Type == null ?
                BuiltInTypeSystem.None :
                Scope.ResolveType(_node, _node.ReturnTy).Type; // Will never be inferred.
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
            if (Scope.SemanticModel.TryGetDeclaredSymbol(paramNode, out var symbol))
            {
                builder.Add((ParamSymbol)symbol);
            }
        }
        return builder.ToImmutable();
    }
}

public abstract class FieldSymbol : Symbol, ISymbolHasType
{
    public override SymbolKind Kind => SymbolKind.Field;

    /// <summary>
    /// The type of the field.
    /// </summary>
    public abstract TypeSymbol Type { get; }

    public abstract bool IsTypeInferred { get; }

    /// <summary>
    /// The type symbol of the enclosing type (struct/class).
    /// </summary>
    public abstract TypeSymbol EnclosingType { get; }

    /// <summary>
    /// The field index in the fields array of its containing type.
    /// </summary>
    public abstract int FieldIndex { get; }

    public abstract bool IsPublic { get; }
}

internal sealed class FieldSymbolImpl : FieldSymbol, ISymbolHasType
{
    private readonly FieldDeclNode _node;
    private ResolvedType? _type;
    private TypeSymbol? _enclosingType;

    internal FieldSymbolImpl(FieldDeclNode node, int fieldIndex)
    {
        _node = node;
        FieldIndex = fieldIndex;
    }

    public override SymbolKind Kind => SymbolKind.Field;

    public override SyntaxNode? DeclaringNode => _node;

    public override IdentifierToken? Identifier => _node.Identifier;

    public override TypeSymbol Type => EnsureResolveType().Type;

    public override bool IsTypeInferred => EnsureResolveType().IsInferred;

    public override TypeSymbol EnclosingType => EnsureResolveEnclosingType();

    public override int FieldIndex { get; }

    public override bool IsPublic => _node.Attrs.HasAttr(KnownAttrs.Public);

    private ResolvedType EnsureResolveType()
    {
        return _type ??= Scope.ResolveType(_node, _node.Type);
    }

    private TypeSymbol EnsureResolveEnclosingType()
    {
        return _enclosingType ??= ResolveEnclosingType();

        TypeSymbol ResolveEnclosingType()
        {
            var enclosingType = Scope.LookupTypeDecl();
            return enclosingType ?? new ErrorTypeSymbol(null);
        }
    }
}

public sealed class ParamSymbol : Symbol, ISymbolHasType
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

    public TypeSymbol Type
    {
        get
        {
            return _type ??= Scope.ResolveType(_node, _node.Type).Type; // Will never be inferred.
        }
    }

    // Param type is always typed.
    public bool IsTypeInferred => false;
}

public sealed class LocalSymbol : Symbol, ISymbolHasType
{
    private readonly VarStmtNode _node;
    private ResolvedType? _type;

    internal LocalSymbol(VarStmtNode node)
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
        return _type ??= Scope.ResolveType(_node, _node.Type);
    }
}

public abstract class TypeSymbol : Symbol
{
    private ImmutableArray<Symbol> _members;

    public bool IsBuiltIn => BuiltInTypeSystem.IsBuiltIn(this);

    public bool IsScalar =>
        this == BuiltInTypeSystem.None ||
        this == BuiltInTypeSystem.Number ||
        this == BuiltInTypeSystem.Char ||
        this == BuiltInTypeSystem.Bool;

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
            if (Scope.SemanticModel.TryGetDeclaredSymbol(node, out var symbol))
            {
                builder.Add(symbol);
            }
        }
        return builder.ToImmutable();
    }

    public Symbol? GetMember(string name)
    {
        return Scope.LookupIdentifierHere(name);
    }

    protected abstract IEnumerable<SyntaxNode> EnumerateMembers();
}

public abstract class NamedTypeSymbol : TypeSymbol
{
    private readonly DeclNode? _node;

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
        foreach (var member in DeclaringNode.Members.Members)
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
        foreach (var member in DeclaringNode.Members.Members)
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
