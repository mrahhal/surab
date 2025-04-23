#ifndef C_WRAPPING_DEFS_H
#define C_WRAPPING_DEFS_H

#include "llvm/Support/Casting.h"

// Similar to llvm-c's wrap/unwrap, but renamed to avoid possible conflict.

#define SCPP_DEFINE_SIMPLE_CONVERSION_FUNCTIONS(Ty, Ref)    \
	inline Ty *uw(Ref ref)                                  \
	{                                                       \
		return reinterpret_cast<Ty *>(ref);                 \
	}                                                       \
	inline Ref w(const Ty *ty)                              \
	{                                                       \
		return reinterpret_cast<Ref>(const_cast<Ty *>(ty)); \
	}

#define SCPP_DEFINE_ISA_CONVERSION_FUNCTIONS(Ty, Ref) \
	SCPP_DEFINE_SIMPLE_CONVERSION_FUNCTIONS(Ty, Ref)  \
                                                      \
	template <typename T>                             \
	inline T *uw(Ref ref)                             \
	{                                                 \
		return llvm::cast<T>(uw(ref));                \
	}

#endif
