using System.Diagnostics.CodeAnalysis;

namespace Surab.Compilation.LLVM;

// scpp is a surab c lib exposing bindings to generate llvm IR.
internal unsafe static class SCPP
{
	public static bool Setup(
		string triple,
		out string? error,
		[NotNullWhen(true)] out SCPPModuleRef? module,
		[NotNullWhen(true)] out SCPPBuilderRef? builder,
		[NotNullWhen(true)] out SCPPTargetMachineRef? targetMachine)
	{
		sbyte* outError = null;
		SCPPOpaque* outModule;
		SCPPOpaque* outBuilder;
		SCPPOpaque* outTargetMachine;
		var succeeded = SCPPInterop.setup(triple, &outError, &outModule, &outBuilder, &outTargetMachine);

		error = outError == null ? string.Empty : SCPPExtensions.AsString(outError);
		module = outModule;
		builder = outBuilder;
		targetMachine = outTargetMachine;

		return succeeded;
	}

	public static bool VerifyFunction(SCPPValueRef fn, out string? messages)
	{
		sbyte* outMessages = null;
		var result = SCPPInterop.verify_function(fn, &outMessages);
		messages = outMessages == null ? string.Empty : SCPPExtensions.AsString(outMessages);
		return result;
	}

	public static SCPPValueRef GetIntrinsic(SCPPModuleRef module, SCPPBuilderRef builder, SCPPIntrinsicOp op, ReadOnlySpan<SCPPTypeRef> paramTypes)
	{
		fixed (SCPPTypeRef* pParamTypes = paramTypes)
		{
			return SCPPInterop.get_intrinsic(module, builder, op, (SCPPOpaque**)pParamTypes, (uint)paramTypes.Length);
		}
	}

	public static uint GetEnumKindForName(string name)
	{
		return SCPPInterop.get_enum_attr_kind_for_name(name, (UIntPtr)name.Length);
	}

	public static uint GetLastEnumAttrKind()
	{
		return SCPPInterop.get_last_enum_attr_Kind();
	}

	public static bool Lld(IEnumerable<string> additionalLibs, IEnumerable<string> inputFiles, string outputFile, out string error)
	{
		sbyte* outError = null;
		var result = SCPPInterop.lld(
			string.Join(';', additionalLibs),
			string.Join(';', inputFiles),
			outputFile,
			&outError);
		error = outError == null ? string.Empty : SCPPExtensions.AsString(outError);
		return result;
	}
}

// A pointer to this represents an opaque pointer used when invoking bindings in scpp.
internal unsafe readonly struct SCPPOpaque
{
}

internal unsafe readonly struct SCPPModuleRef : IEquatable<SCPPModuleRef>
{
	public readonly IntPtr Handle;

	public SCPPModuleRef(IntPtr handle)
	{
		Handle = handle;
	}

	public static implicit operator SCPPModuleRef(SCPPOpaque* value) => new((IntPtr)value);
	public static implicit operator SCPPOpaque*(SCPPModuleRef value) => (SCPPOpaque*)value.Handle;

	public static bool operator ==(SCPPModuleRef left, SCPPModuleRef right) => left.Handle == right.Handle;
	public static bool operator !=(SCPPModuleRef left, SCPPModuleRef right) => !(left == right);

	public override bool Equals(object? obj) => (obj is SCPPModuleRef other) && Equals(other);
	public bool Equals(SCPPModuleRef other) => this == other;

	public override int GetHashCode() => Handle.GetHashCode();

	// ---

	public bool Verify(out string messages)
	{
		sbyte* outMessages = null;
		var result = SCPPInterop.verify_module(this, &outMessages);
		messages = outMessages == null ? string.Empty : SCPPExtensions.AsString(outMessages);
		return result;
	}

	public bool PrintToFile(string fileName, out string error)
	{
		sbyte* outError = null;
		var result = SCPPInterop.print_to_file(this, fileName, &outError);
		error = outError == null ? string.Empty : SCPPExtensions.AsString(outError);
		return result;
	}

	public void RunOptimizationPass()
	{
		SCPPInterop.run_opt_pass(this);
	}

	public SCPPValueRef AddFunction(string name, SCPPTypeRef fnType)
	{
		return SCPPInterop.add_function(this, name, fnType);
	}

	public SCPPValueRef GetNamedFunction(string name)
	{
		return SCPPInterop.get_named_function(this, name);
	}
}

