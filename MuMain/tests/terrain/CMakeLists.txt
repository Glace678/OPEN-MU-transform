mu_add_test(
    NAME test_terrain_light_rows
    SOURCES
        test_terrain_light_rows.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Render/Terrain/TerrainLightRows.cpp
)

mu_add_test(
    NAME test_object_placement_data
    SOURCES
        test_object_placement_data.cpp
        test_pet_data_layout.cpp
        test_terrain_data.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Data/World/ObjectPlacementData.cpp
        ${CMAKE_SOURCE_DIR}/src/source/Data/World/TerrainData.cpp
)
