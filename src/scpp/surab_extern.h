#ifndef SURAB_EXTERN_H
#define SURAB_EXTERN_H

#ifdef __cplusplus
#define SURAB_EXTERN_BEGIN \
	extern "C"             \
	{
#define SURAB_EXTERN_END \
	}
#else
#define SURAB_EXTERN_BEGIN
#define SURAB_EXTERN_END
#endif

#if defined(_WIN32)
#if defined(SURAB_EXPORTS)
#define SURAB_ABI __declspec(dllexport)
#else
#define SURAB_ABI __declspec(dllimport)
#endif
#else
#define SURAB_ABI __attribute__((visibility("default")))
#endif

#endif
