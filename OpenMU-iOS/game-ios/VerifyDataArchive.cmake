# VerifyDataArchive.cmake - verify an archive against a SHA-256 sidecar.
#
# XC-18: extracted from game-ios/CMakeLists.txt (I-03) so the integrity check
# is a pure, host-side script that can run in CTest without the iOS toolchain
# (cross-validation), instead of existing only inside the iOS configure step.
#
# Required parameters:
#   ARCHIVE - path to the archive to verify
#   SIDECAR - text file whose first 64 characters are the expected hex digest
#             (optionally followed by whitespace/the filename)

if(NOT DEFINED ARCHIVE OR NOT DEFINED SIDECAR)
    message(FATAL_ERROR "VerifyDataArchive.cmake requires ARCHIVE and SIDECAR parameters.")
endif()

if(NOT EXISTS "${ARCHIVE}")
    message(FATAL_ERROR "Archive not found: '${ARCHIVE}'.")
endif()

if(NOT EXISTS "${SIDECAR}")
    message(FATAL_ERROR "Checksum sidecar not found: '${SIDECAR}'.")
endif()

file(SHA256 "${ARCHIVE}" MU_VERIFY_ACTUAL)
file(READ "${SIDECAR}" MU_VERIFY_EXPECTED_RAW)
string(STRIP "${MU_VERIFY_EXPECTED_RAW}" MU_VERIFY_EXPECTED)
string(SUBSTRING "${MU_VERIFY_EXPECTED}" 0 64 MU_VERIFY_EXPECTED)

if(NOT MU_VERIFY_ACTUAL STREQUAL MU_VERIFY_EXPECTED)
    message(FATAL_ERROR
        "SHA-256 mismatch:\n  expected: ${MU_VERIFY_EXPECTED}\n  actual:   ${MU_VERIFY_ACTUAL}")
endif()

message(STATUS "SHA-256 verified: '${ARCHIVE}'.")
