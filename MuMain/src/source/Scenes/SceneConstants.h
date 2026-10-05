#pragma once

// Scene-domain constants extracted from the legacy aggregate header
// Core/Globals/_define.h (P2 disassembly). Game-scene ordering and server
// group / combo-button limits live here with the scene layer.
#include <cstdint>

constexpr int MAX_SERVER_PER_GROUP = 20;

enum EGameScene
{
    SERVER_LIST_SCENE = 0,
    WEBZEN_SCENE = 1,
    LOG_IN_SCENE = 2,
    LOADING_SCENE = 3,
    CHARACTER_SCENE = 4,
    MAIN_SCENE = 5,
};

constexpr auto NUM_LINE_CMB = (7);
constexpr auto NUM_BUTTON_CMB = (2);
constexpr auto NUM_PAR_BUTTON_CMB = (5);