using System.Runtime.InteropServices;

namespace Surab.Compilation.LLVM;

#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments

internal unsafe static class SCPPInterop
{
	// llvm

	[DllImport("scpp", EntryPoint = "scpp_setup", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern bool setup(
		string triple, sbyte** outError,
		SCPPOpaque** outModule, SCPPOpaque** outBuilder, SCPPOpaque** outTargetMachine);

	[DllImport("scpp", EntryPoint = "scpp_verify_module", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern bool verify_module(SCPPOpaque* module, sbyte** outMessages);

	[DllImport("scpp", EntryPoint = "scpp_verify_function", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern bool verify_function(SCPPOpaque* fn, sbyte** outMessages);

	[DllImport("scpp", EntryPoint = "scpp_print_to_file", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern bool print_to_file(SCPPOpaque* module, string fileName, sbyte** outError);

	[DllImport("scpp", EntryPoint = "scpp_emit_o", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern bool emit_o(SCPPOpaque* module, SCPPOpaque* targetMachine, SCPPOptLevel optLevel, string outputFile, sbyte** outError);

	[DllImport("scpp", EntryPoint = "scpp_get_abi_alignment_of_type", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern uint get_abi_alignment_of_type(SCPPOpaque* targetMachine, SCPPOpaque* ty);

	[DllImport("scpp", EntryPoint = "scpp_const_int", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* const_int(SCPPOpaque* intTy, ulong n, bool signExtend);

	[DllImport("scpp", EntryPoint = "scpp_const_real", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* const_real(SCPPOpaque* realTy, double n);

	[DllImport("scpp", EntryPoint = "scpp_get_type_kind", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPTypeKind get_type_kind(SCPPOpaque* ty);

	[DllImport("scpp", EntryPoint = "scpp_build_ret", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_ret(SCPPOpaque* b, SCPPOpaque* v);

	[DllImport("scpp", EntryPoint = "scpp_build_gep", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_gep(SCPPOpaque* b, SCPPOpaque* ty, SCPPOpaque* pointer, SCPPOpaque** indices, uint numIndices, string name);

	[DllImport("scpp", EntryPoint = "scpp_build_in_bounds_gep", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_in_bounds_gep(SCPPOpaque* b, SCPPOpaque* ty, SCPPOpaque* pointer, SCPPOpaque** indices, uint numIndices, string name);

	[DllImport("scpp", EntryPoint = "scpp_build_struct_gep", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_struct_gep(SCPPOpaque* b, SCPPOpaque* ty, SCPPOpaque* pointer, uint idx, string name);

	[DllImport("scpp", EntryPoint = "scpp_build_global_string", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_global_string(SCPPOpaque* b, string str, string name);

	[DllImport("scpp", EntryPoint = "scpp_build_global_string_ptr", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_global_string_ptr(SCPPOpaque* b, string str, string name);

	[DllImport("scpp", EntryPoint = "scpp_build_alloca", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_alloca(SCPPOpaque* b, SCPPOpaque* ty, string name);

	[DllImport("scpp", EntryPoint = "scpp_build_array_alloca", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_array_alloca(SCPPOpaque* b, SCPPOpaque* ty, SCPPOpaque* sizeVal, string name);

	[DllImport("scpp", EntryPoint = "scpp_build_free", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_free(SCPPOpaque* b, SCPPOpaque* pointerVal);

	[DllImport("scpp", EntryPoint = "scpp_build_load", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_load(SCPPOpaque* b, SCPPOpaque* ty, SCPPOpaque* pointerVal, string name);

	[DllImport("scpp", EntryPoint = "scpp_build_store", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_store(SCPPOpaque* b, SCPPOpaque* val, SCPPOpaque* pointerVal);

	[DllImport("scpp", EntryPoint = "scpp_build_call_with_type", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_call_with_type(SCPPOpaque* b, SCPPOpaque* ty, SCPPOpaque* fn, SCPPOpaque** args, uint numArgs, string name);

	[DllImport("scpp", EntryPoint = "scpp_build_call", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_call(SCPPOpaque* b, SCPPOpaque* fn, SCPPOpaque** args, uint numArgs, string name);

	[DllImport("scpp", EntryPoint = "scpp_build_icmp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_icmp(SCPPOpaque* b, ICmpPredicate op, SCPPOpaque* lhs, SCPPOpaque* rhs, string name);

	[DllImport("scpp", EntryPoint = "scpp_build_fcmp", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_fcmp(SCPPOpaque* b, FCmpPredicate op, SCPPOpaque* lhs, SCPPOpaque* rhs, string name);

	[DllImport("scpp", EntryPoint = "scpp_build_br", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_br(SCPPOpaque* b, SCPPOpaque* dest);

	[DllImport("scpp", EntryPoint = "scpp_build_cond_br", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_cond_br(SCPPOpaque* b, SCPPOpaque* ifBlock, SCPPOpaque* thenBlock, SCPPOpaque* elseBlock);

	[DllImport("scpp", EntryPoint = "scpp_build_switch", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_switch(SCPPOpaque* b, SCPPOpaque* v, SCPPOpaque* elseBlock, uint numCases);

	[DllImport("scpp", EntryPoint = "scpp_build_indirect_br", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_indirect_br(SCPPOpaque* b, SCPPOpaque* addr, uint numDests);

	[DllImport("scpp", EntryPoint = "scpp_get_basic_block_name", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern string get_basic_block_name(SCPPOpaque* bb);

	[DllImport("scpp", EntryPoint = "scpp_get_basic_block_parent", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_basic_block_parent(SCPPOpaque* bb);

	[DllImport("scpp", EntryPoint = "scpp_get_basic_block_terminator", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_basic_block_terminator(SCPPOpaque* bb);

	[DllImport("scpp", EntryPoint = "scpp_count_basic_blocks", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern uint count_basic_blocks(SCPPOpaque* fnRef);

	[DllImport("scpp", EntryPoint = "scpp_get_basic_blocks", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void get_basic_blocks(SCPPOpaque* fnRef, SCPPOpaque** basicBlocksRefs);

	[DllImport("scpp", EntryPoint = "scpp_get_entry_basic_block", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_entry_basic_block(SCPPOpaque* fn);

	[DllImport("scpp", EntryPoint = "scpp_get_first_basic_block", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_first_basic_block(SCPPOpaque* fn);

	[DllImport("scpp", EntryPoint = "scpp_get_last_basic_block", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_last_basic_block(SCPPOpaque* fn);

	[DllImport("scpp", EntryPoint = "scpp_get_next_basic_block", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_next_basic_block(SCPPOpaque* bb);

	[DllImport("scpp", EntryPoint = "scpp_get_previous_basic_block", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_previous_basic_block(SCPPOpaque* bb);

	[DllImport("scpp", EntryPoint = "scpp_append_basic_block", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* append_basic_block(SCPPOpaque* fn, string name);

	[DllImport("scpp", EntryPoint = "scpp_insert_basic_block", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* insert_basic_block(SCPPOpaque* bb, string name);

	[DllImport("scpp", EntryPoint = "scpp_delete_basic_block", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void delete_basic_block(SCPPOpaque* bb);

	[DllImport("scpp", EntryPoint = "scpp_remove_basic_block_from_parent", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void remove_basic_block_from_parent(SCPPOpaque* bb);

	[DllImport("scpp", EntryPoint = "scpp_move_basic_block_before", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void move_basic_block_before(SCPPOpaque* bb, SCPPOpaque* movePos);

	[DllImport("scpp", EntryPoint = "scpp_move_basic_block_after", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void move_basic_block_after(SCPPOpaque* bb, SCPPOpaque* movePos);

	[DllImport("scpp", EntryPoint = "scpp_position_builder_at_end", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void position_builder_at_end(SCPPOpaque* b, SCPPOpaque* block);

	[DllImport("scpp", EntryPoint = "scpp_build_unary_op", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_unary_op(SCPPOpaque* b, SCPPUnaryOp op, SCPPOpaque* v, string name);

	[DllImport("scpp", EntryPoint = "scpp_build_binary_op", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* build_binary_op(SCPPOpaque* b, SCPPBinaryOp op, SCPPOpaque* lhs, SCPPOpaque* rhs, string name);

	[DllImport("scpp", EntryPoint = "scpp_type_of", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* type_of(SCPPOpaque* val);

	[DllImport("scpp", EntryPoint = "scpp_get_linkage", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPLinkage get_linkage(SCPPOpaque* global);

	[DllImport("scpp", EntryPoint = "scpp_set_linkage", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void set_linkage(SCPPOpaque* global, SCPPLinkage linkage);

	[DllImport("scpp", EntryPoint = "scpp_get_alignment", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern uint get_alignment(SCPPOpaque* v);

	[DllImport("scpp", EntryPoint = "scpp_set_alignment", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void set_alignment(SCPPOpaque* v, uint bytes);

	[DllImport("scpp", EntryPoint = "scpp_count_params", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern uint count_params(SCPPOpaque* fnRef);

	[DllImport("scpp", EntryPoint = "scpp_get_params", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void get_params(SCPPOpaque* fnRef, SCPPOpaque** paramRefs);

	[DllImport("scpp", EntryPoint = "scpp_get_param", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_param(SCPPOpaque* fnRef, uint index);

	[DllImport("scpp", EntryPoint = "scpp_get_param_parent", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_param_parent(SCPPOpaque* v);

	[DllImport("scpp", EntryPoint = "scpp_get_first_param", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_first_param(SCPPOpaque* fn);

	[DllImport("scpp", EntryPoint = "scpp_get_last_param", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_last_param(SCPPOpaque* fn);

	[DllImport("scpp", EntryPoint = "scpp_get_next_param", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_next_param(SCPPOpaque* arg);

	[DllImport("scpp", EntryPoint = "scpp_get_previous_param", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_previous_param(SCPPOpaque* arg);

	[DllImport("scpp", EntryPoint = "scpp_set_param_alignment", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void set_param_alignment(SCPPOpaque* arg, uint align);

	[DllImport("scpp", EntryPoint = "scpp_get_enum_attr_kind_for_name", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPAttributeKind get_enum_attr_kind_for_name(string name, UIntPtr sLen);

	[DllImport("scpp", EntryPoint = "scpp_create_enum_attr", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* create_enum_attr(SCPPAttributeKind kind, ulong val);

	[DllImport("scpp", EntryPoint = "scpp_get_enum_attr_kind", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPAttributeKind get_enum_attr_kind(SCPPOpaque* a);

	[DllImport("scpp", EntryPoint = "scpp_get_enum_attr_value", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern ulong get_enum_attr_value(SCPPOpaque* a);

	[DllImport("scpp", EntryPoint = "scpp_create_type_attr", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* create_type_attr(SCPPAttributeKind kind, SCPPOpaque* type_ref);

	[DllImport("scpp", EntryPoint = "scpp_get_type_attr_value", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_type_attr_value(SCPPOpaque* a);

	[DllImport("scpp", EntryPoint = "scpp_add_attr_at_index", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void add_attr_at_index(SCPPOpaque* fn, int idx, SCPPOpaque* attr);

	[DllImport("scpp", EntryPoint = "scpp_add_call_site_attr", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void add_call_site_attr(SCPPOpaque* cb, int idx, SCPPOpaque* attr);

	[DllImport("scpp", EntryPoint = "scpp_get_type", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_type(SCPPTypeOp op);

	[DllImport("scpp", EntryPoint = "scpp_get_int_type", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_int_type(uint numBits);

	[DllImport("scpp", EntryPoint = "scpp_get_int_type_width", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern uint get_int_type_width(SCPPOpaque* integerTy);

	[DllImport("scpp", EntryPoint = "scpp_add_function", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* add_function(SCPPOpaque* module, string name, SCPPOpaque* fnTy);

	[DllImport("scpp", EntryPoint = "scpp_get_named_function", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_named_function(SCPPOpaque* module, string name);

	[DllImport("scpp", EntryPoint = "scpp_function_type", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* function_type(SCPPOpaque* returnTy, SCPPOpaque** paramTys, uint numParams, bool isVarArg);

	[DllImport("scpp", EntryPoint = "scpp_get_function_type", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_function_type(SCPPOpaque* fn);

	[DllImport("scpp", EntryPoint = "scpp_is_function_var_arg", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern bool is_function_var_arg(SCPPOpaque* functionTy);

	[DllImport("scpp", EntryPoint = "scpp_get_return_type", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_return_type(SCPPOpaque* functionTy);

	[DllImport("scpp", EntryPoint = "scpp_count_param_Types", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern uint count_param_Types(SCPPOpaque* functionTy);

	[DllImport("scpp", EntryPoint = "scpp_get_param_types", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void get_param_types(SCPPOpaque* functionTy, SCPPOpaque** dest);

	[DllImport("scpp", EntryPoint = "scpp_struct_type", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* struct_type(SCPPOpaque** elementTys, uint numElements, bool packed);

	[DllImport("scpp", EntryPoint = "scpp_struct_create_named", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* struct_create_named(string name);

	[DllImport("scpp", EntryPoint = "scpp_get_struct_name", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern string get_struct_name(SCPPOpaque* ty);

	[DllImport("scpp", EntryPoint = "scpp_struct_set_body", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void struct_set_body(SCPPOpaque* structTy, SCPPOpaque** elementTys, uint numElements, bool packed);

	[DllImport("scpp", EntryPoint = "scpp_count_struct_element_types", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern uint count_struct_element_types(SCPPOpaque* structTy);

	[DllImport("scpp", EntryPoint = "scpp_get_struct_element_types", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void get_struct_element_types(SCPPOpaque* structTy, SCPPOpaque** dest);

	[DllImport("scpp", EntryPoint = "scpp_array_type", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* array_type(SCPPOpaque* elementTy, ulong numElements);

	[DllImport("scpp", EntryPoint = "scpp_pointer_type", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* pointer_type(SCPPOpaque* elementTy, uint addressSpace);

	[DllImport("scpp", EntryPoint = "scpp_get_element_type", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_element_type(SCPPOpaque* wrappedTy);

	[DllImport("scpp", EntryPoint = "scpp_get_function_cc", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern CallConv get_function_cc(SCPPOpaque* fn);

	[DllImport("scpp", EntryPoint = "scpp_set_function_cc", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void set_function_cc(SCPPOpaque* fn, CallConv cc);

	[DllImport("scpp", EntryPoint = "scpp_get_intrinsic", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* get_intrinsic(SCPPOpaque* module, SCPPOpaque* b, SCPPIntrinsicOp op, SCPPOpaque** tys, uint numParams);

	// lld

	[DllImport("scpp", EntryPoint = "scpp_clang", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern int clang(string triple, string additionalLibs, string inputFiles, string outputFile, sbyte** outError);
}

// Ensure these are always up to date and match the corresponding values in scpp.

internal static class SCPPAttributeIndex
{
	public static readonly int Function = ~0; // -1
	public static readonly int Return = 0;
	public static readonly int FirstArg = 1;
}

internal enum SCPPTypeOp
{
	@void,

	int1,
	int8,
	int16,
	int32,
	int64,

	real_half,
	real_float,
	real_double,
}

internal enum SCPPIntrinsicOp
{
	memcpy_i64,
}

// From llvm::CmpInst::Predicate, split into two enums for convenience.
internal enum FCmpPredicate : uint
{
	// Opcode          U L G E    Intuitive operation
	FCMP_FALSE = 0, // 0 0 0 0    Always false (always folded)
	FCMP_OEQ = 1,   // 0 0 0 1    True if ordered and equal
	FCMP_OGT = 2,   // 0 0 1 0    True if ordered and greater than
	FCMP_OGE = 3,   // 0 0 1 1    True if ordered and greater than or equal
	FCMP_OLT = 4,   // 0 1 0 0    True if ordered and less than
	FCMP_OLE = 5,   // 0 1 0 1    True if ordered and less than or equal
	FCMP_ONE = 6,   // 0 1 1 0    True if ordered and operands are unequal
	FCMP_ORD = 7,   // 0 1 1 1    True if ordered (no nans)
	FCMP_UNO = 8,   // 1 0 0 0    True if unordered: isnan(X) | isnan(Y)
	FCMP_UEQ = 9,   // 1 0 0 1    True if unordered or equal
	FCMP_UGT = 10,  // 1 0 1 0    True if unordered or greater than
	FCMP_UGE = 11,  // 1 0 1 1    True if unordered, greater than, or equal
	FCMP_ULT = 12,  // 1 1 0 0    True if unordered or less than
	FCMP_ULE = 13,  // 1 1 0 1    True if unordered, less than, or equal
	FCMP_UNE = 14,  // 1 1 1 0    True if unordered or not equal
	FCMP_TRUE = 15, // 1 1 1 1    Always true (always folded)
};
internal enum ICmpPredicate : uint
{
	// Opcode
	ICMP_EQ = 32,  // equal
	ICMP_NE = 33,  // not equal
	ICMP_UGT = 34, // unsigned greater than
	ICMP_UGE = 35, // unsigned greater or equal
	ICMP_ULT = 36, // unsigned less than
	ICMP_ULE = 37, // unsigned less or equal
	ICMP_SGT = 38, // signed greater than
	ICMP_SGE = 39, // signed greater or equal
	ICMP_SLT = 40, // signed less than
	ICMP_SLE = 41, // signed less or equal
};

internal enum SCPPUnaryOp
{
	neg,
	nsw_neg,
	nuw_neg,
	fneg,
	not,
}

enum SCPPBinaryOp
{
	/* Standard Binary Operators */
	add,
	fadd,
	sub,
	fsub,
	mul,
	fmul,
	udiv,
	sdiv,
	fdiv,
	urem,
	srem,
	frem,

	/* Logical Operators */
	shl,
	lshr,
	ashr,
	and,
	or,
	xor,
}

internal enum SCPPAttributeKind
{
	// Enum
	AllocAlign = 1,
	AllocatedPointer = 2,
	AlwaysInline = 3,
	Builtin = 4,
	Cold = 5,
	Convergent = 6,
	CoroDestroyOnlyWhenComplete = 7,
	CoroElideSafe = 8,
	DeadOnUnwind = 9,
	DisableSanitizerInstrumentation = 10,
	FnRetThunkExtern = 11,
	Hot = 12,
	HybridPatchable = 13,
	ImmArg = 14,
	InReg = 15,
	InlineHint = 16,
	JumpTable = 17,
	MinSize = 18,
	MustProgress = 19,
	Naked = 20,
	Nest = 21,
	NoAlias = 22,
	NoBuiltin = 23,
	NoCallback = 24,
	NoCapture = 25,
	NoCfCheck = 26,
	NoDivergenceSource = 27,
	NoDuplicate = 28,
	NoExt = 29,
	NoFree = 30,
	NoImplicitFloat = 31,
	NoInline = 32,
	NoMerge = 33,
	NoProfile = 34,
	NoRecurse = 35,
	NoRedZone = 36,
	NoReturn = 37,
	NoSanitizeBounds = 38,
	NoSanitizeCoverage = 39,
	NoSync = 40,
	NoUndef = 41,
	NoUnwind = 42,
	NonLazyBind = 43,
	NonNull = 44,
	NullPointerIsValid = 45,
	OptForFuzzing = 46,
	OptimizeForDebugging = 47,
	OptimizeForSize = 48,
	OptimizeNone = 49,
	PresplitCoroutine = 50,
	ReadNone = 51,
	ReadOnly = 52,
	Returned = 53,
	ReturnsTwice = 54,
	SExt = 55,
	SafeStack = 56,
	SanitizeAddress = 57,
	SanitizeHWAddress = 58,
	SanitizeMemTag = 59,
	SanitizeMemory = 60,
	SanitizeNumericalStability = 61,
	SanitizeRealtime = 62,
	SanitizeRealtimeBlocking = 63,
	SanitizeThread = 64,
	SanitizeType = 65,
	ShadowCallStack = 66,
	SkipProfile = 67,
	Speculatable = 68,
	SpeculativeLoadHardening = 69,
	StackProtect = 70,
	StackProtectReq = 71,
	StackProtectStrong = 72,
	StrictFP = 73,
	SwiftAsync = 74,
	SwiftError = 75,
	SwiftSelf = 76,
	WillReturn = 77,
	Writable = 78,
	WriteOnly = 79,
	ZExt = 80,

	// Type
	ByRef = 81,
	ByVal = 82,
	ElementType = 83,
	InAlloca = 84,
	Preallocated = 85,
	StructRet = 86,

	// Int
	Alignment = 87,
	AllocKind = 88,
	AllocSize = 89,
	Captures = 90,
	Dereferenceable = 91,
	DereferenceableOrNull = 92,
	Memory = 93,
	NoFPClass = 94,
	StackAlignment = 95,
	UWTable = 96,
	VScaleRange = 97,

	// Range
	Range = 98,
}

internal enum SCPPLinkage
{
	external,
	available_externally,
	link_once_any,
	link_once_odr,
	weak_any,
	weak_odr,
	appending,
	@internal,
	@private,
	external_weak,
	common,
}

internal enum SCPPTypeKind
{
	@void,
	half,
	bfloat,
	@float,
	@double,
	x86_fp80,
	fp128,
	ppc_fp128,
	label,
	metadata,
	integer,
	function,
	@struct,
	array,
	pointer,
	vector,
	x86_amx,
	token,
	scalable_vector,
	target_ext,
}

internal enum SCPPOptLevel
{
	O0,
	O1,
	O2,
	O3,
	Os,
	Oz,
}
