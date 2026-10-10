// Test-only stub of Engine/Object/ZzzInventory.h: declares ONLY the three
// global tooltip buffers that TextListSafe.h operates on. The real header
// declares these same globals at the same type; the wrapper logic (index clamp
// + bounded copy) is what this test exercises.
#pragma once

extern wchar_t TextList[50][100];
extern int TextListColor[50];
extern int TextBold[50];