internal unsafe readonly struct SCPPBuilderRef
{
	public readonly IntPtr Handle;

	public SCPPBuilderRef(IntPtr handle)
	{
		Handle = handle;
	}

	public static implicit operator SCPPBuilderRef(SCPPOpaque* value) => new((IntPtr)value);
	public static implicit operator SCPPOpaque*(SCPPBuilderRef value) => (SCPPOpaque*)value.Handle;

	public static bool operator ==(SCPPBuilderRef left, SCPPBuilderRef right) => left.Handle == right.Handle;
	public static bool operator !=(SCPPBuilderRef left, SCPPBuilderRef right) => !(left == right);

	public override bool Equals(object? obj) => (obj is SCPPBuilderRef other) && Equals(other);
	public bool Equals(SCPPBuilderRef other) => this == other;

	public override int GetHashCode() => Handle.GetHashCode();

	// ---

	public SCPPValueRef BuildRet(SCPPValueRef v)
	{
		return SCPPInterop.build_ret(this, v);
	}

	public SCPPValueRef BuildRetVoid()
	{
		return SCPPInterop.build_ret(this, null);
	}

	public SCPPValueRef BuildGEP(SCPPTypeRef type, SCPPValueRef pointer, ReadOnlySpan<SCPPValueRef> indices, string name = "")
	{
		fixed (SCPPValueRef* pIndices = indices)
		{
			return SCPPInterop.build_gep(this, type, pointer, (SCPPOpaque**)pIndices, (uint)indices.Length, name);
		}
	}

	public SCPPValueRef BuildInBoundsGEP(SCPPTypeRef type, SCPPValueRef pointer, ReadOnlySpan<SCPPValueRef> indices, string name = "")
	{
		fixed (SCPPValueRef* pIndices = indices)
		{
			return SCPPInterop.build_in_bounds_gep(this, type, pointer, (SCPPOpaque**)pIndices, (uint)indices.Length, name);
		}
	}

	public SCPPValueRef BuildStructGEP(SCPPTypeRef type, SCPPValueRef pointer, int index, string name = "")
	{
		return SCPPInterop.build_struct_gep(this, type, pointer, (uint)index, name);
	}

	public SCPPValueRef BuildGlobalString(string str, string name = "")
	{
		return SCPPInterop.build_global_string(this, str, name);
	}

	public SCPPValueRef BuildGlobalStringPtr(string str, string name = "")
	{
		return SCPPInterop.build_global_string_ptr(this, str, name);
	}

	public SCPPValueRef BuildAlloca(SCPPTypeRef type, string name = "")
	{
		return SCPPInterop.build_alloca(this, type, name);
	}

	public SCPPValueRef BuildArrayAlloca(SCPPTypeRef type, SCPPValueRef sizeValue, string name = "")
	{
		return SCPPInterop.build_array_alloca(this, type, sizeValue, name);
	}

	public SCPPValueRef BuildFree(SCPPValueRef pointerValue)
	{
		return SCPPInterop.build_free(this, pointerValue);
	}

	public SCPPValueRef BuildLoad(SCPPTypeRef type, SCPPValueRef pointerValue, string name = "")
	{
		return SCPPInterop.build_load(this, type, pointerValue, name);
	}

	public SCPPValueRef BuildStore(SCPPValueRef value, SCPPValueRef pointerValue)
	{
		return SCPPInterop.build_store(this, value, pointerValue);
	}

	public SCPPValueRef BuildCallWithType(SCPPTypeRef type, SCPPValueRef fn, ReadOnlySpan<SCPPValueRef> args, string name = "")
	{
		fixed (SCPPValueRef* pArgs = args)
		{
			return SCPPInterop.build_call_with_type(this, type, fn, (SCPPOpaque**)pArgs, (uint)args.Length, name);
		}
	}

	public SCPPValueRef BuildCall(SCPPValueRef fn, ReadOnlySpan<SCPPValueRef> args, string name = "")
	{
		fixed (SCPPValueRef* pArgs = args)
		{
			return SCPPInterop.build_call(this, fn, (SCPPOpaque**)pArgs, (uint)args.Length, name);
		}
	}

	public SCPPValueRef BuildICmp(ICmpPredicate op, SCPPValueRef lhs, SCPPValueRef rhs, string name = "")
	{
		return SCPPInterop.build_icmp(this, op, lhs, rhs, name);
	}

	public SCPPValueRef BuildFCmp(FCmpPredicate op, SCPPValueRef lhs, SCPPValueRef rhs, string name = "")
	{
		return SCPPInterop.build_fcmp(this, op, lhs, rhs, name);
	}

	public SCPPValueRef BuildBr(SCPPBasicBlockRef dest)
	{
		return SCPPInterop.build_br(this, dest);
	}

	public SCPPValueRef BuildCondBr(SCPPValueRef ifBlock, SCPPBasicBlockRef thenBlock, SCPPBasicBlockRef elseBlock)
	{
		return SCPPInterop.build_cond_br(this, ifBlock, thenBlock, elseBlock);
	}

	public SCPPValueRef BuildSwitch(SCPPValueRef value, SCPPBasicBlockRef elseBlock, uint numCases)
	{
		return SCPPInterop.build_switch(this, value, elseBlock, numCases);
	}

	public SCPPValueRef BuildIndirectBr(SCPPValueRef addr, uint numDests)
	{
		return SCPPInterop.build_indirect_br(this, addr, numDests);
	}

	public void PositionAtEnd(SCPPBasicBlockRef block)
	{
		SCPPInterop.position_builder_at_end(this, block);
	}

	public SCPPValueRef BuildUnaryOp(SCPPUnaryOp op, SCPPValueRef val, string name = "")
	{
		return SCPPInterop.build_unary_op(this, op, val, name);
	}

	public SCPPValueRef BuildBinaryOp(SCPPBinaryOp op, SCPPValueRef lhs, SCPPValueRef rhs, string name = "")
	{
		return SCPPInterop.build_binary_op(this, op, lhs, rhs, name);
	}
}

