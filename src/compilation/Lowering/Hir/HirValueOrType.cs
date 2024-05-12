using System.Diagnostics.CodeAnalysis;

namespace Surab.Compilation;

public readonly record struct HirValueOrType(HirValue? Value, HirType? Type)
{
	public static implicit operator HirValueOrType(HirValue? value)
	{
		return new HirValueOrType { Value = value };
	}

	public static implicit operator HirValueOrType(HirType? type)
	{
		return new HirValueOrType { Type = type };
	}

	public string Identifier => Apply(v => v.Identifier, t => t.Identifier);

	public string Name => Apply(v => v.Name, t => t.Name);

	public HirValueOrType Original => Apply(v => (HirValueOrType)v.Original, t => (HirValueOrType)t.Original);

	public T Apply<T>(Func<HirValue, T> valueFunc, Func<HirType, T> typeFunc)
	{
		return Value != null ? valueFunc(Value) : typeFunc(Type!);
	}

	public bool TryGetValue([NotNullWhen(true)] out HirValue? value, [NotNullWhen(false)] out HirType? type)
	{
		value = null;
		type = null;
		if (Value != null)
		{
			value = Value;
			return true;
		}
		else
		{
			type = Type!;
			return false;
		}
	}

	public bool TryGetGenericParams([NotNullWhen(true)] out IReadOnlyList<HirGenericParamType>? genericParams)
	{
		if (TryGetValue(out var value, out var type))
		{
			return value.TryGetGenericParams(out genericParams);
		}
		else
		{
			return type.TryGetGenericParams(out genericParams);
		}
	}

	public bool TryGetGenericArgs([NotNullWhen(true)] out IReadOnlyList<HirType>? genericArgs)
	{
		if (TryGetValue(out var value, out var type))
		{
			return value.TryGetGenericArgs(out genericArgs);
		}
		else
		{
			return type.TryGetGenericArgs(out genericArgs);
		}
	}

	public override string ToString()
	{
		return Name;
	}
}
