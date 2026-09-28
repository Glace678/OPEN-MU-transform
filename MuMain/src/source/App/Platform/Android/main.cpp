// Android entry point exported from libmain.so for SDLActivity.
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
                "Failed to set Android data root to '%s': %s",
                argv[argumentIndex + 1], error.message().c_str());
        }
        else
        {
            const std::string configPath = (dataRoot / "config.ini").string();
            // SDL snapshots the process environment on first use. Update both
            // that snapshot and libc so GameConfig and RenderConfig see the
            // per-install config even if SDL_SetHint ran before this point.
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
    SDL_SetHint(SDL_HINT_TOUCH_MOUSE_EVENTS, "0");
    ApplyDataRoot(argc, argv);
    return WinMain(nullptr, nullptr, nullptr, SW_SHOW);
}
