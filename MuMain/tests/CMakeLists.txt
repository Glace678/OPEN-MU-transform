# Test tree. New test modules go in their own subdirectory and call
# add_subdirectory() below. Keep the entry point here minimal — module
# CMakeLists.txt files own their own sources, link, and call mu_add_test().

include(third_party/doctest/doctest.cmake)

add_test(
    NAME client_library_platform_config
    COMMAND "${CMAKE_COMMAND}" -P
        "${CMAKE_SOURCE_DIR}/cmake/tests/ClientLibraryPlatformTests.cmake")
add_test(
    NAME client_library_universal_macos_rejected
    COMMAND "${CMAKE_COMMAND}" -P
        "${CMAKE_SOURCE_DIR}/cmake/tests/UniversalMacOSClientLibraryTest.cmake")
set_tests_properties(
    client_library_universal_macos_rejected
    PROPERTIES WILL_FAIL TRUE)

# XC-18: host-side cross-validation of the iOS game-data integrity check
# (OpenMU-iOS/game-ios/VerifyDataArchive.cmake). The checkout layout puts
# OpenMU-iOS next to MuMain; skip silently on layouts without it.
get_filename_component(MU_IOS_REPO_DIR
    "${CMAKE_SOURCE_DIR}/../OpenMU-iOS" ABSOLUTE)
if(EXISTS "${MU_IOS_REPO_DIR}/tests/VerifyDataArchiveTests.cmake")
    add_test(
        NAME ios_data_archive_verification
        COMMAND "${CMAKE_COMMAND}"
            "-DIOS_REPOSITORY_DIR=${MU_IOS_REPO_DIR}"
            -P "${MU_IOS_REPO_DIR}/tests/VerifyDataArchiveTests.cmake")
endif()

# When cross-compiling Windows binaries on a non-Windows host (CI's Linux
# runner, WSL/MinGW), wine has to launch the .exe. Setting this once makes
# both add_test and doctest_discover_tests pick it up via the
# CROSSCOMPILING_EMULATOR target property.
if(WIN32 AND NOT CMAKE_HOST_WIN32 AND NOT CMAKE_CROSSCOMPILING_EMULATOR)
    find_program(WINE_EXECUTABLE wine)
    if(NOT WINE_EXECUTABLE)
        message(FATAL_ERROR
            "BUILD_TESTING=ON on a non-Windows host requires wine to run "
            "the cross-compiled test binaries. Install it (Ubuntu/Debian: "
            "sudo apt-get install wine wine32) or configure with "
            "-DBUILD_TESTING=OFF.")
    endif()
    set(CMAKE_CROSSCOMPILING_EMULATOR ${WINE_EXECUTABLE} CACHE STRING "" FORCE)
endif()

add_library(mu_test_main STATIC main.cpp)
target_include_directories(mu_test_main PUBLIC third_party/doctest)
target_compile_features(mu_test_main PUBLIC cxx_std_20)
# Test sources contain non-ASCII wide string literals. MSVC defaults to the
# system codepage when reading sources, which mangles UTF-8 characters into
# wrong codepoints and breaks every non-Latin language test. /utf-8 forces
# both the source charset and the narrow execution charset to UTF-8; the wide
# execution charset stays UTF-16 (Windows wchar_t).
if(MSVC)
    # CMake 4 no longer supplies /EHsc for this generator. doctest requires
    # C++ exception support for its normal assertion and test-runner paths.
    target_compile_options(mu_test_main PUBLIC /utf-8 /EHsc)
endif()

# Helper for module CMakeLists. Usage:
#   mu_add_test(NAME <test_name> SOURCES a.cpp b.cpp LINK_LIBS lib1 lib2)
# Registers each TEST_CASE inside the binary as its own CTest entry, so
# failures point at the specific case in CI logs.
function(mu_add_test)
    cmake_parse_arguments(MAT "" "NAME" "SOURCES;LINK_LIBS" ${ARGN})
    add_executable(${MAT_NAME} ${MAT_SOURCES})
    target_link_libraries(${MAT_NAME} PRIVATE mu_test_main ${MAT_LINK_LIBS})
    target_include_directories(${MAT_NAME} PRIVATE
        ${CMAKE_SOURCE_DIR}/src/source
        ${CMAKE_CURRENT_SOURCE_DIR}/third_party/doctest
    )
    if(MSVC)
        target_compile_options(${MAT_NAME} PRIVATE /utf-8)
    endif()
    doctest_discover_tests(${MAT_NAME})
endfunction()

# Architectural dependency guard (P2 baseline-lock, strict at P9). Locks the
# current Core layering and fails only on new reverse dependencies.
find_package(Python3 COMPONENTS Interpreter QUIET)
if(Python3_Interpreter_FOUND)
  add_test(NAME arch_core_includes
           COMMAND Python3::Interpreter
                   "${CMAKE_CURRENT_SOURCE_DIR}/arch/check_includes.py")
else()
  find_program(PYTHON_EXECUTABLE NAMES python python3)
  add_test(NAME arch_core_includes
           COMMAND "${PYTHON_EXECUTABLE}"
                   "${CMAKE_CURRENT_SOURCE_DIR}/arch/check_includes.py")
endif()

add_subdirectory(text)

add_subdirectory(effects)
add_subdirectory(terrain)

add_subdirectory(editor)
add_subdirectory(core)
add_subdirectory(ui)
add_subdirectory(gamelogic)
