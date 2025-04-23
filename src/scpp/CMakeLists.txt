# Builds scpp mainly

cmake_minimum_required(VERSION 4.0)

# https://cmake.org/cmake/help/latest/variable/CMAKE_MSVC_RUNTIME_LIBRARY.html
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded")

list(APPEND CMAKE_MODULE_PATH ${CMAKE_SOURCE_DIR}/cmake)
list(APPEND CMAKE_PREFIX_PATH "libs/llvm")

if(NOT CMAKE_BUILD_TYPE)
	# llvm/clang libs we depend on are always Release so we can't really do Debug here?
	set(CMAKE_BUILD_TYPE "Debug" CACHE STRING
		"Choose the type of build, options are: None Debug Release RelWithDebInfo MinSizeRel." FORCE)
endif()

project(surab
	VERSION 0.0.1
	HOMEPAGE_URL "https://surab-lang.org"
	LANGUAGES C CXX)

find_package(llvm 20)
find_package(clang 20)

configure_file(./cmake/config.json config.json)

# scpp is the cpp lib the surab compiler binds with for LLVM codegen integration.

set(SCPP_SOURCES
	surab_llvm.h
	surab_llvm.cpp)

set(SCPP_OUTPUT_DIR "${PROJECT_BINARY_DIR}/scpp")
message("MESSAGE OUTPUT: ${SCPP_OUTPUT_DIR}")

add_library(scpp SHARED ${SCPP_SOURCES})
target_compile_features(scpp PRIVATE cxx_std_17)

if(MSVC)
	target_link_libraries(scpp PUBLIC ntdll.lib ws2_32.lib) # ntdll is used in LLVM on windows.
elseif(MINGW) # Is this needed?
	target_link_libraries(scpp PUBLIC ntdll ws2_32)
endif()

target_include_directories(scpp PUBLIC
	${LLVM_INCLUDE_DIRS}
	${CLANG_INCLUDE_DIRS})
target_link_libraries(scpp PUBLIC
	${LLVM_LIBRARIES}
	${CLANG_LIBRARIES})

set_target_properties(scpp PROPERTIES ARCHIVE_OUTPUT_DIRECTORY ${SCPP_OUTPUT_DIR})
foreach(CONFIG_TYPE ${CMAKE_CONFIGURATION_TYPES})
	string(TOUPPER ${CONFIG_TYPE} CONFIG_TYPE)
	set_target_properties(scpp PROPERTIES ARCHIVE_OUTPUT_DIRECTORY_${CONFIG_TYPE} ${SCPP_OUTPUT_DIR})
	set_target_properties(scpp PROPERTIES RUNTIME_OUTPUT_DIRECTORY_${CONFIG_TYPE} ${SCPP_OUTPUT_DIR})
	set_target_properties(scpp PROPERTIES LIBRARY_OUTPUT_DIRECTORY_${CONFIG_TYPE} ${SCPP_OUTPUT_DIR})
endforeach()
