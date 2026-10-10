mu_add_test(
    NAME gamelogic_loader_validation_tests
    SOURCES LoaderValidationTests.cpp
)

mu_add_test(
    NAME gamelogic_packet_bounds_tests
    SOURCES PacketBoundsTests.cpp
)
mu_add_test(
    NAME gamelogic_dispatch_lookup_contract_tests
    SOURCES DispatchLookupContractTests.cpp
)

# 86-69 (P0 #17) / 86-72: compile the REAL NewUIItemMng.cpp against a headless
# stub include tree so ParseItemData and CreateItemByParameters can be driven
# with truncated / over-sized server packets.
mu_add_test(
    NAME item_parse_bounds_tests
    SOURCES
        ItemParseBoundsTests.cpp
        item_parse_stubs/item_parse_stubs.cpp
        ${CMAKE_SOURCE_DIR}/src/source/UI/NewUI/Inventory/NewUIItemMng.cpp
)
target_include_directories(item_parse_bounds_tests BEFORE PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/item_parse_stubs")
# TextList[50] unified safe-append layer (86-01 / 86-54 / 87-38).
mu_add_test(
    NAME textlist_safe_append_tests
    SOURCES TextListSafeTests.cpp
)
target_include_directories(textlist_safe_append_tests BEFORE PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/textlist_stubs")

# scope85 L5 client index guards (castle NPC / siege map / guild dialogs /
# shop buy confirm / chat-command parameter page): pin real capacities and
# sweep the exact guard predicates against boundary/malicious indices.
mu_add_test(
    NAME gamelogic_scope85_index_guard_tests
    SOURCES Scope85IndexGuardTests.cpp
)
