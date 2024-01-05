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
	This,

	PtrType,
	NamedType,
	FnType,
	AliasType,
	UnknownType,
}

/// <summary>
/// Represents a symbol.
/// </summary>
[DebuggerDisplay("\\{{GetType().Name,nq}\\} {ToString()}")]
public abstract class Symbol
{
	private List<Symbol>? _children;

	protected Symbol(SymbolKind kind, Reasoner reasoner, SyntaxNode? declaringNode = null)
	{
		Kind = kind;
		Reasoner = reasoner;
		DeclaringNode = declaringNode;
	}

	public SymbolKind Kind { get; }

	public Reasoner Reasoner { get; }

	/// <summary>
	/// The node in the tree representing this symbol.
	/// This can be `null` when the declaration isn't in the src (synthesized, etc).
	/// </summary>
	public SyntaxNode? DeclaringNode { get; }

	/// <summary>
	/// The parent symbol enclosing this symbol.
	/// </summary>
	public Symbol? Parent { get; private set; }

	/// <summary>
	/// The child symbols under this symbol.
	/// </summary>
	public IEnumerable<Symbol> Children => _children ?? [];

	/// <summary>
	/// The scope enclosing this symbol. This is usually not null, except when the symbol is not uniquely created from code (FnType, Error).
	/// </summary>
	internal Scope? Scope { get; private set; }

	public SourceFile? DeclaringSourceFile => DeclaringNode?.Tree.SourceFile;

	public IdentifierToken? Identifier { get; protected set; }

	public virtual TextSpan Span => DeclaringNode?.Span ?? TextSpan.Empty;

	public virtual TextSpan SelectionSpan => Identifier?.Span ?? TextSpan.Empty;

	public virtual string? Name => Identifier?.Value;

	internal void SetParent(Symbol? parent)
	{
		Parent = parent;
		Parent?.AddChild(this);
	}

	private void AddChild(Symbol child)
	{
		_children ??= [];
		_children.Add(child);
	}

	internal void SetScope(Scope scope)
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

	internal ConstSymbol(Reasoner reasoner, ConstDeclNode node)
		: base(SymbolKind.Const, reasoner, node)
	{
		_node = node;
		Identifier = _node.Identifier;
	}

	public new ConstDeclNode DeclaringNode => _node;

	internal new Scope Scope => base.Scope!;

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
	private FnTypeSymbol? _typeWithoutReceiver;
	private ImmutableArray<ParamSymbol> _params;
	private TypeSymbol? _enclosingType;
	private TypeSymbol? _methodReceiverType;

	internal FnSymbol(Reasoner reasoner, FnSigNode node)
		// node.Parent will be either a FunctionDeclNode or a MethodDeclNode.
		: base(SymbolKind.Fn, reasoner, node.Parent)
	{
		_node = node;
		Identifier = _node.Identifier;
	}

	public new FnSigNode DeclaringNode => _node;

	// Parent will be either a FunctionDeclNode or a MethodDeclNode.
	public FnSigNode FnSigNode => _node;

	internal new Scope Scope => base.Scope!;

	public bool HasBody => _node.FnHasBody;

	/// <summary>
	/// Gets whether a function is an outer function. i.e. not a member of another symbol but directly under the module.
	/// </summary>
	public bool IsOuterFn => Parent == null;

	public bool IsMethod => _node.Parent is MethodDeclNode;

	public bool IsMain => IsOuterFn && Name == "main";

	public bool IsInitializerMethod => IsMethod && ((MethodDeclNode)_node.Parent!).IsInitializer;

	public bool IsPublic => _node.Attrs.HasAttr(KnownAttrs.Public);

	public bool IsStatic => _node.Attrs.HasAttr(KnownAttrs.Static);

	public bool IsExtern => _node.Attrs.HasAttr(KnownAttrs.Extern);

	TypeSymbol ISymbolHasType.Type => Type;

	/// <summary>
	/// The type symbol of the function.
	/// </summary>
	public FnTypeSymbol Type
	{
		get
		{
			return _type ??= Reasoner.GetFnType(_node.Parent!);
		}
	}

