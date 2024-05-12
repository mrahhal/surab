using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Surab.Analysis;

namespace Surab.Compilation;

internal class LoweringInterner
{
	private readonly record struct FnClosedValueKey(HirGenericFnValue Target, TypeListKey GenericArgs);

	private readonly struct TypeListKey(IReadOnlyList<HirType> types) : IEquatable<TypeListKey>
	{
		public IReadOnlyList<HirType> Types { get; } = types;

		public override bool Equals(object? obj) => obj is TypeListKey other && Equals(other);

		public bool Equals(TypeListKey other) => CollectionHelper.AreEqual(Types, other.Types);

		public override int GetHashCode() => HashCodeCombiner.Combine(Types);
	}

	private readonly record struct PtrTypeKey(HirType? InnerType);

	private readonly record struct FnTypeKey(TypeListKey ParamTypes, HirType ReturnType);

	private readonly record struct StructClosedTypeKey(HirGenericStructType Target, TypeListKey GenericArgs);


	private readonly ConcurrentDictionary<Symbol, HirValue> _valueMap = [];
	// Values are always added to these maps even if they exist in symbol map.
	private readonly ConcurrentDictionary<FnClosedValueKey, HirClosedGenericFnValue> _fnClosedValueMap = [];

	private readonly ConcurrentDictionary<TypeSymbol, HirType> _typeMap = [];
	// Types are always added to these maps even if they exist in symbol map.
	private readonly ConcurrentDictionary<PtrTypeKey, HirPtrType> _ptrTypeMap = [];
	private readonly ConcurrentDictionary<FnTypeKey, HirFnType> _fnTypeMap = [];
	private readonly ConcurrentDictionary<StructClosedTypeKey, HirClosedGenericStructType> _structClosedTypeMap = [];

	public LoweringInterner()
	{
	}

	// These depend on symbols being interned too so that a single instance is maintained
	// for each unique symbol (e.g. symbols with different generic args) as they're used as keys.

	public HirFnValue LowerValue(FnSymbol symbol) => (HirFnValue)LowerValue((Symbol)symbol);

	public HirFieldValue LowerValue(FieldSymbol symbol) => (HirFieldValue)LowerValue((Symbol)symbol);

	public HirValue LowerValue(Symbol symbol)
	{
		Debug.Assert(symbol.Kind is not SymbolKind.Type && symbol is ISymbolHasType);

		return _valueMap.GetOrAdd(symbol, LowerValueSlow);
	}