internal unsafe readonly struct SCPPTargetMachineRef
{
	public readonly IntPtr Handle;

	public SCPPTargetMachineRef(IntPtr handle)
	{
		Handle = handle;
	}

	public static implicit operator SCPPTargetMachineRef(SCPPOpaque* value) => new((IntPtr)value);
	public static implicit operator SCPPOpaque*(SCPPTargetMachineRef value) => (SCPPOpaque*)value.Handle;

	public static bool operator ==(SCPPTargetMachineRef left, SCPPTargetMachineRef right) => left.Handle == right.Handle;
	public static bool operator !=(SCPPTargetMachineRef left, SCPPTargetMachineRef right) => !(left == right);

	public override bool Equals(object? obj) => (obj is SCPPTargetMachineRef other) && Equals(other);
	public bool Equals(SCPPTargetMachineRef other) => this == other;

	public override int GetHashCode() => Handle.GetHashCode();

	// ---

	public int GetAbiAlignmentOfType(SCPPTypeRef type)
	{
		return (int)SCPPInterop.get_abi_alignment_of_type(this, type);
	}
}

internal unsafe readonly struct SCPPTypeRef
{
	public readonly IntPtr Handle;

	public SCPPTypeRef(IntPtr handle)
	{
		Handle = handle;
	}

	public static implicit operator SCPPTypeRef(SCPPOpaque* value) => new((IntPtr)value);
	public static implicit operator SCPPOpaque*(SCPPTypeRef value) => (SCPPOpaque*)value.Handle;

	public static bool operator ==(SCPPTypeRef left, SCPPTypeRef right) => left.Handle == right.Handle;
	public static bool operator !=(SCPPTypeRef left, SCPPTypeRef right) => !(left == right);

	public override bool Equals(object? obj) => (obj is SCPPTypeRef other) && Equals(other);
	public bool Equals(SCPPTypeRef other) => this == other;

	public override int GetHashCode() => Handle.GetHashCode();

	// ---

	public static SCPPTypeRef Void => SCPPInterop.get_type(SCPPTypeOp.@void);
	public static SCPPTypeRef Int1 => SCPPInterop.get_type(SCPPTypeOp.int1);
	public static SCPPTypeRef Int8 => SCPPInterop.get_type(SCPPTypeOp.int8);
	public static SCPPTypeRef Int16 => SCPPInterop.get_type(SCPPTypeOp.int16);
	public static SCPPTypeRef Int32 => SCPPInterop.get_type(SCPPTypeOp.int32);
	public static SCPPTypeRef Int64 => SCPPInterop.get_type(SCPPTypeOp.int64);
	public static SCPPTypeRef Half => SCPPInterop.get_type(SCPPTypeOp.real_half);
	public static SCPPTypeRef Float => SCPPInterop.get_type(SCPPTypeOp.real_float);
	public static SCPPTypeRef Double => SCPPInterop.get_type(SCPPTypeOp.real_double);

	public static SCPPTypeRef CreateInt(int bits)
	{
		return SCPPInterop.get_int_type((uint)bits);
	}

	public static SCPPTypeRef CreateFunctionType(SCPPTypeRef returnType, ReadOnlySpan<SCPPTypeRef> paramTypes, bool isVarArg = false)
	{
		fixed (SCPPTypeRef* pParamTypes = paramTypes)
		{
			return SCPPInterop.function_type(returnType, (SCPPOpaque**)pParamTypes, (uint)paramTypes.Length, isVarArg);
		}
	}

	public static SCPPTypeRef CreateStructType(ReadOnlySpan<SCPPTypeRef> elementTypes, bool packed)
	{
		fixed (SCPPTypeRef* pElementTypes = elementTypes)
		{
			return SCPPInterop.struct_type((SCPPOpaque**)pElementTypes, (uint)elementTypes.Length, packed);
		}
	}

	public static SCPPTypeRef CreateNamedStruct(string name)
	{
		return SCPPInterop.struct_create_named(name);
	}

	public void StructSetBody(ReadOnlySpan<SCPPTypeRef> elementTypes, bool packed)
	{
		fixed (SCPPTypeRef* pElementTypes = elementTypes)
		{
			SCPPInterop.struct_set_body(this, (SCPPOpaque**)pElementTypes, (uint)elementTypes.Length, packed);
		}
	}

	public static SCPPTypeRef CreateArray(SCPPTypeRef elementType, int numElements)
	{
		return SCPPInterop.array_type(elementType, (ulong)numElements);
	}

	public static SCPPTypeRef CreatePointer(SCPPTypeRef elementType, int addressSpace = 0)
	{
		return SCPPInterop.pointer_type(elementType, (uint)addressSpace);
	}

	public bool IsFunctionVarArg()
	{
		return SCPPInterop.is_function_var_arg(this);
	}

	public SCPPTypeRef GetFunctionReturnType()
	{
		return SCPPInterop.get_return_type(this);
	}

	public int CountParamTypes()
	{
		return (int)SCPPInterop.count_param_Types(this);
	}

	public SCPPTypeRef[] GetParamTypes()
	{
		var dest = new SCPPTypeRef[CountParamTypes()];

		fixed (SCPPTypeRef* pDest = dest)
		{
			SCPPInterop.get_param_types(this, (SCPPOpaque**)pDest);
		}

		return dest;
	}

	public SCPPTypeRef GetElementType()
	{
		return SCPPInterop.get_element_type(this);
	}

	public SCPPTypeKind GetKind()
	{
		return SCPPInterop.get_type_kind(this);
	}

	public int CountStructElementTypes()
	{
		return (int)SCPPInterop.count_struct_element_types(this);
	}

	public SCPPTypeRef[] GetStructElementTypes()
	{
		var dest = new SCPPTypeRef[CountStructElementTypes()];

		fixed (SCPPTypeRef* pDest = dest)
		{
			SCPPInterop.get_struct_element_types(this, (SCPPOpaque**)pDest);
		}

		return dest;
	}
}

