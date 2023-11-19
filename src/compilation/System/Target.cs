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

public abstract class Target(TargetArch arch, TargetOS os, TargetEnv env)
{
    public TargetArch Arch { get; } = arch;

    public TargetOS OS { get; } = os;

    public TargetEnv Env { get; } = env;

    public BitSize PtrWidth { get; protected set; }

    // Should this be in Abi?
    public Layout GetLayout(TypeSymbol type)
    {
        if (type == BuiltInTypeSystem.None)
            return new(0.AsBytes(), 0.AsBytes());
        if (type == BuiltInTypeSystem.Char || type == BuiltInTypeSystem.Bool)
            return new(1.AsBytes(), 1.AsBytes());
        if (type == BuiltInTypeSystem.Number)
            return new(8.AsBytes(), 8.AsBytes());

        if (type is StructTypeSymbol structType)
        {
            return GetStructLayout(structType).Layout;
        }

        // if ptr return ptr bit width

        //if (type == BuiltInTypeSystem.String)
        //    return target.PtrWidth;

        throw new UnreachableException();
    }

    // Should this be in Abi?
    public StructLayout GetStructLayout(StructTypeSymbol structType)
    {
        var offset = 0.AsBytes();
        var structAlignment = 0.AsBytes();
        var sections = new List<StructLayoutSection>();

        var fieldIndex = 0;
        foreach (var m in structType.Members.Fields())
        {
            var fieldLayout = GetLayout(m.Type);
            AddPaddingIfNecessary(fieldLayout.Alignment);
            structAlignment = Math.Max(structAlignment.Bytes, fieldLayout.Alignment.Bytes).AsBytes();
            sections.Add(StructLayoutSection.Data(offset, fieldLayout.Size, fieldIndex));

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
                sections.Add(StructLayoutSection.Padding(offset, padding));
                offset = newOffset;
            }
        }
    }

    public BitSize GetAbiSize(TypeSymbol type) => GetLayout(type).Size;

    public BitSize GetAbiAlignment(TypeSymbol type) => GetLayout(type).Alignment;

    public bool IsReturnSRet(FnSymbol fn)
    {
        if (GetAbiSize(fn.ReturnType).IsZeroSized)
            return false;

        return IsReturnSRetCore(fn);
    }

    public abstract bool IsReturnSRetCore(FnSymbol fn);

    // Consider moving LLVM related stuff to some other place.

    // TODO: Support different calling conventions. Right now it's always Win64 cc.
    public abstract LLVMTypeRef LowerFnReturnType(FnSymbol fn);

    public LLVMTypeRef LowerType(TypeSymbol type)
    {
        if (type == BuiltInTypeSystem.None)
            return LLVMTypeRef.Void;
        if (type == BuiltInTypeSystem.Char || type == BuiltInTypeSystem.Bool)
            return LLVMTypeRef.Int1;
        if (type == BuiltInTypeSystem.Number)
            return LLVMTypeRef.Double;

        if (type is StructTypeSymbol structType)
        {
            var fields = structType.Members.Fields().ToList();
            var structLayout = GetStructLayout(structType);
            var elements = structLayout.Sections.Select(m =>
            {
                return m.Kind switch
                {
                    StructLayoutSectionKind.Padding => LLVMTypeRef.CreateArray(LLVMTypeRef.Int1, (uint)m.Size.Bytes),
                    StructLayoutSectionKind.Data => LowerType(fields[m.FieldIndex].Type),
                    _ => throw new UnreachableException(),
                };
            }).ToArray();
            return LLVMTypeRef.CreateStruct(elements, Packed: false);
        }

        // Class
        // FnPtr
        // ...

        throw new UnreachableException();
    }

    internal LLVMTypeLowering GetLLVMLowering(TypeSymbol type)
    {
        if (GetAbiSize(type).IsZeroSized)
            return LLVMTypeLowering.ZeroSized;

        return GetLLVMLoweringCore(type);
    }

    internal abstract LLVMTypeLowering GetLLVMLoweringCore(TypeSymbol type);
}

internal abstract class x86_64WindowsTarget : Target
{
    protected x86_64WindowsTarget(TargetEnv env) : base(TargetArch.x86_64, TargetOS.Windows, env)
    {
    }

    public override bool IsReturnSRetCore(FnSymbol fn)
    {
        return Classify(fn.ReturnType) == Classification.Memory;
    }

    public override LLVMTypeRef LowerFnReturnType(FnSymbol fn)
    {
        var returnType = fn.ReturnType;
        var classification = Classify(returnType);
        return classification switch
        {
            Classification.Integer => returnType.IsScalar ? LowerType(returnType) : LLVMTypeRef.CreateInt((uint)GetAbiSize(returnType).Bits),
            //win_i128 =>
            Classification.Memory => LLVMTypeRef.Void,
            Classification.SSE => LowerType(returnType),
            _ => throw new UnreachableException(),
        };
    }

    protected Classification Classify(TypeSymbol type)
    {
        return Abi.x86_64.ClassifyTypeForWindows(type, this);
    }

    internal override LLVMTypeLowering GetLLVMLoweringCore(TypeSymbol type)
    {
        Debug.Assert(!GetAbiSize(type).IsZeroSized);

        return Classify(type) switch
        {
            Classification.Integer => type.IsScalar ? LLVMTypeLowering.ByValue : LLVMTypeLowering.AbiInt,
            //win_i128 =>
            Classification.Memory => LLVMTypeLowering.ByMutRef,
            Classification.SSE => LLVMTypeLowering.ByValue,
            _ => throw new UnreachableException(),
        };
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
