using System.Diagnostics;

namespace Surab.Compilation.LLVM;

internal enum LLVMIntrinsic
{
	memcpy,
}

internal sealed class LLVMCompilationContext
{
	private readonly Dictionary<LLVMIntrinsic, LLVMUnit> _intrinsicToValue = [];

	private LLVMCompilationContext(
		SCPPModuleRef module,
		SCPPBuilderRef builder,
		SCPPTargetMachineRef targetMachine)
	{
		Module = module;
		Builder = builder;
		TargetMachine = targetMachine;

		InitIntrinsics();
	}

	public SCPPModuleRef Module { get; }

	public SCPPBuilderRef Builder { get; }

	public SCPPTargetMachineRef TargetMachine { get; }

	public static LLVMCompilationContext Create()
	{
		// TODO
		var triple = "x86_64-pc-windows-msvc19.39.33218";
		if (!SCPP.Setup(triple, out var error, out var module, out var builder, out var targetMachine))
		{
			throw new Exception(error);
		}

		return new(module.Value, builder.Value, targetMachine.Value);
	}

	public void Verify()
	{
		if (!Module.Verify(out var error))
		{
			throw new Exception(error);
		}
	}

	public void CallIntrinsic_memcpy_i64(SCPPValueRef dest, SCPPValueRef src, int length)
	{
		var (fnType, fnValue) = GetIntrinsic(LLVMIntrinsic.memcpy);
		Builder.BuildCallWithType(
			fnType, fnValue,
			[dest, src, SCPPValueRef.CreateConstInt(SCPPTypeRef.Int64, (ulong)length), /* isvolatile */ SCPPValueRef.CreateConstInt(SCPPTypeRef.Int1, 0)]);
	}

	private LLVMUnit GetIntrinsic(LLVMIntrinsic intrinsic)
	{
		return _intrinsicToValue[intrinsic];
	}

	private void InitIntrinsics()
	{
		Debug.Assert(_intrinsicToValue.Count == 0);

		var fn_memcpy = SCPP.GetIntrinsic(
			Module, Builder, SCPPIntrinsicOp.memcpy_i64,
			[SCPPTypeRef.CreatePointer(SCPPTypeRef.Void), SCPPTypeRef.CreatePointer(SCPPTypeRef.Void), SCPPTypeRef.Int64]);
		_intrinsicToValue[LLVMIntrinsic.memcpy] = LLVMUnit.Unwrapped(fn_memcpy.GetFunctionType(), fn_memcpy);
	}
}
