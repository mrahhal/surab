#include "c_wrapping_defs.h"
#include "surab_llvm.h"

// This header has a few warnings that we don't really care about.
#pragma warning(disable : 4624)
#include "llvm/IR/Operator.h"
#pragma warning(pop)

#include "llvm/IR/IRBuilder.h"
#include "llvm/IR/LegacyPassManager.h"
#include "llvm/IR/LLVMContext.h"
#include "llvm/IR/Module.h"
#include "llvm/IR/Verifier.h"
#include "llvm/MC/TargetRegistry.h"
#include "llvm/Passes/PassBuilder.h"
#include "llvm/Support/FileSystem.h"
#include "llvm/Support/TargetSelect.h"
#include "llvm/Target/TargetMachine.h"
#include "llvm/Target/TargetOptions.h"

SCPP_DEFINE_SIMPLE_CONVERSION_FUNCTIONS(llvm::LLVMContext, SCPPContextRef)
SCPP_DEFINE_SIMPLE_CONVERSION_FUNCTIONS(llvm::Module, SCPPModuleRef)
SCPP_DEFINE_SIMPLE_CONVERSION_FUNCTIONS(llvm::IRBuilder<>, SCPPBuilderRef)
SCPP_DEFINE_SIMPLE_CONVERSION_FUNCTIONS(llvm::TargetMachine, SCPPTargetMachineRef)
SCPP_DEFINE_ISA_CONVERSION_FUNCTIONS(llvm::Type, SCPPTypeRef)
SCPP_DEFINE_ISA_CONVERSION_FUNCTIONS(llvm::Value, SCPPValueRef)
SCPP_DEFINE_SIMPLE_CONVERSION_FUNCTIONS(llvm::BasicBlock, SCPPBasicBlockRef)

// Some additional special conversions.

inline SCPPAttributeRef w(llvm::Attribute attr)
{
	return reinterpret_cast<SCPPAttributeRef>(attr.getRawPointer());
}

inline llvm::Attribute uw(SCPPAttributeRef attr)
{
	return llvm::Attribute::fromRawPointer(attr);
}

inline llvm::Type **uw(SCPPTypeRef *tys)
{
	return reinterpret_cast<llvm::Type **>(tys);
}

inline llvm::Value **uw(SCPPValueRef *vals)
{
	return reinterpret_cast<llvm::Value **>(vals);
}

// ---

static llvm::LLVMContext *GlobalContext;

SCPPContextRef getGlobalContext() { return w(GlobalContext); }

// ---

bool scpp_setup(
	const char *triple, char **outError,
	SCPPModuleRef *outModule, SCPPBuilderRef *outBuilder, SCPPTargetMachineRef *outTargetMachine)
{
	llvm::InitializeAllTargetInfos();
	llvm::InitializeAllTargets();
	llvm::InitializeAllTargetMCs();
	llvm::InitializeAllAsmParsers();
	llvm::InitializeAllAsmPrinters();

	GlobalContext = new llvm::LLVMContext();
	auto module = new llvm::Module("surab_main_module", *GlobalContext);
	auto builder = new llvm::IRBuilder<>(*GlobalContext);

	module->setTargetTriple(triple);

	std::string error;
	auto target = llvm::TargetRegistry::lookupTarget(triple, error);
	if (!target)
	{
		if (outError)
			*outError = strdup(error.c_str());

		return false;
	}

	llvm::TargetOptions topts;
	auto targetMachine = target->createTargetMachine(triple, "", "", topts, std::nullopt /* CodeModel::Model, CodeGenOptLevel, bool JIT */);

	*outModule = w(module);
	*outBuilder = w(builder);
	*outTargetMachine = w(targetMachine);

	return true;
}

bool scpp_verify_module(SCPPModuleRef module, char **outMessages)
{
	std::string messages;
	llvm::raw_string_ostream stream(messages);
	auto result = llvm::verifyModule(*uw(module), &stream);
	if (outMessages)
		*outMessages = strdup(stream.str().c_str());
	return !result;
}

bool scpp_verify_function(SCPPValueRef fn, char **outMessages)
{
	std::string messages;
	llvm::raw_string_ostream stream(messages);
	auto result = llvm::verifyFunction(*uw<llvm::Function>(fn), &stream);
	if (outMessages)
		*outMessages = strdup(stream.str().c_str());
	return !result;
}

bool scpp_print_to_file(SCPPModuleRef module, const char *filename, char **outError)
{
	std::error_code ec;
	llvm::raw_fd_ostream dest(filename, ec, llvm::sys::fs::OF_TextWithCRLF);
	if (ec)
	{
		*outError = strdup(ec.message().c_str());
		return false;
	}

	uw(module)->print(dest, nullptr);

	dest.close();

	if (dest.has_error())
	{
		std::string e = "Error printing to file: " + dest.error().message();
		*outError = strdup(e.c_str());
		return false;
	}

	return true;
}

