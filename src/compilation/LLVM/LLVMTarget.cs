using System.Diagnostics;
using System.Runtime.InteropServices;

using Surab.Analysis;
using Surab.Compilation.LLVM;

namespace Surab.Compilation;

/// <summary>
/// LLVM compilation related logic that changes with target.
/// </summary>
internal abstract class LLVMTarget
{
	protected LLVMTarget(CompilationContext context)
	{
		Context = context;
	}

	protected CompilationContext Context { get; }

	protected LLVMCompilationContext LLVM => Context.LLVM;

	protected Target Target => Context.Target;

	public static LLVMTarget Create(CompilationContext context)
	{
		var target = context.Target;

		return target switch
		{
			x86_64WindowsMSVCTarget => new x86_64WindowsMSVCLLVMTarget(context),
			// TODO: linux
			_ => throw new UnreachableException(),
		};
	}

	// TODO: Support overriding calling convention on the fn. Right now it's always cc c.
	public SCPPTypeRef LowerFnReturnType(HirFnType fnType)
	{
		var returnType = fnType.ReturnType;
		var info = ComputeInfo(returnType, isReturnType: true);
		return info switch
		{
			DirectABIArgInfo => LowerType(returnType),
			IgnoreABIArgInfo or IndirectABIArgInfo => SCPPTypeRef.Void,
			AsIntegerABIArgInfo => SCPPTypeRef.CreateInt(Target.GetSize(returnType).Bits),
			AsRealABIArgInfo => Target.GetSize(returnType).Bits switch
			{
				32 => SCPPTypeRef.Float,
				64 => SCPPTypeRef.Double,
				_ => throw new UnreachableException(),
			},
			_ => throw new UnreachableException(),
		};
	}

	/// <summary>
	/// Lowers a type from Surab to LLVM.
	/// </summary>
	/// <param name="type">The Surab type to lower.</param>
	/// <returns>The lowered LLVM type.</returns>
	public SCPPTypeRef LowerType(HirType type)
	{
		if (Context.TypeToLLVMType.TryGetValue(type, out var llvmType))
		{
			return llvmType;
		}

		llvmType = LowerTypeCore(type);
		Context.TypeToLLVMType[type] = llvmType;
		return llvmType;
	}

	private SCPPTypeRef LowerTypeCore(HirType type)
	{
		if (type.Kind is HirTypeKind.GenericParam)
		{
			throw new Exception("Unexpected generic param type still present when lowering type to LLVM.");
		}

		var tag = type.KnownTypeTag;

		if (tag.IsNumericInteger())
		{
			var size = Target.GetSize(type);
			return SCPPTypeRef.CreateInt(size.Bits);
		}

		if (tag is KnownTypeTag.@void)
			return SCPPTypeRef.Void;
		if (tag is KnownTypeTag.@char or KnownTypeTag.@bool)
			return SCPPTypeRef.Int1;

		if (tag is KnownTypeTag.f32)
			return SCPPTypeRef.Float;
		if (tag is KnownTypeTag.f64)
			return SCPPTypeRef.Double;

		if (tag is KnownTypeTag.@string || type.Kind is HirTypeKind.Ptr)
			return SCPPTypeRef.CreatePointer(SCPPTypeRef.Void, 0);

		if (type is HirStructType structType)
		{
			var fields = structType.Fields;
			var structLayout = Target.GetStructLayout(structType);
			var elements = structLayout.Sections.Select(m =>
			{
				return m.Kind switch
				{
					StructLayoutSectionKind.Padding => SCPPTypeRef.CreateArray(SCPPTypeRef.Int1, m.Size.Bytes),
					StructLayoutSectionKind.Data => LowerType(fields[m.FieldIndex].Type),
					_ => throw new UnreachableException(),
				};
			}).ToArray();
			// TODO: Optionally mangled name instead.
			var llvmType = SCPPTypeRef.CreateNamedStruct(structType.FullName);
			llvmType.StructSetBody(elements, packed: false);
			return llvmType;
		}

		if (type is HirFnType fnType)
		{
			var (llvmType, _) = LowerType(fnType);
			return llvmType;
		}

		throw new UnreachableException();
	}

