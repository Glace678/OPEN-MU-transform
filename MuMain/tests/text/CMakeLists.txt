mu_add_test(
    NAME test_text_line_wrap
    SOURCES
        test_text_line_wrap.cpp
        test_text_box_layout.cpp
        test_text_buffer_wrap.cpp
        test_item_explanation_layout.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Core/Text/TextLineWrap.cpp
)

mu_add_test(
    NAME test_server_notice_localization
    SOURCES
        test_server_notice_localization.cpp
        ${CMAKE_SOURCE_DIR}/src/source/UI/NewUI/HUD/ServerNoticeLocalization.cpp
        ${CMAKE_BINARY_DIR}/Generated/I18N/All.cpp
        ${CMAKE_BINARY_DIR}/Generated/I18N/Game.cpp
        ${CMAKE_BINARY_DIR}/Generated/I18N/Dialog.cpp
        ${CMAKE_BINARY_DIR}/Generated/I18N/Editor.cpp
        ${CMAKE_BINARY_DIR}/Generated/I18N/Metadata.cpp
)
add_dependencies(test_server_notice_localization ResxGen)
target_include_directories(test_server_notice_localization PRIVATE ${CMAKE_BINARY_DIR}/Generated)
