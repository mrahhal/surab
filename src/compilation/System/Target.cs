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
            return 0.Bytes();
        }
        if (type == BuiltInTypeSystem.Char || type == BuiltInTypeSystem.Bool)
        {
            return 1.Bytes();
        }
        if (type == BuiltInTypeSystem.Number)
        {
            return 8.Bytes();
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
            return 0.Bytes();
        }
        if (type == BuiltInTypeSystem.Char || type == BuiltInTypeSystem.Bool)
        {
            return 1.Bytes();
        }
        if (type == BuiltInTypeSystem.Number)
        {
            return 8.Bytes();
        }

        throw new UnreachableException();
    }
}

internal sealed class X86_64WindowsMSVCTarget : Target
{
    public X86_64WindowsMSVCTarget() :
        base(TargetArch.x86_64, TargetOS.Windows, TargetEnv.MSVC)
    {
        PtrWidth = 64.Bits();
    }
}