llvm::OptimizationLevel map_opt_level(SCPPOptLevel optLevel)
{
	switch (optLevel)
	{
	case SCPPOptLevel::scpp_O0:
		return llvm::OptimizationLevel::O0;
	case SCPPOptLevel::scpp_O1:
		return llvm::OptimizationLevel::O1;
	case SCPPOptLevel::scpp_O2:
		return llvm::OptimizationLevel::O2;
	case SCPPOptLevel::scpp_O3:
		return llvm::OptimizationLevel::O3;
	case SCPPOptLevel::scpp_Os:
		return llvm::OptimizationLevel::Os;
	case SCPPOptLevel::scpp_Oz:
		return llvm::OptimizationLevel::Oz;

	default:
		llvm_unreachable("Unhandled opt level.");
	}
}

bool scpp_emit_o(SCPPModuleRef moduleRef, SCPPTargetMachineRef targetMachineRef, SCPPOptLevel optLevel, char *outputFile, char **outError)
{
	auto module = uw(moduleRef);
	auto targetMachine = uw(targetMachineRef);

	llvm::LoopAnalysisManager lam;
	llvm::FunctionAnalysisManager fam;
	llvm::CGSCCAnalysisManager cgam;
	llvm::ModuleAnalysisManager mam;

	llvm::PassBuilder pb;

	pb.registerModuleAnalyses(mam);
	pb.registerCGSCCAnalyses(cgam);
	pb.registerFunctionAnalyses(fam);
	pb.registerLoopAnalyses(lam);
	pb.crossRegisterProxies(lam, fam, cgam, mam);

	llvm::ModulePassManager mpm = pb.buildPerModuleDefaultPipeline(
		map_opt_level(optLevel));

	mpm.run(*module, mam);

	// Emit object file.

	std::error_code ec;
	llvm::raw_fd_ostream dest(outputFile, ec, llvm::sys::fs::OF_None);
	if (ec)
	{
		llvm::errs() << "Failed to open output file: " << ec.message() << "\n";
		return false;
	}

	llvm::legacy::PassManager codegenPass;
	if (targetMachine->addPassesToEmitFile(codegenPass, dest, nullptr, llvm::CodeGenFileType::ObjectFile))
	{
		llvm::errs() << "Failed to set up object file emission\n";
		return false;
	}

	codegenPass.run(*module);
	dest.flush();

	return true;
}

uint32_t scpp_get_abi_alignment_of_type(SCPPTargetMachineRef targetMachine, SCPPTypeRef ty)
{
	return uw(targetMachine)->createDataLayout().getABITypeAlign(uw(ty)).value();
}

SCPPValueRef scpp_const_int(SCPPTypeRef intTy, uint64_t n, bool signExtend)
{
	return w(llvm::ConstantInt::get(uw<llvm::IntegerType>(intTy), n, signExtend != 0));
}

SCPPValueRef scpp_const_real(SCPPTypeRef realTy, double n)
{
	return w(llvm::ConstantFP::get(uw(realTy), n));
}

SCPPTypeKind scpp_get_type_kind(SCPPTypeRef ty)
{
	switch (uw(ty)->getTypeID())
	{
	case llvm::Type::VoidTyID:
		return SCPPTypeKind::scpp_void_type_kind;
	case llvm::Type::HalfTyID:
		return SCPPTypeKind::scpp_half_type_kind;
	case llvm::Type::BFloatTyID:
		return SCPPTypeKind::scpp_bfloat_type_kind;
	case llvm::Type::FloatTyID:
		return SCPPTypeKind::scpp_float_type_kind;
	case llvm::Type::DoubleTyID:
		return SCPPTypeKind::scpp_double_type_kind;
	case llvm::Type::X86_FP80TyID:
		return SCPPTypeKind::scpp_x86_fp80_type_kind;
	case llvm::Type::FP128TyID:
		return SCPPTypeKind::scpp_fp128_type_kind;
	case llvm::Type::PPC_FP128TyID:
		return SCPPTypeKind::scpp_ppc_fp128_type_kind;
	case llvm::Type::LabelTyID:
		return SCPPTypeKind::scpp_label_type_kind;
	case llvm::Type::MetadataTyID:
		return SCPPTypeKind::scpp_metadata_type_kind;
	case llvm::Type::IntegerTyID:
		return SCPPTypeKind::scpp_integer_type_kind;
	case llvm::Type::FunctionTyID:
		return SCPPTypeKind::scpp_function_type_kind;
	case llvm::Type::StructTyID:
		return SCPPTypeKind::scpp_struct_type_kind;
	case llvm::Type::ArrayTyID:
		return SCPPTypeKind::scpp_array_type_kind;
	case llvm::Type::PointerTyID:
		return SCPPTypeKind::scpp_pointer_type_kind;
	case llvm::Type::FixedVectorTyID:
		return SCPPTypeKind::scpp_vector_type_kind;
	case llvm::Type::X86_AMXTyID:
		return SCPPTypeKind::scpp_x86_amx_type_kind;
	case llvm::Type::TokenTyID:
		return SCPPTypeKind::scpp_token_type_kind;
	case llvm::Type::ScalableVectorTyID:
		return SCPPTypeKind::scpp_scalable_vector_type_kind;
	case llvm::Type::TargetExtTyID:
		return SCPPTypeKind::scpp_target_ext_type_kind;
	case llvm::Type::TypedPointerTyID:
		llvm_unreachable("Typed pointers are unsupported via the C API");
	}
	llvm_unreachable("Unhandled TypeID.");
}

