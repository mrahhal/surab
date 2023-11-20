using LLVMSharp.Interop;
using Surab.Analysis;

namespace Surab.Compilation.LLVM;

/// <summary>
/// Represents an LLVM ptr value with its original type.
/// Note that this should only be used for ptr values.
/// </summary>
public class LLVMUnit(LLVMTypeRef type, LLVMValueRef value)
{
    /// <summary>
    /// The LLVM type of the unit.
    /// </summary>
    public LLVMTypeRef Type { get; } = type;

    /// <summary>
    /// The LLVM value of the unit.
    /// </summary>
    public LLVMValueRef Value { get; } = value;

    internal void Deconstruct(out LLVMTypeRef type, out LLVMValueRef value)
    {
        type = Type;
        value = Value;
    }

    public static implicit operator LLVMUnit((LLVMTypeRef Type, LLVMValueRef Value) tuple)
    {
        return new(tuple.Type, tuple.Value);
    }

    public static implicit operator (LLVMTypeRef Type, LLVMValueRef Value)(LLVMUnit unit)
    {
        return (unit.Type, unit.Value);
    }
}

/// <summary>
/// Represents a created LLVM function.
/// </summary>
public class LLVMFnUnit(LLVMTypeRef type, LLVMValueRef value, LLVMTypeRef? sret) : LLVMUnit(type, value)
{
    /// <summary>
    /// The type of the sret param if it exists, null otherwise.
    /// </summary>
    public LLVMTypeRef? SRet { get; } = sret;

    internal void Deconstruct(out LLVMTypeRef type, out LLVMValueRef value, out LLVMTypeRef? sret)
    {
        Deconstruct(out type, out value);
        sret = SRet;
    }
}

/// <summary>
/// Represents a created LLVM type lowered from a <see cref="TypeSymbol"/>.
/// </summary>
public class LLVMTypeUnit(LLVMTypeRef type)
{
    /// <summary>
    /// The LLVM lowered type of the <see cref="TypeSymbol"/>.
    /// </summary>
    public LLVMTypeRef Type { get; } = type;
}
