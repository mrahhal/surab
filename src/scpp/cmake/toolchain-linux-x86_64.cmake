# Native Linux build in WSL
set(CMAKE_SYSTEM_NAME Linux)
set(CMAKE_SYSTEM_PROCESSOR x86_64)  # Or aarch64 if you're on ARM WSL

# Use native compilers
set(CMAKE_C_COMPILER clang)
set(CMAKE_CXX_COMPILER clang++)

# set(CMAKE_C_COMPILER x86_64-linux-gnu-gcc)
# set(CMAKE_CXX_COMPILER x86_64-linux-gnu-g++)

# Skip testing executable runs during configuration
set(CMAKE_TRY_COMPILE_TARGET_TYPE STATIC_LIBRARY)
