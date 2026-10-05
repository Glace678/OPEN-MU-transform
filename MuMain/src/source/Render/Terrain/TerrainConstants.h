#pragma once

// LOD terrain scale/size and terrain surface-type constants, extracted from the legacy aggregate
// Core/Globals/_define.h (P2 disassembly).

#define TERRAIN_SCALE     100.f
#define TERRAIN_SIZE      256
#define TERRAIN_SIZE_MASK 255

#define BLOODCASTLE_NUM 8
#define HELLAS_NUM      7
#define CHAOS_NUM       6

#define TERRAIN_MAP_NORMAL 0
#define TERRAIN_MAP_ALPHA  1
#define TERRAIN_MAP_GRASS  2
#define TERRAIN_MAP_TRAP   3
