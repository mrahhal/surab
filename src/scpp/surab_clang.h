#include "surab_extern.h"

#ifndef SURAB_CLANG_H
#define SURAB_CLANG_H

SURAB_EXTERN_BEGIN

int scpp_clang(const char *triple, const char *additionalLibs, const char *inputFiles, const char *outputFile, const char **outError);

SURAB_EXTERN_END

#endif
