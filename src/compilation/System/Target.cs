using LLVMSharp.Interop;
using Surab.Analysis;
using System.Diagnostics;

namespace Surab.Compilation;

internal enum TargetArch
{
    x86_64,
}

internal enum TargetOS
{
    Windows,
}

internal enum TargetEnv
{
    MSVC,
}

internal enum TypeLoweringKind
{
    no_bits,
    byval,
    byref,
    byref_mut,
    abi_sized_int,
}

internal readonly struct TypeLowering
{
    public required TypeLoweringKind Kind { get; init; }
}

internal abstract class Target
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
        {
            return 0.AsBytes();
        }
        if (type == BuiltInTypeSystem.Char || type == BuiltInTypeSystem.Bool)
        {
            return 1.AsBytes();
        }
        if (type == BuiltInTypeSystem.Number)
        {
            return 8.AsBytes();
        }

        //if (type == BuiltInTypeSystem.String)
        //{
        //    return target.PtrWidth;
        //}

        throw new UnreachableException();
    }

    public BitSize GetAlignment(TypeSymbol type)
    {
        if (type == BuiltInTypeSystem.None)
        {
            return 0.AsBytes();
        }
        if (type == BuiltInTypeSystem.Char || type == BuiltInTypeSystem.Bool)
        {
            return 1.AsBytes();
        }
        if (type == BuiltInTypeSystem.Number)
        {
            return 8.AsBytes();
        }

        throw new UnreachableException();
    }

    public abstract bool IsFirstParamSRet(FnSymbol fn, CompilationContext context);

    // Consider moving LLVM related stuff to some other place.

    // TODO: Support different calling conventions. Right now it's always Win64 cc.
    public abstract LLVMTypeRef LowerFnReturnType(FnSymbol fn, CompilationContext context);

    public LLVMTypeRef LowerType(TypeSymbol type, CompilationContext context)
    {
        if (type == BuiltInTypeSystem.None)
        {
            return LLVMTypeRef.Void;
        }
        if (type == BuiltInTypeSystem.Char || type == BuiltInTypeSystem.Bool)
        {
            return LLVMTypeRef.Int1;
        }
        if (type == BuiltInTypeSystem.Number)
        {
            return LLVMTypeRef.Double;
        }

        throw new UnreachableException();
    }
}

internal abstract class x86_64WindowsTarget : Target
{
    protected x86_64WindowsTarget(TargetEnv env) : base(TargetArch.x86_64, TargetOS.Windows, env)
    {
    }

    public override bool IsFirstParamSRet(FnSymbol fn, CompilationContext context)
    {
        return Classify(fn.ReturnType) == Classification.Memory;
    }

    public override LLVMTypeRef LowerFnReturnType(FnSymbol fn, CompilationContext context)
    {
        var returnType = fn.ReturnType;
        var classification = Classify(returnType);
        return classification switch
        {
            Classification.Integer => LowerType(returnType, context),
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
}

internal sealed class x86_64WindowsMSVCTarget : x86_64WindowsTarget
{
    public x86_64WindowsMSVCTarget() :
        base(TargetEnv.MSVC)
    {
        PtrWidth = 64.AsBits();
    }
}