bool scpp_type_is_sized(SCPPTypeRef Ty)
{
	return uw(Ty)->isSized();
}

SCPPValueRef scpp_build_ret(SCPPBuilderRef b, SCPPValueRef v)
{
	return w(uw(b)->CreateRet(uw(v)));
}

SCPPValueRef scpp_build_gep(
	SCPPBuilderRef b, SCPPTypeRef ty,
	SCPPValueRef pointer, SCPPValueRef *indices,
	uint32_t numIndices, const char *name)
{
	llvm::ArrayRef<llvm::Value *> IdxList(uw(indices), numIndices);
	return w(uw(b)->CreateGEP(uw(ty), uw(pointer), IdxList, name));
}

SCPPValueRef scpp_build_in_bounds_gep(
	SCPPBuilderRef b, SCPPTypeRef ty,
	SCPPValueRef pointer, SCPPValueRef *indices,
	uint32_t numIndices, const char *name)
{
	llvm::ArrayRef<llvm::Value *> IdxList(uw(indices), numIndices);
	return w(uw(b)->CreateInBoundsGEP(uw(ty), uw(pointer), IdxList, name));
}

SCPPValueRef scpp_build_struct_gep(
	SCPPBuilderRef b, SCPPTypeRef ty,
	SCPPValueRef pointer, uint32_t idx,
	const char *name)
{
	return w(uw(b)->CreateStructGEP(uw(ty), uw(pointer), idx, name));
}

SCPPValueRef scpp_build_global_string(
	SCPPBuilderRef b, const char *str, const char *name)
{
	return w(uw(b)->CreateGlobalString(str, name));
}

SCPPValueRef scpp_build_global_string_ptr(
	SCPPBuilderRef b, const char *str, const char *name)
{
	return w(uw(b)->CreateGlobalStringPtr(str, name));
}

SCPPValueRef scpp_build_alloca(
	SCPPBuilderRef b, SCPPTypeRef ty,
	const char *name)
{
	return w(uw(b)->CreateAlloca(uw(ty), nullptr, name));
}

SCPPValueRef scpp_build_array_alloca(
	SCPPBuilderRef b, SCPPTypeRef ty,
	SCPPValueRef sizeVal, const char *name)
{
	return w(uw(b)->CreateAlloca(uw(ty), uw(sizeVal), name));
}

SCPPValueRef scpp_build_free(SCPPBuilderRef b, SCPPValueRef pointerVal)
{
	return w(uw(b)->CreateFree(uw(pointerVal)));
}

SCPPValueRef scpp_build_load(
	SCPPBuilderRef b, SCPPTypeRef ty,
	SCPPValueRef pointerVal, const char *name)
{
	return w(uw(b)->CreateLoad(uw(ty), uw(pointerVal), name));
}

SCPPValueRef scpp_build_store(
	SCPPBuilderRef b, SCPPValueRef val,
	SCPPValueRef pointerVal)
{
	return w(uw(b)->CreateStore(uw(val), uw(pointerVal)));
}

SCPPValueRef scpp_build_call_with_type(
	SCPPBuilderRef b, SCPPTypeRef ty, SCPPValueRef fn,
	SCPPValueRef *args, uint32_t numArgs,
	const char *name)
{
	llvm::FunctionType *FTy = uw<llvm::FunctionType>(ty);
	return w(uw(b)->CreateCall(
		FTy, uw(fn),
		llvm::ArrayRef(uw(args), numArgs), name));
}

SCPPValueRef scpp_build_call(
	SCPPBuilderRef b, SCPPValueRef fn,
	SCPPValueRef *args, uint32_t numArgs,
	const char *name)
{
	auto function = uw<llvm::Function>(fn);
	return w(uw(b)->CreateCall(
		function,
		llvm::ArrayRef(uw(args), numArgs), name));
}

SCPPValueRef scpp_build_icmp(
	SCPPBuilderRef b, /* ICmpInst::Predicate */ int op,
	SCPPValueRef lhs, SCPPValueRef rhs,
	const char *name)
{
	return w(uw(b)->CreateICmp(
		static_cast<llvm::ICmpInst::Predicate>(op),
		uw(lhs), uw(rhs), name));
}

SCPPValueRef scpp_build_fcmp(
	SCPPBuilderRef b, /* FCmpInst::Predicate */ int op,
	SCPPValueRef lhs, SCPPValueRef rhs,
	const char *name)
{
	return w(uw(b)->CreateFCmp(
		static_cast<llvm::FCmpInst::Predicate>(op),
		uw(lhs), uw(rhs), name));
}

