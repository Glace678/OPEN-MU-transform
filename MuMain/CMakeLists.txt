cmake_minimum_required(VERSION 3.25)

project(MuMain LANGUAGES C CXX)

if(APPLE)
    enable_language(OBJC OBJCXX)
endif()

#Set global C++ standard
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

option(BUILD_TESTING "Build unit tests" OFF)

# Opt-in static analysis / sanitizers. See docs/build/static-analysis.md.
# Both are OFF by default; enabling them changes compile flags on the targets
# they are attached to and nothing else.
option(ENABLE_CLANG_TIDY "Run clang-tidy over the engine sources during the build (advisory; never fails the build)" OFF)
option(ENABLE_ASAN "Build the client with MSVC AddressSanitizer (x64 builds only)" OFF)

if(ENABLE_CLANG_TIDY AND NOT CMAKE_EXPORT_COMPILE_COMMANDS)
    # clang-tidy reads the compile flags from the compile database. This has to
    # be on before the targets are defined for it to be generated.
    set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
endif()

include("cmake/StaticAnalysis.cmake")
# Every engine, tool and test source is UTF-8 (verified across the tree).
# Force MSVC to read both source and execution charset as UTF-8 so Korean
# comments and string literals are handled deterministically regardless of the
# system codepage (otherwise the compiler defaults to the ANSI codepage, and
# multi-byte UTF-8 sequences can swallow newlines/closure braces and produce
# C4819). This also keeps UTF-8 string bytes identical in the binary.
if(MSVC)
    add_compile_options(/utf-8)
endif()

add_subdirectory("src")

if(BUILD_TESTING)
    enable_testing()
    add_subdirectory(tests)
endif()
