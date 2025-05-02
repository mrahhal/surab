# Builds scpp mainly

cmake_minimum_required(VERSION 3.20)

list(APPEND CMAKE_MODULE_PATH ${CMAKE_SOURCE_DIR}/cmake)
list(APPEND CMAKE_PREFIX_PATH "libs/llvm")

set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)

project(surab
	VERSION 0.0.1
	HOMEPAGE_URL "https://surab-lang.org"
	LANGUAGES C CXX)

add_compile_definitions(SURAB_EXPORTS)

find_package(llvm 20)
find_package(clang 20)

configure_file(./cmake/config.json config.json)

# scpp is the cpp lib the surab compiler binds with for LLVM codegen integration.

set(SCPP_SOURCES
	surab_llvm.h
	surab_llvm.cpp
	surab_clang.h
	surab_clang.cpp)

set(SCPP_OUTPUT_DIR "${PROJECT_BINARY_DIR}/scpp")

add_library(scpp SHARED ${SCPP_SOURCES})
target_compile_features(scpp PRIVATE cxx_std_17)

if (MSVC)
	# Enable standards-conforming preprocessor.
	# https://learn.microsoft.com/en-us/cpp/build/reference/zc-preprocessor
	# This is required when linking llvm with MSVC otherwise we get errors: https://github.com/llvm/llvm-project/issues/86028
	target_compile_options(scpp PRIVATE "/Zc:preprocessor")
	set_property(TARGET scpp PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreadedDLL")
endif()

target_include_directories(scpp PUBLIC
	${CLANG_INCLUDE_DIRS}
	${LLVM_INCLUDE_DIRS})
target_link_libraries(scpp PUBLIC
	${CLANG_LIBRARIES}
	${LLVM_LIBRARIES})

if(MSVC)
	target_link_libraries(scpp PUBLIC ntdll.lib ws2_32.lib) # ntdll is used in LLVM on windows.
elseif(MINGW) # Is this needed?
	target_link_libraries(scpp PUBLIC ntdll ws2_32)
endif()

set_target_properties(scpp PROPERTIES ARCHIVE_OUTPUT_DIRECTORY ${SCPP_OUTPUT_DIR})
foreach(CONFIG_TYPE ${CMAKE_CONFIGURATION_TYPES})
	string(TOUPPER ${CONFIG_TYPE} CONFIG_TYPE)
	set_target_properties(scpp PROPERTIES ARCHIVE_OUTPUT_DIRECTORY_${CONFIG_TYPE} ${SCPP_OUTPUT_DIR})
	set_target_properties(scpp PROPERTIES RUNTIME_OUTPUT_DIRECTORY_${CONFIG_TYPE} ${SCPP_OUTPUT_DIR})
	set_target_properties(scpp PROPERTIES LIBRARY_OUTPUT_DIRECTORY_${CONFIG_TYPE} ${SCPP_OUTPUT_DIR})
endforeach()

# TODO: Use install and -DCMAKE_INSTALL_PREFIX to install the resulting scpp lib in a directory for compilation project to link against. (instead of copying it in the build.ps1 script)