	/// <summary>
	/// The type symbol of the function without the receiver arg for non static methods.
	/// For functions or static methods, the same normal fn type will be returned.
	/// This is useful when analysis is done on a method being accessed through an instance (`a.b()`).
	/// </summary>
	public FnTypeSymbol TypeWithoutReceiver
	{
		get
		{
			if (_typeWithoutReceiver != null)
			{
				return _typeWithoutReceiver;
			}

			var normalType = Type;
			if (!IsMethod || IsStatic)
			{
				return normalType;
			}

			var key = new FnTypeKey(
				normalType.IsExtern,
				normalType.ReturnType,
				[.. normalType.ParamTypes[1..]]);
			return _typeWithoutReceiver = Reasoner.GetOrCreateUnnamedFnType(key);
		}
	}

	public bool IsTypeInferred => false;

	public TypeSymbol? EnclosingType => EnsureResolveEnclosingType();

	/// <summary>
	/// The type symbol of the receiver type (struct/class) if this is a non static method.
	/// </summary>
	public TypeSymbol? MethodReceiverType => EnsureResolveMethodReceiverType();

	/// <summary>
	/// The return type of the function.
	/// </summary>
	public TypeSymbol ReturnType => Type.ReturnType;

	/// <summary>
	/// Gets the fn's params.
	/// </summary>
	/// <remarks>
	/// This won't include the receiver for methods. Use <see cref="MethodReceiverType"/> to access the receiver type.
	/// </remarks>
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
		foreach (var paramNode in _node.Params)
		{
			if (Reasoner.TryGetDeclaredSymbol(paramNode, out var symbol))
			{
				builder.Add((ParamSymbol)symbol);
			}
		}
		return builder.ToImmutable();
	}

	private TypeSymbol? EnsureResolveEnclosingType()
	{
		if (!IsMethod)
			return null;

		return _enclosingType ??= ResolveEnclosingType();

		TypeSymbol ResolveEnclosingType()
		{
			var enclosingType = Scope.LookupTypeDecl();
			return enclosingType ?? UnknownTypeSymbol.Create(Reasoner);
		}
	}

	private TypeSymbol? EnsureResolveMethodReceiverType()
	{
		if (!IsMethod || IsStatic)
			return null;

		return _methodReceiverType ??= ResolveEnclosingType();

		TypeSymbol ResolveEnclosingType()
		{
			var enclosingType = EnclosingType;
			// TODO: Account for @receiver_by_val.
			return Reasoner.GetPtrType(enclosingType ?? UnknownTypeSymbol.Create(Reasoner));
		}
	}

	public override string ToString()
	{
		return $"{Identifier!.Value}({string.Join(", ", Params.Select(p => $"{p.Name}: {p.Type.Name}"))}) {ReturnType.Name}";
	}
}

public abstract class FieldSymbol : Symbol, ISymbolHasType
{
	internal FieldSymbol(Reasoner reasoner, SyntaxNode? node)
		: base(SymbolKind.Field, reasoner, node)
	{
	}

	internal new Scope Scope => base.Scope!;

	/// <summary>
	/// The type of the field.
	/// </summary>
	public abstract TypeSymbol Type { get; }

	public abstract bool IsTypeInferred { get; }

	public bool HasInitializer => InitializerExprNode != null;

	public abstract ExprNode? InitializerExprNode { get; }

	/// <summary>
	/// The type symbol of the enclosing type.
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

	internal FieldSymbolImpl(Reasoner reasoner, FieldDeclNode node, int fieldIndex)
		: base(reasoner, node)
	{
		_node = node;
		Identifier = _node.Identifier;
		FieldIndex = fieldIndex;
	}

	public new FieldDeclNode DeclaringNode => _node;

	public override TypeSymbol Type => EnsureResolveType().Type;

	public override bool IsTypeInferred => EnsureResolveType().IsInferred;

	public override ExprNode? InitializerExprNode => _node.Initializer?.Expr;

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
			return enclosingType ?? UnknownTypeSymbol.Create(Reasoner);
		}
	}
}

public sealed class ParamSymbol : Symbol, ISymbolHasType
{
	private readonly ParamNode _node;
	private TypeSymbol? _type;

	internal ParamSymbol(Reasoner reasoner, ParamNode node)
		: base(SymbolKind.Param, reasoner, node)
	{
		_node = node;
		Identifier = _node.Identifier;
	}

	public new ParamNode DeclaringNode => _node;

	public bool HasInitializer => InitializerExprNode != null;

	public ExprNode? InitializerExprNode => _node.Initializer?.Expr;

	internal new Scope Scope => base.Scope!;

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

