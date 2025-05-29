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
			x86_64LinuxGNUTarget => new x86_64LinuxGNULLVMTarget(context),
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
internal abstract class x86_64LinuxLLVMTarget(CompilationContext context) : LLVMTarget(context)
{
	// AMD64 ABI 1.0 – March 12, 2025
	//
	// We don't exactly need these classes for emit since we emit LLVM, but we still need to follow
	// the spec and classify everything in order to compute what we need (e.g. sret).
	//
	// Note: Right now we don't support many of the types that would require some of the classes
	// such as SSEUP. For that reason, the classify algorithm is simplified and some shortcuts
	// are taken.

	internal enum Class
	{
		/// <summary>
		/// This class is used as initializer in the algorithms. It will be used for padding and empty structures and unions.
		/// </summary>
		NO_CLASS,

		/// <summary>
		/// This class consists of integral types that fit into one of the general purpose registers.
		/// </summary>
		INTEGER,
		/// <summary>
		/// This class consists of types that fit into a vector register.
		/// </summary>
		SSE,
		/// <summary>
		/// This class consists of types that fit into a vector register and can be passed and returned in the upper bytes of it.
		/// </summary>
		SSEUP,
		/// <summary>
		/// This class consists of types that will be returned via the x87 FPU.
		/// </summary>
		X87,
		/// <summary>
		/// This class consists of types that will be returned via the x87 FPU and can be passed and returned in the upper bytes of it.
		/// </summary>
		X87UP,
		/// <summary>
		/// This class consists of types that will be returned via the x87 FPU.
		/// </summary>
		COMPLEX_X87,
		/// <summary>
		/// This class consists of types that will be passed and returned in memory via the stack.
		/// </summary>
		MEMORY,
	}

	// TODO: Optimize
	/// <summary>
	/// Useful wrapper around 8 classes representing all possible slots as defined by the ABI.
	/// Each class corresponds to an eightbyte as defined in 3.2.3.
	/// </summary>
	internal class Classes
	{
		public readonly Class[] _arr = new Class[8];

		public Classes(params IEnumerable<Class> classes)
		{
			var i = 0;
			foreach (var c in classes)
			{
				if (i > 7)
				{
					throw new IndexOutOfRangeException("Classes have exactly 8 items.");
				}
				this[i] = c;
				i++;
			}
			if (i <= 7)
			{
				// Fill rest with NO_CLASS.
				for (var j = i; j < 8; j++)
				{
					this[j] = Class.NO_CLASS;
				}
			}
		}

		public int Length => _arr.Length;

		public IEnumerable<Class> Enumerate()
		{
			return _arr;
		}

		public Class this[int i]
		{
			get
			{
				if (i > 8)
				{
					throw new IndexOutOfRangeException("Classes have exactly 8 slots.");
				}
				return _arr[i];
			}
			set
			{
				if (i > 8)
				{
					throw new IndexOutOfRangeException("Classes have exactly 8 slots.");
				}
				_arr[i] = value;
			}
		}

		// TODO (self-host): Wrap BitSize into ClassOffset with additional static verification?

		//public Class GetAtOffset(BitSize offset)
		//{
		//	var i = GetSlotIndexAtOffset(offset);
		//	return _arr[i];
		//}

		//public void SetAtOffset(BitSize offset, Class c)
		//{
		//	var i = GetSlotIndexAtOffset(offset);
		//	_arr[i] = c;
		//}

		//private int GetSlotIndexAtOffset(BitSize offset)
		//{
		//	Debug.Assert(offset.Bytes >= 0 && offset.Bytes <= 8 * 8); // eightbytes * 8 slots
		//	return offset.Bytes / 8;
		//}
	}

	private static readonly Classes MemoryClasses = new(Class.MEMORY);

	protected new x86_64LinuxTarget Target => (x86_64LinuxTarget)base.Target;

