# BundleRimeDeps.cmake - copy the non-system shared libraries a found librime
# depends on next to the game executable.
#
# PLAT-11: copying just librime.so leaves its private dependencies
# (libyaml-cpp, libmarisa, ...) unresolved, so dlopen fails at runtime and
# controller pinyin looks identical to "librime not installed". This script is
# invoked POST_BUILD on non-Windows builds and resolves dependencies with ldd.
#
# Required parameters:
#   INPUT      - path to the librime shared library
#   TARGET_DIR - output directory (the Main executable directory)

if(NOT DEFINED INPUT OR NOT DEFINED TARGET_DIR)
  message(FATAL_ERROR "BundleRimeDeps.cmake requires INPUT and TARGET_DIR")
endif()

if(CMAKE_HOST_SYSTEM_NAME STREQUAL "Darwin")
  # macOS: dependencies use install names (@rpath/...) that need explicit
  # resolution; report the requirement rather than silently shipping a library
  # that will not load. Homebrew's librime already places its dependencies on
  # the default loader path.
  message(STATUS
    "Rime dependency bundling on macOS expects librime installed via Homebrew "
    "(its dependencies are then on the loader path). If dlopen fails at "
    "runtime, that is a missing dependency, not a missing librime.")
  return()
endif()

execute_process(
  COMMAND ldd "${INPUT}"
  OUTPUT_VARIABLE LDD_OUTPUT
  RESULT_VARIABLE LDD_RESULT
  OUTPUT_STRIP_TRAILING_WHITESPACE)

if(NOT LDD_RESULT EQUAL 0)
  message(WARNING
    "Could not resolve runtime dependencies of '${INPUT}' with ldd; controller "
    "pinyin may fail to start due to missing dependencies (not because librime "
    "is not installed).")
  return()
endif()

# System loader directories: libraries from these need no copying.
set(SYSTEM_DIRECTORY_PATTERNS
  "^/lib/"
  "^/lib64/"
  "^/usr/lib/"
  "^/usr/lib64/")

string(REPLACE "\n" ";" LDD_LINES "${LDD_OUTPUT}")
set(COPIED_COUNT 0)

foreach(LINE IN LISTS LDD_LINES)
  # Typical line: "\tlibfoo.so.1 => /opt/librime/lib/libfoo.so.1 (0x...)"
  if(LINE MATCHES "=> (/[^ ]+) \\(0x[0-9a-fA-F]+\\)")
    set(DEPENDENCY_PATH "${CMAKE_MATCH_1}")

    set(IS_SYSTEM_LIBRARY FALSE)
    foreach(PATTERN IN LISTS SYSTEM_DIRECTORY_PATTERNS)
      if(DEPENDENCY_PATH MATCHES "${PATTERN}")
        set(IS_SYSTEM_LIBRARY TRUE)
      endif()
    endforeach()

    if(NOT IS_SYSTEM_LIBRARY)
      execute_process(
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different
                "${DEPENDENCY_PATH}" "${TARGET_DIR}"
        RESULT_VARIABLE COPY_RESULT)
      if(COPY_RESULT EQUAL 0)
        math(EXPR COPIED_COUNT "${COPIED_COUNT} + 1")
      else()
        message(WARNING "Failed to bundle rime dependency '${DEPENDENCY_PATH}'.")
      endif()
    endif()
  endif()
endforeach()

message(STATUS
  "Bundled ${COPIED_COUNT} non-system runtime dependency/ies for librime.")
