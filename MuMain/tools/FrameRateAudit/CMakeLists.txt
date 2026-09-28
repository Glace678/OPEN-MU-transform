cmake_minimum_required(VERSION 3.25)

project(MuMainFramePacingBenchmark LANGUAGES CXX)

get_filename_component(MUMAIN_ROOT "${CMAKE_CURRENT_LIST_DIR}/../.." ABSOLUTE)

add_executable(frame_pacing_benchmark
    FramePacingBenchmark.cpp
    "${MUMAIN_ROOT}/src/source/Core/Time/FrameClock.cpp"
    "${MUMAIN_ROOT}/src/source/Core/Time/FramePacer.cpp")

target_compile_features(frame_pacing_benchmark PRIVATE cxx_std_20)
target_include_directories(frame_pacing_benchmark PRIVATE
    "${MUMAIN_ROOT}/src/source")

if(MSVC)
    target_compile_options(frame_pacing_benchmark PRIVATE /utf-8 /EHsc)
endif()

if(WIN32)
    target_link_libraries(frame_pacing_benchmark PRIVATE winmm)
endif()
