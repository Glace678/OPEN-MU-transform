# VerifyDataArchiveTests.cmake - host-side cross-validation for
# game-ios/VerifyDataArchive.cmake (XC-18).
#
# Invoked through CTest as:
#   cmake -DIOS_REPOSITORY_DIR=<OpenMU-iOS root> -P VerifyDataArchiveTests.cmake
# Runs the verifier as a subprocess with `cmake -P` so each negative case can
# fail via message(FATAL_ERROR) without aborting the whole test. No iOS
# toolchain or Xcode is required.

if(NOT DEFINED IOS_REPOSITORY_DIR)
    get_filename_component(IOS_REPOSITORY_DIR
        "${CMAKE_CURRENT_LIST_DIR}/.." ABSOLUTE)
endif()

set(MU_VERIFY_SCRIPT
    "${IOS_REPOSITORY_DIR}/game-ios/VerifyDataArchive.cmake")

if(NOT EXISTS "${MU_VERIFY_SCRIPT}")
    message(FATAL_ERROR "VerifyDataArchive.cmake not found at '${MU_VERIFY_SCRIPT}'.")
endif()

set(testCount 0)
set(failureCount 0)

# run_verifier(<name> <expect_exit_nonzero>) sets resultVar and retcodeVar.
function(run_verifier caseName expectFailure archive sidecar)
    execute_process(
        COMMAND "${CMAKE_COMMAND}"
            "-DARCHIVE=${archive}"
            "-DSIDECAR=${sidecar}"
            "-P" "${MU_VERIFY_SCRIPT}"
        WORKING_DIRECTORY "${workDir}"
        RESULT_VARIABLE exitCode
        OUTPUT_VARIABLE outText
        ERROR_VARIABLE errText)

    if(expectFailure)
        if(exitCode EQUAL 0)
            message(FATAL_ERROR
                "[${caseName}] verifier unexpectedly succeeded for a corrupt case.")
        endif()
        message(STATUS "[${caseName}] rejected as expected.")
    else()
        if(NOT exitCode EQUAL 0)
            message(FATAL_ERROR
                "[${caseName}] verifier unexpectedly failed (code ${exitCode}):\n${errText}")
        endif()
        message(STATUS "[${caseName}] accepted as expected.")
    endif()
endfunction()

# --- Fixtures ---------------------------------------------------------------

# In script mode (-P) CMAKE_CURRENT_BINARY_DIR is empty, so anchor fixtures in
# the OS temp directory rather than wherever the caller happens to stand.
set(workDir "$ENV{TEMP}/ios_verify_fixtures")
if(workDir STREQUAL "/ios_verify_fixtures")
    set(workDir "/tmp/ios_verify_fixtures")
endif()
file(REMOVE_RECURSE "${workDir}")
file(MAKE_DIRECTORY "${workDir}")

set(archive "${workDir}/game-data.zip")
file(WRITE "${archive}" "openmu game data fixture\n")

file(SHA256 "${archive}" actualDigest)

# 1. Valid sidecar (digest + trailing filename, as sha256sum emits).
file(WRITE "${workDir}/valid.sha256" "${actualDigest}  game-data.zip\n")
run_verifier("valid sidecar" FALSE "${archive}" "${workDir}/valid.sha256")

# 2. Valid sidecar with surrounding whitespace only.
file(WRITE "${workDir}/plain.sha256" "  ${actualDigest}\n")
run_verifier("plain digest sidecar" FALSE "${archive}" "${workDir}/plain.sha256")

# 3. Missing sidecar.
run_verifier("missing sidecar" TRUE "${archive}" "${workDir}/does-not-exist.sha256")

# 4. Missing archive.
file(WRITE "${workDir}/sidecar-only.sha256" "${actualDigest}\n")
run_verifier("missing archive" TRUE "${workDir}/does-not-exist.zip"
    "${workDir}/sidecar-only.sha256")

# 5. Tampered archive after the sidecar was pinned.
file(WRITE "${workDir}/tampered.zip" "different payload\n")
file(WRITE "${workDir}/tampered.sha256" "${actualDigest}\n")
run_verifier("tampered archive" TRUE "${workDir}/tampered.zip"
    "${workDir}/tampered.sha256")

# 6. Wrong digest in the sidecar.
file(WRITE "${workDir}/wrong.sha256"
    "0000000000000000000000000000000000000000000000000000000000000000\n")
run_verifier("wrong digest" TRUE "${archive}" "${workDir}/wrong.sha256")

message(STATUS "All iOS data-archive verification cases passed.")
