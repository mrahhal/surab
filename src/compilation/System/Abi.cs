using System.Diagnostics;

using Surab.Analysis;

namespace Surab.Compilation;

internal enum Classification
{
	// x86_64
	Integer,
	// x86_64
	SSE,
	// x86_64
	SSEUP,
	// x86_64
	Memory,
}

internal static class Abi
{
	public static class x86_64
	{
		// https://learn.microsoft.com/en-us/cpp/build/x64-software-conventions
		public static Classification ClassifyTypeForWindows(HirType type, Target target)
		{
			var size = target.GetLayout(type).Size;
			Debug.Assert(!size.IsZeroSized, "Classify shouldn't be called with zero sized types.");

			if (!(size.Bytes is 1 or 2 or 4 or 8))
			{
				return Classification.Memory;
			}

			return type.GetRuntimeKind() switch
			{
				RuntimeTypeKind.Bool or
				RuntimeTypeKind.Char or
				RuntimeTypeKind.Ptr or
				RuntimeTypeKind.Struct
				=> Classification.Integer,

				RuntimeTypeKind.Numeric when type.KnownTypeTag.IsNumericInteger()
				=> Classification.Integer,

				RuntimeTypeKind.Numeric when type.KnownTypeTag.IsNumericFloat()
				=> Classification.SSE,

				_ => throw new UnreachableException(),
			};
		}
	}
}
