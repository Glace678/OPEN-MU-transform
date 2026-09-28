cmake_minimum_required(VERSION 3.25)

include("${CMAKE_CURRENT_LIST_DIR}/../ClientLibraryPlatform.cmake")

mu_resolve_client_library_platform(
  SYSTEM_NAME "Darwin"
  POINTER_SIZE 8
  SYSTEM_PROCESSOR "arm64"
  OSX_ARCHITECTURES "arm64;x86_64"
  OUT_LIBRARY_NAME library_name
  OUT_RID runtime_identifier
  OUT_PLATFORM platform
  OUT_EXTRA_ARGS extra_args)