	internal LocalSymbol(Reasoner reasoner, VarStmtNode node)
		: base(SymbolKind.Local, reasoner, node)
	{
		_node = node;
		Identifier = _node.Identifier;
	}

	public new VarStmtNode DeclaringNode => _node;

	internal new Scope Scope => base.Scope!;

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

[DebuggerDisplay("{ToString()}")]
internal readonly record struct ThisSymbolKey(TypeSymbol type)
{
	public readonly TypeSymbol Type { get; } = type;

	public override string ToString() => "this";
}

public sealed class ThisSymbol : Symbol, ISymbolHasType
{
	internal ThisSymbol(Reasoner reasoner, ThisSymbolKey key)
		: base(SymbolKind.This, reasoner, null)
	{
		Key = key;
	}

	public override string? Name => "this";

	internal ThisSymbolKey Key { get; }

	/// <summary>
	/// The type of `this`. Includes the pointer in the case the receiver is by ref.
	/// </summary>
	public TypeSymbol Type => Key.Type;

	public bool IsTypeInferred => false;
}

/// <summary>
/// Represents a temporary symbol that gets created in the lowering phase to be used in subsequent lowered nodes. (e.g. struct creation)
/// </summary>
public sealed class TempSymbol : Symbol, ISymbolHasType
{
	internal TempSymbol(Reasoner reasoner, TypeSymbol type)
		: base(SymbolKind.Local, reasoner)
	{
		Type = type;
	}

	public TypeSymbol Type { get; }

	public bool IsTypeInferred => false;
}

public enum KnownTypeTag
{
	None,

	i32,
	i64,
	u32,
	u64,
	f32,
	f64,

	@bool,

	@char,
	@string,
}

public readonly struct TypeCreationSymbol
{
	internal TypeCreationSymbol(Symbol symbol, bool isConflicting)
	{
		Symbol = symbol;
		IsConflicting = isConflicting;
	}

	public Symbol Symbol { get; }

	/// <summary>
	/// Gets whether this symbol conflicts with another creation symbol.
	/// </summary>
	public bool IsConflicting { get; }

	public readonly bool IsOptional => Symbol switch
	{
		FieldSymbol fieldSymbol => fieldSymbol.HasInitializer,
		ParamSymbol paramSymbol => paramSymbol.HasInitializer,
		_ => throw new UnreachableException(),
	};
}

/// <summary>
/// Represents a type symbol. Can be unnamed or generated.
/// </summary>
/// <remarks>
/// <see cref="IsAssignableFrom(TypeSymbol?)"/> should be used instead of referential equality.
/// </remarks>
public abstract class TypeSymbol : Symbol
{
	private ImmutableArray<Symbol> _members;
	private ImmutableArray<TypeCreationSymbol> _creationSymbols;
	private InitializerMethod? _initializerMethod = default;

	internal TypeSymbol(SymbolKind kind, Reasoner reasoner, SyntaxNode? node)
		: base(kind, reasoner, node)
	{
	}

	private readonly struct InitializerMethod
	{
		public MethodDeclNode? Node { get; init; }
	}

	public bool IsUnknown => this is UnknownTypeSymbol;

	public KnownTypeTag? KnownTypeTag { get; internal set; }

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

	/// <summary>
	/// Gets the members that can be set in a type creation expr. Can include both field and param symbols (from the initializer).
	/// </summary>
	public virtual ImmutableArray<TypeCreationSymbol> CreationSymbols
	{
		get
		{
			if (_creationSymbols.IsDefault)
			{
				ImmutableInterlocked.InterlockedCompareExchange(ref _creationSymbols, BuildCreationSymbols(), default);
			}

			return _creationSymbols;
		}
	}

	public MethodDeclNode? InitializerMethodDecl
	{
		get
		{
			if (!_initializerMethod.HasValue)
			{
				_initializerMethod = BuildInitializerMethod();
			}

			return _initializerMethod!.Value.Node;
		}
	}

	private ImmutableArray<Symbol> BuildMembers()
	{
		var builder = ImmutableArray.CreateBuilder<Symbol>();
		foreach (var node in EnumerateMembers())
		{
			if (Reasoner.TryGetDeclaredSymbol(node, out var symbol))
			{
				builder.Add(symbol);
			}
		}
		return builder.ToImmutable();
	}

