using System.Diagnostics;
using Surab.Analysis;
using static Surab.Compilation.MonomorphizationCollection;

namespace Surab.Compilation;

internal class MonomorphizationCollect
{
	private readonly HirLoweredProject _project;
	private readonly LoweringInterner _interner;

	private MonomorphizationCollect(HirLoweredProject project, LoweringInterner interner)
	{
		_project = project;
		_interner = interner;
	}

	public static MonomorphizationCollection Collect(HirLoweredProject project, LoweringInterner interner)
	{
		var m = new MonomorphizationCollect(project, interner);
		return m.Collect();
	}

	private MonomorphizationCollection Collect()
	{
		// TODO: Do polymorphization by detecting unused generic types to decrease compilation time and binary size.

		// We're going to do this in two passes:
		// Pass 1: In each type and function, record all other direct usages of other generic types and functions.
		// Pass 2: In functions, follow concrete generic instantiations of types and functions, drill down
		// using the recorded usages from pass 1 and record the concretes for each.

		var recordedUsages = UsagesRecordingPass.Run(_project);
		var collection = ConcretesCollectionPass.Run(_project, _interner, recordedUsages);

		return collection;
	}

	// Records generic usages inside types and fns.
	private sealed class UsagesRecordingPass : HirTreeVisitor
	{
		private readonly Dictionary<HirValueOrType, Usage> _map = [];

		public UsagesRecordingPass()
		{
		}

		public static IReadOnlyDictionary<HirValueOrType, Usage> Run(HirLoweredProject project)
		{
			var pass = new UsagesRecordingPass();

			foreach (var tree in project.Trees)
			{
				pass.Visit(tree.Root);
			}

			return pass._map;
		}

		public override void VisitStructDecl(HirStructDecl node)
		{
			if (!node.Type.TryGetGenericParams(out var typeGenericParams))
				return;

			HashSet<HirGenericParamType> genericParamTypesUsed = [];
			HashSet<HirValueOrType> dependents = [];

			foreach (var field in node.Type.Fields)
			{
				if (ProcessTypeForPossibleGenericUsages(typeGenericParams, field.Type, genericParamTypesUsed) &&
					field.Type.Kind is not HirTypeKind.GenericParam)
				{
					dependents.Add(field.Type);
				}
			}

			if (genericParamTypesUsed.Count != 0)
			{
				_map.Add(node.Type, new Usage
				{
					GenericParamTypesUsed = [.. genericParamTypesUsed],
					Dependents = [.. dependents],
				});
			}
		}

		public override void VisitFnDecl(HirFnDecl node)
		{
			if (node.Body == null)
				return;

			if (!node.FnValue.TryGetGenericParams(out var fnGenericParams))
				return;

			var (genericParamTypesUsed, dependents) = FnDeclVisitor.Run(node, fnGenericParams);
			if (genericParamTypesUsed.Count != 0)
			{
				_map.Add(node.FnValue, new Usage
				{
					GenericParamTypesUsed = [.. genericParamTypesUsed],
					Dependents = [.. dependents],
				});
			}
		}

		private class FnDeclVisitor : HirTreeVisitor
		{
			private readonly IReadOnlyList<HirGenericParamType> _genericParams;
			private readonly HashSet<HirGenericParamType> _genericParamTypesUsed = [];
			private readonly HashSet<HirValueOrType> _dependents = [];

			private FnDeclVisitor(IReadOnlyList<HirGenericParamType> genericParams)
			{
				_genericParams = genericParams;
			}

			public static (HashSet<HirGenericParamType>, HashSet<HirValueOrType>) Run(HirFnDecl fnDecl, IReadOnlyList<HirGenericParamType> genericParams)
			{
				Debug.Assert(fnDecl.Body != null);
				var visitor = new FnDeclVisitor(genericParams);
				visitor.Walk(fnDecl);
				return (visitor._genericParamTypesUsed, visitor._dependents);
			}

