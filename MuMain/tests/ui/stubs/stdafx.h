#pragma once

#include "Core/Platform/WinCompat.h"
#include <cassert>
#include <cstdlib>
#include <cstring>

template <class T> inline void SafeDelete(T*& pointer) { if (pointer != nullptr) { delete pointer; pointer = nullptr; } }
template <class T> inline void SafeDeleteArray(T*& pointer) { if (pointer != nullptr) { delete[] pointer; pointer = nullptr; } }
