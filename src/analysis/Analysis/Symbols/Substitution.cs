using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Surab.Analysis;

/// <summary>
/// Represents a possibly generic symbol.
/// </summary>
public interface IGenericSymbol
{
	/// <summary>
	/// A symbol's generic type params.
	/// </summary>
	ImmutableArray<GenericParamTypeSymbol> GenericParams { get; }

	/// <summary>
	/// A symbol's generic args substituted for generic params. If a generic param
	/// isn't substituted then the generic param will be returned.
	/// </summary>
	ImmutableArray<TypeSymbol> GenericArgs { get; }

	internal Symbol SubstituteConstructedGeneric(SubstitutionMap smap, IReadOnlyList<TypeSymbol> genericArgs);
}

public static class GenericSymbolExtensions
{
	/// <summary>
	/// Returns the effective generic types. Generic params if this is a definition, generic args if this is closed.
	/// </summary>
	public static IEnumerable<TypeSymbol> GetGenericTypes(this IGenericSymbol genericSymbol)
	{
		if (genericSymbol.GenericArgs.Any())
		{
			return genericSymbol.GenericArgs;
		}
		else
		{
			return genericSymbol.GenericParams;
		}
	}
}

internal static class GenericHelper
{
	public static string CreateName(IGenericSymbol genericSymbol, string identifier)
	{
		if (genericSymbol.GenericParams.Any())
		{
			var genericTypesText = genericSymbol.GetGenericTypes().Select(g => g.Name);
			return $"{identifier}<{string.Join(", ", genericTypesText)}>";
		}
		else
		{
			return identifier;
		}
	}
}

// TODO: Very similar to FnTypeKey. Consider refactoring these similar keys.
/// <summary>
/// Represents a key that is used to identify substituted generic symbols.
///
/// Some examples:
/// - S<i32>
/// - S<i32>.M<f64>
/// </summary>
[DebuggerDisplay("{ToString()}")]
internal readonly struct SubstitutedGenericSymbolKey : IEquatable<SubstitutedGenericSymbolKey>
{
	public SubstitutedGenericSymbolKey(
		Symbol symbol,
		IReadOnlyList<TypeSymbol> genericArgs)
	{
		Debug.Assert(genericArgs.Any(), "We can't have non generic symbols or ones that aren't being closed here.");

		Symbol = symbol;
		GenericArgs = genericArgs;
	}

	public readonly Symbol Symbol { get; }

	public readonly IReadOnlyList<TypeSymbol> GenericArgs { get; }

	public override bool Equals(object? obj) => obj is SubstitutedGenericSymbolKey other && Equals(other);

	public bool Equals(SubstitutedGenericSymbolKey other)
	{
		return
			TypesEqual(Symbol, other.Symbol) &&
			ArgsEqual(this, other);
	}

	private static bool TypesEqual(Symbol x, Symbol y) => x == y;

	private static bool ArgsEqual(SubstitutedGenericSymbolKey x, SubstitutedGenericSymbolKey y) => ArgsCompare(x, y, (x, y) => x == y);

	private static bool ArgsCompare(SubstitutedGenericSymbolKey x, SubstitutedGenericSymbolKey y, Func<TypeSymbol, TypeSymbol, bool> compare)
	{
		if (x.GenericArgs.Count != y.GenericArgs.Count)
			return false;
		for (var i = 0; i < x.GenericArgs.Count; i++)
		{
			if (!compare(x.GenericArgs[i], y.GenericArgs[i]))
				return false;
		}
		return true;
	}

	public override int GetHashCode()
	{
		unchecked
		{
			var hash = 92821;
			hash = hash * 29 + Symbol.GetHashCode();
			foreach (var p in GenericArgs)
			{
				hash = hash * 29 + p.GetHashCode();
			}
			return hash;
		}
	}

	public override string ToString()
	{
		IEnumerable<string?> args = [.. GenericArgs.Select(p => p.Name)];
		return $"{Symbol.Identifier}<{string.Join(", ", args)}>";
	}
}

internal class SubstitutionMap
{
	private readonly Dictionary<Symbol, Symbol> _map = [];

	private SubstitutionMap(SubstitutionMap? parent)
	{
		Parent = parent;
	}

	public SubstitutionMap? Parent { get; }

	public bool IsEmpty
	{
		get
		{
			if (_map.Count > 0)
				return false;
			return Parent?.IsEmpty ?? true;
		}
	}

	public static SubstitutionMap Create()
	{
		return new SubstitutionMap(null);
	}

	public SubstitutionMap CreateSub()
	{
		return new SubstitutionMap(this);
	}

	public void AddGenericSubstitutions(
		IReadOnlyList<TypeSymbol> genericParams,
		IReadOnlyList<TypeSymbol> genericArgs)
	{
		// Go up to the min of the two. Different lengths are possible in case of syntax errors.
		for (var i = 0; i < Math.Min(genericParams.Count, genericArgs.Count); i++)
		{
			AddSubstitution(genericParams[i], genericArgs[i]);
		}
	}

	private void AddSubstitution(Symbol from, Symbol to)
	{
		_map.Add(from.OriginalDef, to);
	}

	public bool Contains(Symbol symbol)
	{
		return
			_map.ContainsKey(symbol.OriginalDef) ||
			(Parent?.Contains(symbol) ?? false);
	}

	public List<TypeSymbol> ReplaceGenericTypes(IEnumerable<TypeSymbol> genericParams)
	{
		ArgumentNullException.ThrowIfNull(genericParams);
		return genericParams
			.Select(ReplaceGenericType)
			.Cast<TypeSymbol>()
			.ToList();
	}

	// Fully resolves transitive substitutions.
	private TypeSymbol ReplaceGenericType(TypeSymbol genericParam)
	{
		Debug.Assert(genericParam.TypeKind is TypeSymbolKind.GenericParam);
		var current = genericParam;
		while (current.TypeKind is TypeSymbolKind.GenericParam && Contains(current))
		{
			current = (TypeSymbol)Replace(current);
		}
		return current;
	}

	public Symbol Replace(Symbol symbol)
	{
		ArgumentNullException.ThrowIfNull(symbol);
		if (_map.TryGetValue(symbol.OriginalDef, out var subd))
		{
			return subd;
		}
		if (Parent != null)
		{
			return Parent.Replace(symbol.OriginalDef);
		}
		return symbol;
	}
}
