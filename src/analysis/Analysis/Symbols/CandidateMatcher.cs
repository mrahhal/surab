namespace Surab.Analysis;

internal static class CandidateMatcher
{
	public static bool FnsMatch(FnSymbol? fn1, FnSymbol? fn2)
	{
		if (fn1 == null || fn2 == null) return false;

		// TODO: What about extern, attrs, access visibility, etc?

		if (!fn1.Identifier.Equals(fn2.Identifier)) return false;
		if (!fn1.ReturnType.IsAssignableFrom(fn2.ReturnType, allowCoercion: false)) return false;

		// Generics
		{
			fn1.TryGetGenericArgs(out var genericArgs1);
			fn2.TryGetGenericArgs(out var genericArgs2);
			if (genericArgs1 == null && genericArgs2 != null || genericArgs1 != null && genericArgs2 == null)
				return false;
			if (genericArgs1 != null && genericArgs2 != null)
			{
				if (genericArgs1.Value.Length != genericArgs2.Value.Length) return false;
				for (var i = 0; i < genericArgs1.Value.Length; i++)
				{
					if (!genericArgs1.Value[i].IsAssignableFrom(genericArgs2.Value[i], allowCoercion: false))
					{
						return false;
					}
				}
			}
		}

		// Params
		{
			if (fn1.Params.Length != fn2.Params.Length) return false;
			for (var i = 0; i < fn1.Params.Length; i++)
			{
				if (!fn1.Params[i].Type.IsAssignableFrom(fn2.Params[i].Type, allowCoercion: false))
				{
					return false;
				}
			}
		}

		return true;
	}
}