			public override void Visit(HirNode node)
			{
				var values = node.EnumerateValues();
				IEnumerable<HirType> types = [.. node.EnumerateTypes(), .. node.EnumerateValues().Select(x => x.Type)];
				foreach (var value in values)
				{
					if (ProcessValueForPossibleGenericUsages(_genericParams, value, _genericParamTypesUsed))
					{
						_dependents.Add(value);
					}
				}
				foreach (var type in types)
				{
					if (ProcessTypeForPossibleGenericUsages(_genericParams, type, _genericParamTypesUsed) &&
						type.Kind is not HirTypeKind.GenericParam)
					{
						_dependents.Add(type);
					}
				}

				Walk(node);
			}
		}
	}

	// On fully closed symbols, collects concretes of used types and fns.
	private sealed class ConcretesCollectionPass : HirTreeVisitor
	{
		private readonly LoweringInterner _interner;
		private readonly Dictionary<HirValueOrType, IReadOnlyList<Concrete>> _concretesMap = [];
		private readonly IReadOnlyDictionary<HirValueOrType, Usage> _usageMap;

		private ConcretesCollectionPass(LoweringInterner interner, IReadOnlyDictionary<HirValueOrType, Usage> usageMap)
		{
			_usageMap = usageMap;
			_interner = interner;
		}

		public static MonomorphizationCollection Run(HirLoweredProject project, LoweringInterner interner, IReadOnlyDictionary<HirValueOrType, Usage> usageMap)
		{
			var pass = new ConcretesCollectionPass(interner, usageMap);

			foreach (var tree in project.Trees)
			{
				pass.Visit(tree.Root);
			}

			return new MonomorphizationCollection
			{
				ConcretesMap = pass._concretesMap,
				UsageMap = pass._usageMap,
			};
		}

		public override void Visit(HirNode node)
		{
			var values = node.EnumerateValues();
			IEnumerable<HirType> types = [.. node.EnumerateTypes(), .. values.Select(x => x.Type)];

			foreach (var value in values)
			{
				ProcessValue(value);
			}

			foreach (var type in types)
			{
				ProcessType(type);
			}

			Walk(node);
		}

		private void ProcessValue(HirValue value)
		{
			if (value.TryGetGenericArgs(out var genericArgs) &&
				value.GetIsFullyConcrete())
			{
				Debug.Assert(
					value.TryGetGenericParams(out var genericParams));
				var smap = new FlatSubstitutionMap();
				smap.AddGenericSubstitutions(genericParams, genericArgs);
				AddConcreteForSymbolDeep(value, smap);
			}
		}

		private void ProcessType(HirType type)
		{
			if (type.TryGetGenericArgs(out var genericArgs) &&
				type.GetIsFullyConcrete())
			{
				Debug.Assert(
					type.TryGetGenericParams(out var genericParams));
				var smap = new FlatSubstitutionMap();
				smap.AddGenericSubstitutions(genericParams, genericArgs);
				AddConcreteForSymbolDeep(type, smap);
			}
		}

		private void AddConcreteForSymbolDeep(HirValueOrType valueOrType, FlatSubstitutionMap smap)
		{
			var changed = AddConcreteForSymbol(valueOrType, smap);
			if (!changed)
			{
				// If this concrete was already added before, no need to traverse usages.
				return;
			}

			if (_usageMap.TryGetValue(valueOrType, out var usage))
			{
				foreach (var dependent in usage.Dependents)
				{
					AddConcreteForSymbolDeep(dependent, smap);
				}
			}
		}