internal unsafe readonly struct SCPPValueRef
{
	public readonly IntPtr Handle;

	public SCPPValueRef(IntPtr handle)
	{
		Handle = handle;
	}

	public static implicit operator SCPPValueRef(SCPPOpaque* value) => new((IntPtr)value);
	public static implicit operator SCPPOpaque*(SCPPValueRef value) => (SCPPOpaque*)value.Handle;

	public static bool operator ==(SCPPValueRef left, SCPPValueRef right) => left.Handle == right.Handle;
	public static bool operator !=(SCPPValueRef left, SCPPValueRef right) => !(left == right);

	public override bool Equals(object? obj) => (obj is SCPPValueRef other) && Equals(other);
	public bool Equals(SCPPValueRef other) => this == other;

	public override int GetHashCode() => Handle.GetHashCode();

	// ---

	public SCPPTypeRef GetFunctionType()
	{
		return SCPPInterop.get_function_type(this);
	}

	public CallConv GetFunctionCallConv()
	{
		return SCPPInterop.get_function_cc(this);
	}

	public void SetFunctionCallConv(CallConv cc)
	{
		SCPPInterop.set_function_cc(this, cc);
	}

	public static SCPPValueRef CreateConstInt(SCPPTypeRef intType, ulong n, bool signExtend = false)
	{
		return SCPPInterop.const_int(intType, n, signExtend);
	}

	public static SCPPValueRef CreateConstReal(SCPPTypeRef realType, double n)
	{
		return SCPPInterop.const_real(realType, n);
	}

	public void AddAttributeAtIndex(int index, SCPPAttributeRef attribute)
	{
		SCPPInterop.add_attr_at_index(this, index, attribute);
	}

	public void AddCallSiteAttribute(int index, SCPPAttributeRef attribute)
	{
		SCPPInterop.add_call_site_attr(this, index, attribute);
	}

	public SCPPBasicBlockRef AppendBasicBlock(string name)
	{
		return SCPPInterop.append_basic_block(this, name);
	}

	public int GetBasicBlocksCount()
	{
		return (int)SCPPInterop.count_basic_blocks(this);
	}

	public SCPPBasicBlockRef[] GetBasicBlocks()
	{
		var basicBlocks = new SCPPBasicBlockRef[GetBasicBlocksCount()];

		fixed (SCPPBasicBlockRef* pBasicBlocks = basicBlocks)
		{
			SCPPInterop.get_basic_blocks(this, (SCPPOpaque**)pBasicBlocks);
		}

		return basicBlocks;
	}

	public SCPPBasicBlockRef GetEntryBasicBlock()
	{
		return SCPPInterop.get_entry_basic_block(this);
	}

	public SCPPBasicBlockRef GetFirstBasicBlock()
	{
		return SCPPInterop.get_first_basic_block(this);
	}

	public SCPPBasicBlockRef GetLastBasicBlock()
	{
		return SCPPInterop.get_last_basic_block(this);
	}

	public int GetAlignment()
	{
		return (int)SCPPInterop.get_alignment(this);
	}

	public void SetAlignment(int bytes)
	{
		SCPPInterop.set_alignment(this, (uint)bytes);
	}

	public int CountParams()
	{
		return (int)SCPPInterop.count_params(this);
	}

	public SCPPValueRef[] GetParams()
	{
		var dest = new SCPPValueRef[CountParams()];

		fixed (SCPPValueRef* pDest = dest)
		{
			SCPPInterop.get_params(this, (SCPPOpaque**)pDest);
		}

		return dest;
	}

	public SCPPValueRef GetParam(int index)
	{
		return SCPPInterop.get_param(this, (uint)index);
	}

	public SCPPValueRef GetParamParent()
	{
		return SCPPInterop.get_param_parent(this);
	}

	public SCPPValueRef GetFirstParam()
	{
		return SCPPInterop.get_first_param(this);
	}

	public SCPPValueRef GetLastParam()
	{
		return SCPPInterop.get_last_param(this);
	}

	public SCPPValueRef GetNextParam()
	{
		return SCPPInterop.get_next_param(this);
	}

	public SCPPValueRef GetPreviousParam()
	{
		return SCPPInterop.get_previous_param(this);
	}

	public void SetParamAlignment(int align)
	{
		SCPPInterop.set_param_alignment(this, (uint)align);
	}

	public SCPPTypeRef GetTypeOf()
	{
		return SCPPInterop.type_of(this);
	}

	public SCPPLinkage GetLinkage()
	{
		return SCPPInterop.get_linkage(this);
	}

	public void SetLinkage(SCPPLinkage linkage)
	{
		SCPPInterop.set_linkage(this, linkage);
	}
}

