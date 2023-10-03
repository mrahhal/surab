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

public abstract class Target
{
    public Target(TargetArch arch, TargetOS os, TargetEnv env)
    {
        Arch = arch;
        OS = os;
        Env = env;
    }

    public TargetArch Arch { get; }

    public TargetOS OS { get; }

    public TargetEnv Env { get; }

    public BitSize PtrWidth { get; protected set; }

    public BitSize GetSize(TypeSymbol type)
    {
        if (type == BuiltInTypeSystem.None)
            return 0.AsBytes();
        if (type == BuiltInTypeSystem.Char || type == BuiltInTypeSystem.Bool)
            return 1.AsBytes();
        if (type == BuiltInTypeSystem.Number)
            return 8.AsBytes();

        // if ptr return ptr bit width

        //if (type == BuiltInTypeSystem.String)
        //    return target.PtrWidth;

        throw new UnreachableException();
    }

    public abstract BitSize GetAbiSize(TypeSymbol type);

    public BitSize GetAbiAlignment(TypeSymbol type)
    {
        if (GetAbiSize(type).IsZeroSized)
            return 0.AsBytes();

        if (type == BuiltInTypeSystem.Char || type == BuiltInTypeSystem.Bool)
            return 1.AsBytes();
        if (type == BuiltInTypeSystem.Number)
            return 8.AsBytes();

        throw new UnreachableException();
    }

    public bool IsReturnSRet(FnSymbol fn, CompilationContext context)
    {
        if (GetAbiSize(fn.ReturnType).IsZeroSized)
            return false;

        return IsReturnSRetCore(fn, context);
    }

    public abstract bool IsReturnSRetCore(FnSymbol fn, CompilationContext context);

    // Consider moving LLVM related stuff to some other place.

    // TODO: Support different calling conventions. Right now it's always Win64 cc.
    public abstract LLVMTypeRef LowerFnReturnType(FnSymbol fn, CompilationContext context);

    public LLVMTypeRef LowerType(TypeSymbol type, CompilationContext context)
    {
        if (type == BuiltInTypeSystem.None)
            return LLVMTypeRef.Void;
        if (type == BuiltInTypeSystem.Char || type == BuiltInTypeSystem.Bool)
            return LLVMTypeRef.Int1;
        if (type == BuiltInTypeSystem.Number)
            return LLVMTypeRef.Double;

        // Struct
        // Class
        // FnPtr
        // ...

        throw new UnreachableException();
    }

    internal LLVMTypeLowering GetLLVMLowering(TypeSymbol type)
    {
        if (GetAbiSize(type).IsZeroSized)
            return LLVMTypeLowering.no_bits;

        return GetLLVMLoweringCore(type);
    }

    internal abstract LLVMTypeLowering GetLLVMLoweringCore(TypeSymbol type);
}

internal abstract class x86_64WindowsTarget : Target
{
    protected x86_64WindowsTarget(TargetEnv env) : base(TargetArch.x86_64, TargetOS.Windows, env)
    {
    }

    public override BitSize GetAbiSize(TypeSymbol type)
    {
        return Abi.x86_64.GetAbiSize(type, this);
    }

    public override bool IsReturnSRetCore(FnSymbol fn, CompilationContext context)
    {
        return Classify(fn.ReturnType) == Classification.Memory;
    }

    public override LLVMTypeRef LowerFnReturnType(FnSymbol fn, CompilationContext context)
    {
        var returnType = fn.ReturnType;
        var classification = Classify(returnType);
        return classification switch
        {
            Classification.Integer => LowerType(returnType, context), // TODO: This is only for scalars?
            //win_i128 =>
            Classification.Memory => LLVMTypeRef.Void,
            Classification.SSE => LowerType(returnType, context),
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
            Classification.Integer => LLVMTypeLowering.byval, // abi_sized_int in what cases?
            //win_i128 =>
            Classification.Memory => LLVMTypeLowering.byref_mut,
            Classification.SSE => LLVMTypeLowering.byval,
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