SCPPValueRef scpp_build_br(SCPPBuilderRef b, SCPPBasicBlockRef dest)
{
	return w(uw(b)->CreateBr(uw(dest)));
}

SCPPValueRef scpp_build_cond_br(
	SCPPBuilderRef b, SCPPValueRef ifBlock,
	SCPPBasicBlockRef thenBlock, SCPPBasicBlockRef elseBlock)
{
	return w(uw(b)->CreateCondBr(uw(ifBlock), uw(thenBlock), uw(elseBlock)));
}

SCPPValueRef scpp_build_switch(
	SCPPBuilderRef b, SCPPValueRef v,
	SCPPBasicBlockRef elseBlock, uint32_t numCases)
{
	return w(uw(b)->CreateSwitch(uw(v), uw(elseBlock), numCases));
}

SCPPValueRef scpp_build_indirect_br(
	SCPPBuilderRef b, SCPPValueRef addr,
	uint32_t numDests)
{
	return w(uw(b)->CreateIndirectBr(uw(addr), numDests));
}

const char *scpp_get_basic_block_name(SCPPBasicBlockRef bb)
{
	return uw(bb)->getName().data();
}

SCPPValueRef scpp_get_basic_block_parent(SCPPBasicBlockRef bb)
{
	return w(uw(bb)->getParent());
}

SCPPValueRef scpp_get_basic_block_terminator(SCPPBasicBlockRef bb)
{
	return w(uw(bb)->getTerminator());
}

uint32_t scpp_count_basic_blocks(SCPPValueRef fnRef)
{
	return uw<llvm::Function>(fnRef)->size();
}

void scpp_get_basic_blocks(SCPPValueRef fnRef, SCPPBasicBlockRef *basicBlocksRefs)
{
	auto fn = uw<llvm::Function>(fnRef);
	for (llvm::BasicBlock &bb : *fn)
		*basicBlocksRefs++ = w(&bb);
}

SCPPBasicBlockRef scpp_get_entry_basic_block(SCPPValueRef fn)
{
	return w(&uw<llvm::Function>(fn)->getEntryBlock());
}

SCPPBasicBlockRef scpp_get_first_basic_block(SCPPValueRef fn)
{
	auto func = uw<llvm::Function>(fn);
	llvm::Function::iterator i = func->begin();
	if (i == func->end())
		return nullptr;
	return w(&*i);
}

SCPPBasicBlockRef scpp_get_last_basic_block(SCPPValueRef fn)
{
	auto func = uw<llvm::Function>(fn);
	llvm::Function::iterator i = func->end();
	if (i == func->begin())
		return nullptr;
	return w(&*--i);
}

SCPPBasicBlockRef scpp_get_next_basic_block(SCPPBasicBlockRef bb)
{
	auto block = uw(bb);
	llvm::Function::iterator i(block);
	if (++i == block->getParent()->end())
		return nullptr;
	return w(&*i);
}

SCPPBasicBlockRef scpp_get_previous_basic_block(SCPPBasicBlockRef bb)
{
	auto block = uw(bb);
	llvm::Function::iterator i(block);
	if (i == block->getParent()->begin())
		return nullptr;
	return w(&*--i);
}

SCPPBasicBlockRef scpp_append_basic_block(SCPPValueRef fn, const char *name)
{
	return w(llvm::BasicBlock::Create(*GlobalContext, name, uw<llvm::Function>(fn)));
}

void scpp_delete_basic_block(SCPPBasicBlockRef bb)
{
	uw(bb)->eraseFromParent();
}

void scpp_remove_basic_block_from_parent(SCPPBasicBlockRef bb)
{
	uw(bb)->removeFromParent();
}

void scpp_move_basic_block_before(SCPPBasicBlockRef bb, SCPPBasicBlockRef movePos)
{
	uw(bb)->moveBefore(uw(movePos));
}

void scpp_move_basic_block_after(SCPPBasicBlockRef bb, SCPPBasicBlockRef movePos)
{
	uw(bb)->moveAfter(uw(movePos));
}

void scpp_position_builder_at_end(SCPPBuilderRef b, SCPPBasicBlockRef block)
{
	uw(b)->SetInsertPoint(uw(block));
}

SCPPValueRef scpp_build_unary_op(SCPPBuilderRef b, SCPPUnaryOp op, SCPPValueRef v, const char *name)
{
	auto builder = uw(b);

	switch (op)
	{
	case SCPPUnaryOp::scpp_neg:
		return w(builder->CreateNeg(uw(v), name));

	case SCPPUnaryOp::scpp_nsw_neg:
		return w(builder->CreateNSWNeg(uw(v), name));

	case SCPPUnaryOp::scpp_nuw_neg:
	{
		llvm::Value *Neg = builder->CreateNeg(uw(v), name);
		if (auto *I = llvm::dyn_cast<llvm::BinaryOperator>(Neg))
			I->setHasNoUnsignedWrap();
		return w(Neg);
	}

	case SCPPUnaryOp::scpp_fneg:
		return w(builder->CreateFNeg(uw(v), name));

	case SCPPUnaryOp::scpp_not:
		return w(builder->CreateNot(uw(v), name));

	default:
		return nullptr;
	}
}

