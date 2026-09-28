mu_add_test(
    NAME legacy_window_geometry_tests
    SOURCES
        LegacyWindowGeometryTests.cpp
        ${CMAKE_SOURCE_DIR}/src/source/UI/Widgets/Win.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Core/Utilities/PList.cpp
    LINK_LIBS SDL3::SDL3
)

# Compile the real CWin implementation with headless rendering/input doubles.
# These includes are target-local and never affect Main or other test modules.
target_include_directories(legacy_window_geometry_tests BEFORE PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/stubs")

add_test(
    NAME login_resize_state_contract
    COMMAND "${CMAKE_COMMAND}" -P
        "${CMAKE_SOURCE_DIR}/cmake/tests/LoginResizeStateTests.cmake")

mu_add_test(
    NAME item_touch_controller_tests
    SOURCES ItemTouchControllerTests.cpp
        ${CMAKE_SOURCE_DIR}/src/source/UI/Items/Touch/ItemTouchController.cpp
)

mu_add_test(
    NAME inventory_touch_operations_tests
    SOURCES InventoryTouchOperationsTests.cpp
        ${CMAKE_SOURCE_DIR}/src/source/UI/Items/Touch/InventoryTouchOperations.cpp
)
target_include_directories(inventory_touch_operations_tests BEFORE PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/inventory_stubs")

mu_add_test(NAME character_balloon_layout_tests SOURCES CharacterBalloonLayoutTests.cpp)
mu_add_test(NAME message_box_pointer_tests SOURCES MessageBoxPointerTests.cpp)
mu_add_test(NAME interface_input_policy_tests SOURCES InterfaceInputPolicyTests.cpp
    ${CMAKE_SOURCE_DIR}/src/source/UI/Input/InterfaceInputPolicy.cpp)
mu_add_test(NAME message_box_manager_tests
    SOURCES MessageBoxManagerTests.cpp
        ${CMAKE_SOURCE_DIR}/src/source/UI/NewUI/Dialogs/NewUIMessageBox.cpp
        ${CMAKE_SOURCE_DIR}/src/source/ExternalObject/Leaf/xstreambuf.cpp
    LINK_LIBS SDL3::SDL3)
target_include_directories(message_box_manager_tests BEFORE PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/message_stubs")
add_test(NAME message_box_pointer_bridge_contract
    COMMAND "${CMAKE_COMMAND}" -P
        "${CMAKE_SOURCE_DIR}/cmake/tests/MessageBoxPointerBridgeTests.cmake")

add_test(NAME mobile_item_touch_contract
    COMMAND "${CMAKE_COMMAND}" -P
        "${CMAKE_SOURCE_DIR}/cmake/tests/MobileItemTouchTests.cmake")