	internal Classes Classify(HirType type)
	{
		var layout = Target.GetLayout(type);
		var runtimeKind = type.GetRuntimeKind();

		// "Arguments of types (signed and unsigned) _Bool, char, short, int, long, long long, and pointers are in the INTEGER class."
		if (runtimeKind is RuntimeTypeKind.Bool or RuntimeTypeKind.Char or RuntimeTypeKind.Ptr ||
			type.KnownTypeTag.IsNumericInteger())
		{
			return new(Class.INTEGER);
		}

		// "Arguments of types _Float16, float, double, _Decimal32, _Decimal64 and __m64 are in class SSE."
		if (type.KnownTypeTag.IsNumericFloat())
		{
			return new(Class.SSE);
		}

		// TODO: "Arguments of types __float128, _Decimal128 and __m128 are split into two halves. The least significant ones belong to class SSE, the most significant one to class SSEUP."
		// TODO: "Arguments of type __m256 are split into four eightbyte chunks. The least significant one belongs to class SSE and all the others to class SSEUP."
		// TODO: "Arguments of type __m512 are split into eight eightbyte chunks. The least significant one belongs to class SSE and all the others to class SSEUP."

		// TODO: ? "The 64-bit mantissa of arguments of type long double belongs to class X87, the 16 - bit exponent plus 6 bytes of padding belongs to class X87UP."

		// TODO: "Arguments of type __int128 offer the same operations as INTEGERs, yet they do not fit into one general purpose register but require two registers. [...]"

		// TODO: If arbitrary bit sizes become supported:
		// - "Arguments of type _BitInt(N) with N <= 64 are in the INTEGER class."
		// - "Arguments of type _BitInt(N) with N > 64 are classified as if they were implemented as struct of 64-bit integer fields."

		// TODO: "Arguments of complex T where T is one of the types _Float16, float, double or __float128 are treated as if they are implemented as: [...]"
		// TODO: "A variable of type complex long double is classified as type COMPLEX_X87."

		// "The classification of aggregate (structures and arrays) and union types works as follows:"

		// "1. If the size of an object is larger than eight eightbytes, or it contains unaligned fields, it has class MEMORY."
		if (layout.Size > (8 * 8).AsBytes()) // TODO: or unaligned fields
		{
			return MemoryClasses;
		}

		var aggregateClasses = new Classes();

		if (runtimeKind is RuntimeTypeKind.Struct)
		{
			ClassifyStruct(ref aggregateClasses,(HirStructType)type, 0.AsBytes());
		}

		// TODO: Arrays

		// "5. Then a post merger cleanup is done: [...]"

		for (var i = 0; i < aggregateClasses.Length; i++)
		{
			var c = aggregateClasses[i];

			// "(a) If one of the classes is MEMORY, the whole argument is passed in memory."
			if (c is Class.MEMORY)
				return MemoryClasses;
			// "(b) If X87UP is not preceded by X87, the whole argument is passed in memory."
			if (c is Class.X87UP && (i == 0 || aggregateClasses[i - 1] is not Class.X87))
				return MemoryClasses;
		}

		// "(c) If the size of the aggregate exceeds two eightbytes and the first eightbyte
		// isn’t SSE or any other eightbyte isn’t SSEUP, the whole argument is passed in memory."
		if (layout.Size.Bytes > (2 * 8) &&
			(aggregateClasses[0] is not Class.SSE || !aggregateClasses.Enumerate().Skip(1).All(x => x is Class.SSEUP or Class.NO_CLASS)))
		{
			return MemoryClasses;
		}

		// "(d) If SSEUP is not preceded by SSE or SSEUP, it is converted to SSE."
		for (var i = 0; i < aggregateClasses.Length; i++)
		{
			if (aggregateClasses[i] is Class.SSEUP && aggregateClasses[i - 1] is not Class.SSE and not Class.SSEUP)
			{
				aggregateClasses[i] = Class.SSE;
			}
		}

		return aggregateClasses;
	}

	internal BitSize ClassifyStruct(ref Classes classes, HirStructType type, BitSize offset)
	{
		var structLayout = Target.GetStructLayout(type);
		var sections = structLayout.Sections;

		// "3. If the size of the aggregate exceeds a single eightbyte, each is classified
		// separately. Each eightbyte gets initialized to class NO_CLASS."
		// "4. Each field of an object is classified recursively so that always two fields are considered.

		var currentOffset = offset;
		foreach (var section in sections)
		{
			if (section.Kind is StructLayoutSectionKind.Data)
			{
				var field = type.Fields[section.FieldIndex];
				if (field.Type.GetRuntimeKind() is RuntimeTypeKind.Struct)
				{
					currentOffset = ClassifyStruct(ref classes, (HirStructType)field.Type, currentOffset);
				}
				else
				{
					var fieldClasses = Classify(field.Type);
					for (int slotIndex = currentOffset.Bytes / 8, j = 0; j < fieldClasses.Length; slotIndex++, j++)
					{
						Debug.Assert(slotIndex < 8 && slotIndex + j < 8); // eightbytes * 8 slots

						if (fieldClasses[j] is Class.NO_CLASS)
							break;

						classes[slotIndex] = MergeInSlot(classes[slotIndex], fieldClasses[j]);
					}
				}
			}

			currentOffset += section.Size;
		}

		return currentOffset;
	}

	/// <summary>
	/// Merges two classes occupying the same eightbyte slot.
	/// </summary>
	private Class MergeInSlot(Class a, Class b)
	{
		// "4. Each field of an object is classified recursively so that always two fields 18 are considered.
		// The resulting class is calculated according to the classes of the fields in the eightbyte: [...]"
		if (a == b) return a;
		if (a == Class.NO_CLASS) return b;
		if (b == Class.NO_CLASS) return a;
		if (a == Class.MEMORY || b == Class.MEMORY) return Class.MEMORY;
		if (a == Class.INTEGER || b == Class.INTEGER) return Class.INTEGER;
		if (a == Class.X87 || a == Class.X87UP || a == Class.COMPLEX_X87 || b == Class.X87 || b == Class.X87UP || b == Class.COMPLEX_X87)
			return Class.MEMORY;
		return Class.SSE;
	}

	public override ABIArgInfo ComputeInfo(HirType type, bool isReturnType)
	{
		var size = Target.GetLayout(type).Size;
		if (size.IsZeroSized)
		{
			return new IgnoreABIArgInfo();
		}

		var classes = Classify(type);

		if (classes[0] is Class.MEMORY)
		{
			Debug.Assert(classes[1] is Class.NO_CLASS);
			return new IndirectABIArgInfo();
		}

		if (classes[0] is Class.SSE && classes[1] is Class.NO_CLASS &&
			type.GetRuntimeKind() is RuntimeTypeKind.Struct)
		{
			return new AsRealABIArgInfo();
		}

		if (classes[0] is Class.INTEGER && classes[1] is Class.NO_CLASS &&
			type.GetRuntimeKind() is RuntimeTypeKind.Struct)
		{
			return new AsIntegerABIArgInfo();
		}

		return new DirectABIArgInfo();
	}
}

internal sealed class x86_64LinuxGNULLVMTarget(CompilationContext context) : x86_64LinuxLLVMTarget(context)
{
	private new x86_64LinuxGNUTarget Target => (x86_64LinuxGNUTarget)Context.Target;
}