	private InitializerMethod BuildInitializerMethod()
	{
		foreach (var node in EnumerateMembers())
		{
			if (node is MethodDeclNode methodDecl && methodDecl.IsInitializer)
			{
				// We process only the first init found. Multiple inits will produce an error.
				return new InitializerMethod { Node = methodDecl };
			}
		}
		return new InitializerMethod { Node = null };
	}

	private ImmutableArray<TypeCreationSymbol> BuildCreationSymbols()
	{
		var symbols = new List<Symbol>();

		foreach (var memberNode in EnumerateMembers())
		{
			// An uninitialized field
			if (memberNode is FieldDeclNode fieldDecl && Reasoner.TryGetDeclaredSymbol(memberNode, out var symbol) &&
				symbol is FieldSymbol fieldSymbol && !fieldSymbol.HasInitializer)
			{
				symbols.Add(symbol);
			}
		}

		// Initializer params
		if (InitializerMethodDecl != null)
		{
			foreach (var paramNode in InitializerMethodDecl.Sig.Params)
			{
				if (Reasoner.TryGetDeclaredSymbol(paramNode, out var symbol))
				{
					symbols.Add(symbol);
				}
				else
				{
					throw new UnreachableException();
				}
			}
		}

		var fields = symbols.OfType<FieldSymbol>().Select(x => new TypeCreationSymbol(x, false));
		var @params = symbols.OfType<ParamSymbol>().Select(x => new TypeCreationSymbol(x, isConflicting: fields.Any(f => f.Symbol.Name == x.Name)));

		return [.. fields, .. @params];
	}

	public Symbol? GetMember(IdentifierToken identifier) => GetMember(identifier.Value);

	public Symbol? GetMember(string name)
	{
		// REVIEW: Some duplication between symbols and scopes here (and GetCreationMember)?

		// Move from bottom to top trying to match the identifier.
		for (var i = Members.Length - 1; i >= 0; i--)
		{
			var item = Members[i];
			if (item.Identifier != null && item.Identifier.Value == name)
			{
				return item;
			}
		}
		return null;
	}

	public TypeCreationSymbol? GetCreationSymbol(IdentifierToken identifier) => GetCreationSymbol(identifier.Value);

	public TypeCreationSymbol? GetCreationSymbol(string name)
	{
		for (var i = CreationSymbols.Length - 1; i >= 0; i--)
		{
			var item = CreationSymbols[i];
			if (!item.IsConflicting && item.Symbol.Identifier != null && item.Symbol.Identifier.Value == name)
			{
				return item;
			}
		}
		return null;
	}

	internal virtual IEnumerable<SyntaxNode> EnumerateMembers()
	{
		yield break;
	}

	/// <summary>
	/// Unwraps aliases and return the actual type. The result should always be a non aliased type.
	/// </summary>
	public virtual TypeSymbol UnwrapAliases()
	{
		return this;
	}

	/// <summary>
	/// Unwraps pointers to return the inner type. The result should always be a non pointer type or null if it's an opaque pointer.
	/// This is useful because the compiler does auto derefing/addressing, so in most cases we want to pass through to the inner non pointer type for type analysis.
	/// </summary>
	public virtual TypeSymbol? UnwrapPointers()
	{
		return this;
	}

	/// <summary>
	/// If `true` is returned, the <paramref name="from"/> type can be assigned to this type. i.e. `this = <paramref name="from"/>` is valid.
	/// This also performs aliased type unwrapping on both sides.
	/// </summary>
	public bool IsAssignableFrom(TypeSymbol? from)
	{
		if (from == null) return false;
		return IsAssignableFromCore(UnwrapAliases(), from.UnwrapAliases());
	}

