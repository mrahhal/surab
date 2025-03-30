using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Surab.Analysis;

namespace Surab.Compilation;

public enum HirTypeKind
{
	GenericParam,
	Fn,
	Ptr,
	Struct,
}

/// <summary>
/// Represents an HIR type lowered from a type symbol.
/// </summary>
[DebuggerDisplay("\\{{GetType().Name,nq}\\}: {FullName,nq}")]
public abstract class HirType
{
	public HirType(
		HirTypeKind kind,
		string identifier,
		string name,
		string fullName,
		KnownTypeTag? knownTypeTag)
	{
		Kind = kind;
		Identifier = identifier;
		Name = name;
		FullName = fullName;
		KnownTypeTag = knownTypeTag;
	}

	public HirTypeKind Kind { get; }

	public string Identifier { get; }

	public string Name { get; }

	public string FullName { get; }

	public KnownTypeTag? KnownTypeTag { get; }

	public HirType Original
	{
		get
		{
			return this switch
			{
				HirClosedGenericStructType v => v.GenericDef,
				_ => this,
			};
		}
	}

	public bool TryGetGenericParams([NotNullWhen(true)] out IReadOnlyList<HirGenericParamType>? genericParams)
	{
		genericParams = this switch
		{
			HirGenericStructType v => v.GenericParams,
			HirClosedGenericStructType v => v.GenericDef.GenericParams,
			_ => null,
		};
		return genericParams != null;
	}

	public bool TryGetGenericArgs([NotNullWhen(true)] out IReadOnlyList<HirType>? genericArgs)
	{
		genericArgs = this switch
		{
			HirClosedGenericStructType v => v.GenericArgs,
			_ => null,
		};
		return genericArgs != null;
	}

	public bool GetIsFullyConcrete()
	{
		return this switch
		{
			HirGenericParamType => false,
			HirGenericStructType => false,
			HirClosedGenericStructType v => v.GenericArgs.All(x => x.GetIsFullyConcrete()),
			_ => true,
		};
	}
}

public sealed class HirGenericParamType : HirType
{
	public HirGenericParamType(
		string identifier,
		string name,
		string fullName)
		: base(HirTypeKind.GenericParam, identifier, name, fullName, null)
	{
	}
}

public sealed class HirFnType : HirType
{
	public HirFnType(
		string identifier,
		string name,
		string fullName,
		IReadOnlyList<HirType> paramTypes,
		HirType returnType)
		: base(HirTypeKind.Fn, identifier, name, fullName, null)
	{
		ParamTypes = paramTypes;
		ReturnType = returnType;
	}

	public IReadOnlyList<HirType> ParamTypes { get; }

	public HirType ReturnType { get; }
}

public sealed class HirPtrType : HirType
{
	public HirPtrType(
		string identifier,
		string name,
		string fullName,
		HirType? innerType)
		: base(HirTypeKind.Ptr, identifier, name, fullName, null)
	{
		InnerType = innerType;
	}

	public HirType? InnerType { get; }
}

public abstract class HirStructType : HirType
{
	public HirStructType(
		string identifier,
		string name,
		string fullName,
		KnownTypeTag? knownTypeTag)
		: base(HirTypeKind.Struct, identifier, name, fullName, knownTypeTag)
	{
	}

	public abstract IReadOnlyList<HirFieldValue> Fields { get; }
}

public sealed class HirNonGenericStructType : HirStructType
{
	public HirNonGenericStructType(
		string identifier,
		string name,
		string fullName,
		KnownTypeTag? knownTypeTag,
		IReadOnlyList<HirFieldValue> fields)
		: base(identifier, name, fullName, knownTypeTag)
	{
		Fields = fields;
	}

	public override IReadOnlyList<HirFieldValue> Fields { get; }
}

public sealed class HirGenericStructType : HirStructType
{
	public HirGenericStructType(
		string identifier,
		string name,
		string fullName,
		string fullPrefix,
		KnownTypeTag? knownTypeTag,
		IReadOnlyList<HirGenericParamType> genericParams,
		IReadOnlyList<HirFieldValue> fields)
		: base(identifier, name, fullName, knownTypeTag)
	{
		FullPrefix = fullPrefix;
		GenericParams = genericParams;
		Fields = fields;
	}

	public string FullPrefix { get; }

	public IReadOnlyList<HirGenericParamType> GenericParams { get; }

	public override IReadOnlyList<HirFieldValue> Fields { get; }
}

public sealed class HirClosedGenericStructType : HirStructType
{
	public HirClosedGenericStructType(
		string identifier,
		string name,
		string fullName,
		KnownTypeTag? knownTypeTag,
		HirGenericStructType genericDef,
		IReadOnlyList<HirType> genericArgs,
		IReadOnlyList<HirFieldValue> fields)
		: base(identifier, name, fullName, knownTypeTag)
	{
		GenericDef = genericDef;
		GenericArgs = genericArgs;
		Fields = fields;
	}

	public HirGenericStructType GenericDef { get; }

	public IReadOnlyList<HirType> GenericArgs { get; }

	public override IReadOnlyList<HirFieldValue> Fields { get; }
}
