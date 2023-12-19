using LLVMSharp.Interop;
using Surab.Analysis;
using Surab.Compilation.LLVM;
using System.Diagnostics;

namespace Surab.Compilation;

public enum TargetArch
{
	x86_64,
}

public enum TargetOS
{
	Windows,
}

public enum TargetEnv
{
	MSVC,
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

	// Should this be in Abi?
	public Layout GetLayout(TypeSymbol type)
	{
		ArgumentNullException.ThrowIfNull(type);

		type = type.UnwrapAliases();
		var tag = type.KnownTypeTag;

		if (tag == KnownTypeTag.None)
			return new(0.AsBytes(), 0.AsBytes());
		if (tag == KnownTypeTag.@char || tag == KnownTypeTag.@bool)
			return new(1.AsBytes(), 1.AsBytes());
		if (tag == KnownTypeTag.@string)
			return new(PtrWidth, PtrWidth);
		if (tag == KnownTypeTag.i32 || tag == KnownTypeTag.u32)
			return new(4.AsBytes(), 4.AsBytes());
		if (tag == KnownTypeTag.i64 || tag == KnownTypeTag.u64)
			return new(8.AsBytes(), 8.AsBytes());
		if (tag == KnownTypeTag.f32)
			return new(4.AsBytes(), 4.AsBytes());
		if (tag == KnownTypeTag.f64)
			return new(8.AsBytes(), 8.AsBytes());

		if (type is StructTypeSymbol structType)
		{
			return GetStructLayout(structType).Layout;
		}

		// if ptr return ptr bit width

		if (type is FnTypeSymbol)
		{
			return new(PtrWidth, PtrWidth);
		}

		//if (type == BuiltInTypeSystem.String)
		//    return target.PtrWidth;

		throw new UnreachableException();
	}

	// Should this be in Abi?
	public StructLayout GetStructLayout(StructTypeSymbol structType)
	{
		ArgumentNullException.ThrowIfNull(structType);

		if (structType.IsBuiltin())
		{
			return new StructLayout(GetLayout(structType), []);
		}

		// TODO: What about Zero Sized types (that aren't built in)?
		var offset = 0.AsBytes();
		var structAlignment = 0.AsBytes();
		var sections = new List<StructLayoutSection>();

		var fieldIndex = 0;
		foreach (var m in structType.Members.Fields())
		{
			var fieldLayout = GetLayout(m.Type);
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

	public BitSize GetAbiSize(TypeSymbol type) => GetLayout(type).Size;

	public BitSize GetAbiAlignment(TypeSymbol type) => GetLayout(type).Alignment;

	public IntegerSign GetIntegerSign(TypeSymbol type)
	{
		type = type.UnwrapAliases();
		var tag = type.KnownTypeTag;

		if (tag == KnownTypeTag.i32 || tag == KnownTypeTag.i64)
			return IntegerSign.Signed;
		if (tag == KnownTypeTag.u32 || tag == KnownTypeTag.u64)
			return IntegerSign.Unsigned;

		throw new UnreachableException();
	}
}

internal abstract class x86_64WindowsTarget : Target
{
	protected x86_64WindowsTarget(TargetEnv env) : base(TargetArch.x86_64, TargetOS.Windows, env)
	{
	}

	public Classification Classify(TypeSymbol type)
	{
		return Abi.x86_64.ClassifyTypeForWindows(type, this);
	}
}

internal sealed class x86_64WindowsMSVCTarget : x86_64WindowsTarget
{
	public x86_64WindowsMSVCTarget() :
		base(TargetEnv.MSVC)
	{
		PtrWidth = 64.AsBits();
	}
}