internal unsafe readonly struct SCPPBasicBlockRef
{
	public readonly IntPtr Handle;

	public SCPPBasicBlockRef(IntPtr handle)
	{
		Handle = handle;
	}

	public static implicit operator SCPPBasicBlockRef(SCPPOpaque* value) => new((IntPtr)value);
	public static implicit operator SCPPOpaque*(SCPPBasicBlockRef value) => (SCPPOpaque*)value.Handle;

	public static bool operator ==(SCPPBasicBlockRef left, SCPPBasicBlockRef right) => left.Handle == right.Handle;
	public static bool operator !=(SCPPBasicBlockRef left, SCPPBasicBlockRef right) => !(left == right);

	public override bool Equals(object? obj) => (obj is SCPPBasicBlockRef other) && Equals(other);
	public bool Equals(SCPPBasicBlockRef other) => this == other;

	public override int GetHashCode() => Handle.GetHashCode();

	// ---

	public void Delete()
	{
		SCPPInterop.delete_basic_block(this);
	}

	public void RemoveFromParent()
	{
		SCPPInterop.remove_basic_block_from_parent(this);
	}

	public void MoveBefore(SCPPBasicBlockRef movePos)
	{
		SCPPInterop.move_basic_block_before(this, movePos);
	}

	public void MoveAfter(SCPPBasicBlockRef movePos)
	{
		SCPPInterop.move_basic_block_after(this, movePos);
	}

	public string GetName()
	{
		return SCPPInterop.get_basic_block_name(this);
	}

	public SCPPValueRef GetParent()
	{
		return SCPPInterop.get_basic_block_parent(this);
	}

	public SCPPValueRef GetTerminator()
	{
		return SCPPInterop.get_basic_block_terminator(this);
	}

	public SCPPBasicBlockRef GetNext()
	{
		return SCPPInterop.get_next_basic_block(this);
	}

	public SCPPBasicBlockRef GetPrevious()
	{
		return SCPPInterop.get_previous_basic_block(this);
	}
}