		private bool AddConcreteForSymbol(HirValueOrType valueOrType, FlatSubstitutionMap smap)
		{
			// Symbol could be partially concrete (e.g. field of type `Map<i32, T>`).
			Debug.Assert(
				valueOrType.TryGetGenericArgs(out var genericArgs),
				"Should only be here if the value or type has generic args.");

			// If smap has T -> f64, then the following does:
			// <i32, T, Bar<T>> -> <i32, f64, Bar<f64>>
			var actualReplacements = genericArgs
				.Select(arg => _interner.EnsureReplaceTypeDeep(arg, smap, t =>
				{
					// Start another process for the generic args that are also generic and were replaced.
					// e.g. valueOrType is `S<Bar<i32>>`, we need to repeat the process for the inner `Bar<i32>`.
					if (t is HirClosedGenericStructType structClosedGenericType)
					{
						AddConcreteForSymbolDeep(structClosedGenericType, smap);
					}
				}))
				.ToList();

			return EnsureRecordConcrete(valueOrType, actualReplacements);
		}

		/// <summary>
		/// Ensure the concrete is recorded and returns whether or not a new concrete was actually added.
		/// </summary>
		private bool EnsureRecordConcrete(HirValueOrType valueOrType, List<HirType> types)
		{
			Debug.Assert(types.Count != 0);
			Debug.Assert(types.All(t => t.GetIsFullyConcrete()));

			var originalDef = valueOrType.Original;
			List<Concrete> concretes;
			if (!_concretesMap.TryGetValue(originalDef, out var readOnlyConcretes))
			{
				_concretesMap[originalDef] = concretes = [];
			}
			else
			{
				concretes = (List<Concrete>)readOnlyConcretes;
			}

			if (concretes.Any(concrete => ListMatches(concrete.Types, types)))
			{
				// Already recorded.
				return false;
			}

			concretes.Add(new() { Types = types });
			return true;
		}

		private static bool ListMatches(IReadOnlyList<HirType> types1, IReadOnlyList<HirType> types2)
		{
			return CollectionHelper.AreEqual(types1, types2);
		}
	}

	public static bool ProcessValueForPossibleGenericUsages(
		IReadOnlyList<HirGenericParamType> genericParams,
		HirValue value,
		HashSet<HirGenericParamType> genericParamTypesUsed)
	{
		if (value is HirClosedGenericFnValue closedGenericFnValue)
		{
			// The fn's type is covered with another call to ProcessTypeForPossibleGenericUsages separately from here.
			// Here, we only need to worry about the generic args.
			var yes = false;
			foreach (var genericArg in closedGenericFnValue.GenericArgs)
				yes = yes || ProcessTypeForPossibleGenericUsages(genericParams, genericArg, genericParamTypesUsed);
			return yes;
		}

		return false;
	}

	public static bool ProcessTypeForPossibleGenericUsages(
		IReadOnlyList<HirGenericParamType> genericParams,
		HirType type,
		HashSet<HirGenericParamType> genericParamTypesUsed)
	{
		if (type is HirGenericParamType genericParam)
		{
			Debug.Assert(genericParams.Contains(genericParam));
			genericParamTypesUsed.Add(genericParam);
			return true;
		}
		else if (type is HirPtrType ptrType && ptrType.InnerType != null)
		{
			return ProcessTypeForPossibleGenericUsages(genericParams, ptrType.InnerType, genericParamTypesUsed);
		}
		else if (type is HirFnType fnType)
		{
			var yes = false;
			foreach (var paramType in fnType.ParamTypes)
				yes = yes || ProcessTypeForPossibleGenericUsages(genericParams, paramType, genericParamTypesUsed);
			yes = yes || ProcessTypeForPossibleGenericUsages(genericParams, fnType.ReturnType, genericParamTypesUsed);
			return yes;
		}
		else if (type is HirNonGenericStructType)
		{
			return false;
		}
		else if (type is HirClosedGenericStructType structClosedGenericType)
		{
			var yes = false;
			foreach (var genericArg in structClosedGenericType.GenericArgs)
				yes = yes || ProcessTypeForPossibleGenericUsages(genericParams, genericArg, genericParamTypesUsed);
			return yes;
		}

		throw new UnreachableException();
	}
}