static llvm::Instruction::BinaryOps map_bin_op(SCPPBinaryOp op)
{
	switch (op)
	{
	case SCPPBinaryOp::scpp_add:
		return llvm::Instruction::BinaryOps::Add;
	case SCPPBinaryOp::scpp_fadd:
		return llvm::Instruction::BinaryOps::FAdd;
	case SCPPBinaryOp::scpp_sub:
		return llvm::Instruction::BinaryOps::Sub;
	case SCPPBinaryOp::scpp_fsub:
		return llvm::Instruction::BinaryOps::FSub;
	case SCPPBinaryOp::scpp_mul:
		return llvm::Instruction::BinaryOps::Mul;
	case SCPPBinaryOp::scpp_fmul:
		return llvm::Instruction::BinaryOps::FMul;
	case SCPPBinaryOp::scpp_udiv:
		return llvm::Instruction::BinaryOps::UDiv;
	case SCPPBinaryOp::scpp_sdiv:
		return llvm::Instruction::BinaryOps::SDiv;
	case SCPPBinaryOp::scpp_fdiv:
		return llvm::Instruction::BinaryOps::FDiv;
	case SCPPBinaryOp::scpp_urem:
		return llvm::Instruction::BinaryOps::URem;
	case SCPPBinaryOp::scpp_srem:
		return llvm::Instruction::BinaryOps::SRem;
	case SCPPBinaryOp::scpp_frem:
		return llvm::Instruction::BinaryOps::FRem;
	case SCPPBinaryOp::scpp_shl:
		return llvm::Instruction::BinaryOps::Shl;
	case SCPPBinaryOp::scpp_lshr:
		return llvm::Instruction::BinaryOps::LShr;
	case SCPPBinaryOp::scpp_ashr:
		return llvm::Instruction::BinaryOps::AShr;
	case SCPPBinaryOp::scpp_and:
		return llvm::Instruction::BinaryOps::And;
	case SCPPBinaryOp::scpp_or:
		return llvm::Instruction::BinaryOps::Or;
	case SCPPBinaryOp::scpp_xor:
		return llvm::Instruction::BinaryOps::Xor;
	default:
		llvm_unreachable("Unhandled binary operation.");
	}
}

SCPPValueRef scpp_build_binary_op(
	SCPPBuilderRef b, SCPPBinaryOp op, SCPPValueRef lhs, SCPPValueRef rhs, const char *name)
{
	return w(uw(b)->CreateBinOp(
		llvm::Instruction::BinaryOps(map_bin_op(op)), uw(lhs),
		uw(rhs), name));
}

SCPPTypeRef scpp_type_of(SCPPValueRef val)
{
	return w(uw(val)->getType());
}

SCPPLinkage scpp_get_linkage(SCPPValueRef global)
{
	switch (uw<llvm::GlobalValue>(global)->getLinkage())
	{
	case llvm::GlobalValue::ExternalLinkage:
		return SCPPLinkage::scpp_external;
	case llvm::GlobalValue::AvailableExternallyLinkage:
		return SCPPLinkage::scpp_available_externally;
	case llvm::GlobalValue::LinkOnceAnyLinkage:
		return SCPPLinkage::scpp_link_once_any;
	case llvm::GlobalValue::LinkOnceODRLinkage:
		return SCPPLinkage::scpp_link_once_odr;
	case llvm::GlobalValue::WeakAnyLinkage:
		return SCPPLinkage::scpp_weak_any;
	case llvm::GlobalValue::WeakODRLinkage:
		return SCPPLinkage::scpp_weak_odr;
	case llvm::GlobalValue::AppendingLinkage:
		return SCPPLinkage::scpp_appending;
	case llvm::GlobalValue::InternalLinkage:
		return SCPPLinkage::scpp_internal;
	case llvm::GlobalValue::PrivateLinkage:
		return SCPPLinkage::scpp_private;
	case llvm::GlobalValue::ExternalWeakLinkage:
		return SCPPLinkage::scpp_external_weak;
	case llvm::GlobalValue::CommonLinkage:
		return SCPPLinkage::scpp_common;
	}

	llvm_unreachable("Invalid GlobalValue linkage!");
}

