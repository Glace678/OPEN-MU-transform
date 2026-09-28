// iOS entry point (preview scaffold).
//
// SDL3 owns the UIKit application lifecycle on iOS: including SDL_main.h
// renames main() to SDL_main(), and SDL's own bootstrap installs the real
// main(), sets up UIWindow/UIApplication and calls SDL_main on the main
// thread once the app is active.
//
// This mirrors Platform/Android/main.cpp and Platform/HarmonyOS/main.cpp: the
// engine itself is platform-neutral C++ (the shared bootstrap lives in
// App/Platform/Windows/Winmain.cpp), so this file only prepares the sandbox
// data root and hands off. Touch, gestures and auto-login are shared with the
// other mobile targets behind `#if defined(__ANDROID__) || defined(__OHOS__)`
// plus the iOS-specific handling below.
#include <SDL3/SDL.h>
#include <SDL3/SDL_main.h>

#include "Core/Platform/WinCompat.h"

#import <Foundation/Foundation.h>

#include <cstdlib>
#include <filesystem>
#include <string>
#include <system_error>

int WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, PSTR commandLine, int showCommand);

namespace
{
// iOS apps get no user argv from the UIKit bootstrap, so the data root comes
// from the environment instead of a --data-root argument:
//
//   MU_IOS_DATA_ROOT  directory the shell/packaging step extracted
//                     game-data.zip into (typically <sandbox>/Documents/Data)
//
// Fallback: <Documents>/Data, matching the layout the iOS packaging docs use.
// The working directory is moved there so relative asset paths ("Data\\...")
// resolve exactly like on the other platforms, and MU_CONFIG_FILE is pointed
// at the sibling config.ini unless the shell already set it.
void ApplyDataRoot()
{
    std::filesystem::path dataRoot;
    if (const char* fromEnvironment = std::getenv("MU_IOS_DATA_ROOT"))
    {
        dataRoot = std::filesystem::u8path(fromEnvironment);
    }
    else
    {
        @autoreleasepool
        {
            NSArray<NSString*>* documents = NSSearchPathForDirectoriesInDomains(
                NSDocumentDirectory, NSUserDomainMask, YES);
            if (documents.count > 0)
            {
                dataRoot = std::filesystem::u8path(documents.firstObject.UTF8String) / "Data";
            }
        }
    }

    if (dataRoot.empty())
    {
        SDL_LogWarn(SDL_LOG_CATEGORY_APPLICATION,
            "iOS data root could not be determined; relying on the current directory.");
        return;
    }

    std::error_code error;
    if (!std::filesystem::is_directory(dataRoot, error))
    {
        // First launch before the data archive has been extracted: the shell
        // is expected to extract game-data.zip from the app bundle and relaunch
        // the flow; keep the engine's own error reporting intact.
        SDL_LogWarn(SDL_LOG_CATEGORY_APPLICATION,
            "iOS data root '%s' does not exist yet (first-run extraction pending?).",
            dataRoot.string().c_str());
        return;
    }

    std::filesystem::current_path(dataRoot, error);
    if (error)
    {
        SDL_LogError(SDL_LOG_CATEGORY_APPLICATION,
            "Failed to set iOS data root to '%s': %s",
            dataRoot.string().c_str(), error.message().c_str());
        return;
    }

    if (std::getenv("MU_CONFIG_FILE") == nullptr)
    {
        const std::string configPath = (dataRoot.parent_path() / "config.ini").string();
        // SDL snapshots the process environment on first use; update both the
        // snapshot and libc so GameConfig sees the per-install config even if
        // SDL_SetHint ran before this point (same reasoning as Android).
        SDL_setenv_unsafe("MU_CONFIG_FILE", configPath.c_str(), 1);
    }
}
} // namespace

int main(int argc, char* argv[])
{
    // Touch is the native input on iOS; MobileGestureMapper consumes finger
    // events directly, so SDL must not additionally synthesize mouse events.
    SDL_SetHint(SDL_HINT_TOUCH_MOUSE_EVENTS, "0");
    ApplyDataRoot();
    return WinMain(nullptr, nullptr, nullptr, SW_SHOW);
}
