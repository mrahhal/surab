#include "surab_extern.h"

#ifndef SURAB_LLVM_H
#define SURAB_LLVM_H

SURAB_EXTERN_BEGIN

typedef struct SCPPOpaqueContext *SCPPContextRef;

typedef struct SCPPOpaqueModule *SCPPModuleRef;

typedef struct SCPPOpaqueBuilder *SCPPBuilderRef;

typedef struct SCPPOpaqueTargetMachine *SCPPTargetMachineRef;

typedef struct SCPPOpaqueType *SCPPTypeRef;

typedef struct SCPPOpaqueValue *SCPPValueRef;

typedef struct SCPPOpaqueBasicBlock *SCPPBasicBlockRef;

typedef struct SCPPOpaqueAttribute *SCPPAttributeRef;

// ---

bool scpp_setup(
	const char *triple, char **outError,
	SCPPModuleRef *outModule, SCPPBuilderRef *outBuilder, SCPPTargetMachineRef *outTargetMachine);

bool scpp_verify_module(SCPPModuleRef module, char **outMessages);

bool scpp_verify_function(SCPPValueRef fn, char **outMessages);

bool scpp_print_to_file(SCPPModuleRef module, const char *filename, char **outError);

typedef enum
{
	scpp_O0,
	scpp_O1,
	scpp_O2,
	scpp_O3,
	scpp_Os,
	scpp_Oz
} SCPPOptLevel;

bool scpp_emit_o(SCPPModuleRef module, SCPPTargetMachineRef targetMachine, SCPPOptLevel optLevel, char *outputFile, char **outError);

const char *scpp_get_module_name(SCPPModuleRef module);

const char *scpp_get_module_triple(SCPPModuleRef module);

const char *scpp_get_target_machine_triple(SCPPTargetMachineRef targetMachine);

const char *scpp_get_target_machine_cpu(SCPPTargetMachineRef targetMachine);

uint32_t scpp_get_abi_alignment_of_type(SCPPTargetMachineRef targetMachine, SCPPTypeRef ty);

SCPPValueRef scpp_const_int(SCPPTypeRef intTy, uint64_t n, bool signExtend);

SCPPValueRef scpp_const_real(SCPPTypeRef realTy, double n);

typedef enum
{
	scpp_void_type_kind,
	scpp_half_type_kind,
	scpp_bfloat_type_kind,
	scpp_float_type_kind,
	scpp_double_type_kind,
	scpp_x86_fp80_type_kind,
	scpp_fp128_type_kind,
	scpp_ppc_fp128_type_kind,
	scpp_label_type_kind,
	scpp_metadata_type_kind,
	scpp_integer_type_kind,
	scpp_function_type_kind,
	scpp_struct_type_kind,
	scpp_array_type_kind,
	scpp_pointer_type_kind,
	scpp_vector_type_kind,
	scpp_x86_amx_type_kind,
	scpp_token_type_kind,
	scpp_scalable_vector_type_kind,
	scpp_target_ext_type_kind
} SCPPTypeKind;

SCPPTypeKind scpp_get_type_kind(SCPPTypeRef ty);

SCPPValueRef scpp_build_ret(SCPPBuilderRef b, SCPPValueRef v);

SCPPValueRef scpp_build_gep(
	SCPPBuilderRef b, SCPPTypeRef ty,
	SCPPValueRef pointer, SCPPValueRef *indices,
	uint32_t numIndices, const char *name);

SCPPValueRef scpp_build_in_bounds_gep(
	SCPPBuilderRef b, SCPPTypeRef ty,
	SCPPValueRef pointer, SCPPValueRef *indices,
	uint32_t numIndices, const char *name);

SCPPValueRef scpp_build_struct_gep(
	SCPPBuilderRef b, SCPPTypeRef ty,
	SCPPValueRef pointer, uint32_t idx,
	const char *name);

SCPPValueRef scpp_build_global_string(
	SCPPBuilderRef b, const char *str, const char *name);

SCPPValueRef scpp_build_global_string_ptr(
	SCPPBuilderRef b, const char *str, const char *name);

SCPPValueRef scpp_build_alloca(
	SCPPBuilderRef b, SCPPTypeRef ty,
	const char *name);

SCPPValueRef scpp_build_array_alloca(
	SCPPBuilderRef b, SCPPTypeRef ty,
	SCPPValueRef sizeVal, const char *name);

SCPPValueRef scpp_build_free(SCPPBuilderRef b, SCPPValueRef pointerVal);

SCPPValueRef scpp_build_load(
	SCPPBuilderRef b, SCPPTypeRef ty,
	SCPPValueRef pointerVal, const char *name);