void scpp_set_linkage(SCPPValueRef global, SCPPLinkage linkage)
{
	auto *gv = uw<llvm::GlobalValue>(global);

	switch (linkage)
	{
	case SCPPLinkage::scpp_external:
		gv->setLinkage(llvm::GlobalValue::ExternalLinkage);
		break;
	case SCPPLinkage::scpp_available_externally:
		gv->setLinkage(llvm::GlobalValue::AvailableExternallyLinkage);
		break;
	case SCPPLinkage::scpp_link_once_any:
		gv->setLinkage(llvm::GlobalValue::LinkOnceAnyLinkage);
		break;
	case SCPPLinkage::scpp_link_once_odr:
		gv->setLinkage(llvm::GlobalValue::LinkOnceODRLinkage);
		break;
	case SCPPLinkage::scpp_weak_any:
		gv->setLinkage(llvm::GlobalValue::WeakAnyLinkage);
		break;
	case SCPPLinkage::scpp_weak_odr:
		gv->setLinkage(llvm::GlobalValue::WeakODRLinkage);
		break;
	case SCPPLinkage::scpp_appending:
		gv->setLinkage(llvm::GlobalValue::AppendingLinkage);
		break;
	case SCPPLinkage::scpp_internal:
		gv->setLinkage(llvm::GlobalValue::InternalLinkage);
		break;
	case SCPPLinkage::scpp_private:
		gv->setLinkage(llvm::GlobalValue::PrivateLinkage);
		break;
	case SCPPLinkage::scpp_external_weak:
		gv->setLinkage(llvm::GlobalValue::ExternalWeakLinkage);
		break;
	case SCPPLinkage::scpp_common:
		gv->setLinkage(llvm::GlobalValue::CommonLinkage);
		break;
	default:
		llvm_unreachable("Invalid linkage type!");
	}
}

unsigned scpp_get_alignment(SCPPValueRef v)
{
	auto *p = uw(v);
	if (auto *gv = llvm::dyn_cast<llvm::GlobalObject>(p))
		return gv->getAlign() ? gv->getAlign()->value() : 0;
	if (auto *ai = llvm::dyn_cast<llvm::AllocaInst>(p))
		return ai->getAlign().value();
	if (auto *li = llvm::dyn_cast<llvm::LoadInst>(p))
		return li->getAlign().value();
	if (auto *si = llvm::dyn_cast<llvm::StoreInst>(p))
		return si->getAlign().value();
	if (auto *rmwi = llvm::dyn_cast<llvm::AtomicRMWInst>(p))
		return rmwi->getAlign().value();
	if (auto *cxi = llvm::dyn_cast<llvm::AtomicCmpXchgInst>(p))
		return cxi->getAlign().value();

	llvm_unreachable(
		"only GlobalValue, AllocaInst, LoadInst, StoreInst, AtomicRMWInst, "
		"and AtomicCmpXchgInst have alignment");
}

void scpp_set_alignment(SCPPValueRef v, unsigned bytes)
{
	auto *p = uw(v);
	if (auto *gv = llvm::dyn_cast<llvm::GlobalObject>(p))
		gv->setAlignment(llvm::MaybeAlign(bytes));
	else if (auto *ai = llvm::dyn_cast<llvm::AllocaInst>(p))
		ai->setAlignment(llvm::Align(bytes));
	else if (auto *li = llvm::dyn_cast<llvm::LoadInst>(p))
		li->setAlignment(llvm::Align(bytes));
	else if (auto *si = llvm::dyn_cast<llvm::StoreInst>(p))
		si->setAlignment(llvm::Align(bytes));
	else if (auto *rmwi = llvm::dyn_cast<llvm::AtomicRMWInst>(p))
		rmwi->setAlignment(llvm::Align(bytes));
	else if (auto *cxi = llvm::dyn_cast<llvm::AtomicCmpXchgInst>(p))
		cxi->setAlignment(llvm::Align(bytes));
	else
		llvm_unreachable(
			"only GlobalValue, AllocaInst, LoadInst, StoreInst, AtomicRMWInst, and "
			"AtomicCmpXchgInst have alignment");
}

unsigned scpp_count_params(SCPPValueRef fnRef)
{
	return uw<llvm::Function>(fnRef)->arg_size();
}

void scpp_get_params(SCPPValueRef fnRef, SCPPValueRef *paramRefs)
{
	auto *fn = uw<llvm::Function>(fnRef);
	for (llvm::Argument &a : fn->args())
		*paramRefs++ = w(&a);
}

SCPPValueRef scpp_get_param(SCPPValueRef fnRef, unsigned index)
{
	auto *fn = uw<llvm::Function>(fnRef);
	return w(&fn->arg_begin()[index]);
}

SCPPValueRef scpp_get_param_parent(SCPPValueRef v)
{
	return w(uw<llvm::Argument>(v)->getParent());
}

SCPPValueRef scpp_get_first_param(SCPPValueRef fn)
{
	auto *func = uw<llvm::Function>(fn);
	auto i = func->arg_begin();
	if (i == func->arg_end())
		return nullptr;
	return w(&*i);
}

SCPPValueRef scpp_get_last_param(SCPPValueRef fn)
{
	auto *func = uw<llvm::Function>(fn);
	auto i = func->arg_end();
	if (i == func->arg_begin())
		return nullptr;
	return w(&*--i);
}