	private HirValue LowerValueSlow(Symbol symbol)
	{
		var hasType = (ISymbolHasType)symbol;

		if (symbol is FieldSymbol field)
		{
			var structType = (HirStructType)LowerType(field.EnclosingType);
			return structType.Fields.First(x => x.FieldIndex == field.FieldIndex);
		}
		else if (symbol.Kind is SymbolKind.Param or SymbolKind.Local)
		{
			// Simple values without data.
			var kind = symbol.Kind switch
			{
				SymbolKind.Param => HirValueKind.Param,
				SymbolKind.Local => HirValueKind.Local,
				_ => throw new UnreachableException(),
			};
			var type = LowerType(hasType.Type);
			return new HirLocalOrParamValue(kind, symbol.Identifier, symbol.Name, symbol.FullName, type);
		}

		// Fns
		Debug.Assert(symbol.Kind is SymbolKind.Fn);
		var fnSymbol = (FnSymbol)symbol;

		var genericParams = fnSymbol.GetAllGenericParams();
		var genericArgs = fnSymbol.GetAllGenericArgs();

		var fnType = LowerType(fnSymbol.Type);
		if (genericParams.Count == 0)
		{
			Debug.Assert(genericArgs.Count == 0);
			var @params = fnSymbol.Params.Select(LowerValue).ToList();
			var receiverParam = CreateReceiverParam();
			if (receiverParam != null)
				@params.Insert(0, receiverParam);
			var returnType = LowerType(fnSymbol.ReturnType);
			return new HirNonGenericFnValue(
				fnSymbol.Identifier, fnSymbol.Name, fnSymbol.FullName, fnType,
				@params, returnType, fnSymbol.IsMain, fnSymbol.IsExtern);
		}
		else if (genericArgs.Count == 0)
		{
			var loweredGenericParams = genericParams.Select(LowerType).Cast<HirGenericParamType>().ToList();
			var @params = fnSymbol.Params.Select(LowerValue).ToList();
			var receiverParam = CreateReceiverParam();
			if (receiverParam != null)
				@params.Insert(0, receiverParam);
			var returnType = LowerType(fnSymbol.ReturnType);
			return new HirGenericFnValue(
				fnSymbol.Identifier, fnSymbol.Name, fnSymbol.FullName, fnSymbol.BuildFullPrefix(), fnType,
				loweredGenericParams, @params, returnType, fnSymbol.IsExtern);
		}
		else
		{
			Debug.Assert(genericParams.Count == genericArgs.Count);
			var genericDef = (HirGenericFnValue)LowerValue(fnSymbol.OriginalDef);
			var loweredGenericArgs = genericArgs.Select(LowerType).ToList();
			return GetClosedFnValue(genericDef, loweredGenericArgs);
		}

		HirValue? CreateReceiverParam()
		{
			if (fnSymbol.MethodReceiverType == null)
				return null;
			var receiverParamType = LowerType(fnSymbol.MethodReceiverType);
			var name = "this";
			return new HirLocalOrParamValue(HirValueKind.Param, name, name, name, receiverParamType);
		}

		throw new UnreachableException();
	}

	public HirStructType LowerType(NamedTypeSymbol symbol) => (HirStructType)LowerType((TypeSymbol)symbol);

	public HirFnType LowerType(FnTypeSymbol symbol) => (HirFnType)LowerType((TypeSymbol)symbol);

	public HirType LowerType(TypeSymbol symbol)
	{
		// We can strip away type aliases at this point in compilation.
		symbol = symbol.UnwrapTypeAliases();

		return _typeMap.GetOrAdd(symbol, LowerTypeSlow);
	}

	private HirType LowerTypeSlow(TypeSymbol symbol)
	{
		if (symbol.TypeKind is TypeSymbolKind.GenericParam)
		{
			return new HirGenericParamType(
				symbol.Identifier, symbol.Name, symbol.FullName);
		}
		else if (symbol.TypeKind is TypeSymbolKind.Fn)
		{
			var fnTypeSymbol = (FnTypeSymbol)symbol;
			var paramTypes = fnTypeSymbol.ParamTypes.Select(LowerType).ToList();
			var returnType = LowerType(fnTypeSymbol.ReturnType);
			return GetFnType(paramTypes, returnType);
		}
		else if (symbol.TypeKind is TypeSymbolKind.Ptr)
		{
			var ptrTypeSymbol = (PtrTypeSymbol)symbol;
			var innerType = ptrTypeSymbol.InnerType == null ? null : LowerType(ptrTypeSymbol.InnerType);
			return GetPtrType(innerType);
		}

		// Structs
		Debug.Assert(symbol.TypeKind is TypeSymbolKind.Struct);
		var namedTypeSymbol = (NamedTypeSymbol)symbol;

		var genericParams = namedTypeSymbol.GetAllGenericParams();
		var genericArgs = namedTypeSymbol.GetAllGenericArgs();

		if (genericParams.Count == 0)
		{
			Debug.Assert(genericArgs.Count == 0);
			var fields = CreateFields(namedTypeSymbol);
			var structType = new HirNonGenericStructType(
				namedTypeSymbol.Identifier, namedTypeSymbol.Name, namedTypeSymbol.FullName, namedTypeSymbol.KnownTypeTag,
				fields);
			foreach (var field in fields)
			{
				field.SetStructType(structType);
			}
			return structType;
		}
		else if (genericArgs.Count == 0)
		{
			var loweredGenericParams = genericParams.Select(LowerType).Cast<HirGenericParamType>().ToList();
			var fields = CreateFields(namedTypeSymbol);
			var structType = new HirGenericStructType(
				namedTypeSymbol.Identifier, namedTypeSymbol.Name, namedTypeSymbol.FullName, namedTypeSymbol.BuildFullPrefix(),
				namedTypeSymbol.KnownTypeTag, loweredGenericParams, fields);
			foreach (var field in fields)
			{
				field.SetStructType(structType);
			}
			return structType;
		}
		else
		{
			Debug.Assert(genericParams.Count == genericArgs.Count);
			var genericDef = (HirGenericStructType)LowerType(namedTypeSymbol.OriginalDef);
			var loweredGenericArgs = genericArgs.Select(LowerType).ToList();
			return GetClosedStructType(genericDef, loweredGenericArgs);
		}

		throw new UnreachableException();
	}

