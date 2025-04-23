#ifndef SURAB_CLANG_H
#define SURAB_CLANG_H

#ifdef __cplusplus
#define SURAB_EXTERN_C extern "C"
#else
#define SURAB_EXTERN_C
#endif

#define DllExport __declspec(dllexport)

SURAB_EXTERN_C DllExport bool scpp_lld(const char *additionalLibs, const char *inputFiles, const char *outputFile, const char **outError);

#endif
