namespace Surab.Compilation.LLVM;

// TODO(docs): Explain more the idea behind LLVMUnit.IsWrapped.

/// <summary>
/// Represents an LLVM type and value pair that is optionally wrapped in a ptr.
/// </summary>
internal sealed class LLVMUnit(SCPPTypeRef type, SCPPValueRef value, bool isWrapped)
{
	/// <summary>
	/// The LLVM type of the unit.
	/// </summary>
	public SCPPTypeRef Type { get; } = type;

	/// <summary>
	/// The LLVM value of the unit.
	/// </summary>
	public SCPPValueRef Value { get; } = value;

	/// <summary>
	/// Whether the value is the wrapper ptr of the actual value.
	/// </summary>
	public bool IsWrapped { get; } = isWrapped;

	public static LLVMUnit Wrapped(SCPPTypeRef type, SCPPValueRef value)
	{
		return new LLVMUnit(type, value, true);
	}

	public static LLVMUnit Unwrapped(SCPPTypeRef type, SCPPValueRef value)
	{
		return new LLVMUnit(type, value, false);
	}

	public void Deconstruct(out SCPPTypeRef type, out SCPPValueRef value)
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
		return WithUnwrapped(context.Builder.BuildLoad(Type, Value));
	}

	private LLVMUnit WithUnwrapped(SCPPValueRef unwrappedValue)
	{
		return Unwrapped(Type, unwrappedValue);
	}
}

/// <summary>
/// Contains fn related info.
/// </summary>
internal class LLVMFn(SCPPTypeRef? sret, SCPPFnAttrsDef attrs)
{
	/// <summary>
	/// The type of the sret param if it exists, null otherwise.
	/// </summary>
	public SCPPTypeRef? SRet { get; } = sret;

	public SCPPFnAttrsDef Attrs => attrs;

	public void Deconstruct(out SCPPTypeRef? sret, out SCPPFnAttrsDef attrs)
	{
		sret = SRet;
		attrs = Attrs;
	}
}
