using System.Diagnostics;

using Surab.Analysis;

namespace Surab.Compilation;

public enum TargetArch
{
	x86_64,
}

public enum TargetOS
{
	Windows,
	Linux,
}

public enum TargetEnv
{
	MSVC,
	GNU,
}

public enum IntegerSign
{
	Signed,
	Unsigned,
}

public abstract class Target(TargetArch arch, TargetOS os, TargetEnv env)
{
	public TargetArch Arch { get; } = arch;

	public TargetOS OS { get; } = os;

	public TargetEnv Env { get; } = env;

	public BitSize PtrWidth { get; protected set; }

	public Layout GetLayout(HirType type)
	{
		Debug.Assert(type.Kind is not HirTypeKind.GenericParam);
		ArgumentNullException.ThrowIfNull(type);

		var tag = type.KnownTypeTag;

		if (tag is KnownTypeTag.@void)
			return new(0.AsBytes(), 0.AsBytes());
		if (tag is KnownTypeTag.@char or KnownTypeTag.@bool or KnownTypeTag.i8 or KnownTypeTag.u8)
			return new(1.AsBytes(), 1.AsBytes());
		if (tag is KnownTypeTag.i16 or KnownTypeTag.u16)
			return new(2.AsBytes(), 2.AsBytes());
		if (tag is KnownTypeTag.i32 or KnownTypeTag.u32 or KnownTypeTag.f32)
			return new(4.AsBytes(), 4.AsBytes());
		if (tag is KnownTypeTag.i64 or KnownTypeTag.u64 or KnownTypeTag.f64)
			return new(8.AsBytes(), 8.AsBytes());
		if (tag is KnownTypeTag.isize or KnownTypeTag.usize)
			return new(PtrWidth, PtrWidth);
		if (tag is KnownTypeTag.@string || type.Kind is HirTypeKind.Ptr or HirTypeKind.Fn)
			return new(PtrWidth, PtrWidth);

		if (type is HirStructType structType)
		{
			return GetStructLayout(structType).Layout;
		}

		throw new UnreachableException();
	}

	public StructLayout GetStructLayout(HirStructType structType)
	{
		ArgumentNullException.ThrowIfNull(structType);
		Debug.Assert(structType is not HirGenericStructType);

		if (structType.KnownTypeTag.IsBuiltin())
		{
			return new StructLayout(GetLayout(structType), []);
		}

		// TODO: What about Zero Sized types (that aren't built in)?
		var offset = 0.AsBytes();
		var structAlignment = 0.AsBytes();
		var sections = new List<StructLayoutSection>();

		var fieldIndex = 0;
		foreach (var f in structType.Fields)
		{
			var fieldLayout = GetLayout(f.Type);
			AddPaddingIfNecessary(fieldLayout.Alignment);
			structAlignment = Math.Max(structAlignment.Bytes, fieldLayout.Alignment.Bytes).AsBytes();
			sections.Add(StructLayoutSection.Data(offset, fieldLayout.Size, fieldIndex, sectionIndex: sections.Count));

			offset += fieldLayout.Size;
			fieldIndex++;
		}

		AddPaddingIfNecessary(structAlignment);

		var layout = new Layout(offset, structAlignment);
		return new(layout, sections);

		void AddPaddingIfNecessary(BitSize alignment)
		{
			var newOffset = Memory.AlignForward(offset, alignment);
			if (newOffset != offset)
			{
				// Padding was needed.
				var padding = newOffset - offset;
				sections.Add(StructLayoutSection.Padding(offset, padding, sectionIndex: sections.Count));
				offset = newOffset;
			}
		}
	}

	public BitSize GetSize(HirType type) => GetLayout(type).Size;

	public BitSize GetAlignment(HirType type) => GetLayout(type).Alignment;

	public IntegerSign GetIntegerSign(HirType type)
	{
		var tag = type.KnownTypeTag;

		if (tag is KnownTypeTag.i8 or KnownTypeTag.i16 or KnownTypeTag.i32 or KnownTypeTag.i64 or KnownTypeTag.isize)
			return IntegerSign.Signed;
		if (tag is KnownTypeTag.u8 or KnownTypeTag.u16 or KnownTypeTag.u32 or KnownTypeTag.u64 or KnownTypeTag.usize)
			return IntegerSign.Unsigned;

		throw new UnreachableException();
	}
}

internal abstract class x86_64Target : Target
{
	protected x86_64Target(TargetOS os, TargetEnv env) : base(TargetArch.x86_64, os, env)
	{
		PtrWidth = 64.AsBits();
	}
}

internal abstract class x86_64WindowsTarget : x86_64Target
{
	protected x86_64WindowsTarget(TargetEnv env) : base(TargetOS.Windows, env)
	{
		PtrWidth = 64.AsBits();
	}
}

internal sealed class x86_64WindowsMSVCTarget : x86_64WindowsTarget
{
	public x86_64WindowsMSVCTarget() : base(TargetEnv.MSVC)
	{
	}
}

internal abstract class x86_64LinuxTarget : x86_64Target
{
	protected x86_64LinuxTarget(TargetEnv env) : base(TargetOS.Linux, env)
	{
	}
}

internal sealed class x86_64LinuxGNUTarget : x86_64LinuxTarget
{
	public x86_64LinuxGNUTarget() : base(TargetEnv.GNU)
	{
	}
}
