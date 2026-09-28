#pragma once
#include "Core/Platform/WinCompat.h"
#include <algorithm>
#include <vector>
#include <span>

enum class STORAGE_TYPE { INVENTORY, STORAGE };
struct ITEM { DWORD Key{}; int x{}, y{}; short Type{}; };
inline constexpr int BITMAP_INTERFACE_NEW_INVENTORY_BASE_BEGIN = 0;