SCPPValueRef scpp_get_next_param(SCPPValueRef arg)
{
	auto *a = uw<llvm::Argument>(arg);
	auto *fn = a->getParent();
	if (a->getArgNo() + 1 >= fn->arg_size())
		return nullptr;
	return w(&fn->arg_begin()[a->getArgNo() + 1]);
}

SCPPValueRef scpp_get_previous_param(SCPPValueRef arg)
{
	auto *a = uw<llvm::Argument>(arg);
	if (a->getArgNo() == 0)
		return nullptr;
	return w(&a->getParent()->arg_begin()[a->getArgNo() - 1]);
}

void scpp_set_param_alignment(SCPPValueRef arg, unsigned align)
{
	auto *a = uw<llvm::Argument>(arg);
	a->addAttr(llvm::Attribute::getWithAlignment(a->getContext(), llvm::Align(align)));
}

uint32_t scpp_get_enum_attr_kind_for_name(const char *name, size_t sLen)
{
	return llvm::Attribute::getAttrKindFromName(llvm::StringRef(name, sLen));
}

uint32_t scpp_get_last_enum_attr_Kind()
{
	return llvm::Attribute::AttrKind::EndAttrKinds;
}

SCPPAttributeRef scpp_create_enum_attr(
	uint32_t kindID,
	uint64_t val)
{
	auto &ctx = *GlobalContext;
	auto attrKind = (llvm::Attribute::AttrKind)kindID;
	return w(llvm::Attribute::get(ctx, attrKind, val));
}

uint32_t scpp_get_enum_attr_kind(SCPPAttributeRef a)
{
	return uw(a).getKindAsEnum();
}

uint64_t scpp_get_enum_attr_value(SCPPAttributeRef a)
{
	auto attr = uw(a);
	if (attr.isEnumAttribute())
		return 0;
	return attr.getValueAsInt();
}

SCPPAttributeRef scpp_create_type_attr(
	uint32_t kindID,
	SCPPTypeRef type_ref)
{
	auto &ctx = *GlobalContext;
	auto attrKind = (llvm::Attribute::AttrKind)kindID;
	return w(llvm::Attribute::get(ctx, attrKind, uw(type_ref)));
}

SCPPTypeRef scpp_get_type_attr_value(SCPPAttributeRef a)
{
	auto attr = uw(a);
	return w(attr.getValueAsType());
}

void scpp_add_attr_at_index(
	SCPPValueRef fn, int idx, SCPPAttributeRef attr)
{
	uw<llvm::Function>(fn)->addAttributeAtIndex(idx, uw(attr));
}

void scpp_add_call_site_attr(
	SCPPValueRef cb, int idx, SCPPAttributeRef attr)
{
	uw<llvm::CallBase>(cb)->addAttributeAtIndex(idx, uw(attr));
}

SCPPTypeRef scpp_get_type(SCPPTypeOp op)
{
	switch (op)
	{
	case SCPPTypeOp::scpp_void:
		return (SCPPTypeRef)llvm::Type::getVoidTy(*GlobalContext);

	case SCPPTypeOp::scpp_int1:
		return (SCPPTypeRef)llvm::Type::getInt1Ty(*GlobalContext);

	case SCPPTypeOp::scpp_int8:
		return (SCPPTypeRef)llvm::Type::getInt8Ty(*GlobalContext);

	case SCPPTypeOp::scpp_int16:
		return (SCPPTypeRef)llvm::Type::getInt16Ty(*GlobalContext);

	case SCPPTypeOp::scpp_int32:
		return (SCPPTypeRef)llvm::Type::getInt32Ty(*GlobalContext);

	case SCPPTypeOp::scpp_int64:
		return (SCPPTypeRef)llvm::Type::getInt64Ty(*GlobalContext);

	case SCPPTypeOp::scpp_real_half:
		return (SCPPTypeRef)llvm::Type::getHalfTy(*GlobalContext);

	case SCPPTypeOp::scpp_real_float:
		return (SCPPTypeRef)llvm::Type::getFloatTy(*GlobalContext);

	case SCPPTypeOp::scpp_real_double:
		return (SCPPTypeRef)llvm::Type::getDoubleTy(*GlobalContext);

	default:
		llvm_unreachable("Unhandled op.");
	}
}

SCPPTypeRef scpp_get_int_type(uint32_t numBits)
{
	return w(llvm::IntegerType::get(*GlobalContext, numBits));
}

uint32_t scpp_get_int_type_width(SCPPTypeRef integerTy)
{
	return uw<llvm::IntegerType>(integerTy)->getBitWidth();
}

SCPPValueRef scpp_add_function(
	SCPPModuleRef m, const char *name, SCPPTypeRef fnTy)
{
	return w(llvm::Function::Create(
		uw<llvm::FunctionType>(fnTy),
		llvm::GlobalValue::ExternalLinkage, name, uw(m)));
}

