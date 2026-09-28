#pragma once

#include "Core/Platform/WinCompat.h"
#include <cassert>
#include <cstdlib>
#include <cstring>

#define SAFE_DELETE(pointer) do { delete (pointer); (pointer) = nullptr; } while (false)