	private IReadOnlyList<HirFieldValue> CreateFields(NamedTypeSymbol symbol)
	{
		return symbol.Members.Fields().Select(f =>
		{
			var type = LowerType(f.Type);
			return new HirFieldValue(
				f.Identifier, f.Name, f.FullName,
				type, f.FieldIndex);
		}).ToList();
	}

	public HirClosedGenericFnValue GetClosedFnValue(HirGenericFnValue target, IReadOnlyList<HirType> genericArgs)
	{
		Debug.Assert(target.GenericParams.Count == genericArgs.Count);
		var key = new FnClosedValueKey(target, new TypeListKey(genericArgs));
		return _fnClosedValueMap.GetOrAdd(key, key =>
		{
			var fullPrefix = key.Target.FullPrefix;
			var identifierWithGeneric = CreateGenericName(key.Target.Identifier, key.GenericArgs.Types);
			var name = identifierWithGeneric;
			var fullName = $"{fullPrefix}{identifierWithGeneric}";
			var smap = new FlatSubstitutionMap();
			smap.AddGenericSubstitutions(key.Target.GenericParams, key.GenericArgs.Types);
			var fnType = (HirFnType)EnsureReplaceTypeDeep(key.Target.Type, smap);
			var replacedParams = key.Target.Params.Select(p =>
			{
				return new HirLocalOrParamValue(
					HirValueKind.Param, p.Identifier, p.Name, p.FullName,
					EnsureReplaceTypeDeep(p.Type, smap));
			}).ToList();
			var replacedReturnType = EnsureReplaceTypeDeep(key.Target.ReturnType, smap);

			return new HirClosedGenericFnValue(
				key.Target.Identifier, name, fullName, fnType,
				key.Target, key.GenericArgs.Types, replacedParams, replacedReturnType);
		});
	}

	private HirPtrType GetPtrType(HirType? innerType)
	{
		var key = new PtrTypeKey(innerType);
		return _ptrTypeMap.GetOrAdd(key, key =>
		{
			// REVIEW: Duplicate with PtrTypeSymbol's name?
			var name = $"*{key.InnerType?.Name}";
			return new HirPtrType(name, name, name, key.InnerType);
		});
	}

	private HirFnType GetFnType(IReadOnlyList<HirType> paramTypes, HirType returnType)
	{
		var key = new FnTypeKey(new TypeListKey(paramTypes), returnType);
		return _fnTypeMap.GetOrAdd(key, key =>
		{
			var name = string.Empty;
			return new HirFnType(name, name, name, key.ParamTypes.Types, returnType);
		});
	}

