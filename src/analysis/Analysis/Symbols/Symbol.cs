using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Surab.Analysis;

public enum SymbolKind
{
	Module,
	Const,
	Fn,
	Field,
	Param,
	Local,
	Temp,
	This,
	Type,
	Impl,
}

/// <summary>
/// Represents a symbol that can be referenced, either by name (e.g. functions) or by special syntax (ptr type).
/// </summary>
[DebuggerDisplay("\\{{GetType().Name,nq}\\}: {ToString()}")]
public abstract class Symbol
{
	private Reasoner? _reasoner;
	private SymbolInterner? _symbolInterner;
	private Symbol? _parent;
	private string? _fullName;

	protected Symbol(SymbolKind kind)
	{
		Kind = kind;
	}

	public SymbolKind Kind { get; }

	// TODO: Do this better.
	internal Reasoner Reasoner
	{
		get
		{
			if (_reasoner == null)
			{
				throw new InvalidOperationException($"Reasoner was not set on symbol '{GetType().Name}'.");
			}
			return _reasoner;
		}
		set
		{
			_reasoner = value;
		}
	}

	// TODO: Do this better.
	internal SymbolInterner SymbolInterner
	{
		get
		{
			if (_symbolInterner == null)
			{
				throw new InvalidOperationException($"SymbolInterner was not set on symbol '{GetType().Name}'.");
			}
			return _symbolInterner;
		}
		set
		{
			_symbolInterner = value;
		}
	}

	/// <summary>
	/// The parent symbol enclosing this symbol.
	/// For symbols not tied to a particular file or module, this can be null.
	/// For top level symbols in the file, this can be a <see cref="ModuleSymbol"/>.
	/// For top level modules, this will be null.
	/// </summary>
	public Symbol? Parent => _parent;

	/// <summary>
	/// The non module parent enclosing this symbol.
	/// Meaning, if the parent is a module, this will be null.
	/// </summary>
	public Symbol? NonModuleParent
	{
		get
		{
			if (Parent is not ModuleSymbol)
			{
				return Parent;
			}
			return null;
		}
	}

	/// <summary>
	/// The child symbols under this symbol. Will only be defined for non substituted symbols.
	///
	/// The use case is to view a symbol document from source. Use particular symbol props for normal usage.
	/// </summary>
	public virtual IEnumerable<Symbol> Children => [];

	/// <summary>
	/// The original definition of this symbol as defined in the source in case this is a substituted symbol (e.g. closed generic).
	/// </summary>
	public abstract Symbol OriginalDef { get; }

	/// <summary>
	/// The node in the tree representing this symbol.
	/// This can be `null` when the declaration isn't in the source (synthesized, etc).
	/// </summary>
	public abstract SyntaxNode? DeclaringNode { get; }

	/// <summary>
	/// The project file that contains this symbol.
	/// </summary>
	public ProjectFile? DeclaringFile => DeclaringNode?.Tree.File;

	/// <summary>
	/// The span of this symbol.
	/// </summary>
	public virtual TextSpan Span => DeclaringNode?.Span ?? TextSpan.Empty;

	/// <summary>
	/// The location of this symbol.
	/// </summary>
	public TextLocation? Location => DeclaringNode == null ? null : new TextLocation(DeclaringFile!.Path, Span);

	/// <summary>
	/// The <see cref="Analysis.IdentifierToken"/> of the symbol if it has one. Implementing this enables other props such as <see cref="Identifier"/> and <see cref="SelectionSpan"/>.
	/// </summary>
	internal abstract IdentifierToken? IdentifierToken { get; }

	/// <summary>
	/// The identifier of the symbol if it has one or an empty string.
	/// </summary>
	public virtual string Identifier => IdentifierToken?.Value ?? string.Empty;

	public bool IdentifierMatches(Symbol otherSymbol)
	{
		if (string.IsNullOrWhiteSpace(otherSymbol.Identifier))
			return false;
		return IdentifierMatches(otherSymbol.Identifier);
	}

	public bool IdentifierMatches(string? identifier)
	{
		if (string.IsNullOrWhiteSpace(identifier))
			return false;
		return Identifier == identifier;
	}

	/// <summary>
	/// The selection span. The difference between this and the normal span is that the selection
	/// span is that of the main identifier of this symbol if it exists (usually the symbol's name).
	/// </summary>
	public TextSpan SelectionSpan => IdentifierToken?.Span ?? TextSpan.Empty;

	/// <summary>
	/// The name of the symbol if it has one or an empty string.
	/// </summary>
	public virtual string Name => IdentifierToken?.Value ?? string.Empty;

	/// <summary>
	/// If this symbol has a name, this will be the full name of the symbol including the module, any enclosing symbols, etc.
	/// e.g. "main.foo::S<T>.a"
	/// </summary>
	public string FullName => _fullName ??= BuildFullName();

