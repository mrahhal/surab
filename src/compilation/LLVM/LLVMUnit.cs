using LLVMSharp.Interop;

namespace Surab.Compilation.LLVM;

/// <summary>
/// Represents an LLVM ptr value with its original type.
/// Note that this should only be used for ptr values.
/// </summary>
public record LLVMUnit(LLVMTypeRef Type, LLVMValueRef Value)
{
    public static implicit operator LLVMUnit((LLVMTypeRef Type, LLVMValueRef Value) tuple)
    {
        return new(tuple.Type, tuple.Value);
    }
}