	/// <summary>
	/// Lowers an fn type from Surab to LLVM.
	/// </summary>
	/// <param name="fnType">The Surab fn type to lower.</param>
	/// <returns>The lowered LLVM type (which is an opaque ptr in the case of fns here) and the actual LLVM type (the actual fn type).</returns>
	public (SCPPTypeRef llvmType, SCPPTypeRef llvmActualType) LowerType(HirFnType fnType)
	{
		var (sret, _) = GetFnInfoFromFnType(fnType);
		var returnType = LowerFnReturnType(fnType);

		// TODO: Better way?
		var llvmIndex = 0;
		var capacity = fnType.ParamTypes.Count + 1; // Max length of llvm params.
		var @params = new List<SCPPTypeRef>(capacity);

		if (sret.HasValue)
		{
			@params.Add(SCPPTypeRef.CreatePointer(LowerType(fnType.ReturnType), 0));
			llvmIndex++;
		}

		foreach (var paramType in fnType.ParamTypes)
		{
			// Create fn param types
			// ---

			var info = ComputeInfo(paramType, isReturnType: false);
			var paramLLVMType = LowerType(paramType);
			switch (info)
			{
				case DirectABIArgInfo:
					{
						@params.Add(paramLLVMType);
					}
					break;
				case IndirectABIArgInfo:
					{
						@params.Add(SCPPTypeRef.CreatePointer(paramLLVMType, 0));
					}
					break;
				case AsIntegerABIArgInfo:
					{
						var layout = Target.GetLayout(paramType);
						var llvmIntType = SCPPTypeRef.CreateInt(layout.Size.Bits);
						@params.Add(llvmIntType);
					}
					break;
				case AsRealABIArgInfo:
					{
						var layout = Target.GetLayout(paramType);
						var llvmRealType = layout.Size.Bits switch
						{
							32 => SCPPTypeRef.Float,
							64 => SCPPTypeRef.Double,
							_ => throw new UnreachableException(),
						};
						@params.Add(llvmRealType);
					}
					break;
			}

			llvmIndex++;
		}

		var llvmFnType = SCPPTypeRef.CreateFunctionType(returnType, CollectionsMarshal.AsSpan(@params), false);

		return (SCPPTypeRef.CreatePointer(llvmFnType, 0), llvmFnType);
	}

	// TODO: Optimize calls to this method? This is being called multiple times in sequence when we compile an fn. We could cache the result.
	public LLVMFn GetFnInfoFromFnType(HirFnType fnType)
	{
		//Debug.Assert(fnType is HirFnType fnType);
		var returnType = LowerType(fnType.ReturnType);

		// TODO: Better way?
		var llvmIndex = 0;
		var attrs = new SCPPFnAttrsDef();

		var sret = IsReturnSRet(fnType);
		if (sret)
		{
			attrs.AddParam(llvmIndex, SCPPAttributeRef.CreateTypeAttr(SCPPAttributeKind.StructRet, returnType));
			llvmIndex++;
		}

		foreach (var paramType in fnType.ParamTypes)
		{
			// Add attrs
			// ---

			var info = ComputeInfo(paramType, isReturnType: false);
			//var llvmType = Target.LowerType(paramType);
			switch (info)
			{
				case DirectABIArgInfo:
					{
					}
					break;
				case IndirectABIArgInfo:
					{
					}
					break;
				case AsIntegerABIArgInfo:
					{
					}
					break;
				case AsRealABIArgInfo:
					{
					}
					break;
			}

			llvmIndex++;
		}

		return new LLVMFn(sret ? returnType : (SCPPTypeRef?)null, attrs);
	}

	private bool IsReturnSRet(HirFnType fnType)
	{
		return ComputeInfo(fnType.ReturnType, isReturnType: true) is IndirectABIArgInfo;
	}

	/// <summary>
	/// Computes the ABI arg info for the type.
	///
	/// In general, we only need to compute enough info to know how to correctly compile to LLVM IR.
	/// We don't need to fully implement the ABI's specification because a big part of that is
	/// handled by LLVM itself.
	/// </summary>
	public abstract ABIArgInfo ComputeInfo(HirType type, bool isReturnType);
}

// https://learn.microsoft.com/en-us/cpp/build/x64-calling-convention
internal abstract class x86_64WindowsLLVMTarget(CompilationContext context) : LLVMTarget(context)
{
	protected new x86_64WindowsTarget Target => (x86_64WindowsTarget)base.Target;

	public override ABIArgInfo ComputeInfo(HirType type, bool isReturnType)
	{
		var size = Target.GetLayout(type).Size;
		if (size.IsZeroSized)
		{
			return new IgnoreABIArgInfo();
		}

		if (!(size.Bytes is 1 or 2 or 4 or 8))
		{
			return new IndirectABIArgInfo();
		}

		return type.GetRuntimeKind() switch
		{
			RuntimeTypeKind.Bool or
			RuntimeTypeKind.Char or
			RuntimeTypeKind.Ptr
			=> new DirectABIArgInfo(),

			RuntimeTypeKind.Numeric
			=> new DirectABIArgInfo(),

			RuntimeTypeKind.Struct
			=> new AsIntegerABIArgInfo(),

			_ => throw new UnreachableException(),
		};
	}
}

internal sealed class x86_64WindowsMSVCLLVMTarget(CompilationContext context) : x86_64WindowsLLVMTarget(context)
{
	private new x86_64WindowsMSVCTarget Target => (x86_64WindowsMSVCTarget)Context.Target;
}

// https://gitlab.com/x86-psABIs/x86-64-ABI
