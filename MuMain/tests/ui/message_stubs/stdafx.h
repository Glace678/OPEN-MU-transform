#pragma once

#include "Core/Platform/WinCompat.h"
#include <algorithm>
#include <cassert>
#include <cstring>
#include <list>
#include <map>
#include <queue>
#include <vector>

#define SAFE_DELETE(pointer) do { delete (pointer); (pointer) = nullptr; } while (false)

using vec3_t = float[3];
inline void Vector(float x, float y, float z, vec3_t value) { value[0] = x; value[1] = y; value[2] = z; }
inline void VectorCopy(const vec3_t from, vec3_t to) { std::copy_n(from, 3, to); }
inline constexpr int REFERENCE_WIDTH = 640;
inline constexpr int REFERENCE_HEIGHT = 480;

extern HWND g_hWnd;
extern int MouseX, MouseY;
extern bool MouseLButton, MouseLButtonPush, MouseLButtonPop;
extern bool MouseRButton, MouseRButtonPush, MouseRButtonPop;