	/// <summary>
	/// The <see cref="ModuleSymbol"/> this symbol is in.
	/// If `this` itself is a <see cref="ModuleSymbol"/> then `this` is returned.
	/// </summary>
	public ModuleSymbol Module
	{
		get
		{
			if (this is ModuleSymbol module)
				return module;
			// Climb up parents until we find the module.
			var current = Parent;
			while (current != null)
			{
				if (current is ModuleSymbol module2)
					return module2;
				current = current.Parent;
			}
			throw new UnreachableException();
		}
	}

	/// <summary>
	/// The type symbol of the enclosing type if any.
	/// </summary>
	public TypeSymbol? EnclosingType
	{
		get
		{
			return Parent switch
			{
				TypeSymbol type => type,
				ImplSymbol impl => impl.EnclosingType,
				_ => throw new UnreachableException(),
			};
		}
	}

	/// <summary>
	/// The enclosing impl symbol if this is a part of an impl.
	/// </summary>
	public ImplSymbol? EnclosingImpl
	{
		get
		{
			if (Parent is ImplSymbol impl)
			{
				return impl;
			}
			return null;
		}
	}

	/// <summary>
	/// Returns a symbol's hierarchy as a list, starting from the top level symbol down to this symbol.
	/// S<T>.M<i64>.N<f64> -> [S<T>, M<i64>, N<f64>]
	/// </summary>
	internal List<Symbol> GetHierarchy()
	{
		// TODO: Consider caching this.
		// REVIEW: Should this support modules too?
		var list = new List<Symbol>();
		var current = this;
		while (current != null && current is not ModuleSymbol)
		{
			list.Add(current);
			current = current.Parent;
		}
		list.Reverse();
		return list;
	}

	// TODO: This is only here temporarily. To be replaced by lowering to a new value/type.
	/// <summary>
	/// Gets all generic params, both explicit (on the symbol itself) and implicit (inherited from
	/// a parent symbol).
	///
	/// This is used so that if we have an fn such as: `struct S<T1> { f<T2>() { } }`, then
	/// [T1, T2] will be extracted.
	/// </summary>
	internal List<GenericParamTypeSymbol> GetAllGenericParams()
	{
		var hierarchy = GetHierarchy();
		var list = new List<GenericParamTypeSymbol>();

		foreach (var symbol in hierarchy)
		{
			if (symbol is IPossiblyGenericSymbol genericSymbol)
			{
				list.AddRange(genericSymbol.GenericParams);
			}
		}

		return list;
	}

	// TODO: This is only here temporarily. To be replaced by lowering to a new value/type.
	/// <summary>
	/// Gets all generic args, both explicit (on the symbol itself) and implicit(inherited
	/// from a parent symbol).
	///
	/// This is used so that if we have a call expr such as: `S<i32>.f<f64>()`, then
	/// [i32, f64] will be extracted, which would correspond to the GenericParams of that
	/// lowered fn in the same order.
	/// </summary>
	internal List<TypeSymbol> GetAllGenericArgs()
	{
		var hierarchy = GetHierarchy();
		var list = new List<TypeSymbol>();

		foreach (var symbol in hierarchy)
		{
			if (symbol is IPossiblyGenericSymbol genericSymbol)
			{
				list.AddRange(genericSymbol.GenericArgs);
			}
		}

		return list;
	}

	internal bool TryGetGenericParams([NotNullWhen(true)] out ImmutableArray<GenericParamTypeSymbol> genericParams)
	{
		genericParams = default;
		if (this is IPossiblyGenericSymbol genericSymbol && genericSymbol.GenericParams.Any())
		{
			genericParams = genericSymbol.GenericParams;
			return true;
		}
		return false;
	}

	internal bool HasGenericParams()
	{
		return TryGetGenericParams(out _);
	}

	internal bool TryGetGenericArgs([NotNullWhen(true)] out ImmutableArray<TypeSymbol>? genericArgs)
	{
		genericArgs = null;
		if (this is IPossiblyGenericSymbol genericSymbol && genericSymbol.GenericArgs.Any())
		{
			genericArgs = genericSymbol.GenericArgs;
			return true;
		}
		return false;
	}

	internal bool HasGenericArgs()
	{
		return TryGetGenericArgs(out _);
	}

	/// <summary>
	/// Goes over the hierarchy of the symbol, patching parents and replacing generic types according to the smap,
	/// eventually returning a new symbol (with a possibly different hierarchy).
	/// </summary>
	internal Symbol SubstituteWithHierarchyPatching(SubstitutionMap smap)
	{
		Debug.Assert(Kind is SymbolKind.Type, "Hierarchy patching should only be called on type symbols.");
		return SubstituteWithHierarchyPatching(OriginalDef.SymbolInterner, smap, (TypeSymbol)this);
	}