SCPPValueRef scpp_build_store(
	SCPPBuilderRef b, SCPPValueRef val,
	SCPPValueRef pointerVal);

SCPPValueRef scpp_build_call_with_type(
	SCPPBuilderRef b, SCPPTypeRef ty, SCPPValueRef fn,
	SCPPValueRef *args, uint32_t numArgs,
	const char *name);

SCPPValueRef scpp_build_call(
	SCPPBuilderRef b, SCPPValueRef fn,
	SCPPValueRef *args, uint32_t numArgs,
	const char *name);

SCPPValueRef scpp_build_icmp(
	SCPPBuilderRef b, /* ICmpInst::Predicate */ int op,
	SCPPValueRef lhs, SCPPValueRef rhs,
	const char *name);

SCPPValueRef scpp_build_fcmp(
	SCPPBuilderRef b, /* FCmpInst::Predicate */ int op,
	SCPPValueRef lhs, SCPPValueRef rhs,
	const char *name);

SCPPValueRef scpp_build_br(SCPPBuilderRef b, SCPPBasicBlockRef dest);

SCPPValueRef scpp_build_cond_br(
	SCPPBuilderRef b, SCPPValueRef ifBlock,
	SCPPBasicBlockRef thenBlock, SCPPBasicBlockRef elseBlock);

SCPPValueRef scpp_build_switch(
	SCPPBuilderRef b, SCPPValueRef v,
	SCPPBasicBlockRef elseBlock, uint32_t numCases);

SCPPValueRef scpp_build_indirect_br(
	SCPPBuilderRef b, SCPPValueRef addr,
	uint32_t numDests);

const char *scpp_get_basic_block_name(SCPPBasicBlockRef bb);

SCPPValueRef scpp_get_basic_block_parent(SCPPBasicBlockRef bb);

SCPPValueRef scpp_get_basic_block_terminator(SCPPBasicBlockRef bb);

uint32_t scpp_count_basic_blocks(SCPPValueRef fnRef);

void scpp_get_basic_blocks(SCPPValueRef fnRef, SCPPBasicBlockRef *basicBlocksRefs);

SCPPBasicBlockRef scpp_get_entry_basic_block(SCPPValueRef fn);

SCPPBasicBlockRef scpp_get_first_basic_block(SCPPValueRef fn);

SCPPBasicBlockRef scpp_get_last_basic_block(SCPPValueRef fn);

SCPPBasicBlockRef scpp_get_next_basic_block(SCPPBasicBlockRef bb);

SCPPBasicBlockRef scpp_get_previous_basic_block(SCPPBasicBlockRef bb);

SCPPBasicBlockRef scpp_append_basic_block(SCPPValueRef fn, const char *name);

void scpp_delete_basic_block(SCPPBasicBlockRef bb);

void scpp_remove_basic_block_from_parent(SCPPBasicBlockRef bb);

void scpp_move_basic_block_before(SCPPBasicBlockRef bb, SCPPBasicBlockRef movePos);

void scpp_move_basic_block_after(SCPPBasicBlockRef bb, SCPPBasicBlockRef movePos);

void scpp_position_builder_at_end(SCPPBuilderRef b, SCPPBasicBlockRef block);

typedef enum
{
	scpp_neg,
	scpp_nsw_neg,
	scpp_nuw_neg,
	scpp_fneg,
	scpp_not,
} SCPPUnaryOp;

SCPPValueRef scpp_build_unary_op(SCPPBuilderRef b, SCPPUnaryOp op, SCPPValueRef v, const char *name);

typedef enum
{
	/* Standard Binary Operators */
	scpp_add,
	scpp_fadd,
	scpp_sub,
	scpp_fsub,
	scpp_mul,
	scpp_fmul,
	scpp_udiv,
	scpp_sdiv,
	scpp_fdiv,
	scpp_urem,
	scpp_srem,
	scpp_frem,

	/* Logical Operators */
	scpp_shl,
	scpp_lshr,
	scpp_ashr,
	scpp_and,
	scpp_or,
	scpp_xor,
} SCPPBinaryOp;

SCPPValueRef scpp_build_binary_op(
	SCPPBuilderRef b, SCPPBinaryOp op, SCPPValueRef lhs, SCPPValueRef rhs, const char *name);

const char *scpp_get_value_name(SCPPValueRef val);

SCPPTypeRef scpp_type_of(SCPPValueRef val);

typedef enum
{
	scpp_external,
	scpp_available_externally,
	scpp_link_once_any,
	scpp_link_once_odr,
	scpp_weak_any,
	scpp_weak_odr,
	scpp_appending,
	scpp_internal,
	scpp_private,
	scpp_external_weak,
	scpp_common
} SCPPLinkage;

