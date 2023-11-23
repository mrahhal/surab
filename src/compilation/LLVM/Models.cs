using LLVMSharp.Interop;
using Surab.Analysis;

namespace Surab.Compilation.LLVM;

// TODO(docs): Explain more the idea behind LLVMUnit.IsWrapped.

/// <summary>
/// Represents an LLVM type and value pair that is optionally wrapped in a ptr.
/// </summary>
internal class LLVMUnit(LLVMTypeRef type, LLVMValueRef value, bool isWrapped)
{
    /// <summary>
    /// The LLVM type of the unit.
    /// </summary>
    public LLVMTypeRef Type { get; } = type;

    /// <summary>
    /// The LLVM value of the unit.
    /// </summary>
    public LLVMValueRef Value { get; } = value;

    /// <summary>
    /// Whether the value is the wrapper ptr of the actual value.
    /// </summary>
    public bool IsWrapped { get; } = isWrapped;

    public static LLVMUnit Wrapped(LLVMTypeRef type, LLVMValueRef value)
    {
        return new LLVMUnit(type, value, true);
    }

    public static LLVMUnit UnWrapped(LLVMTypeRef type, LLVMValueRef value)
    {
        return new LLVMUnit(type, value, false);
    }

    public void Deconstruct(out LLVMTypeRef type, out LLVMValueRef value)
    {
        type = Type;
        value = Value;
    }

    /// <summary>
    /// Returns the unrwapped value, usually does a load on the wrapper ptr. If this is called on an already unwrapped unit the same unit is returned.
    /// </summary>
    public LLVMUnit Unwrap(LLVMCompilationContext context)
    {
        if (!IsWrapped) return this;
        return WithUnwrapped(context.Builder.BuildLoad2(Type, Value));
    }

    protected virtual LLVMUnit WithUnwrapped(LLVMValueRef unwrappedValue)
    {
        return UnWrapped(Type, unwrappedValue);
    }
}

/// <summary>
/// Represents a created LLVM function.
/// </summary>
internal class LLVMFn(LLVMUnit unit, LLVMTypeRef? sret)
{
    public LLVMUnit Unit { get; } = unit;

    public LLVMTypeRef Type => Unit.Type;

    public LLVMValueRef Value => Unit.Value;

    /// <summary>
    /// The type of the sret param if it exists, null otherwise.
    /// </summary>
    public LLVMTypeRef? SRet { get; } = sret;

    public void Deconstruct(out LLVMTypeRef type, out LLVMValueRef value, out LLVMTypeRef? sret)
    {
        Unit.Deconstruct(out type, out value);
        sret = SRet;
    }
}

/// <summary>
/// Represents a created LLVM type lowered from a <see cref="TypeSymbol"/>.
/// </summary>
internal class LLVMType(LLVMTypeRef type)
{
    /// <summary>
    /// The LLVM lowered type of the <see cref="TypeSymbol"/>.
    /// </summary>
    public LLVMTypeRef Type { get; } = type;
}
