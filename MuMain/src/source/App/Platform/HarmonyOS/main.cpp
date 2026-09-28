// HarmonyOS / OpenHarmony entry point. The SDL OpenHarmony backend loads
// libmain.so and invokes main() on the SDL thread after the ArkTS
// SDLAbility/XComponent has created the EGL surface.
//
// This mirrors the Android entry point: the game itself is platform-neutral
// C++ (it calls WinMain in App/Platform/Windows/Winmain.cpp), so this file only
// prepares the sandboxed data root and hands off. Touch, gestures and the
// auto-login identity are shared with the Android build behind
// `#if defined(__ANDROID__) || defined(__OHOS__)`.
#include <SDL3/SDL.h>
#include <SDL3/SDL_main.h>

#include "Core/Platform/WinCompat.h"

#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <system_error>

int WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, PSTR commandLine, int showCommand);

namespace
{
// The ArkTS shell passes --data-root <sandbox files dir>/Data so the game runs
// with the extracted data archive as its working directory.
void ApplyDataRoot(int argc, char* argv[])
{
    constexpr char DataRootArgument[] = "--data-root";
    for (int argumentIndex = 1; argumentIndex + 1 < argc; ++argumentIndex)
    {
        if (std::strcmp(argv[argumentIndex], DataRootArgument) != 0)
        {
            continue;
        }

        std::error_code error;
        const std::filesystem::path dataRoot = std::filesystem::u8path(argv[argumentIndex + 1]);
        std::filesystem::current_path(dataRoot, error);
        if (error)
        {
            SDL_LogError(SDL_LOG_CATEGORY_APPLICATION,
                "Failed to set HarmonyOS data root to '%s': %s",
                argv[argumentIndex + 1], error.message().c_str());
        }
        else
        {
            const std::string configPath = (dataRoot / "config.ini").string();
            // Keep SDL's snapshotted environment and libc in sync so GameConfig
            // and RenderConfig see the per-install config.
            if (SDL_setenv_unsafe("MU_CONFIG_FILE", configPath.c_str(), 1) != 0)
            {
                SDL_LogError(SDL_LOG_CATEGORY_APPLICATION,
                    "Failed to set MU_CONFIG_FILE to '%s'", configPath.c_str());
            }
        }
        return;
    }
}
} // namespace

int main(int argc, char* argv[])
{
    // Fingers drive MobileGestureMapper directly; never synthesize mouse motion
    // from touch, which would double-feed movement/click handling.
    SDL_SetHint(SDL_HINT_TOUCH_MOUSE_EVENTS, "0");
    ApplyDataRoot(argc, argv);
    return WinMain(nullptr, nullptr, nullptr, SW_SHOW);
}