	public virtual bool IsAssignableFromCore(TypeSymbol to, TypeSymbol from)
	{
		return Equals(to, from);
	}
}

/// <summary>
/// Represents a key that is used to structurally compare fn types.
/// Any two fn types with the same signature will have equal keys.
/// </summary>
[DebuggerDisplay("{ToString()}")]
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
			ReturnTypesEqual(ReturnType, other.ReturnType) &&
			ParamTypesEqual(this, other);
	}

	public bool IsAssignableFrom(FnTypeKey other)
	{
		return
			IsExtern == other.IsExtern &&
			ReturnTypesAssignable(ReturnType, other.ReturnType) &&
			ParamTypesAssignable(this, other);
	}

	private static bool ReturnTypesEqual(TypeSymbol x, TypeSymbol y) => x == y;
	private static bool ReturnTypesAssignable(TypeSymbol x, TypeSymbol y) => x.IsAssignableFrom(y);

	private static bool ParamTypesEqual(FnTypeKey x, FnTypeKey y) => ParamTypesCompare(x, y, (x, y) => x == y);
	private static bool ParamTypesAssignable(FnTypeKey x, FnTypeKey y) => ParamTypesCompare(x, y, (x, y) => x.IsAssignableFrom(y));
	private static bool ParamTypesCompare(FnTypeKey x, FnTypeKey y, Func<TypeSymbol, TypeSymbol, bool> compare)
	{
		if (x.ParamTypes.Length != y.ParamTypes.Length)
			return false;
		for (var i = 0; i < x.ParamTypes.Length; i++)
		{
			if (!compare(x.ParamTypes[i], y.ParamTypes[i]))
				return false;
		}
		return true;
	}

	public override int GetHashCode()
	{
		// Shouldn't call UnwrapAliases() so we get different keys for different compatible types.
		// This is fine because analyzing compatibility uses IsAssignableFrom.

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
		IEnumerable<string?> @params = [.. ParamTypes.Select(p => p.Name)];
		return $"{(IsExtern ? "@extern " : "")}fn ({string.Join(", ", @params)}) {ReturnType.Name}";
	}
}

/// <summary>
/// Represents an fn type symbol.
/// A symbol will be created for each custom named fn type. Another shared symbol will be created for untyped fn types.
/// This symbol overrides equality so that comparison is structural instead of referential.
/// i.e. A named fn's fn type symbol and a method's fn type symbol and an alias' fn type symbol are equal if they're structurally compatible (signatures match).
/// </summary>
public class FnTypeSymbol : TypeSymbol
{
	// AliasDeclNode (node is null for FnDeclNode, MethodDeclNode because these create shared fn type symbols)
	private readonly DeclNode? _node;

	// node will be null for untyped fn types (fns and methods).
	internal FnTypeSymbol(Reasoner reasoner, DeclNode? node, FnTypeKey key)
		: base(SymbolKind.FnType, reasoner, node)
	{
		_node = node;
		Key = key;
	}

	internal FnTypeKey Key { get; }

	public bool IsExtern => Key.IsExtern;

	public TypeSymbol ReturnType => Key.ReturnType;

	/// <summary>
	/// Param types including the receiver if it exists.
	/// </summary>
	public ImmutableArray<TypeSymbol> ParamTypes => Key.ParamTypes;

	// TODO: If it's a named fn type (alias), display that instead?
	public override string? Name => Key.ToString();

	public override bool IsAssignableFromCore(TypeSymbol to, TypeSymbol from)
	{
		if (to is not FnTypeSymbol toFnType || from is not FnTypeSymbol fromFnType)
			return false;
		return toFnType.Key.IsAssignableFrom(fromFnType.Key);
	}

	public override string ToString()
	{
		return Key.ToString();
	}
}

/// <summary>
/// Represents a key that is used to structurally compare ptr types.
/// Any two ptr types with the same inner type will have equal keys.
/// </summary>
[DebuggerDisplay("{ToString()}")]
internal readonly record struct PtrTypeKey(TypeSymbol? innerType)
{
	public readonly TypeSymbol? InnerType { get; } = innerType;

	public bool IsAssignableFrom(PtrTypeKey other)
	{
		if (InnerType == null)
		{
			// Opaque ptr.
			return other.InnerType == null;
		}
		return InnerType.IsAssignableFrom(other.InnerType);
	}

	public override string ToString()
	{
		return $"*{InnerType?.Name}";
	}
}

public class PtrTypeSymbol : TypeSymbol
{
	internal PtrTypeSymbol(Reasoner reasoner, PtrTypeKey key)
		: base(SymbolKind.PtrType, reasoner, null)
	{
		Key = key;
	}

	internal PtrTypeKey Key { get; }

	/// <summary>
	/// Gets the inner type of the pointer. Will be null for an opaque pointer.
	/// </summary>
	public TypeSymbol? InnerType => Key.InnerType;

	public override string? Name => $"*{InnerType?.Name}";

	public override bool IsAssignableFromCore(TypeSymbol to, TypeSymbol from)
	{
		if (to is not PtrTypeSymbol toPtrType || from is not PtrTypeSymbol fromPtrType)
			return false;
		return toPtrType.Key.IsAssignableFrom(fromPtrType.Key);
	}