SCPPValueRef scpp_get_named_function(SCPPModuleRef m, const char *name)
{
	return w(uw(m)->getFunction(name));
}

SCPPTypeRef scpp_function_type(
	SCPPTypeRef returnTy,
	SCPPTypeRef *paramTys, uint32_t numParams,
	bool isVarArg)
{
	llvm::ArrayRef<llvm::Type *> Tys(uw(paramTys), numParams);
	return w(llvm::FunctionType::get(uw(returnTy), Tys, isVarArg != 0));
}

SCPPTypeRef scpp_get_function_type(SCPPValueRef fn)
{
	auto function = uw<llvm::Function>(fn);
	return w(function->getFunctionType());
}

bool scpp_is_function_var_arg(SCPPTypeRef functionTy)
{
	return uw<llvm::FunctionType>(functionTy)->isVarArg();
}

SCPPTypeRef scpp_get_return_type(SCPPTypeRef functionTy)
{
	return w(uw<llvm::FunctionType>(functionTy)->getReturnType());
}

uint32_t scpp_count_param_Types(SCPPTypeRef functionTy)
{
	return uw<llvm::FunctionType>(functionTy)->getNumParams();
}

void scpp_get_param_types(SCPPTypeRef functionTy, SCPPTypeRef *dest)
{
	llvm::FunctionType *ty = uw<llvm::FunctionType>(functionTy);
	for (llvm::Type *T : ty->params())
		*dest++ = w(T);
}

SCPPTypeRef scpp_struct_type_in_context(
	SCPPContextRef c, SCPPTypeRef *elementTys,
	uint32_t numElements, bool packed)
{
	llvm::ArrayRef<llvm::Type *> Tys(uw(elementTys), numElements);
	return w(llvm::StructType::get(*uw(c), Tys, packed != 0));
}

SCPPTypeRef scpp_struct_type(
	SCPPTypeRef *elementTys,
	uint32_t numElements, bool packed)
{
	return scpp_struct_type_in_context(
		w(GlobalContext), elementTys,
		numElements, packed);
}

SCPPTypeRef scpp_struct_create_named(const char *name)
{
	return w(llvm::StructType::create(*GlobalContext, name));
}

const char *scpp_get_struct_name(SCPPTypeRef ty)
{
	llvm::StructType *type = uw<llvm::StructType>(ty);
	if (!type->hasName())
		return nullptr;
	return type->getName().data();
}

void scpp_struct_set_body(
	SCPPTypeRef structTy, SCPPTypeRef *elementTys,
	uint32_t numElements, bool packed)
{
	llvm::ArrayRef<llvm::Type *> Tys(uw(elementTys), numElements);
	uw<llvm::StructType>(structTy)->setBody(Tys, packed != 0);
}

unsigned scpp_count_struct_element_types(SCPPTypeRef structTy)
{
	return uw<llvm::StructType>(structTy)->getNumElements();
}

void scpp_get_struct_element_types(SCPPTypeRef structTy, SCPPTypeRef *dest)
{
	auto ty = uw<llvm::StructType>(structTy);
	for (llvm::Type *T : ty->elements())
		*dest++ = w(T);
}

SCPPTypeRef scpp_array_type(SCPPTypeRef elementTy, uint64_t numElements)
{
	return w(llvm::ArrayType::get(uw(elementTy), numElements));
}

SCPPTypeRef scpp_pointer_type(SCPPTypeRef elementTy, uint32_t addressSpace)
{
	return w(
		llvm::PointerType::get(uw(elementTy)->getContext(), addressSpace));
}

SCPPTypeRef scpp_get_element_type(SCPPTypeRef wrappedTy)
{
	auto *Ty = uw(wrappedTy);
	if (auto *ATy = llvm::dyn_cast<llvm::ArrayType>(Ty))
		return w(ATy->getElementType());
	return w(llvm::cast<llvm::VectorType>(Ty)->getElementType());
}

uint32_t scpp_get_function_cc(SCPPValueRef fn)
{
	return uw<llvm::Function>(fn)->getCallingConv();
}

void scpp_set_function_cc(SCPPValueRef fn, uint32_t cc)
{
	return uw<llvm::Function>(fn)->setCallingConv(static_cast<llvm::CallingConv::ID>(cc));
}

static llvm::Intrinsic::ID map_from_intrinsic(SCPPIntrinsicOp op)
{
	switch (op)
	{
	case SCPPIntrinsicOp::scpp_memcpy:
		return llvm::Intrinsic::memcpy;

	default:
		llvm_unreachable("Unhandled op.");
	}
}

SCPPValueRef scpp_get_intrinsic(
	SCPPModuleRef module, SCPPBuilderRef b, SCPPIntrinsicOp op,
	SCPPTypeRef *tys, uint32_t numParams)
{
	auto fn = llvm::Intrinsic::getDeclaration(
		uw(module), map_from_intrinsic(op), llvm::ArrayRef(uw(tys), numParams));
	return w(fn);
}
