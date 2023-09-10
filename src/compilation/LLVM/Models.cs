using LLVMSharp.Interop;

namespace Surab.Compilation.LLVM;

/// <summary>
/// Represents an LLVM ptr value with its original type.
/// Note that this should only be used for ptr values.
/// </summary>
/// <param name="Type">The LLVM type of the unit.</param>
/// <param name="Value">The LLVM value of the unit.</param>
public record LLVMUnit(LLVMTypeRef Type, LLVMValueRef Value)
{
    public static implicit operator LLVMUnit((LLVMTypeRef Type, LLVMValueRef Value) tuple)
    {
        return new(tuple.Type, tuple.Value);
    }
}

/// <summary>
/// Represents a created LLVM function.
/// </summary>
/// <param name="Type">The LLVM type of the function.</param>
/// <param name="Value">The LLVM value of the function.</param>
/// <param name="SRet">The type of the sret param if it exists, null otherwise.</param>
public record LLVMFn(LLVMTypeRef Type, LLVMValueRef Value, LLVMTypeRef? SRet) : LLVMUnit(Type, Value)
{
}