	public override TypeSymbol? UnwrapPointers()
	{
		return InnerType?.UnwrapPointers();
	}

	// REVIEW: Is this what we should really do? Shouldn't we depend on unwrapping instead?

	internal override IEnumerable<SyntaxNode> EnumerateMembers() => InnerType?.EnumerateMembers() ?? [];

	public override ImmutableArray<Symbol> Members => InnerType?.Members ?? [];

	public override string ToString()
	{
		return Key.ToString();
	}
}

public abstract class NamedTypeSymbol : TypeSymbol
{
	private readonly DeclNode? _node;

	internal NamedTypeSymbol(Reasoner reasoner, DeclNode? node)
		: base(SymbolKind.NamedType, reasoner, node)
	{
		_node = node;
		Identifier = _node switch
		{
			ClassDeclNode classDecl => classDecl.Identifier,
			StructDeclNode structDecl => structDecl.Identifier,
			_ => null,
		};
	}

	public new DeclNode? DeclaringNode => _node;
}

public class StructTypeSymbol : NamedTypeSymbol
{
	private readonly StructDeclNode? _node;

	internal StructTypeSymbol(Reasoner reasoner, StructDeclNode? node)
		: base(reasoner, node)
	{
		_node = node;
	}

	public new StructDeclNode? DeclaringNode => _node;

	internal override IEnumerable<SyntaxNode> EnumerateMembers()
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
	private readonly ClassDeclNode? _node;

	internal ClassTypeSymbol(Reasoner reasoner, ClassDeclNode? node)
		: base(reasoner, node)
	{
		_node = node;
	}

	public new ClassDeclNode? DeclaringNode => _node;

	internal override IEnumerable<SyntaxNode> EnumerateMembers()
	{
		Debug.Assert(DeclaringNode != null, "Unreachable in built in types.");
		foreach (var member in DeclaringNode.Members)
		{
			yield return member;
		}
	}
}

public class AliasTypeSymbol : TypeSymbol
{
	private readonly AliasDeclNode _node;
	private TypeSymbol? _type;

	internal AliasTypeSymbol(Reasoner reasoner, AliasDeclNode node)
		: base(SymbolKind.AliasType, reasoner, node)
	{
		_node = node;
		Identifier = _node.Identifier;
	}

	internal new Scope Scope => base.Scope!;

	public new AliasDeclNode DeclaringNode => _node;

	public TypeSymbol Type
	{
		get
		{
			return _type ??= Scope.ResolveType(_node.Type).Type;
		}
	}

	public override TypeSymbol UnwrapAliases()
	{
		// A second unwrap call on the Type is required in case the aliased type is another alias.
		return Type.UnwrapAliases();
	}

	// REVIEW: Is this what we should really do? Shouldn't we depend on unwrapping instead?

	internal override IEnumerable<SyntaxNode> EnumerateMembers() => Type.EnumerateMembers();

	public override ImmutableArray<Symbol> Members => Type.Members;
}

/// <summary>
/// Represents an unknown type symbol.
/// </summary>
public sealed class UnknownTypeSymbol : TypeSymbol
{
	private UnknownTypeSymbol(Reasoner reasoner, SyntaxNode? node, IdentifierToken? identifier)
		: base(SymbolKind.UnknownType, reasoner, node)
	{
		Identifier = identifier;
	}

	public override string? Name => Identifier?.Value ?? "_?_";

	public static UnknownTypeSymbol Create(Reasoner reasoner) => new(reasoner, null, null);

	public static UnknownTypeSymbol Create(Reasoner reasoner, SyntaxNode? node) => new(reasoner, node, null);

	public static UnknownTypeSymbol Create(Reasoner reasoner, IdentifierToken? identifier) => new(reasoner, null, identifier);

	public static UnknownTypeSymbol Create(Reasoner reasoner, SyntaxNode? node, IdentifierToken? identifier) => new(reasoner, node, identifier);
}

public static class SymbolExtensions
{
	public static bool IsType(this Symbol? symbol) => symbol is TypeSymbol;

	public static TypeSymbol AsTypeSymbolOrUnknown(this Symbol? symbol, Reasoner reasoner)
	{
		return (symbol as TypeSymbol) ?? UnknownTypeSymbol.Create(reasoner);
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