	private static Symbol SubstituteWithHierarchyPatching(
		SymbolInterner symbolInterner,
		SubstitutionMap smap,
		TypeSymbol symbol)
	{
		switch (symbol.TypeKind)
		{
			case TypeSymbolKind.Unknown:
				return symbol;

			case TypeSymbolKind.GenericParam:
			case TypeSymbolKind.Fn:
			case TypeSymbolKind.Ptr:
				return symbol.Substitute(smap);

			case TypeSymbolKind.Struct:
			case TypeSymbolKind.Trait:
			case TypeSymbolKind.Alias:
				// Continued after switch
				break;

			default:
				throw new UnreachableException();
		}

		var hierarchy = symbol.GetHierarchy();
		// Start from left, close and get next member, call sub on it to replace possible generics, until we reach the right.
		var prev = default(Symbol?);
		foreach (var current in hierarchy)
		{
			if (prev == null)
			{
				prev = TryCloseGeneric(current.OriginalDef, current);
			}
			else
			{
				// prev can only be a NamedTypeSymbol if we can dot into it.
				Debug.Assert(prev is NamedTypeSymbol);
				// Get the corresponding member under prev, and try to close that to get the right symbol.
				var member = ((NamedTypeSymbol)prev).Members.Single(m => m.OriginalDef == current.OriginalDef);
				prev = TryCloseGeneric(member, current);
			}
		}
		Debug.Assert(prev != null);
		return prev;

		Symbol TryCloseGeneric(Symbol symbol, Symbol og)
		{
			// symbol is the symbol we're trying to close. og is the original definition that might be already closed.
			// og takes precedence to assume the generic args over the replacements in the smap, b/c:
			// - `a: S<i32>` field inside `S<T>`, if we create `S<i64> {}`, `S<i64>.a`'s type should be from og (<i32>) and not from smap (<i64>).

			if (!symbol.HasGenericParams())
			{
				// Either pure non generic (i32, string), or it'll be already subd if inside a generic (S<T>.M).
				// Either way, return as is.
				return symbol;
			}
			var genericSymbol = (IPossiblyGenericSymbol)symbol;

			if (symbol == og)
			{
				return symbolInterner.CloseGeneric(
					symbol,
					smap);
			}

			if (!og.HasGenericArgs())
			{
				return symbol;
			}

			var ogGenericSymbol = (IPossiblyGenericSymbol)og;
			Debug.Assert(genericSymbol.GenericParams.Length == ogGenericSymbol.GenericArgs.Length);

			// This is important in cases such as `B2<B3<T>>`, we need to ensure the inner `B3<T>`
			// is properly subd before we try closing `B2<...>`.
			var ogGenericArgs = ogGenericSymbol.GenericArgs
				.Select(x => (TypeSymbol)x.SubstituteWithHierarchyPatching(smap))
				.ToList();

			var smapSub = smap.CreateSub();
			smapSub.AddGenericSubstitutions(
				genericSymbol.GenericParams,
				ogGenericArgs);
			return symbolInterner.CloseGeneric(
				symbol,
				smapSub);
		}
	}

	internal Symbol SubstituteAndSetParent(Symbol? parent, SubstitutionMap smap)
	{
		Debug.Assert(
			(parent == null && Parent == null) ||
			(parent != null && Parent != null && parent.Kind == Parent.Kind));
		var subd = SubstituteCore(smap);
		Debug.Assert(subd is not SrcNamedTypeSymbol);
		subd.SetParent(parent);
		return subd;
	}

	internal Symbol Substitute(SubstitutionMap smap)
	{
		var subd = SubstituteCore(smap);
		return subd;
	}

	/// <summary>
	/// Creates the subd symbol version of the current symbol directly.
	/// </summary>
	internal virtual Symbol SubstituteCore(SubstitutionMap smap)
	{
		throw new UnreachableException($"SubstituteCore isn't implemented on {GetType().Name}.");
	}

	internal virtual void SetParent(Symbol? parent)
	{
		_parent = parent;
	}

	private string BuildFullName()
	{
		if (Name == null)
			return string.Empty;

		var prefix = BuildFullPrefix();
		return prefix + Name;
	}

	internal string BuildFullPrefix()
	{
		if (Name == null)
			return string.Empty;

		var sb = new StringBuilder();
		var current = this;
		var parent = Parent;
		while (parent != null)
		{
			var delim = parent.Kind is SymbolKind.Module && current.Kind is not SymbolKind.Module ? "::" : ".";
			sb.Insert(0, delim);
			sb.Insert(0, parent.Name);
			current = parent;
			parent = parent.Parent;
		}

		return sb.ToString();
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

public interface ISymbolHasExternAttr
{
	public bool IsExtern { get; }
}

public static class SymbolExtensions
{
	public static bool IsType(this Symbol? symbol) => symbol?.Kind is SymbolKind.Type;

	public static TypeSymbol AsTypeSymbolOrUnknown(this Symbol? symbol)
	{
		return (symbol as TypeSymbol) ?? UnknownTypeSymbol.Create();
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
