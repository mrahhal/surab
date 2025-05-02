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

#endif
