using System.Collections.Immutable;
using System.Diagnostics;

namespace Surab.Analysis;

public enum SymbolKind
{
    Const,
    Function,
    Field,
    Parameter,
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
    private readonly ConstDeclarationNode _node;
    private ResolvedType? _type;

    internal ConstSymbol(ConstDeclarationNode node)
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
        return _type ??= Context.BindType(_node, _node.Ty);
    }
}

public sealed class FunctionSymbol : Symbol
{
    private readonly FunctionSignatureNode _node;
    private TypeSymbol? _returnType;
    private ImmutableArray<ParameterSymbol> _parameters;

    internal FunctionSymbol(FunctionSignatureNode node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.Function;

    // Parent will be either a FunctionDeclarationNode or a MethodDeclarationNode.
    public override SyntaxNode? DeclaringNode => _node.Parent;

    public override IdentifierToken? Identifier => _node.Identifier;

    /// <summary>
    /// The return type of the function.
    /// </summary>
    public TypeSymbol ReturnType
    {
        get
        {
            return _returnType ??= _node.ReturnTy?.Ty == null ?
                BuiltInTypeSystem.None :
                Context.BindType(_node, _node.ReturnTy).Type; // Will never be inferred.
        }
    }

    public ImmutableArray<ParameterSymbol> Parameters
    {
        get
        {
            if (_parameters.IsDefault)
            {
                ImmutableInterlocked.InterlockedCompareExchange(ref _parameters, BuildParameters(), default);
            }

            return _parameters;
        }
    }

    private ImmutableArray<ParameterSymbol> BuildParameters()
    {
        var builder = ImmutableArray.CreateBuilder<ParameterSymbol>();
        foreach (var parameterNode in _node.Parameters.Parameters)
        {
            if (Context.SemanticModel.TryGetDeclaredSymbol(parameterNode, out var symbol))
            {
                builder.Add((ParameterSymbol)symbol);
            }
        }
        return builder.ToImmutable();
    }
}

public sealed class FieldSymbol : Symbol
{
    private readonly FieldDeclarationNode _node;
    private ResolvedType? _type;

    internal FieldSymbol(FieldDeclarationNode node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.Field;

    public override SyntaxNode? DeclaringNode => _node;

    public override IdentifierToken? Identifier => _node.Identifier;

    /// <summary>
    /// The type of the field.
    /// </summary>
    public TypeSymbol Type => EnsureResolveType().Type;

    public bool IsTypeInferred => EnsureResolveType().IsInferred;

    private ResolvedType EnsureResolveType()
    {
        return _type ??= Context.BindType(_node, _node.Ty);
    }
}

public sealed class ParameterSymbol : Symbol
{
    private readonly ParameterNode _node;
    private TypeSymbol? _type;

    internal ParameterSymbol(ParameterNode node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.Parameter;

    public override SyntaxNode? DeclaringNode => _node;

    public override IdentifierToken? Identifier => _node.Identifier;

    /// <summary>
    /// The type of the parameter.
    /// </summary>
    public TypeSymbol Type
    {
        get
        {
            return _type ??= Context.BindType(_node, _node.Ty).Type; // Will never be inferred.
        }
    }
}

public sealed class LocalSymbol : Symbol
{
    private readonly LetStatementNode _node;
    private ResolvedType? _type;

    internal LocalSymbol(LetStatementNode node)
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
        return _type ??= Context.BindType(_node, _node.Ty);
    }
}

public abstract class TypeSymbol : Symbol
{
}

public class NamedTypeSymbol : TypeSymbol
{
    private readonly DeclarationNode? _node;
    private ImmutableArray<Symbol> _members;

    internal NamedTypeSymbol(DeclarationNode? node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.NamedType;

    public override DeclarationNode? DeclaringNode => _node;

    public override IdentifierToken? Identifier => _node switch
    {
        ClassDeclarationNode classDeclaration => classDeclaration.Identifier,
        _ => null,
    };

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

    private IEnumerable<SyntaxNode> EnumerateMembers()
    {
        switch (_node)
        {
            case ClassDeclarationNode classDeclaration:
                foreach (var member in classDeclaration.Members)
                {
                    yield return member;
                }
                break;

            default:
                break;
        }
    }
}

public sealed class ErrorTypeSymbol : NamedTypeSymbol
{
    private readonly IdentifierToken? _identifier;

    internal ErrorTypeSymbol(IdentifierToken? identifier) : base(null)
    {
        _identifier = identifier;
    }

    public override IdentifierToken? Identifier => _identifier;
}

public sealed class BuiltInNamedTypeSymbol : NamedTypeSymbol
{
    private readonly IdentifierToken _name;
    private readonly ImmutableArray<Symbol> _members;

    internal BuiltInNamedTypeSymbol(
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
