# Opt-in static analysis and sanitizer wiring.
#
# Included by the top-level CMakeLists.txt. Nothing here runs unless the caller
# asks for it (ENABLE_CLANG_TIDY / ENABLE_ASAN), so a normal build pays no cost
# and sees no behavior change. See docs/build/static-analysis.md.
#
# Why clang-tidy runs over an MSVC build: clang-tidy parses each translation
# unit with clang's own front end but takes the compile flags from CMake, so it
# analyses exactly what cl.exe would have compiled. Two MSVC specifics have to
# be supplied as extra arguments:
#
#   --target   The x86 build defines _USE_32BIT_TIME_T, which corecrt.h rejects
#              under a 64-bit target ("You cannot use 32-bit time_t with
#              _WIN64"). clang-tidy defaults to the host's pointer size, so the
#              x86 build needs the triple spelled out.
#   /EHsc      The engine throws (BaseCls.h) and compiles with /EHsc. The /EHsc
#              on cl.exe's own command line is not re-driven through clang's cl
#              driver in CMake's co-compile path, so without restating it every
#              throw site reports "cannot use 'throw' with exceptions disabled"
#              and the TU fails to analyse.
#
# Both are passed via -extra-arg so they reach the internal compile, and the
# .clang-tidy file at the repository root holds the check list.

include_guard(GLOBAL)

# Locate clang-tidy. The Visual Studio-bundled LLVM (VC/Tools/Llvm/<arch>/bin)
# is tried last, after PATH and an explicit MU_CLANG_TIDY cache variable, so a
# standalone LLVM install wins over the VS copy.
function(mu_find_clang_tidy out_var)
    if(DEFINED CACHE{MU_CLANG_TIDY})
        set(result "$CACHE{MU_CLANG_TIDY}")
        if(NOT EXISTS "${result}")
            message(FATAL_ERROR "MU_CLANG_TIDY does not exist: ${result}")
        endif()
    else()
        find_program(result
            NAMES clang-tidy
            DOC "Path to clang-tidy. Set to override auto-detection.")
        if(NOT result AND MSVC)
            # VS ships LLVM per architecture; pick the one matching the target.
            if(CMAKE_SIZEOF_VOID_P EQUAL 8)
                set(_llvm_arch x64)
            else()
                set(_llvm_arch x86)
            endif()
            # cl.exe sits at <VS>/VC/Tools/MSVC/<ver>/bin/Host<arch>/<arch>/cl.exe
            # while LLVM lives at <VS>/VC/Tools/Llvm/<arch>/bin/clang-tidy.exe --
            # a different depth for every toolchain version, so walk up from
            # cl.exe's directory instead of assuming a fixed number of levels.
            get_filename_component(_dir "${CMAKE_CXX_COMPILER}" DIRECTORY)
            foreach(_ RANGE 1 10)
                get_filename_component(_parent "${_dir}" DIRECTORY)
                if(NOT _parent OR _parent STREQUAL _dir)
                    break()
                endif()
                set(_dir "${_parent}")
                foreach(_candidate
                        "${_dir}/Llvm/${_llvm_arch}/bin/clang-tidy.exe"
                        "${_dir}/Llvm/bin/clang-tidy.exe")
                    if(EXISTS "${_candidate}")
                        set(result "${_candidate}")
                        break()
                    endif()
                endforeach()
                if(result)
                    break()
                endif()
            endforeach()
        endif()
    endif()

    if(result)
        message(STATUS "clang-tidy: ${result}")
        set(${out_var} "${result}" PARENT_SCOPE)
    else()
        message(FATAL_ERROR
            "ENABLE_CLANG_TIDY=ON but clang-tidy was not found. Install LLVM, "
            "or point MU_CLANG_TIDY at the executable. Note the Visual Studio "
            "C++ workload 'C++ Clang tools for Windows' supplies it.")
    endif()
endfunction()

# Attach clang-tidy to a target. Called once per target that should be analysed
# (the engine library and the thin entry-point executable -- never the vendored
# third-party libraries, whose findings are not actionable here).
function(mu_enable_clang_tidy target)
    if(NOT ENABLE_CLANG_TIDY)
        return()
    endif()

    if(NOT CMAKE_EXPORT_COMPILE_COMMANDS)
        message(WARNING
            "ENABLE_CLANG_TIDY=ON without CMAKE_EXPORT_COMPILE_COMMANDS=ON: "
            "clang-tidy will run without a compile database and may not resolve "
            "system headers. The CMake presets set it; hand configurations "
            "should too.")
    endif()

    mu_find_clang_tidy(_tidy)

    set(_args "")
    # MSVC builds need the target triple spelled out (see file header). GCC/Clang
    # builds already carry -m32/-m64 in their flags, so no extra argument there.
    if(MSVC)
        if(CMAKE_SIZEOF_VOID_P EQUAL 8)
            set(_triple "x86_64-pc-windows-msvc")
        else()
            set(_triple "i686-pc-windows-msvc")
        endif()
        # The /EHsc that cl.exe is invoked with does not reach the front end in
        # the no-compile-database path CMake's co-compile uses: flags passed
        # after `--` are not re-driven through clang's cl driver, so every throw
        # site reports "cannot use 'throw' with exceptions disabled" and the TU
        # fails. /EHsc restated as an extra-arg is what enables C++ exceptions
        # there (clang's cl mode silently drops -fexceptions, so that is not a
        # substitute). -fexceptions is used for the non-MSVC toolchains.
        list(APPEND _args "-extra-arg=--target=${_triple}" "-extra-arg=/EHsc")
    else()
        list(APPEND _args "-extra-arg=-fexceptions")
    endif()

    # CMake appends the translation unit's own compile flags after `--`, so no
    # -p compile database is needed here. WarningsAsErrors in .clang-tidy is
    # empty, so this can never fail the build.
    set_property(TARGET ${target} APPEND PROPERTY
        CXX_CLANG_TIDY "${_tidy}" ${_args})
endfunction()

# MSVC AddressSanitizer. MSVC's ASan is x64-only; the 32-bit client cannot use
# it. The build keeps WIN32_EXECUTABLE on (no console), so ASan's report goes to
# the debugger output window -- attach a debugger or set a subsystem override to
# read it on stdout.
function(mu_enable_asan target)
    if(NOT ENABLE_ASAN)
        return()
    endif()

    if(NOT MSVC)
        message(FATAL_ERROR
            "ENABLE_ASAN currently wires MSVC's /fsanitize=address only. For a "
            "GCC/Clang build, add -fsanitize=address to the toolchain instead.")
    endif()

    if(NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
        message(FATAL_ERROR
            "ENABLE_ASAN requires an x64 build (MSVC AddressSanitizer has no "
            "32-bit support). Configure with the windows-x64 presets.")
    endif()

    # Instrumentation is a compile-time choice, so it applies to every target.
    target_compile_options(${target} PRIVATE /fsanitize=address)

    # The sanitizer runtime is linked in, so the flag belongs on the final
    # binary. On a static library (MuClient) it would be a no-op and CMake
    # warns about it; the executable picks the runtime up anyway through
    # /fsanitize=address on its own link line.
    get_target_property(_type ${target} TYPE)
    if(_type STREQUAL "EXECUTABLE" OR _type STREQUAL "SHARED_LIBRARY"
            OR _type STREQUAL "MODULE_LIBRARY")
        target_link_options(${target} PRIVATE /fsanitize=address)
    endif()
    message(STATUS "AddressSanitizer enabled on ${target} (MSVC, x64)")
endfunction()