	public HirClosedGenericStructType GetClosedStructType(HirGenericStructType target, IReadOnlyList<HirType> genericArgs)
	{
		Debug.Assert(target.GenericParams.Count == genericArgs.Count);
		var key = new StructClosedTypeKey(target, new TypeListKey(genericArgs));
		return _structClosedTypeMap.GetOrAdd(key, key =>
		{
			var fullPrefix = key.Target.FullPrefix;
			var identifierWithGeneric = CreateGenericName(key.Target.Identifier, key.GenericArgs.Types);
			var name = identifierWithGeneric;
			var fullName = $"{fullPrefix}{identifierWithGeneric}";
			var smap = new FlatSubstitutionMap();
			smap.AddGenericSubstitutions(key.Target.GenericParams, key.GenericArgs.Types);
			var replacedFields = key.Target.Fields.Select(f =>
			{
				return new HirFieldValue(
					f.Identifier, f.Name, f.FullName,
					EnsureReplaceTypeDeep(f.Type, smap), f.FieldIndex);
			}).ToList();

			var structType = new HirClosedGenericStructType(
				key.Target.Identifier, name, fullName, key.Target.KnownTypeTag,
				key.Target, key.GenericArgs.Types, replacedFields);
			foreach (var field in replacedFields)
			{
				field.SetStructType(structType);
			}
			return structType;
		});
	}

	// REVIEW: Kinda duplicate with GenericHelper.CreateName.
	public static string CreateGenericName(string prefix, IReadOnlyList<HirType> genericTypes)
	{
		Debug.Assert(genericTypes.Count != 0);
		var genericTypesText = genericTypes.Select(g => g.Name);
		return $"{prefix}<{string.Join(", ", genericTypesText)}>";
	}

	/// <summary>
	/// Recursively replaces type usages from the given smap, taking into account the different ways
	/// types could be using generic types: S<T>, *T, fn(a: T) T, etc.
	/// replacedAction is only called on types that were actually replaced anywhere in the given type.
	/// </summary>
	[return: NotNullIfNotNull(nameof(type))]
	public HirType? EnsureReplaceTypeDeep(HirType? type, FlatSubstitutionMap smap, Action<HirType>? replacedAction = null)
	{
		if (type == null)
			return null;

		// The "deep" part is to enable the 2nd case, if we have T -> i32 in the smap:
		// - T -> i32
		// - A<B<T>> -> A<B<i32>>
		// This is very similar to the `var ogGenericArgs = ...` part in `Symbol.SubstituteWithHierarchyPatching`.

		if (type is HirPtrType ptrType && ptrType.InnerType != null)
		{
			var replaced = GetPtrType(EnsureReplaceTypeDeep(ptrType.InnerType, smap, replacedAction));
			CallReplacedAction(ptrType, replaced);
			return replaced;
		}
		else if (type is HirFnType fnType)
		{
			var replaced = GetFnType(
				fnType.ParamTypes.Select(paramType => EnsureReplaceTypeDeep(paramType, smap, replacedAction)).ToList(),
				EnsureReplaceTypeDeep(fnType.ReturnType, smap, replacedAction));
			CallReplacedAction(fnType, replaced);
			return replaced;
		}
		else if (type is HirGenericStructType genericStructType)
		{
			var replaced = GetClosedStructType(
				genericStructType,
				genericStructType.GenericParams.Select(smap.ReplaceGenericParam).ToList());
			CallReplacedAction(genericStructType, replaced);
			return replaced;
		}
		else if (type is HirClosedGenericStructType closedGenericStructType)
		{
			var genericDef = closedGenericStructType.GenericDef;
			var genericArgs = closedGenericStructType.GenericArgs;
			var replaced = GetClosedStructType(
				genericDef,
				genericArgs.Select(arg => EnsureReplaceTypeDeep(arg, smap, replacedAction)).ToList());
			CallReplacedAction(closedGenericStructType, replaced);
			return replaced;
		}

		// Replaced HirGenericParamType or same type will be returned.
		var simpleReplaced = smap.Replace(type);
		CallReplacedAction(type, simpleReplaced);
		return simpleReplaced;

		void CallReplacedAction(HirType original, HirType replaced)
		{
			if (replacedAction == null || original == replaced || original.Kind is HirTypeKind.GenericParam)
				return;
			replacedAction(replaced);
		}
	}
}
