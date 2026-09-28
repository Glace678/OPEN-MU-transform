cmake_minimum_required(VERSION 3.25)
project(OpenMUAndroid LANGUAGES C CXX)

get_filename_component(MU_REPOSITORY_ROOT
    "${CMAKE_CURRENT_LIST_DIR}/../../../../../MuMain" ABSOLUTE)

set(MU_ANDROID_BUILD ON CACHE BOOL "Build the SDL Android game library" FORCE)
add_subdirectory("${MU_REPOSITORY_ROOT}/src" "${CMAKE_CURRENT_BINARY_DIR}/mumain")

