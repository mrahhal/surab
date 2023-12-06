using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;

namespace Surab.Analysis;

public enum SymbolKind
{
    Const,
    Fn,
    Field,
    Param,
    Local,

    NamedType,
    FnType,
}

/// <summary>
/// Represents a symbol.
/// </summary>
/// <remarks>
/// <see cref="object.Equals(object?)"/> should be used instead of referential equality as some symbols will override equality.
/// </remarks>
[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
public abstract class Symbol
{
    public abstract SymbolKind Kind { get; }

    public Symbol? Parent { get; private set; }

    /// <summary>
    /// The scope enclosing this symbol. This is usually always not null, except when the symbol is manually created (FnType, Error).
    /// </summary>
    internal Scope? Scope { get; private set; }

    /// <summary>
    /// The node in the tree representing this symbol.
    /// This can be `null` when the declaration isn't in the src (built-in type, synthesized, external package, etc).
    /// </summary>
    public virtual SyntaxNode? DeclaringNode => null;

    public virtual IdentifierToken? Identifier => null;

    public virtual string? Name => Identifier?.Value;

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

    internal new Scope Scope => base.Scope!;

    public override SyntaxNode? DeclaringNode => _node;

    public override IdentifierToken? Identifier => _node.Identifier;

    public TypeSymbol Type => EnsureResolveType().Type;

    public bool IsTypeInferred => EnsureResolveType().IsInferred;

    private ResolvedType EnsureResolveType()
    {
        return _type ??= Scope.ResolveType(_node, _node.Type);
    }
}

public sealed class FnSymbol : Symbol, ISymbolHasType
{
    private readonly FnSigNode _node;
    private FnTypeSymbol? _type;
    private ImmutableArray<ParamSymbol> _params;

    internal FnSymbol(FnSigNode node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.Fn;

    internal new Scope Scope => base.Scope!;

    // Parent will be either a FunctionDeclNode or a MethodDeclNode.
    public override SyntaxNode? DeclaringNode => _node.Parent;

    public override IdentifierToken? Identifier => _node.Identifier;

    public bool HasBody => _node.FnHasBody;

    /// <summary>
    /// Gets whether a function is an outer function. i.e. not a member of another symbol but directly under the module.
    /// </summary>
    public bool IsOuterFn => Parent == null;

    public bool IsMain => IsOuterFn && Name == "main";

    public bool IsPublic => _node.Attrs.HasAttr(KnownAttrs.Public);

    public bool IsExtern => _node.Attrs.HasAttr(KnownAttrs.Extern);

    TypeSymbol ISymbolHasType.Type => Type;

    /// <summary>
    /// The type symbol of the function.
    /// </summary>
    public FnTypeSymbol Type
    {
        get
        {
            return _type ??= Scope.SemanticModel.GetFnType(_node.Parent!);
        }
    }

    public bool IsTypeInferred => false;

    /// <summary>
    /// The return type of the function.
    /// </summary>
    public TypeSymbol ReturnType => Type.ReturnType;

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

    internal new Scope Scope => base.Scope!;

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
            return enclosingType ?? ErrorTypeSymbol.General;
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

    internal new Scope Scope => base.Scope!;

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

    internal new Scope Scope => base.Scope!;

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

public enum TypeTag
{
    Invalid,
    ZeroSized,
    Numeric,
    Char,
    Bool,
    Struct,
    Ptr,
}

public abstract class TypeSymbol : Symbol
{
    private ImmutableArray<Symbol> _members;

    public bool IsError => this is ErrorTypeSymbol;

    public bool IsBuiltin => BuiltinTypeSystem.IsBuiltin(this);

    public bool IsScalar => GetTag() switch
    {
        TypeTag.Numeric or TypeTag.Char or TypeTag.Bool or TypeTag.Ptr => true,
        _ => false,
    };

    public TypeTag GetTag()
    {
        var type = this;
        if (type == BuiltinTypeSystem.None) return TypeTag.ZeroSized;
        if (type == BuiltinTypeSystem.@char) return TypeTag.Char;
        if (BuiltinTypeSystem.IsNumeric(type)) return TypeTag.Numeric;
        if (type == BuiltinTypeSystem.@bool) return TypeTag.Bool;
        if (type is StructTypeSymbol) return TypeTag.Struct;
        if (type is FnTypeSymbol) return TypeTag.Ptr;

        throw new UnreachableException();
    }

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
            Debug.Assert(Scope != null, "If symbol has members, it should have a scope.");
            if (Scope.SemanticModel.TryGetDeclaredSymbol(node, out var symbol))
            {
                builder.Add(symbol);
            }
        }
        return builder.ToImmutable();
    }

    public Symbol? GetMember(IdentifierToken identifier) => GetMember(identifier.Value);

    public Symbol? GetMember(string name)
    {
        return Scope?.LookupIdentifierHere(name);
    }

    protected virtual IEnumerable<SyntaxNode> EnumerateMembers()
    {
        yield break;
    }
}

/// <summary>
/// Represents a key that is used to structurally compare fn types.
/// Any two fn types with the same signature will have equal keys.
/// </summary>
internal readonly struct FnTypeKey(
    bool isExtern,
    TypeSymbol returnType,
    IEnumerable<TypeSymbol> paramTypes) : IEquatable<FnTypeKey>
{
    public readonly bool IsExtern { get; } = isExtern;
    public readonly TypeSymbol ReturnType { get; } = returnType;
    public readonly ImmutableArray<TypeSymbol> ParamTypes { get; } = paramTypes.ToImmutableArray();

    public override bool Equals(object? obj) => obj is FnTypeKey other && Equals(other);

    public bool Equals(FnTypeKey other)
    {
        return
            IsExtern == other.IsExtern &&
            Equals(ReturnType, other.ReturnType) &&
            ParamTypesEqual(this, other);
    }

    private static bool ParamTypesEqual(FnTypeKey x, FnTypeKey y)
    {
        if (x.ParamTypes.Length != y.ParamTypes.Length)
            return false;
        for (var i = 0; i < x.ParamTypes.Length; i++)
        {
            if (!Equals(x.ParamTypes[i], y.ParamTypes[i]))
                return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 92821;
            hash = hash * 29 + IsExtern.GetHashCode();
            hash = hash * 29 + ReturnType.GetHashCode();
            foreach (var p in ParamTypes)
            {
                hash = hash * 29 + p.GetHashCode();
            }
            return hash;
        }
    }

    public override string ToString()
    {
        return $"{(IsExtern ? "@extern() " : "")}fn ({string.Join(", ", ParamTypes.Select(p => p.Name))}) {ReturnType.Name}";
    }
}

/// <summary>
/// Represents an fn type symbol.
/// A symbol will be created for each custom named fn type. Another shared symbol will be created for untyped fn types.
/// This symbol overrides equality so that comparison is structural instead of referential.
/// i.e. A named fn's fn type symbol and a method's fn type symbol and an alias' fn type symbol are equal if they're structurally compatible (signatures match).
/// </summary>
public class FnTypeSymbol : TypeSymbol, IEquatable<FnTypeSymbol>
{
    // AliasDeclNode (node is null for FnDeclNode, MethodDeclNode because these create shared fn type symbols)
    private readonly DeclNode? _node;
    private readonly FnTypeKey _key;

    // node will be null for untyped fn types (fns and methods).
    internal FnTypeSymbol(DeclNode? node, FnTypeKey key)
    {
        _node = node;
        _key = key;
    }

    public override SymbolKind Kind => SymbolKind.FnType;

    //public new StructDeclNode? DeclaringNode => (StructDeclNode?)base.DeclaringNode;

    internal FnTypeKey Key => _key;

    public bool IsExtern => Key.IsExtern;

    public TypeSymbol ReturnType => Key.ReturnType;

    public ImmutableArray<TypeSymbol> ParamTypes => Key.ParamTypes;

    // TODO: If it's a named fn type (alias), display that instead?
    public override string? Name => _key.ToString();

    public override bool Equals(object? obj) => Equals(obj as FnTypeSymbol);

    public bool Equals(FnTypeSymbol? other)
    {
        if (other == null) return false;
        return Equals(_key, other._key);
    }

    public override int GetHashCode()
    {
        return _key.GetHashCode();
    }
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
public sealed class ErrorTypeSymbol : NamedTypeSymbol, IEquatable<ErrorTypeSymbol>
{
    private ErrorTypeSymbol() : base(null)
    {
    }

    // Do we need an error type taking an identifier?

    internal static readonly ErrorTypeSymbol General = new();

    public override bool Equals(object? obj) => Equals(obj as ErrorTypeSymbol);

    public bool Equals(ErrorTypeSymbol? other)
    {
        // No two error type symbols are equal.
        return false;
    }

    public override int GetHashCode()
    {
        throw new UnreachableException();
    }
}

public static class SymbolCollectionExtensions
{
    public static IEnumerable<FieldSymbol> Fields<TSymbol>(this IEnumerable<TSymbol> collection)
        where TSymbol : Symbol
    {
        return collection.OfType<FieldSymbol>();
    }
}
