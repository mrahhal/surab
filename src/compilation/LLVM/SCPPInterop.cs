using System.Runtime.InteropServices;

namespace Surab.Compilation.LLVM;

#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments

internal unsafe static class SCPPInterop
{
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

	[DllImport("scpp", EntryPoint = "scpp_run_opt_pass", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern void run_opt_pass(SCPPOpaque* module);

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
	public static extern uint get_enum_attr_kind_for_name(string name, UIntPtr sLen);

	[DllImport("scpp", EntryPoint = "scpp_get_last_enum_attr_Kind", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern uint get_last_enum_attr_Kind();

	[DllImport("scpp", EntryPoint = "scpp_create_enum_attr", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* create_enum_attr(uint kindID, ulong val);

	[DllImport("scpp", EntryPoint = "scpp_get_enum_attr_kind", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern uint get_enum_attr_kind(SCPPOpaque* a);

	[DllImport("scpp", EntryPoint = "scpp_get_enum_attr_value", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern ulong get_enum_attr_value(SCPPOpaque* a);

	[DllImport("scpp", EntryPoint = "scpp_create_type_attr", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
	public static extern SCPPOpaque* create_type_attr(SCPPAttributeKind kindID, SCPPOpaque* type_ref);

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
	None,

	// Enum
	AllocAlign = 1,
	AllocatedPointer = 2,
	AlwaysInline = 3,
	Builtin = 4,
	Cold = 5,
	Convergent = 6,
	DisableSanitizerInstrumentation = 7,
	FnRetThunkExtern = 8,
	Hot = 9,
	ImmArg = 10,
	InReg = 11,
	InlineHint = 12,
	JumpTable = 13,
	MinSize = 14,
	MustProgress = 15,
	Naked = 16,
	Nest = 17,
	NoAlias = 18,
	NoBuiltin = 19,
	NoCallback = 20,
	NoCapture = 21,
	NoCfCheck = 22,
	NoDuplicate = 23,
	NoFree = 24,
	NoImplicitFloat = 25,
	NoInline = 26,
	NoMerge = 27,
	NoProfile = 28,
	NoRecurse = 29,
	NoRedZone = 30,
	NoReturn = 31,
	NoSanitizeBounds = 32,
	NoSanitizeCoverage = 33,
	NoSync = 34,
	NoUndef = 35,
	NoUnwind = 36,
	NonLazyBind = 37,
	NonNull = 38,
	NullPointerIsValid = 39,
	OptForFuzzing = 40,
	OptimizeForSize = 41,
	OptimizeNone = 42,
	PresplitCoroutine = 43,
	ReadNone = 44,
	ReadOnly = 45,
	Returned = 46,
	ReturnsTwice = 47,
	SExt = 48,
	SafeStack = 49,
	SanitizeAddress = 50,
	SanitizeHWAddress = 51,
	SanitizeMemTag = 52,
	SanitizeMemory = 53,
	SanitizeThread = 54,
	ShadowCallStack = 55,
	SkipProfile = 56,
	Speculatable = 57,
	SpeculativeLoadHardening = 58,
	StackProtect = 59,
	StackProtectReq = 60,
	StackProtectStrong = 61,
	StrictFP = 62,
	SwiftAsync = 63,
	SwiftError = 64,
	SwiftSelf = 65,
	WillReturn = 66,
	WriteOnly = 67,
	ZExt = 68,

	// Type
	ByRef = 69,
	ByVal = 70,
	ElementType = 71,
	InAlloca = 72,
	Preallocated = 73,
	StructRet = 74,

	// Int
	Alignment = 75,
	AllocKind = 76,
	AllocSize = 77,
	Dereferenceable = 78,
	DereferenceableOrNull = 79,
	Memory = 80,
	StackAlignment = 81,
	UWTable = 82,
	VScaleRange = 83,
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
