// macOS entry point. SDL owns the application window and event loop; the
// shared desktop bootstrap remains in Winmain.cpp until it is renamed.
#include "Core/Platform/WinCompat.h"

int WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, PSTR commandLine, int showCommand);

int main(int /*argc*/, char* /*argv*/[])
{
    return WinMain(nullptr, nullptr, nullptr, SW_SHOW);
}
