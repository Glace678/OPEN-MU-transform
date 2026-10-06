mu_add_test(
    NAME core_ui_font_tests
    SOURCES UIFontSelectionTests.cpp
)

mu_add_test(
    NAME core_input_timing_tests
    SOURCES
        FrameClockTests.cpp
        FocusNavigatorTests.cpp
        GamepadMapperTests.cpp
        HapticsTests.cpp
        KeyboardStateTests.cpp
        LocalAutoLoginTests.cpp
        MobileGestureMapperTests.cpp
        SdlGamepadBackendTests.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Core/Time/FrameClock.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Core/Input/FocusNavigator.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Core/Input/GamepadMapper.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Core/Input/GamepadBindings.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Core/Input/GamepadService.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Core/Input/KeyState.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Core/Input/MobileGestureMapper.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Core/Input/SdlGamepadBackend.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Core/Haptics/Haptics.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Network/Login/LocalAutoLogin.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Network/Login/LocalLoginCredentials.cpp
    LINK_LIBS
        SDL3::SDL3
)

if(WIN32)
    if(CMAKE_SIZEOF_VOID_P EQUAL 8)
        set(RIME_TEST_ARCH "x64")
    else()
        set(RIME_TEST_ARCH "x86")
    endif()
    set(RIME_TEST_DLL
        "${CMAKE_SOURCE_DIR}/src/third_party/librime/runtime/${RIME_TEST_ARCH}/rime.dll")
else()
    set(RIME_TEST_DLL "${MU_RIME_RUNTIME_LIBRARY}")
endif()

if(EXISTS "${RIME_TEST_DLL}")
    add_executable(rime_runtime_smoke RimeRuntimeSmoke.cpp)
    target_compile_features(rime_runtime_smoke PRIVATE cxx_std_20)
    target_include_directories(rime_runtime_smoke PRIVATE
        "${CMAKE_SOURCE_DIR}/src/source"
        "${CMAKE_SOURCE_DIR}/src/third_party/librime/include")
    target_link_libraries(rime_runtime_smoke PRIVATE ${CMAKE_DL_LIBS})
    if(MSVC)
        target_compile_options(rime_runtime_smoke PRIVATE /utf-8 /EHsc)
    endif()
    add_test(
        NAME rime_runtime_smoke
        COMMAND rime_runtime_smoke
            "${RIME_TEST_DLL}"
            "${CMAKE_SOURCE_DIR}/src/third_party/librime/share"
            "${CMAKE_CURRENT_BINARY_DIR}/rime-smoke-user")
else()
    message(STATUS "skipped rime_runtime_smoke: runtime dll absent: ${RIME_TEST_DLL}")
endif()
mu_add_test(
    NAME core_account_portal_tests
    SOURCES AccountPortalTests.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Network/Login/AccountPortal.cpp
)

mu_add_test(
    NAME core_merchant_price_tests
    SOURCES MerchantPriceQuoteTests.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Network/Server/MerchantPriceQuoteCache.cpp
)
