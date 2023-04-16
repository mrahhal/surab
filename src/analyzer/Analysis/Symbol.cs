using System.Collections.Immutable;

namespace Surab.Analyzer;

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

    public virtual IdentifierToken? Name => null;

    internal void SetParent(Symbol? parent)
    {
        Parent = parent;
    }

    internal void SetContextBinder(Binder context)
    {
        Context = context;
    }
}

public class ConstSymbol : Symbol
{
    private readonly ConstDeclarationNode _node;
    private TypeSymbol? _type;

    internal ConstSymbol(ConstDeclarationNode node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.Const;

    public override SyntaxNode? DeclaringNode => _node;

    public override IdentifierToken? Name => _node.Identifier;

    /// <summary>
    /// The type of the const. `null` here means it's an inferred type.
    /// </summary>
    public TypeSymbol? Type
    {
        get
        {
            return _type ??= Context.BindType(_node.Ty);
        }
    }
}

public class FunctionSymbol : Symbol
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

    public override IdentifierToken? Name => _node.Identifier;

    /// <summary>
    /// The return type of the function. `null` here is a semantic error, but we have to allow it in the tree.
    /// </summary>
    public TypeSymbol? ReturnType
    {
        get
        {
            return _returnType ??= Context.BindType(_node.ReturnTy);
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
            if (Context.SemanticModel.TryGetSymbol(parameterNode, out var symbol))
            {
                builder.Add((ParameterSymbol)symbol);
            }
        }
        return builder.ToImmutable();
    }
}

public class FieldSymbol : Symbol
{
    private readonly FieldDeclarationNode _node;
    private TypeSymbol? _type;

    internal FieldSymbol(FieldDeclarationNode node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.Field;

    public override SyntaxNode? DeclaringNode => _node;

    public override IdentifierToken? Name => _node.Identifier;

    /// <summary>
    /// The type of the field. `null` here is a semantic error, but we have to allow it in the tree.
    /// </summary>
    public TypeSymbol? Type
    {
        get
        {
            return _type ??= Context.BindType(_node.Ty);
        }
    }
}

public class ParameterSymbol : Symbol
{
    private readonly ParameterNode _node;
    private TypeSymbol? _type;

    internal ParameterSymbol(ParameterNode node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.Parameter;

    public override SyntaxNode? DeclaringNode => _node;

    public override IdentifierToken? Name => _node.Identifier;

    /// <summary>
    /// The type of the parameter. `null` here is a semantic error, but we have to allow it in the tree.
    /// </summary>
    public TypeSymbol? Type
    {
        get
        {
            return _type ??= Context.BindType(_node.Ty);
        }
    }
}

public class LocalSymbol : Symbol
{
    private readonly LetStatementNode _node;
    private TypeSymbol? _type;

    internal LocalSymbol(LetStatementNode node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.Local;

    public override SyntaxNode? DeclaringNode => _node;

    public override IdentifierToken? Name => _node.Identifier;

    /// <summary>
    /// The type of the local. `null` here means it's an inferred type.
    /// </summary>
    public TypeSymbol? Type
    {
        get
        {
            return _type ??= Context.BindType(_node.Ty);
        }
    }
}

public abstract class TypeSymbol : Symbol
{
}

public class NamedTypeSymbol : TypeSymbol
{
    private readonly DeclarationNode _node;
    private ImmutableArray<Symbol> _members;

    internal NamedTypeSymbol(DeclarationNode node)
    {
        _node = node;
    }

    public override SymbolKind Kind => SymbolKind.NamedType;

    public override DeclarationNode? DeclaringNode => _node;

    public override IdentifierToken? Name => _node switch
    {
        ClassDeclarationNode classDeclaration => classDeclaration.Identifier,
        _ => null,
    };

    public ImmutableArray<Symbol> Members
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
            if (Context.SemanticModel.TryGetSymbol(node, out var symbol))
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

public class BuiltInNamedTypeSymbol : NamedTypeSymbol
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

    public override IdentifierToken? Name => _name;

    public override ImmutableArray<Symbol> Members => _members;
}

public class ArrayTypeSymbol : TypeSymbol
{
    public override SymbolKind Kind => SymbolKind.ArrayType;

    /// <summary>
    /// The type symbol representing the element of the array. (i.e the `T` in `[T]`)
    /// </summary>
    public required TypeSymbol ElementType { get; init; }
}