SCPPLinkage scpp_get_linkage(SCPPValueRef global);

void scpp_set_linkage(SCPPValueRef global, SCPPLinkage linkage);

unsigned scpp_get_alignment(SCPPValueRef v);

void scpp_set_alignment(SCPPValueRef v, unsigned bytes);

unsigned scpp_count_params(SCPPValueRef fnRef);

void scpp_get_params(SCPPValueRef fnRef, SCPPValueRef *paramRefs);

SCPPValueRef scpp_get_param(SCPPValueRef fnRef, unsigned index);

SCPPValueRef scpp_get_param_parent(SCPPValueRef v);

SCPPValueRef scpp_get_first_param(SCPPValueRef fn);

SCPPValueRef scpp_get_last_param(SCPPValueRef fn);

SCPPValueRef scpp_get_next_param(SCPPValueRef arg);

SCPPValueRef scpp_get_previous_param(SCPPValueRef arg);

void scpp_set_param_alignment(SCPPValueRef arg, unsigned align);

uint32_t scpp_get_attr_kind(SCPPAttributeRef a);

bool scpp_is_attr_enum(SCPPAttributeRef a);

bool scpp_is_attr_type(SCPPAttributeRef a);

bool scpp_is_attr_int(SCPPAttributeRef a);

SCPPAttributeRef scpp_create_enum_attr(
	uint32_t kind, uint64_t val);

SCPPAttributeRef scpp_create_type_attr(
	uint32_t kind, SCPPTypeRef type_ref);

SCPPTypeRef scpp_get_type_attr_value(SCPPAttributeRef a);

void scpp_add_attr_at_index(
	SCPPValueRef fn, int idx, SCPPAttributeRef attr);

void scpp_add_call_site_attr(
	SCPPValueRef cb, int idx, SCPPAttributeRef attr);

typedef enum
{
	scpp_void,

	scpp_int1,
	scpp_int8,
	scpp_int16,
	scpp_int32,
	scpp_int64,

	scpp_real_half,
	scpp_real_float,
	scpp_real_double,
} SCPPTypeOp;

SCPPTypeRef scpp_get_type(SCPPTypeOp op);

SCPPTypeRef scpp_get_int_type(uint32_t numBits);

uint32_t scpp_get_int_type_width(SCPPTypeRef integerTy);

SCPPValueRef scpp_add_function(
	SCPPModuleRef m, const char *name, SCPPTypeRef fnTy);

SCPPValueRef scpp_get_named_function(SCPPModuleRef m, const char *name);

SCPPTypeRef scpp_function_type(
	SCPPTypeRef returnTy,
	SCPPTypeRef *paramTys, uint32_t numParams,
	bool isVarArg);

SCPPTypeRef scpp_get_function_type(SCPPValueRef fn);

bool scpp_is_function_var_arg(SCPPTypeRef functionTy);

SCPPTypeRef scpp_get_return_type(SCPPTypeRef functionTy);

uint32_t scpp_count_param_Types(SCPPTypeRef functionTy);

void scpp_get_param_types(SCPPTypeRef functionTy, SCPPTypeRef *dest);

SCPPTypeRef scpp_struct_type(
	SCPPTypeRef *elementTys,
	uint32_t numElements, bool packed);

SCPPTypeRef scpp_struct_create_named(const char *name);

const char *scpp_get_struct_name(SCPPTypeRef ty);

void scpp_struct_set_body(
	SCPPTypeRef structTy, SCPPTypeRef *elementTys,
	uint32_t numElements, bool packed);

unsigned scpp_count_struct_element_types(SCPPTypeRef structTy);

void scpp_get_struct_element_types(SCPPTypeRef structTy, SCPPTypeRef *dest);

SCPPTypeRef scpp_array_type(SCPPTypeRef elementTy, uint64_t numElements);

SCPPTypeRef scpp_pointer_type(SCPPTypeRef elementTy, uint32_t addressSpace);

SCPPTypeRef scpp_get_element_type(SCPPTypeRef wrappedTy);

uint32_t scpp_get_function_cc(SCPPValueRef fn);

void scpp_set_function_cc(SCPPValueRef fn, uint32_t /* llvm::CallingConv */ cc);

typedef enum
{
	scpp_memcpy,
} SCPPIntrinsicOp;

SCPPValueRef scpp_get_intrinsic(
	SCPPModuleRef module, SCPPBuilderRef b, SCPPIntrinsicOp op,
	SCPPTypeRef *tys, uint32_t numParams);

SURAB_EXTERN_END

#endif