internal unsafe readonly struct SCPPAttributeRef
{
	public readonly IntPtr Handle;

	public SCPPAttributeRef(IntPtr handle)
	{
		Handle = handle;
	}

	public static implicit operator SCPPAttributeRef(SCPPOpaque* value) => new((IntPtr)value);
	public static implicit operator SCPPOpaque*(SCPPAttributeRef value) => (SCPPOpaque*)value.Handle;

	public static bool operator ==(SCPPAttributeRef left, SCPPAttributeRef right) => left.Handle == right.Handle;
	public static bool operator !=(SCPPAttributeRef left, SCPPAttributeRef right) => !(left == right);

	public override bool Equals(object? obj) => (obj is SCPPAttributeRef other) && Equals(other);
	public bool Equals(SCPPAttributeRef other) => this == other;

	public override int GetHashCode() => Handle.GetHashCode();

	// ---

	public static SCPPAttributeRef CreateEnum(uint kindID, ulong value)
	{
		return SCPPInterop.create_enum_attr(kindID, value);
	}

	public static SCPPAttributeRef Create(SCPPAttributeKind kindID, SCPPTypeRef type)
	{
		return SCPPInterop.create_type_attr(kindID, type);
	}

	public uint GetEnumKind()
	{
		return SCPPInterop.get_enum_attr_kind(this);
	}

	public ulong GetEnumValue()
	{
		return SCPPInterop.get_enum_attr_value(this);
	}

	public SCPPTypeRef GetTypeValue()
	{
		return SCPPInterop.get_type_attr_value(this);
	}
}
