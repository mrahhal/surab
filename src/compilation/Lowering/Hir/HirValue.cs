using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Surab.Compilation;

public enum HirValueKind
{
	Local,
	Param,
	Field,
	Fn,
}

/// <summary>
/// Represents an HIR value lowered from a value symbol.
///
/// With the exception of a closed fn, all symbols that are substituted are always the original def
/// here. e.g. A single param value instance will be created even in a generic fn.
/// It's the responsibility of the later codegen phase to deal with this properly.
/// </summary>
[DebuggerDisplay("\\{{GetType().Name,nq}\\}: {FullName,nq}")]
public abstract class HirValue
{
	public HirValue(
		HirValueKind kind,
		string identifier,
		string name,
		string fullName,
		HirType type)
	{
		Kind = kind;
		Identifier = identifier;
		Name = name;
		FullName = fullName;
		Type = type;
	}

	public HirValueKind Kind { get; }

	public string Identifier { get; }

	public string Name { get; }

	public string FullName { get; }

	public HirType Type { get; }

	public HirValue Original
	{
		get
		{
			return this switch
			{
				HirClosedGenericFnValue v => v.GenericDef,
				_ => this,
			};
		}
	}

	public bool TryGetGenericParams([NotNullWhen(true)] out IReadOnlyList<HirGenericParamType>? genericParams)
	{
		genericParams = this switch
		{
			HirGenericFnValue v => v.GenericParams,
			HirClosedGenericFnValue v => v.GenericDef.GenericParams,
			_ => null,
		};
		return genericParams != null;
	}

	public bool TryGetGenericArgs([NotNullWhen(true)] out IReadOnlyList<HirType>? genericArgs)
	{
		genericArgs = this switch
		{
			HirClosedGenericFnValue v => v.GenericArgs,
			_ => null,
		};
		return genericArgs != null;
	}

	public bool GetIsFullyConcrete()
	{
		return this switch
		{
			HirGenericFnValue => false,
			HirClosedGenericFnValue v => v.GenericArgs.All(x => x.GetIsFullyConcrete()),
			_ => true,
		};
	}
}

public sealed class HirFieldValue : HirValue
{
	public HirFieldValue(
		string identifier,
		string name,
		string fullName,
		HirType type,
		int fieldIndex)
		: base(HirValueKind.Field, identifier, name, fullName, type)
	{
		FieldIndex = fieldIndex;
	}

	public int FieldIndex { get; }

	public HirStructType StructType { get; private set; } = null!;

	internal void SetStructType(HirStructType structType)
	{
		StructType = structType;
	}
}

public sealed class HirLocalOrParamValue : HirValue
{
	public HirLocalOrParamValue(
		HirValueKind kind,
		string identifier,
		string name,
		string fullName,
		HirType type)
		: base(kind, identifier, name, fullName, type)
	{
		Debug.Assert(kind is HirValueKind.Param or HirValueKind.Local);
	}
}

public abstract class HirFnValue : HirValue
{
	public HirFnValue(
		string identifier,
		string name,
		string fullName,
		HirFnType type)
		: base(HirValueKind.Fn, identifier, name, fullName, type)
	{
	}

	public new HirFnType Type => (HirFnType)base.Type;

	public abstract IReadOnlyList<HirValue> Params { get; }

	public abstract HirType ReturnType { get; }

	public abstract bool IsMain { get; }

	public abstract bool IsExtern { get; }
}

public sealed class HirNonGenericFnValue : HirFnValue
{
	public HirNonGenericFnValue(
		string identifier,
		string name,
		string fullName,
		HirFnType type,
		IReadOnlyList<HirValue> @params,
		HirType returnType,
		bool isMain,
		bool isExtern)
		: base(identifier, name, fullName, type)
	{
		Params = @params;
		ReturnType = returnType;
		IsMain = isMain;
		IsExtern = isExtern;
	}

	public override IReadOnlyList<HirValue> Params { get; }

	public override HirType ReturnType { get; }

	public override bool IsMain { get; }

	public override bool IsExtern { get; }
}

public sealed class HirGenericFnValue : HirFnValue
{
	public HirGenericFnValue(
		string identifier,
		string name,
		string fullName,
		string fullPrefix,
		HirFnType type,
		IReadOnlyList<HirGenericParamType> genericParams,
		IReadOnlyList<HirValue> @params,
		HirType returnType,
		bool isExtern)
		: base(identifier, name, fullName, type)
	{
		FullPrefix = fullPrefix;
		GenericParams = genericParams;
		Params = @params;
		ReturnType = returnType;
		IsExtern = isExtern;
	}

	public string FullPrefix { get; }

	public IReadOnlyList<HirGenericParamType> GenericParams { get; }

	public override IReadOnlyList<HirValue> Params { get; }

	public override HirType ReturnType { get; }

	public override bool IsMain => false;

	public override bool IsExtern { get; }
}

public sealed class HirClosedGenericFnValue : HirFnValue
{
	public HirClosedGenericFnValue(
		string identifier,
		string name,
		string fullName,
		HirFnType type,
		HirGenericFnValue genericDef,
		IReadOnlyList<HirType> genericArgs,
		IReadOnlyList<HirValue> @params,
		HirType returnType)
		: base(identifier, name, fullName, type)
	{
		GenericDef = genericDef;
		GenericArgs = genericArgs;
		Params = @params;
		ReturnType = returnType;
	}

	public HirGenericFnValue GenericDef { get; }

	public IReadOnlyList<HirType> GenericArgs { get; }

	public override IReadOnlyList<HirValue> Params { get; }

	public override HirType ReturnType { get; }

	public override bool IsMain => false;

	public override bool IsExtern => GenericDef.IsExtern;
}
