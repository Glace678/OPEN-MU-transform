///////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "Core/Platform/PlatformDetect.h"
#include "Core/Input/FocusNavigator.h"
#include "Core/Input/Input.h"
#include "Core/Input/KeyState.h"
#include "Core/Input/GamepadService.h"
#include "Core/Input/MobileGestureMapper.h"
#include "UI/Items/Touch/InventoryTouchAdapter.h"
#include "UI/NewUI/Dialogs/NewUIMessageBox.h"
#include "UI/ControllerKeyboard/ControllerShortcuts.h"

#define WIN32_LEAN_AND_MEAN
#define WIN32_EXTRA_LEAN

#ifdef _WIN32
#include <dpapi.h>
#endif
#include <clocale>
#include "Core/Platform/WinIni.h"  // private-profile (.ini) API
#include "Core/Platform/NativeModal.h"
#include "Data/GameConfig/GameConfig.h"
#include "UI/Legacy/UIWindows.h"
#include "UI/Legacy/UIManager.h"
#include "Render/Textures/ZzzOpenglUtil.h"
#include "Render/Textures/ZzzTexture.h"
#include "Render/RHI/RHI.h"
#include "Engine/Object/ZzzOpenData.h"
#include "Scenes/SceneCore.h"
#include "Scenes/SceneManager.h"
#include "Network/Reconnect/ReconnectManager.h"
#include "Network/IncomingPacketQueue.h"
#include "Network/Login/LocalLoginCredentials.h"
#include "Core/Time/FrameTimerScheduler.h"
#include <SDL3/SDL.h>
#include "App/Platform/Android/TextInput.h"
#include "Render/Models/ZzzBMD.h"
#include "Render/Shaders/ItemSpecularShader.h"
#include "Render/Core/RenderConfig.h"
#include "Render/Core/GlobalUBO.h"
#include "Render/Core/SceneUBO.h"
#include "Render/Core/BoneUBO.h"
#include "Render/Core/ImmediateRenderer.h"
#include "Render/Shaders/PassthroughShader.h"
#include "Render/Shaders/TerrainShader.h"
#include "Render/Shaders/BMDMeshShader.h"
#include "Render/Shaders/PlanarShadowShader.h"
#include "Render/Shaders/TerrainShader.h"
#include "Engine/Object/ZzzInfomation.h"
#include "Engine/Object/ZzzObject.h"
#include "Engine/AI/ZzzAI.h"
#include "Engine/Object/ZzzCharacter.h"
#include "Engine/Object/AnimationTaskPool.h"
#include "Engine/Object/ZzzInterface.h"
#include "Engine/Object/ZzzInventory.h"
#include "Render/Terrain/ZzzLodTerrain.h"
#include "Audio/DSPlaySound.h"

#include "App/Platform/Windows/resource.h"
#include "Core/Platform/Imm.h"
#include "Core/Platform/BundledFonts.h"
#include "Core/Platform/UIFontSelection.h"
#include "Engine/Pathing/ZzzPath.h"
#include "App/Platform/Windows/Local.h"
#include "GameLogic/Items/PersonalShopTitleImp.h"
#include "GameLogic/Items/MixMgr.h"

#include "UI/Legacy/UIMapName.h"		// rozy
#include "Core/Utilities/CpuUsage.h"

#include "MUHelper/MuHelper.h"
#include "Camera/CameraManager.h"

#include "UI/Windows/CBTMessageBox.h"

#include "GameLogic/Events/CSChaosCastle.h"
#ifdef _WIN32
#include <io.h>
#endif
#include <filesystem>
#include "Core/Input/Input.h"
#include "Core/Time/Timer.h"
#include "UI/Legacy/UIMng.h"


#include "World/MapInfra/w_MapHeaders.h"

#include "GameLogic/Pets/w_PetProcess.h"



#include "UI/NewUI/NewUISystem.h"
#include "Camera/CameraConfig.h"
#include "Camera/CameraProjection.h"
#include "Input/Selection.h"
#include "UI/ControllerKeyboard/ControllerKeyboard.h"
#include "I18N/All.h"

#ifdef _EDITOR
#include "../MuEditor/Core/MuEditorCore.h"
#include "imgui.h"
#include "imgui_impl_sdl3.h"
#include "../MuEditor/Config/MuEditorConfig.h"
#endif

namespace
{
// BMD-backed data is still addressed through the historical
// Data/Local/<legacy-code> directories. New UI-only locales (fr/ko/vi) map to
// Eng in the selector, and this startup guard also protects hand-edited or
// stale config.ini files from sending every loader to a missing directory.
std::wstring ResolveDataLanguageSelection(const std::wstring& requested)
{
    if (requested.empty())
        return L"Eng";

    std::error_code error;
    const auto dataDirectory = std::filesystem::path(L"Data")
        / L"Local" / requested;
    if (std::filesystem::is_directory(dataDirectory, error))
        return requested;

    return L"Eng";
}
}

CUIMercenaryInputBox* g_pMercenaryInputBox = nullptr;
CUITextInputBox* g_pSingleTextInputBox = nullptr;
CUITextInputBox* g_pSinglePasswdInputBox = nullptr;
int g_iChatInputType = 1;
extern BOOL g_bIMEBlock;

CMultiLanguage* pMultiLanguage = nullptr;

extern DWORD g_dwTopWindow;

CUIManager* g_pUIManager = nullptr;
CUIMapName* g_pUIMapName = nullptr;		// rozy

float Time_Effect = 0;
bool ashies = false;
int weather = rand() % 3;

HWND      g_hWnd = nullptr;
HINSTANCE g_hInst = nullptr;
HDC       g_hDC = nullptr;
HGLRC     g_hRC = nullptr;

// SDL owns the window and GL context (issue #442). The native HWND is bridged
// into g_hWnd so the remaining Win32 code (IME, DirectSound, cursor, the legacy
// EDIT-control text boxes) keeps working until those are migrated.
static SDL_Window*   g_sdlWindow = nullptr;
static SDL_GLContext g_sdlGLContext = nullptr;
static Core::Time::EffectiveFramePolicy g_effectiveFramePolicy;
HFONT     g_hFont = nullptr;
HFONT     g_hFontBold = nullptr;
HFONT     g_hFontBig = nullptr;
HFONT     g_hFixFont = nullptr;

CTimer* g_pTimer = new CTimer();    // performance counter.
bool      Destroy = false;
bool      ActiveIME = false;

BYTE* RendomMemoryDump;
ITEM_ATTRIBUTE* ItemAttRibuteMemoryDump;
CHARACTER* CharacterMemoryDump;

int       RandomTable[100];

CErrorReport g_ErrorReport;

BOOL g_bMinimizedEnabled = FALSE;
int g_iScreenSaverOldValue = 60 * 15;



BOOL g_bUseWindowMode = TRUE;
BOOL g_bUseFullscreenMode = FALSE;
bool g_bDisableAnimationTaskPool = true;

#include "Audio/AudioPlayer.h"

extern int  LogIn;
extern wchar_t LogInID[];
extern bool First;
extern int FirstTime;
extern BOOL g_bGameServerConnected;

void CheckHack()
{
    if (!g_bGameServerConnected)
    {
        return;
    }

    g_ConsoleDebug->Write(MCD_SEND, L"SendCheck");

    auto attackSpeed = CharacterAttribute->AttackSpeed;
    auto magicSpeed = CharacterAttribute->MagicSpeed;
    if (CharacterAttribute->Ability & ABILITY_FAST_ATTACK_SPEED
        || CharacterAttribute->Ability & ABILITY_FAST_ATTACK_SPEED2)
    {
        attackSpeed -= 20;
        magicSpeed -= 20;
    }

    const int dwTick = GetTickCount();
    SocketClient->ToGameServer()->SendPing(dwTick, attackSpeed);

    if (!First)
    {
        First = true;
        FirstTime = dwTick;
    }
}

GLvoid KillGLWindow(GLvoid)
{
    Core::Input::FocusNavigator::Instance().Clear();
    Core::Input::GamepadService::Instance().Shutdown();
    Core::Input::ClearVirtualKeys();
    // Release the bridged GDI DC obtained from the SDL window.
    if (g_hDC)
    {
        ReleaseDC(g_hWnd, g_hDC);
        g_hDC = nullptr;
    }

    // SDL owns the GL context and the window (it also restores the display mode
    // when a fullscreen window is destroyed).
    if (g_sdlGLContext)
    {
        SDL_GL_DestroyContext(g_sdlGLContext);
        g_sdlGLContext = nullptr;
        g_hRC = nullptr;
    }

    if (g_sdlWindow)
    {
        SDL_DestroyWindow(g_sdlWindow);
        g_sdlWindow = nullptr;
        g_hWnd = nullptr;
    }
}

#if defined(MU_ENABLE_FRAMEBUFFER_CAPTURE_TESTS)
// Acceptance-only framebuffer capture: when MU_CAPTURE_FRAME=<N> is set, dump
// the Nth matching frame to MU_CAPTURE_PATH as a PPM. Only explicit acceptance
// builds enable this; normal release packages do not expose the capture bridge.
static void MaybeCaptureFrame()
{
    const char* want = std::getenv("MU_CAPTURE_FRAME");
    if (!want) return;
    const char* scene = std::getenv("MU_CAPTURE_SCENE");
    if (scene && SceneFlag != std::strtol(scene, nullptr, 10)) return;
    static long s_frame = 0;
    const long target = std::strtol(want, nullptr, 10);
    if (++s_frame != target) return;

    int w = 0, h = 0;
    SDL_GetWindowSizeInPixels(g_sdlWindow, &w, &h);
    if (w <= 0 || h <= 0) return;

    std::vector<unsigned char> pixels(static_cast<size_t>(w) * h * 3);
    // RHI doesn't manage pixel-store state; keep the explicit alignment (RGB rows
    // aren't guaranteed 4-byte aligned for arbitrary widths).
    glPixelStorei(GL_PACK_ALIGNMENT, 1);
    RHI::ReadColorFramebuffer(0, 0, w, h, pixels.data());

    const char* path = std::getenv("MU_CAPTURE_PATH");
#ifdef _WIN32
    if (!path) path = "mu-frame.ppm";
#else
    if (!path) path = "/tmp/mu-frame.ppm";
#endif
    if (FILE* fp = std::fopen(path, "wb"))
    {
        std::fprintf(fp, "P6\n%d %d\n255\n", w, h);
        // RHI::ReadColorFramebuffer's contract is top-down, matching PPM's row order directly.
        std::fwrite(pixels.data(), 1, static_cast<size_t>(w) * h * 3, fp);
        std::fclose(fp);
        std::fprintf(stderr, "[capture] wrote frame %ld (%dx%d) to %s\n", target, w, h, path);
        std::fprintf(stderr, "[capture] scene=%d protocol=%d loginVisible=%d serverSelectionVisible=%d\n",
            SceneFlag, CurrentProtocolState, CUIMng::Instance().m_LoginWin.IsShow(),
            CUIMng::Instance().m_ServerSelWin.IsShow());
    }
}
#endif

// Present the current frame. SDL owns the window/GL context, so GL swapping goes through
// SDL_GL_SwapWindow instead of the Win32 ::SwapBuffers (issue #442). This is the one place all
// of this file's/LoadingScene.cpp's/SceneManager.cpp's/UIMng.cpp's present call sites funnel
// through, so branching here (rather than at each call site) covers all of them uniformly.
void PlatformSwapBuffers()
{
    if (g_sdlWindow)
    {
        // GLP-19: IR defers its draw until the next incompatible Begin() or an explicit flush, so
        // the frame's last batch would otherwise sit unsubmitted until some later frame. Every
        // swap path in the tree funnels through here (SceneManager, LoadingScene, UIMng), which
        // makes this the one place that cannot be missed.
        IR::Flush();
#if defined(MU_ENABLE_FRAMEBUFFER_CAPTURE_TESTS)
        MaybeCaptureFrame();
#endif
        SDL_GL_SwapWindow(g_sdlWindow);
    }
}

// Monitor refresh rate (Hz) for the display the window is on, via SDL instead
// of the Win32 GetDeviceCaps(VREFRESH) (issue #442). Falls back to 60.
Core::Time::DisplayRefreshRate GetDisplayRefreshRate()
{
    constexpr double DEFAULT_REFRESH_HZ = 60.0;
    Core::Time::DisplayRefreshRate result{DEFAULT_REFRESH_HZ, false};
    if (g_sdlWindow)
    {
        // Before the window is mapped to a display, SDL_GetDisplayForWindow
        // returns 0; fall back to the primary display so a high-refresh monitor
        // isn't capped at the default 60 Hz.
        SDL_DisplayID displayID = SDL_GetDisplayForWindow(g_sdlWindow);
        if (displayID == 0)
            displayID = SDL_GetPrimaryDisplay();
        if (displayID != 0)
        {
            const bool fullscreen = (SDL_GetWindowFlags(g_sdlWindow) & SDL_WINDOW_FULLSCREEN) != 0;
            const SDL_DisplayMode* mode = fullscreen
                ? SDL_GetWindowFullscreenMode(g_sdlWindow)
                : nullptr;
            if (!mode)
                mode = SDL_GetCurrentDisplayMode(displayID);
            if (mode && mode->refresh_rate > 0.0f)
            {
                result.hertz = mode->refresh_rate_denominator > 0
                    ? static_cast<double>(mode->refresh_rate_numerator) / mode->refresh_rate_denominator
                    : static_cast<double>(mode->refresh_rate);
                result.detected = true;
            }
        }
    }
    return result;
}

static Core::Time::DisplayRefreshRate GetMaximumRefreshForResolution(int windowWidth, int windowHeight)
{
    Core::Time::DisplayRefreshRate result = GetDisplayRefreshRate();
    if (!g_sdlWindow)
        return result;

    SDL_DisplayID displayID = SDL_GetDisplayForWindow(g_sdlWindow);
    if (displayID == 0)
        displayID = SDL_GetPrimaryDisplay();
    if (displayID == 0)
        return result;

    int modeCount = 0;
    SDL_DisplayMode** modes = SDL_GetFullscreenDisplayModes(displayID, &modeCount);
    if (!modes)
        return result;

    double maximumHertz = 0.0;
    for (int i = 0; i < modeCount; ++i)
    {
        const SDL_DisplayMode* mode = modes[i];
        if (!mode || mode->w != windowWidth || mode->h != windowHeight)
            continue;

        const double hertz = mode->refresh_rate_denominator > 0
            ? static_cast<double>(mode->refresh_rate_numerator) / mode->refresh_rate_denominator
            : static_cast<double>(mode->refresh_rate);
        maximumHertz = std::max(maximumHertz, hertz);
    }
    SDL_free(modes);

    if (maximumHertz > 0.0)
        return {maximumHertz, true};

    return result;
}

Core::Time::DisplayRefreshRate GetMaximumDisplayRefreshRate()
{
    if (!g_sdlWindow)
        return GetDisplayRefreshRate();

    int width = 0;
    int height = 0;
    SDL_GetWindowSize(g_sdlWindow, &width, &height);
    if ((SDL_GetWindowFlags(g_sdlWindow) & SDL_WINDOW_FULLSCREEN) == 0)
    {
        const SDL_DisplayID display = SDL_GetDisplayForWindow(g_sdlWindow);
        // Window client dimensions are not monitor modes.
        if (const SDL_DisplayMode* desktop = SDL_GetDesktopDisplayMode(
                display != 0 ? display : SDL_GetPrimaryDisplay()))
        {
            width = desktop->w;
            height = desktop->h;
        }
    }
    return GetMaximumRefreshForResolution(width, height);
}

int GetFPSLimit()
{
    return static_cast<int>(std::lround(GetDisplayRefreshRate().hertz));
}

#if defined(__ANDROID__) || defined(__OHOS__)
// High-refresh phones leave the fullscreen surface at their default refresh
// (often 60 Hz) until an application asks for a faster display mode. Request the
// closest fullscreen mode at the desired rate so the frame-rate slider's "max"
// position (and the default DisplayMaximum policy) actually presents at the
// device's peak refresh. Lower fixed caps are enforced by the frame limiter
// without a (flickery) mode switch, so this is only called for the max case.
// Desktop manages display modes through MuApplyWindowResolution; this is
// intentionally mobile-only and never compiles into the Windows build.
static void RequestMobileDisplayMode(double desiredHz)
{
    if (!g_sdlWindow)
        return;
    if ((SDL_GetWindowFlags(g_sdlWindow) & SDL_WINDOW_FULLSCREEN) == 0)
        return;

    const SDL_DisplayID display = SDL_GetDisplayForWindow(g_sdlWindow);
    if (display == 0)
        return;

    int width = 0;
    int height = 0;
    SDL_GetWindowSize(g_sdlWindow, &width, &height);

    SDL_DisplayMode mode;
    if (SDL_GetClosestFullscreenDisplayMode(
            display, width, height, static_cast<float>(desiredHz), false, &mode))
    {
        SDL_SetWindowFullscreenMode(g_sdlWindow, &mode);
    }
}
#endif

const Core::Time::EffectiveFramePolicy& GetEffectiveFramePolicy()
{
    return g_effectiveFramePolicy;
}

void ApplyFrameTimingConfiguration(bool foreground)
{
    const auto& settings = GameConfig::GetInstance().GetFrameTimingSettings();
    bool verticalSyncEngaged = false;
    if (settings.verticalSync && IsVSyncAvailable())
        verticalSyncEngaged = EnableVSync();
    else
        DisableVSync();

    g_effectiveFramePolicy = Core::Time::ResolveFramePolicy(
        settings,
        GetDisplayRefreshRate(),
        foreground,
        verticalSyncEngaged,
        GetMaximumDisplayRefreshRate());
    ResetFrameStats();
    SetTargetFps(g_effectiveFramePolicy.verticalSyncPacesFrames
        ? -1.0
        : g_effectiveFramePolicy.effectiveFrameRate);

#if defined(__ANDROID__) || defined(__OHOS__)
    // Ask the OS for the high-refresh surface when running at the device's peak
    // rate (the "max" end of the frame-rate slider / default DisplayMaximum).
    if (settings.mode == Core::Time::FrameRateMode::DisplayMaximum)
        RequestMobileDisplayMode(g_effectiveFramePolicy.effectiveFrameRate);
#endif

    g_ErrorReport.Write(
        L"> Frame policy: requested %.3f Hz, effective %.3f Hz, display %.3f Hz, %hs.\r\n",
        g_effectiveFramePolicy.requestedFrameRate,
        g_effectiveFramePolicy.effectiveFrameRate,
        g_effectiveFramePolicy.displayRefreshRate.hertz,
        Core::Time::ToString(g_effectiveFramePolicy.reason).data());
}

BOOL GetFileNameOfFilePath(wchar_t* lpszFile, wchar_t* lpszPath)
{
    auto iFind = (int)'\\';
    wchar_t* lpFound = lpszPath;
    wchar_t* lpOld = lpFound;
    while (lpFound)
    {
        lpOld = lpFound;
        lpFound = wcschr(lpFound + 1, iFind);
    }

    if (wcschr(lpszPath, iFind))
    {
        wcscpy(lpszFile, lpOld + 1);
    }
    else
    {
        wcscpy(lpszFile, lpOld);
    }

    BOOL bCheck = TRUE;
    for (wchar_t* lpTemp = lpszFile; bCheck; ++lpTemp)
    {
        switch (*lpTemp)
        {
        case '\"':
        case '\\':
        case '/':
        case ' ':
            *lpTemp = '\0';
        case '\0':
            bCheck = FALSE;
            break;
        }
    }

    return (TRUE);
}

WORD DecryptCheckSumKey(WORD wSource)
{
    WORD wAcc = wSource ^ 0xB479;
    return ((wAcc >> 10) << 4) | (wAcc & 0xF);
}

DWORD GenerateCheckSum(BYTE* pbyBuffer, DWORD dwSize, WORD wKey)
{
    auto dwKey = (DWORD)wKey;
    DWORD dwResult = dwKey << 9;
    for (DWORD dwChecked = 0; dwChecked <= dwSize - 4; dwChecked += 4)
    {
        DWORD dwTemp;
        memcpy(&dwTemp, pbyBuffer + dwChecked, sizeof(DWORD));

        switch ((dwChecked / 4 + wKey) % 3)
        {
        case 0:
            dwResult ^= dwTemp;
            break;
        case 1:
            dwResult += dwTemp;
            break;
        case 2:
            dwResult <<= (dwTemp % 11);
            dwResult ^= dwTemp;
            break;
        }

        if (0 == (dwChecked % 4))
        {
            dwResult ^= ((dwKey + dwResult) >> ((dwChecked / 4) % 16 + 3));
        }
    }

    return (dwResult);
}

DWORD GetCheckSum(WORD wKey)
{
    wKey = DecryptCheckSumKey(wKey);

    wchar_t lpszFile[MAX_PATH];

    wcscpy(lpszFile, L"data\\local\\Gameguard.csr");

    HANDLE hFile = CreateFile(lpszFile, GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (INVALID_HANDLE_VALUE == hFile)
    {
        return (0);
    }

    DWORD dwSize = GetFileSize(hFile, nullptr);
    auto* pbyBuffer = new BYTE[dwSize];
    DWORD dwNumber;
    ReadFile(hFile, pbyBuffer, dwSize, &dwNumber, nullptr);
    CloseHandle(hFile);

    DWORD dwCheckSum = GenerateCheckSum(pbyBuffer, dwSize, wKey);
    delete[] pbyBuffer;

    return (dwCheckSum);
}

BOOL GetFileVersion(wchar_t* lpszFileName, WORD* pwVersion)
{
#ifndef _WIN32
    // File version-info is a Win32 crash-report detail; report "unknown".
    (void)lpszFileName; (void)pwVersion;
    return FALSE;
#else
    DWORD dwHandle;
    DWORD dwLen = GetFileVersionInfoSize(lpszFileName, &dwHandle);
    if (dwLen <= 0)
    {
        return (FALSE);
    }

    auto* pbyData = new BYTE[dwLen];
    if (!GetFileVersionInfo(lpszFileName, dwHandle, dwLen, pbyData))
    {
        delete[] pbyData;
        return (FALSE);
    }

    VS_FIXEDFILEINFO* pffi;
    UINT uLen;
    if (!VerQueryValue(pbyData, L"\\", (LPVOID*)&pffi, &uLen))
    {
        delete[] pbyData;
        return (FALSE);
    }

    pwVersion[0] = HIWORD(pffi->dwFileVersionMS);
    pwVersion[1] = LOWORD(pffi->dwFileVersionMS);
    pwVersion[2] = HIWORD(pffi->dwFileVersionLS);
    pwVersion[3] = LOWORD(pffi->dwFileVersionLS);

    delete[] pbyData;
    return (TRUE);
#endif
}

extern PATH* path;

void DestroyWindow()
{
    // Save game configuration to config.ini
    GameConfig::GetInstance().SetSoundVolume(g_pOption->GetVolumeLevel());
    GameConfig::GetInstance().Save();

#ifdef _EDITOR
    // Save editor configuration
    g_MuEditorConfig.Save();
#endif

    CUIMng::Instance().Release();

    //. release font handle
    if (g_hFont)
        DeleteObject((HGDIOBJ)g_hFont);

    if (g_hFontBold)
        DeleteObject((HGDIOBJ)g_hFontBold);

    if (g_hFontBig)
        DeleteObject((HGDIOBJ)g_hFontBig);

    if (g_hFixFont)
        ::DeleteObject((HGDIOBJ)g_hFixFont);

    ReleaseCharacters();

    SafeDelete(GateAttribute);

    SafeDelete(SkillAttribute);

    SafeDelete(CharacterMachine);

    DeleteWaterTerrain();

    {
        gMapManager.DeleteObjects();

        // Object.
        for (int i = MODEL_LOGO; i < MAX_MODELS; i++)
        {
            Models[i].Release();
        }

        // Bitmap
        Bitmaps.UnloadAllImages();
    }

    SafeDeleteArray(CharacterMemoryDump);
    SafeDeleteArray(ItemAttRibuteMemoryDump);
    SafeDeleteArray(RendomMemoryDump);
    SafeDeleteArray(ModelsDump);

#ifdef DYNAMIC_FRUSTRUM
    DeleteAllFrustrum();
#endif //DYNAMIC_FRUSTRUM

    SafeDelete(g_pMercenaryInputBox);
    SafeDelete(g_pSingleTextInputBox);
    SafeDelete(g_pSinglePasswdInputBox);

    SafeDelete(g_pUIMapName);	// rozy
    SafeDelete(g_pTimer);
    SafeDelete(g_pUIManager);

    SafeDelete(pMultiLanguage);
    g_BuffSystem.reset();
    g_MapProcess.reset();
    g_petProcess.reset();

    g_ErrorReport.Write(L"Destroy");

    HWND shWnd = FindWindow(nullptr, L"MuPlayer");
    if (shWnd)
        SendMessage(shWnd, WM_DESTROY, 0, 0);
}
void DestroySound()
{
    for (int i = 0; i < MAX_BUFFER; i++)
        ReleaseBuffer(i);

    FreeDirectSound();
    AudioPlayer::Shutdown();
}

int g_iInactiveTime = 0;
int g_iNoMouseTime = 0;
int g_iInactiveWarning = 0;
bool g_bWndActive = false;
bool HangulDelete = false;
int Hangul = 0;
bool g_bEnterPressed = false;

static double g_TargetFpsBeforeInactive = -1.0;
static bool g_HasInactiveFpsOverride = false;

int g_iMousePopPosition_x = 0;
int g_iMousePopPosition_y = 0;

extern int TimeRemain;
extern bool EnableFastInput;

// The legacy Win32 message handler. SDL owns the event loop on Linux and only
// bridges to this via the Windows-only message hook, so guard it off there.
#ifdef _WIN32
LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    // F10 zoom-lock toggle. Handled before the ImGui forwarder so editor-open
    // sessions still get the toggle (ImGui captures keyboard messages while a
    // window has focus). Bit 30 of lParam = previous key state — skip
    // auto-repeat ticks so a held key only toggles once.
    constexpr LPARAM PREVIOUS_KEY_STATE_MASK = 1 << 30;
    if (msg == WM_SYSKEYDOWN && wParam == VK_F10 && (lParam & PREVIOUS_KEY_STATE_MASK) == 0)
    {
        CameraManager::Instance().ToggleZoomLock();
        return 0;
    }

    // ImGui (editor) now consumes input from the SDL event loop via
    // ImGui_ImplSDL3_ProcessEvent, not from Win32 messages (issue #442).

    switch (msg)
    {
    case WM_SYSKEYDOWN:
    {
        // F10 is handled above (intercepted before ImGui). Other system keys
        // are silenced here — returning 0 prevents the OS menu activation.
        return 0;
    }
    break;
    // WM_ACTIVATE is handled via SDL window focus events (issue #442).
    case WM_NPROTECT_EXIT_TWO:
        SocketClient->ToGameServer()->SendLogOutByCheatDetection(0);
        // Inform the user, then close. A frame-ticked timer cannot fire while
        // this modal dialog blocks the main loop, so close right after the
        // dialog is dismissed instead of via a timer.
        MessageBox(nullptr, I18N::Game::Error9AHackingToolHasBeen, L"Error", MB_OK);
        PostMessage(g_hWnd, WM_CLOSE, 0, 0);
        break;
    case WM_ERASEBKGND:
        return TRUE;
        break;
    // WM_SIZE is handled via SDL window resize events (issue #442).
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hDC = BeginPaint(hwnd, &ps);
        EndPaint(hwnd, &ps);
    }
    return 0;
    break;
    case WM_DESTROY:
    {
        // Just request shutdown; the main loop exits on Destroy and the teardown
        // (sound, GL, window) runs after it. SDL owns the window now, so the GL
        // context and window must not be torn down from inside a message.
        Destroy = true;
        if (SocketClient != nullptr)
        {
            SocketClient->Close();
            g_bGameServerConnected = false;
        }
    }
    break;
    case WM_SETCURSOR:
        if (Core::Platform::IsNativeModalActive())
        {
            // A same-thread native dialog (e.g. the restart confirmation) is up:
            // keep the real arrow visible even while the pointer crosses the
            // game window behind it, instead of hiding it again.
            ::SetCursor(::LoadCursorW(nullptr, IDC_ARROW));
            return TRUE;
        }
#ifdef _EDITOR
        // When hovering UI (including Open Editor button), let Windows show cursor
        // Otherwise hide Windows cursor for game cursor
        if (g_MuEditorCore.IsHoveringUI())
        {
            // Let Windows cursor show - don't hide it
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }
        else
#endif
        {
            ShowCursor(false);
        }
        break;
        //-----------------------------
    default:
        break;
    }

    // Mouse input (move/buttons/wheel) is handled via SDL events (issue #442).
    switch (msg)
    {
    case WM_IME_NOTIFY:
    {
        if (g_iChatInputType == 1)
        {
            switch (wParam)
            {
            case IMN_SETCONVERSIONMODE:
                if (GetFocus() == g_hWnd)
                {
                    CheckTextInputBoxIME(IME_CONVERSIONMODE);
                }
                break;
            case IMN_SETSENTENCEMODE:
                if (GetFocus() == g_hWnd)
                {
                    CheckTextInputBoxIME(IME_SENTENCEMODE);
                }
                break;
            default:
                break;
            }
        }
    }
    break;
    case WM_CHAR:
    {
        switch (wParam)
        {
        case VK_RETURN:
        {
            SetEnterPressed(true);
        }
        break;
        }
    }
    break;
    }

    return DefWindowProc(hwnd, msg, wParam, lParam);
}
#endif // _WIN32 (WndProc)

wchar_t m_Username[11];
wchar_t m_Password[21];
wchar_t m_Version[11];
wchar_t m_ExeVersion[11];
int  m_SoundOnOff;
int  m_MusicOnOff;
int  m_Resolution;
int m_RememberMe;

wchar_t g_aszMLSelection[MAX_LANGUAGE_NAME_LENGTH] = { '\0' };


BOOL Util_CheckOption(std::wstring lpszCommandLine, wchar_t cOption, std::wstring& lpszString)
{
    if (lpszCommandLine.empty()) {
        return FALSE;
    }

    // Create both lowercase and uppercase variants of the option character
    std::wstring cOptionLower = L"/";
    cOptionLower += static_cast<wchar_t>(towlower(static_cast<wint_t>(cOption)));
    auto foundIndex = lpszCommandLine.find(cOptionLower);
    if (foundIndex == std::wstring::npos)
    {
        std::wstring cOptionUpper = L"/";
        cOptionUpper += static_cast<wchar_t>(towupper(static_cast<wint_t>(cOption)));
        foundIndex = lpszCommandLine.find(cOptionUpper);
    }

    if (foundIndex == std::wstring::npos)
    {
        return FALSE;
    }

    auto endIndex = lpszCommandLine.find(L' ', foundIndex);
    if (endIndex == std::wstring::npos)
    {
        endIndex = lpszCommandLine.length();
    }

    lpszString = lpszCommandLine.substr(foundIndex + 2, endIndex - foundIndex - 2);
    return TRUE;
}

#ifdef _WIN32
#include <tlhelp32.h>
#endif

wchar_t g_lpszCmdURL[50];
BOOL GetConnectServerInfo(wchar_t* szCmdLine, wchar_t* lpszURL, WORD* pwPort)
{
    std::wstring lpszTemp = { 0, };

    if (!Util_CheckOption(szCmdLine, L'u', lpszTemp))
    {
        return FALSE;
    }

    wcscpy(lpszURL, lpszTemp.c_str());
    if (!Util_CheckOption(szCmdLine, L'p', lpszTemp))
    {
        return FALSE;
    }

    *pwPort = static_cast<WORD>(std::stoi(lpszTemp));

    return TRUE;
}

extern int TimeRemain;
BOOL g_bInactiveTimeChecked = FALSE;
void MoveObject(OBJECT* o);

bool ExceptionCallback(_EXCEPTION_POINTERS* pExceptionInfo)
{
    if (g_bUseWindowMode == FALSE && g_bUseFullscreenMode == TRUE)
    {
        ChangeDisplaySettings(nullptr, 0);
    }
    return true;
}

double CPU_AVG = 0.0;
void RecordCpuUsage() 
{
    constexpr int max_recordings = 60;
    double CPU_Recordings[max_recordings] = { 0.0 };
    double currentAvg = 0.0;
    double sum = 0.0;
    int count = 0;
    int numFilled = 0;
    auto lastUpdateTime = std::chrono::steady_clock::now();

    while (!Destroy) 
    {
        double currentUsage = CpuUsage::Instance()->GetUsage();

        currentUsage = std::max<double>(0.0, std::min<double>(100.0, currentUsage));

        // Subtract the old value to maintain the sum
        sum -= CPU_Recordings[count];

        sum += currentUsage;

        CPU_Recordings[count] = currentUsage;

        // Update the count (wrap around when full - FIFO behavior)
        count = (count + 1) % max_recordings;

        if (numFilled < max_recordings)
        {
            numFilled++;
        }

        // Calculate the current average
        currentAvg = sum / numFilled;

        // Update the CPU_AVG every 250 ms
        auto currentTime = std::chrono::steady_clock::now();
        if (std::chrono::duration_cast<std::chrono::milliseconds>(currentTime - lastUpdateTime).count() >= 250)
        {
            CPU_AVG = currentAvg;
            lastUpdateTime = currentTime;
        }

        // Sleep to match a 60Hz frame rate as the basis
        std::this_thread::sleep_for(std::chrono::milliseconds(16));
    }
}

// unlimited as default (same behavior as original)
int g_MaxMessagePerCycle = -1; 

void SetMaxMessagePerCycle(int messages)
{
    constexpr int custom_min = 3;
    g_MaxMessagePerCycle = (messages > 0) ? std::max<int>(messages, custom_min) : messages;
}

#ifdef _WIN32
// Transitional bridge (issue #442): SDL owns the window, but the existing
// WndProc still handles input, IME, the legacy Win32 EDIT-control text boxes,
// the cursor, and shutdown. SDL invokes this for every Win32 message it pumps;
// forward to WndProc and return true so SDL continues its own processing (which
// also dispatches the child EDIT controls). Removed once input/IME are
// SDL-native and the legacy text boxes are replaced (issue #447).
static bool SDLCALL Win32MessageHook(void* /*userdata*/, MSG* msg)
{
    // Let SDL own window close. Forwarding WM_CLOSE to WndProc would reach
    // DefWindowProc, which destroys the window synchronously and out from under
    // SDL. SDL turns the close into SDL_EVENT_QUIT, which the main loop handles.
    if (msg->message == WM_CLOSE)
        return true;

    WndProc(msg->hwnd, msg->message, msg->wParam, msg->lParam);
    return true;
}
#endif

// SDL event translation (issue #442). Mouse and window events are handled from
// the SDL event loop instead of WndProc, feeding the same global input state.
namespace
{
    bool g_virtualLeftButtonDown = false;
    bool g_virtualRightButtonDown = false;
    bool g_virtualLeftButtonOwnsState = false;
    bool g_virtualRightButtonOwnsState = false;
    bool g_controllerInputWasAvailable = false;
    bool g_controllerKeyboardWasCapturing = false;
#if defined(MU_ENABLE_VIRTUAL_GAMEPAD_TESTS)
    bool g_acceptanceGamepadConnected = false;
    bool g_acceptanceInputEnabled = false;
    bool g_acceptanceInputAvailable = false;
    bool g_acceptanceMenuDown = false;
    bool g_acceptanceMenuPressed = false;
    bool g_acceptanceConfirmDown = false;
    bool g_acceptanceConfirmPressed = false;
    std::string g_acceptanceGamepadName;
#endif

    void SyncMousePosition(float winX, float winY)
    {
        MouseX = static_cast<int>(winX / g_fScreenRate_x);
        MouseY = static_cast<int>(winY / g_fScreenRate_y);
        if (MouseX < 0) MouseX = 0;
        if (MouseX > REFERENCE_WIDTH) MouseX = REFERENCE_WIDTH;
        if (MouseY < 0) MouseY = 0;
        if (MouseY > REFERENCE_HEIGHT) MouseY = REFERENCE_HEIGHT;
        Core::Input::GamepadService::Instance().SetPointerPosition(
            static_cast<float>(MouseX), static_cast<float>(MouseY));
        Core::Input::FocusNavigator::Instance().SelectNearest(
            static_cast<float>(MouseX), static_cast<float>(MouseY));
    }

    const Core::Input::InputActionState& Action(
        const Core::Input::GamepadFrameState& frame,
        Core::Input::InputAction action)
    {
        return frame.actions[static_cast<std::size_t>(action)];
    }

    class FocusNavigationRepeater
    {
    public:
        std::optional<Core::Input::FocusDirection> Update(
            const Core::Input::GamepadFrameState& frame,
            double nowMs,
            bool enabled)
        {
            const int direction = enabled ? ResolveDirection(frame) : -1;
            if (direction < 0)
            {
                m_heldDirection = -1;
                m_nextRepeatMs = 0.0;
                return std::nullopt;
            }

            if (direction != m_heldDirection)
            {
                m_heldDirection = direction;
                m_nextRepeatMs = nowMs + InitialRepeatDelayMs;
                return static_cast<Core::Input::FocusDirection>(direction);
            }

            if (nowMs < m_nextRepeatMs)
                return std::nullopt;

            m_nextRepeatMs = nowMs + RepeatIntervalMs;
            return static_cast<Core::Input::FocusDirection>(direction);
        }

        static bool IsHeld(const Core::Input::GamepadFrameState& frame)
        {
            return ResolveDirection(frame) >= 0;
        }

    private:
        static int ResolveDirection(const Core::Input::GamepadFrameState& frame)
        {
            constexpr float Threshold = 0.55f;
            float horizontal = frame.moveX;
            float vertical = frame.moveY;

            if (Action(frame, Core::Input::InputAction::QuickItem1).down)
                horizontal = -1.0f;
            else if (Action(frame, Core::Input::InputAction::QuickItem3).down)
                horizontal = 1.0f;
            if (Action(frame, Core::Input::InputAction::QuickItem2).down)
                vertical = -1.0f;
            else if (Action(frame, Core::Input::InputAction::QuickItem4).down)
                vertical = 1.0f;

            if (std::abs(horizontal) < Threshold && std::abs(vertical) < Threshold)
                return -1;
            if (std::abs(horizontal) > std::abs(vertical))
            {
                return static_cast<int>(horizontal < 0.0f
                    ? Core::Input::FocusDirection::Left
                    : Core::Input::FocusDirection::Right);
            }
            return static_cast<int>(vertical < 0.0f
                ? Core::Input::FocusDirection::Up
                : Core::Input::FocusDirection::Down);
        }

        static constexpr double InitialRepeatDelayMs = 300.0;
        static constexpr double RepeatIntervalMs = 110.0;
        int m_heldDirection = -1;
        double m_nextRepeatMs = 0.0;
    };

    Core::Input::InputContext GetGamepadInputContext()
    {
        if (CUITextInputBox::GetFocusedPortable())
            return Core::Input::InputContext::TextEntry;
#ifdef _EDITOR
        if (g_MuEditorCore.IsEnabled())
            return Core::Input::InputContext::Editor;
#endif
        switch (SceneFlag)
        {
        case LOG_IN_SCENE: return Core::Input::InputContext::Login;
        case CHARACTER_SCENE: return Core::Input::InputContext::CharacterSelect;
        case MAIN_SCENE:
        {
            bool legacyInterfaceOpen = false;
            if (g_pUIManager != nullptr)
            {
                for (DWORD key = INTERFACE_FRIEND; key < INTERFACE_MAX_COUNT; ++key)
                {
                    if (g_pUIManager->IsOpen(key))
                    {
                        legacyInterfaceOpen = true;
                        break;
                    }
                }
            }

            return (g_pNewUISystem->CheckMouseUse()
                    || g_pNewUISystem->HasVisibleGamepadInterface()
                    || legacyInterfaceOpen)
                ? Core::Input::InputContext::UserInterface
                : Core::Input::InputContext::World;
        }
        default:
            return Core::Input::InputContext::UserInterface;
        }
    }

    void UpdateVirtualMouseButton(
        bool desiredDown,
        bool& virtualWasDown,
        bool& virtualOwnsState,
        bool physicalDown,
        bool& buttonDown,
        bool& buttonPush,
        bool& buttonPop)
    {
        if (!virtualWasDown && virtualOwnsState
            && !buttonDown && !buttonPush && !buttonPop)
        {
            virtualOwnsState = false;
        }

        if (desiredDown && !virtualWasDown)
        {
            virtualOwnsState = true;
            buttonPop = false;
            buttonPush = true;
            buttonDown = true;
        }
        else if (!desiredDown && virtualWasDown)
        {
            if (!physicalDown)
            {
                virtualOwnsState = true;
                buttonPush = false;
                buttonPop = true;
                buttonDown = false;
            }
            else
            {
                virtualOwnsState = false;
            }
        }
        else if (desiredDown)
        {
            virtualOwnsState = true;
            buttonDown = true;
        }

        virtualWasDown = desiredDown;
    }

    void CancelVirtualMouseButton(
        bool& virtualWasDown,
        bool& virtualOwnsState,
        bool physicalDown,
        bool& buttonDown,
        bool& buttonPush,
        bool& buttonPop)
    {
        if (!virtualWasDown && !virtualOwnsState)
            return;

        g_MessageBox->CancelPointerInput();
        virtualWasDown = false;
        virtualOwnsState = false;
        buttonPush = false;
        buttonPop = false;
        if (!physicalDown)
            buttonDown = false;
    }

    void TakePhysicalPointerOwnership()
    {
        const Uint32 physicalButtons = SDL_GetMouseState(nullptr, nullptr);
        if (g_virtualLeftButtonOwnsState || g_virtualRightButtonOwnsState)
        {
            g_pNewKeyInput->SetKeyState(VK_LBUTTON, SEASON3B::CNewKeyInput::KEY_NONE);
            g_pNewKeyInput->SetKeyState(VK_RBUTTON, SEASON3B::CNewKeyInput::KEY_NONE);
            CancelLegacyButtonPress();
            CancelVirtualMouseButton(
                g_virtualLeftButtonDown,
                g_virtualLeftButtonOwnsState,
                (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_LEFT)) != 0,
                MouseLButton,
                MouseLButtonPush,
                MouseLButtonPop);
            CancelVirtualMouseButton(
                g_virtualRightButtonDown,
                g_virtualRightButtonOwnsState,
                (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_RIGHT)) != 0,
                MouseRButton,
                MouseRButtonPush,
                MouseRButtonPop);
        }
        Core::Input::GamepadService::Instance().OnPhysicalPointerInput(
            static_cast<float>(MouseX), static_cast<float>(MouseY));
    }

    void HandleMouseMotion(float winX, float winY)
    {
        SyncMousePosition(winX, winY);
        TakePhysicalPointerOwnership();
    }

    void UpdateGamepadInput()
    {
        auto& gamepad = Core::Input::GamepadService::Instance();
        const double nowMs = static_cast<double>(SDL_GetTicks());
        const auto context = GetGamepadInputContext();
        bool acceptGamepadInput = g_bWndActive;
#if defined(MU_ENABLE_VIRTUAL_GAMEPAD_TESTS)
        // A hidden acceptance runner cannot give the native game window OS
        // focus. Keep the normal background-input lock unless this explicit
        // Debug-only override is requested by the test harness.
        static const bool forceAcceptanceFocus = [] {
            const char* value = std::getenv("MU_VIRTUAL_GAMEPAD_FORCE_FOCUS");
            return value != nullptr && std::strcmp(value, "1") == 0;
        }();
        if (forceAcceptanceFocus && !g_bWndActive)
        {
            // The legacy key scanner independently rejects input while this
            // flag is false. Mirror foreground state for this explicit
            // Debug-only runner so virtual button edges reach CButton too.
            g_bWndActive = true;
            gamepad.OnFocusChanged(true);
            acceptGamepadInput = true;
        }
#endif
        const auto& frame = gamepad.Update(
            context,
            nowMs,
            static_cast<float>(REFERENCE_WIDTH + 1),
            static_cast<float>(REFERENCE_HEIGHT + 1),
            acceptGamepadInput);
        const bool inputOwnershipChanged = gamepad.ConsumeInputOwnershipChanged();

        Core::Input::ClearVirtualKeys();
        const bool inputAvailable = gamepad.IsInputEnabled()
            && gamepad.IsConnected()
            && acceptGamepadInput;
#if defined(MU_ENABLE_VIRTUAL_GAMEPAD_TESTS)
        g_acceptanceGamepadConnected = gamepad.IsConnected();
        g_acceptanceInputEnabled = gamepad.IsInputEnabled();
        g_acceptanceInputAvailable = inputAvailable;
        g_acceptanceMenuDown = Action(frame, Core::Input::InputAction::Menu).down;
        g_acceptanceMenuPressed = Action(frame, Core::Input::InputAction::Menu).pressed;
        g_acceptanceConfirmDown = Action(frame, Core::Input::InputAction::Confirm).down;
        g_acceptanceConfirmPressed = Action(frame, Core::Input::InputAction::Confirm).pressed;
        g_acceptanceGamepadName = gamepad.GetDeviceName();
#endif
        static FocusNavigationRepeater focusRepeater;
        const Uint32 physicalButtons = SDL_GetMouseState(nullptr, nullptr);
        const bool shortcutCaptured = UI::Controller::ControllerShortcuts::Instance().Update(
            frame, context, nowMs, inputAvailable);
        const bool keyboardCaptured = shortcutCaptured || UI::Controller::ControllerKeyboard::Instance().Update(
            frame,
            CUITextInputBox::GetFocusedPortable(),
            nowMs,
            gamepad,
            inputAvailable);
        if (inputOwnershipChanged)
        {
            g_pNewKeyInput->ResetKeyStates();
            CancelLegacyButtonPress();
            CancelVirtualMouseButton(
                g_virtualLeftButtonDown,
                g_virtualLeftButtonOwnsState,
                (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_LEFT)) != 0,
                MouseLButton,
                MouseLButtonPush,
                MouseLButtonPop);
            CancelVirtualMouseButton(
                g_virtualRightButtonDown,
                g_virtualRightButtonOwnsState,
                (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_RIGHT)) != 0,
                MouseRButton,
                MouseRButtonPush,
                MouseRButtonPop);
        }
        if (!inputAvailable)
        {
            (void)focusRepeater.Update(frame, nowMs, false);
            Core::Input::FocusNavigator::Instance().Clear();
            if (g_controllerInputWasAvailable || g_controllerKeyboardWasCapturing)
            {
                g_pNewKeyInput->ResetKeyStates();
                CancelLegacyButtonPress();
            }
            g_controllerInputWasAvailable = false;
            g_controllerKeyboardWasCapturing = false;
            CancelVirtualMouseButton(
                g_virtualLeftButtonDown,
                g_virtualLeftButtonOwnsState,
                (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_LEFT)) != 0,
                MouseLButton,
                MouseLButtonPush,
                MouseLButtonPop);
            CancelVirtualMouseButton(
                g_virtualRightButtonDown,
                g_virtualRightButtonOwnsState,
                (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_RIGHT)) != 0,
                MouseRButton,
                MouseRButtonPush,
                MouseRButtonPop);
            return;
        }
        if (keyboardCaptured)
        {
            (void)focusRepeater.Update(frame, nowMs, false);
            if (!g_controllerKeyboardWasCapturing)
            {
                g_pNewKeyInput->ResetKeyStates();
                CancelLegacyButtonPress();
            }
            g_controllerInputWasAvailable = true;
            g_controllerKeyboardWasCapturing = true;
            CancelVirtualMouseButton(
                g_virtualLeftButtonDown,
                g_virtualLeftButtonOwnsState,
                (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_LEFT)) != 0,
                MouseLButton,
                MouseLButtonPush,
                MouseLButtonPop);
            CancelVirtualMouseButton(
                g_virtualRightButtonDown,
                g_virtualRightButtonOwnsState,
                (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_RIGHT)) != 0,
                MouseRButton,
                MouseRButtonPush,
                MouseRButtonPop);
            return;
        }

        if (g_controllerKeyboardWasCapturing)
        {
            // The text field may disappear without one of the keyboard's own
            // close commands. Keep the closing control from reaching the UI
            // underneath on this transition frame or any held frame after it.
            gamepad.RequireNeutralInput();
            g_pNewKeyInput->ResetKeyStates();
            CancelLegacyButtonPress();
            CancelVirtualMouseButton(
                g_virtualLeftButtonDown,
                g_virtualLeftButtonOwnsState,
                (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_LEFT)) != 0,
                MouseLButton,
                MouseLButtonPush,
                MouseLButtonPop);
            CancelVirtualMouseButton(
                g_virtualRightButtonDown,
                g_virtualRightButtonOwnsState,
                (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_RIGHT)) != 0,
                MouseRButton,
                MouseRButtonPush,
                MouseRButtonPop);
            g_controllerInputWasAvailable = true;
            g_controllerKeyboardWasCapturing = false;
            return;
        }

        g_controllerInputWasAvailable = true;
        g_controllerKeyboardWasCapturing = false;

        // While the options window is capturing a physical control for
        // remapping, every button/stick belongs to the capture -- pressing
        // Start/B/D-pad must not also close the window or move focus.
        if (gamepad.IsCapturingControl())
        {
            (void)focusRepeater.Update(frame, nowMs, false);
            Core::Input::ClearVirtualKeys();
            CancelVirtualMouseButton(
                g_virtualLeftButtonDown,
                g_virtualLeftButtonOwnsState,
                (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_LEFT)) != 0,
                MouseLButton,
                MouseLButtonPush,
                MouseLButtonPop);
            CancelVirtualMouseButton(
                g_virtualRightButtonDown,
                g_virtualRightButtonOwnsState,
                (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_RIGHT)) != 0,
                MouseRButton,
                MouseRButtonPush,
                MouseRButtonPop);
            return;
        }

        const bool uiContext = context != Core::Input::InputContext::World;
        const auto& move = Action(frame, Core::Input::InputAction::Move);
        const bool usingSkill = Action(frame, Core::Input::InputAction::UseSkill).down;
        const bool stickWalking = !uiContext && move.down && !usingSkill;

        if (stickWalking)
        {
            constexpr float MovementRadiusX = 145.0f;
            constexpr float MovementRadiusY = 105.0f;
            float moveX = frame.moveX;
            float moveY = frame.moveY;
            const float magnitude = std::sqrt(moveX * moveX + moveY * moveY);
            if (magnitude > 1.0f)
            {
                moveX /= magnitude;
                moveY /= magnitude;
            }
            MouseX = static_cast<int>(GetScreenWidth() * 0.5f + moveX * MovementRadiusX);
            MouseY = static_cast<int>(180.0f + moveY * MovementRadiusY);
        }
        else
        {
            MouseX = static_cast<int>(frame.pointer.x);
            MouseY = static_cast<int>(frame.pointer.y);
        }

        const bool targetCycleRequested = !uiContext
            && (Action(frame, Core::Input::InputAction::NextTarget).pressed
                || Action(frame, Core::Input::InputAction::LockTarget).pressed);
        if (targetCycleRequested)
        {
            int targetX = MouseX;
            int targetY = MouseY;
            if (Input::Selection::CycleGamepadTarget(targetX, targetY))
            {
                MouseX = targetX;
                MouseY = targetY;
                gamepad.SetPointerPosition(static_cast<float>(targetX), static_cast<float>(targetY));
                gamepad.PublishHaptic(Core::Haptics::HapticEvent::FocusMoved, nowMs);
            }
        }

        if (!uiContext
            && Action(frame, Core::Input::InputAction::LockTarget).down
            && (usingSkill || Action(frame, Core::Input::InputAction::PrimaryAttack).down))
        {
            int targetX = MouseX;
            int targetY = MouseY;
            if (Input::Selection::ProjectGamepadTarget(targetX, targetY))
            {
                MouseX = targetX;
                MouseY = targetY;
                gamepad.SetPointerPosition(static_cast<float>(targetX), static_cast<float>(targetY));
            }
        }

        auto& focusNavigator = Core::Input::FocusNavigator::Instance();
        const bool focusNavigationHeld = uiContext
            && FocusNavigationRepeater::IsHeld(frame);
        const auto focusDirection = focusRepeater.Update(frame, nowMs, uiContext);
        const bool focusMoved = focusDirection.has_value()
            && focusNavigator.Move(*focusDirection);
        if (focusMoved || focusNavigationHeld)
        {
            if (const auto focused = focusNavigator.Current())
            {
                MouseX = static_cast<int>(std::lround(focused->centerX));
                MouseY = static_cast<int>(std::lround(focused->centerY));
                gamepad.SetPointerPosition(
                    static_cast<float>(MouseX),
                    static_cast<float>(MouseY));
                if (focusMoved)
                    gamepad.PublishHaptic(Core::Haptics::HapticEvent::FocusMoved, nowMs);
            }
        }

        CInput::Instance().OverrideCursorPositionForNextUpdate(
            static_cast<long>(std::lround(MouseX * g_fScreenRate_x)),
            static_cast<long>(std::lround(MouseY * g_fScreenRate_y)));

        const bool primaryAttack = Action(frame, Core::Input::InputAction::PrimaryAttack).down;
        const bool desiredLeft = frame.pointer.leftDown || (!uiContext && (primaryAttack || stickWalking));
        const bool desiredRight = uiContext
            ? Action(frame, Core::Input::InputAction::SecondaryAction).down
            : frame.pointer.rightDown;
        const bool virtualLeftWasDown = g_virtualLeftButtonDown;
        UpdateVirtualMouseButton(
            desiredLeft,
            g_virtualLeftButtonDown,
            g_virtualLeftButtonOwnsState,
            (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_LEFT)) != 0,
            MouseLButton,
            MouseLButtonPush,
            MouseLButtonPop);
        if (virtualLeftWasDown && !desiredLeft && MouseLButtonPop)
        {
            g_iMousePopPosition_x = MouseX;
            g_iMousePopPosition_y = MouseY;
        }
        UpdateVirtualMouseButton(
            desiredRight,
            g_virtualRightButtonDown,
            g_virtualRightButtonOwnsState,
            (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_RIGHT)) != 0,
            MouseRButton,
            MouseRButtonPush,
            MouseRButtonPop);

        Core::Input::SetVirtualKeyDown(VK_LBUTTON, desiredLeft);
        Core::Input::SetVirtualKeyDown(VK_RBUTTON, desiredRight);
        Core::Input::SetVirtualKeyDown(VK_ESCAPE,
            Action(frame, Core::Input::InputAction::Cancel).down
            || Action(frame, Core::Input::InputAction::Menu).down);
        const bool confirmDisplayChange = g_pOption != nullptr
            && g_pOption->IsDisplayChangePending()
            && Action(frame, Core::Input::InputAction::Confirm).down;
        Core::Input::SetVirtualKeyDown(VK_RETURN, confirmDisplayChange);
        if (confirmDisplayChange)
            SetEnterPressed(true);
        Core::Input::SetVirtualKeyDown(VK_TAB, Action(frame, Core::Input::InputAction::Map).down);
        Core::Input::SetVirtualKeyDown(VK_HOME, Action(frame, Core::Input::InputAction::AutoMove).down);

        if (uiContext && Action(frame, Core::Input::InputAction::Details).pressed)
            MouseLButtonDBClick = true;

        if (uiContext)
        {
            Core::Input::SetVirtualKeyDown(VK_CONTROL,
                Core::Input::IsKeyDown(VK_CONTROL) || Action(frame, Core::Input::InputAction::LockTarget).down);
            Core::Input::SetVirtualKeyDown(VK_SHIFT,
                Core::Input::IsKeyDown(VK_SHIFT) || Action(frame, Core::Input::InputAction::UseSkill).down);
            constexpr float NavigationThreshold = 0.55f;
            const bool left = Action(frame, Core::Input::InputAction::QuickItem1).down || frame.moveX < -NavigationThreshold;
            const bool up = Action(frame, Core::Input::InputAction::QuickItem2).down || frame.moveY < -NavigationThreshold;
            const bool right = Action(frame, Core::Input::InputAction::QuickItem3).down || frame.moveX > NavigationThreshold;
            const bool down = Action(frame, Core::Input::InputAction::QuickItem4).down || frame.moveY > NavigationThreshold;
            Core::Input::SetVirtualKeyDown(VK_LEFT, left);
            Core::Input::SetVirtualKeyDown(VK_UP, up);
            Core::Input::SetVirtualKeyDown(VK_RIGHT, right);
            Core::Input::SetVirtualKeyDown(VK_DOWN, down);
            Core::Input::SetVirtualKeyDown(VK_PRIOR, Action(frame, Core::Input::InputAction::PreviousPage).down);
            Core::Input::SetVirtualKeyDown(VK_NEXT, Action(frame, Core::Input::InputAction::NextPage).down);
        }
        else
        {
            Core::Input::SetVirtualKeyDown('Q', Action(frame, Core::Input::InputAction::QuickItem1).down);
            Core::Input::SetVirtualKeyDown('W', Action(frame, Core::Input::InputAction::QuickItem2).down);
            Core::Input::SetVirtualKeyDown('E', Action(frame, Core::Input::InputAction::QuickItem3).down);
            Core::Input::SetVirtualKeyDown('R', Action(frame, Core::Input::InputAction::QuickItem4).down);

            if (Action(frame, Core::Input::InputAction::PreviousPage).pressed && g_pSkillList != nullptr)
            {
                if (g_pSkillList->SelectAdjacentHotKeySkill(-1))
                    gamepad.PublishHaptic(Core::Haptics::HapticEvent::SettingChanged, nowMs);
            }
            if (Action(frame, Core::Input::InputAction::NextPage).pressed && g_pSkillList != nullptr)
            {
                if (g_pSkillList->SelectAdjacentHotKeySkill(1))
                    gamepad.PublishHaptic(Core::Haptics::HapticEvent::SettingChanged, nowMs);
            }
        }

        if (uiContext && Action(frame, Core::Input::InputAction::Confirm).pressed)
            gamepad.PublishHaptic(Core::Haptics::HapticEvent::Confirmed, nowMs);
        if (uiContext && Action(frame, Core::Input::InputAction::Cancel).pressed)
            gamepad.PublishHaptic(Core::Haptics::HapticEvent::Cancelled, nowMs);
    }

#if defined(MU_ENABLE_VIRTUAL_GAMEPAD_TESTS)
    static void WriteUIDebugDump(const char* path)
    {
        const char* enabled = std::getenv("MU_UI_DEBUG_DUMP");
        if (enabled == nullptr || enabled[0] == '\0')
            return;

        const auto wI = [path](const char* key, long value)
        {
            WritePrivateProfileStringA("UIDebug", key, std::to_string(value).c_str(), path);
        };
        const auto wF = [path](const char* key, float value)
        {
            char buf[32];
            (void)snprintf(buf, sizeof(buf), "%.3f", value);
            WritePrivateProfileStringA("UIDebug", key, buf, path);
        };

        wI("Tick", static_cast<long>(SDL_GetTicks()));
        wI("WindowWidth", static_cast<long>(WindowWidth));
        wI("WindowHeight", static_cast<long>(WindowHeight));
        wI("OpenglWindowWidth", static_cast<long>(OpenglWindowWidth));
        wI("OpenglWindowHeight", static_cast<long>(OpenglWindowHeight));
        wF("RateX", g_fScreenRate_x);
        wF("RateY", g_fScreenRate_y);

        int logicalW = 0, logicalH = 0, drawW = 0, drawH = 0;
        if (g_sdlWindow != nullptr)
        {
            SDL_GetWindowSize(g_sdlWindow, &logicalW, &logicalH);
            SDL_GetWindowSizeInPixels(g_sdlWindow, &drawW, &drawH);
        }
        wI("SdlLogicalW", logicalW);
        wI("SdlLogicalH", logicalH);
        wI("SdlDrawableW", drawW);
        wI("SdlDrawableH", drawH);

        auto& legacyInput = CInput::Instance();
        wI("InputScreenW", legacyInput.GetScreenWidth());
        wI("InputScreenH", legacyInput.GetScreenHeight());
        wI("InputCursorX", legacyInput.GetCursorX());
        wI("InputCursorY", legacyInput.GetCursorY());

        const float* proj = GlobalUBO::Instance().GetProj();
        wF("Proj00", proj[0]);
        wF("Proj05", proj[5]);
        wF("Proj15", proj[15]);

        wI("Scene", static_cast<long>(SceneFlag));

        auto dumpSysMenuSnapshot = [&](const char* prefix,
                                      const CSysMenuWin::SpriteSnapshot& snapshot)
        {
            wI((std::string(prefix) + "X").c_str(), snapshot.x);
            wI((std::string(prefix) + "Y").c_str(), snapshot.y);
            wI((std::string(prefix) + "W").c_str(), snapshot.width);
            wI((std::string(prefix) + "H").c_str(), snapshot.height);
            wF((std::string(prefix) + "SX").c_str(), snapshot.scaleX);
            wF((std::string(prefix) + "SY").c_str(), snapshot.scaleY);
            wF((std::string(prefix) + "SH").c_str(), snapshot.scrHeight);
        };
        auto dumpCharSelSnapshot = [&](const char* prefix,
                                       const CCharSelMainWin::SpriteSnapshot& snapshot)
        {
            wI((std::string(prefix) + "X").c_str(), snapshot.x);
            wI((std::string(prefix) + "Y").c_str(), snapshot.y);
            wI((std::string(prefix) + "W").c_str(), snapshot.width);
            wI((std::string(prefix) + "H").c_str(), snapshot.height);
            wF((std::string(prefix) + "SX").c_str(), snapshot.scaleX);
            wF((std::string(prefix) + "SY").c_str(), snapshot.scaleY);
            wF((std::string(prefix) + "SH").c_str(), snapshot.scrHeight);
        };

        CUIMng& uiManager = CUIMng::Instance();

        wI("SysMenuVisible", uiManager.m_SysMenuWin.IsShow() ? 1 : 0);
        dumpSysMenuSnapshot("SmBack", uiManager.m_SysMenuWin.DebugBackSnapshot());
        static constexpr const char* kSysMenuPrefixes[4] =
        { "SmB0", "SmB1", "SmB2", "SmB3" };
        for (int i = 0; i < 4; ++i)
            dumpSysMenuSnapshot(kSysMenuPrefixes[i],
                uiManager.m_SysMenuWin.DebugButtonSnapshot(i));

        wI("CharBarVisible", uiManager.m_CharSelMainWin.IsShow() ? 1 : 0);
        wI("CharBarX", uiManager.m_CharSelMainWin.GetXPos());
        wI("CharBarY", uiManager.m_CharSelMainWin.GetYPos());
        wI("CharBarW", uiManager.m_CharSelMainWin.GetWidth());
        wI("CharBarH", uiManager.m_CharSelMainWin.GetHeight());
        dumpCharSelSnapshot("CsInfo",
            uiManager.m_CharSelMainWin.DebugBackSpriteSnapshot(1));
        static constexpr const char* kCharButtonPrefixes[4] =
        { "CsB0", "CsB1", "CsB2", "CsB3" };
        for (int i = 0; i < 4; ++i)
            dumpCharSelSnapshot(kCharButtonPrefixes[i],
                uiManager.m_CharSelMainWin.DebugButtonSnapshot(i));
    }

    void WriteVirtualGamepadAcceptanceState()
    {
        const char* path = std::getenv("MU_VIRTUAL_GAMEPAD_ACCEPTANCE_PATH");
        if (path == nullptr || path[0] == '\0')
            return;

        const Uint64 nowMs = SDL_GetTicks();
        static Uint64 nextWriteMs = 0;
        if (nowMs < nextWriteMs)
            return;
        nextWriteMs = nowMs + 100;

        const auto writeInt = [path](const char* key, long value)
        {
            const std::string text = std::to_string(value);
            WritePrivateProfileStringA("Acceptance", key, text.c_str(), path);
        };
        const auto writeText = [path](const char* key, const std::string& value)
        {
            WritePrivateProfileStringA("Acceptance", key, value.c_str(), path);
        };

        auto& focusNavigator = Core::Input::FocusNavigator::Instance();
        const auto focused = focusNavigator.Current();
        writeInt("FocusCount", static_cast<long>(focusNavigator.Size()));
        writeInt("FocusX", focused
            ? static_cast<long>(std::lround(focused->centerX))
            : -1);
        writeInt("FocusY", focused
            ? static_cast<long>(std::lround(focused->centerY))
            : -1);
        writeInt("SysMenuVisible", CUIMng::Instance().m_SysMenuWin.IsShow() ? 1 : 0);
        writeInt("OptionVisible",
            g_pNewUISystem != nullptr
                && g_pNewUISystem->IsVisible(SEASON3B::INTERFACE_OPTION)
            ? 1
            : 0);

        auto& input = CInput::Instance();
        writeInt("LegacyCursorX", input.GetCursorX());
        writeInt("LegacyCursorY", input.GetCursorY());
        writeInt("LegacyLeftDown", input.IsLBtnDn() ? 1 : 0);
        writeInt("LegacyLeftHeld", input.IsLBtnHeldDn() ? 1 : 0);
        writeInt("LegacyLeftUp", input.IsLBtnUp() ? 1 : 0);
        writeInt("RawLeftPress", SEASON3B::IsPress(VK_LBUTTON) ? 1 : 0);
        writeInt("RawLeftRepeat", SEASON3B::IsRepeat(VK_LBUTTON) ? 1 : 0);
        writeInt("RawLeftRelease", SEASON3B::IsRelease(VK_LBUTTON) ? 1 : 0);
        writeInt("RawLeftNone", SEASON3B::IsNone(VK_LBUTTON) ? 1 : 0);
        writeInt("RawEscapePress", SEASON3B::IsPress(VK_ESCAPE) ? 1 : 0);
        writeInt("RawEscapeRepeat", SEASON3B::IsRepeat(VK_ESCAPE) ? 1 : 0);
        writeInt("RawEscapeRelease", SEASON3B::IsRelease(VK_ESCAPE) ? 1 : 0);
        writeInt("RawEscapeNone", SEASON3B::IsNone(VK_ESCAPE) ? 1 : 0);
        writeInt("GamepadConnected", g_acceptanceGamepadConnected ? 1 : 0);
        writeInt("GamepadEnabled", g_acceptanceInputEnabled ? 1 : 0);
        writeInt("GamepadInputAvailable", g_acceptanceInputAvailable ? 1 : 0);
        writeInt("MenuDown", g_acceptanceMenuDown ? 1 : 0);
        writeInt("MenuPressed", g_acceptanceMenuPressed ? 1 : 0);
        writeInt("ConfirmDown", g_acceptanceConfirmDown ? 1 : 0);
        writeInt("ConfirmPressed", g_acceptanceConfirmPressed ? 1 : 0);
        writeText("GamepadName", g_acceptanceGamepadName);

        const char* inputPath = std::getenv("MU_VIRTUAL_GAMEPAD_STATE");
        writeInt("IniStart", inputPath != nullptr
            ? GetPrivateProfileIntA("Gamepad", "Start", -1, inputPath)
            : -1);
        writeInt("IniSouth", inputPath != nullptr
            ? GetPrivateProfileIntA("Gamepad", "South", -1, inputPath)
            : -1);
        SDL_Gamepad* activeGamepad =
            Core::Input::GamepadService::Instance().GetActiveGamepad();
        writeInt("SdlStart", activeGamepad != nullptr
            && SDL_GetGamepadButton(activeGamepad, SDL_GAMEPAD_BUTTON_START)
            ? 1
            : 0);
        writeInt("SdlSouth", activeGamepad != nullptr
            && SDL_GetGamepadButton(activeGamepad, SDL_GAMEPAD_BUTTON_SOUTH)
            ? 1
            : 0);

        WriteUIDebugDump(path);
    }
#endif

    void HandleMouseButton(const SDL_Event& e)
    {
        const bool down = (e.type == SDL_EVENT_MOUSE_BUTTON_DOWN);
        SyncMousePosition(e.button.x, e.button.y);
        g_iNoMouseTime = 0;
        if (down)
        {
            const Uint32 physicalButtons = SDL_GetMouseState(nullptr, nullptr);
            g_pNewKeyInput->SetKeyState(VK_LBUTTON, SEASON3B::CNewKeyInput::KEY_NONE);
            g_pNewKeyInput->SetKeyState(VK_RBUTTON, SEASON3B::CNewKeyInput::KEY_NONE);
            CancelLegacyButtonPress();
            CancelVirtualMouseButton(
                g_virtualLeftButtonDown,
                g_virtualLeftButtonOwnsState,
                e.button.button == SDL_BUTTON_LEFT
                    ? false
                    : (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_LEFT)) != 0,
                MouseLButton,
                MouseLButtonPush,
                MouseLButtonPop);
            CancelVirtualMouseButton(
                g_virtualRightButtonDown,
                g_virtualRightButtonOwnsState,
                e.button.button == SDL_BUTTON_RIGHT
                    ? false
                    : (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_RIGHT)) != 0,
                MouseRButton,
                MouseRButtonPush,
                MouseRButtonPop);
            Core::Input::GamepadService::Instance().OnPhysicalPointerInput(
                static_cast<float>(MouseX), static_cast<float>(MouseY));
        }
        switch (e.button.button)
        {
        case SDL_BUTTON_LEFT:
            if (down)
            {
                MouseLButtonPop = false;
                if (!MouseLButton) MouseLButtonPush = true;
                MouseLButton = true;
                if (e.button.clicks >= 2) MouseLButtonDBClick = true;
                SetCapture(g_hWnd);
            }
            else
            {
                MouseLButtonPush = false;
                if (MouseLButton) MouseLButtonPop = true;
                MouseLButton = false;
                g_iMousePopPosition_x = MouseX;
                g_iMousePopPosition_y = MouseY;
                ReleaseCapture();
            }
            break;
        case SDL_BUTTON_RIGHT:
            if (down)
            {
                MouseRButtonPop = false;
                if (!MouseRButton) MouseRButtonPush = true;
                MouseRButton = true;
                SetCapture(g_hWnd);
            }
            else
            {
                MouseRButtonPush = false;
                if (MouseRButton) MouseRButtonPop = true;
                MouseRButton = false;
                ReleaseCapture();
            }
            break;
        case SDL_BUTTON_MIDDLE:
            if (down)
            {
                MouseMButtonPop = false;
                if (!MouseMButton) MouseMButtonPush = true;
                MouseMButton = true;
                SetCapture(g_hWnd);
            }
            else
            {
                MouseMButtonPush = false;
                if (MouseMButton) MouseMButtonPop = true;
                MouseMButton = false;
                ReleaseCapture();
            }
            break;
        }
        if (e.button.button == SDL_BUTTON_LEFT || e.button.button == SDL_BUTTON_RIGHT)
            g_MessageBox->RecordPointerButton(e.button.button == SDL_BUTTON_LEFT, down, MouseX, MouseY);
    }

#if defined(__ANDROID__) || defined(__OHOS__)
    Core::Input::MobileGestureMapper g_mobileGestureMapper;
    bool g_touchLeftWasDown = false;
    bool g_touchRightWasDown = false;
    bool g_touchLeftOwnsState = false;
    bool g_touchRightOwnsState = false;

    void UpdateMobileMouseButtons()
    {
        CInput::Instance().OverrideCursorPositionForNextUpdate(
            static_cast<long>(std::lround(MouseX * g_fScreenRate_x)),
            static_cast<long>(std::lround(MouseY * g_fScreenRate_y)));
        const Uint32 physicalButtons = SDL_GetMouseState(nullptr, nullptr);
        UpdateVirtualMouseButton(
            Core::Input::IsTouchMouseButtonDown(VK_LBUTTON),
            g_touchLeftWasDown, g_touchLeftOwnsState,
            (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_LEFT)) != 0,
            MouseLButton, MouseLButtonPush, MouseLButtonPop);
        UpdateVirtualMouseButton(
            Core::Input::IsTouchMouseButtonDown(VK_RBUTTON),
            g_touchRightWasDown, g_touchRightOwnsState,
            (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_RIGHT)) != 0,
            MouseRButton, MouseRButtonPush, MouseRButtonPop);
        if (MouseLButtonPop)
        {
            g_iMousePopPosition_x = MouseX;
            g_iMousePopPosition_y = MouseY;
        }
    }

    void CancelMobileMouseButton(bool left)
    {
        g_MessageBox->CancelPointerInput();
        Core::Input::CancelTouchMouseButton(left ? VK_LBUTTON : VK_RBUTTON);
        const Uint32 physicalButtons = SDL_GetMouseState(nullptr, nullptr);
        if (left)
            CancelVirtualMouseButton(g_touchLeftWasDown, g_touchLeftOwnsState,
                (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_LEFT)) != 0,
                MouseLButton, MouseLButtonPush, MouseLButtonPop);
        else
            CancelVirtualMouseButton(g_touchRightWasDown, g_touchRightOwnsState,
                (physicalButtons & SDL_BUTTON_MASK(SDL_BUTTON_RIGHT)) != 0,
                MouseRButton, MouseRButtonPush, MouseRButtonPop);
        CancelLegacyButtonPress();
    }

    void HandleMobileMouseAction(const Core::Input::MobileGestureAction& action)
    {
        using Core::Input::MobileGestureActionType;
        const bool left = action.type == MobileGestureActionType::LeftButtonDown
            || action.type == MobileGestureActionType::LeftButtonUp;
        const bool down = action.type == MobileGestureActionType::LeftButtonDown
            || action.type == MobileGestureActionType::RightButtonDown;
        Core::Input::SetTouchMouseButtonDown(left ? VK_LBUTTON : VK_RBUTTON, down);
        const char* diagnostics = std::getenv("MU_INPUT_DIAGNOSTICS");
        if (diagnostics != nullptr && std::strcmp(diagnostics, "1") == 0)
            SDL_Log("[Touch] down=%d left=%d position=%.3f,%.3f window=%d,%d active=%d",
                down, left, action.x, action.y, WindowWidth, WindowHeight, g_bWndActive);
        SDL_Event mouse{};
        mouse.type = down ? SDL_EVENT_MOUSE_BUTTON_DOWN : SDL_EVENT_MOUSE_BUTTON_UP;
        mouse.button.button = left ? SDL_BUTTON_LEFT : SDL_BUTTON_RIGHT;
        mouse.button.clicks = 1;
        mouse.button.x = action.x * static_cast<float>(WindowWidth);
        mouse.button.y = action.y * static_cast<float>(WindowHeight);
        HandleMouseButton(mouse);
        if (down)
            Core::Input::GamepadService::Instance().PublishHaptic(
                Core::Haptics::HapticEvent::InputAcknowledged,
                static_cast<double>(SDL_GetTicks()));
    }

    void HandleMobileGestureAction(const Core::Input::MobileGestureAction& action)
    {
        using Core::Input::MobileGestureActionType;
        const float windowX = action.x * static_cast<float>(WindowWidth);
        const float windowY = action.y * static_cast<float>(WindowHeight);
        switch (action.type)
        {
        case MobileGestureActionType::PointerMove:
            HandleMouseMotion(windowX, windowY);
            break;
        case MobileGestureActionType::LeftButtonDown:
        case MobileGestureActionType::LeftButtonUp:
        case MobileGestureActionType::RightButtonDown:
        case MobileGestureActionType::RightButtonUp:
            HandleMobileMouseAction(action);
            break;
        case MobileGestureActionType::CancelLeftButton:
            CancelMobileMouseButton(true);
            MouseLButtonPush = false;
            MouseLButtonPop = false;
            MouseLButton = false;
            ReleaseCapture();
            break;
        case MobileGestureActionType::CancelRightButton:
            CancelMobileMouseButton(false);
            MouseRButtonPush = false;
            MouseRButtonPop = false;
            MouseRButton = false;
            ReleaseCapture();
            break;
        case MobileGestureActionType::PreviousSkill:
            if (g_pNewUISystem != nullptr
                && g_pNewUISystem->IsVisible(SEASON3B::INTERFACE_HELP))
            {
                g_pHelp->PreviousPage();
                break;
            }
            if (g_pSkillList != nullptr)
                g_pSkillList->SelectAdjacentHotKeySkill(-1);
            Core::Input::GamepadService::Instance().PublishHaptic(
                Core::Haptics::HapticEvent::FocusMoved, static_cast<double>(SDL_GetTicks()));
            break;
        case MobileGestureActionType::NextSkill:
            if (g_pNewUISystem != nullptr
                && g_pNewUISystem->IsVisible(SEASON3B::INTERFACE_HELP))
            {
                g_pHelp->NextPage();
                break;
            }
            if (g_pSkillList != nullptr)
                g_pSkillList->SelectAdjacentHotKeySkill(1);
            Core::Input::GamepadService::Instance().PublishHaptic(
                Core::Haptics::HapticEvent::FocusMoved, static_cast<double>(SDL_GetTicks()));
            break;
        case MobileGestureActionType::ZoomOut:
            MouseWheel = -1;
            Core::Input::GamepadService::Instance().PublishHaptic(
                Core::Haptics::HapticEvent::SettingChanged, static_cast<double>(SDL_GetTicks()));
            break;
        case MobileGestureActionType::ZoomIn:
            MouseWheel = 1;
            Core::Input::GamepadService::Instance().PublishHaptic(
                Core::Haptics::HapticEvent::SettingChanged, static_cast<double>(SDL_GetTicks()));
            break;
        case MobileGestureActionType::OpenMap:
            if (g_pNewUISystem != nullptr)
                g_pNewUISystem->Toggle(SEASON3B::INTERFACE_MOVEMAP);
            Core::Input::GamepadService::Instance().PublishHaptic(
                Core::Haptics::HapticEvent::Confirmed, static_cast<double>(SDL_GetTicks()));
            break;
        case MobileGestureActionType::OpenSettings:
            if (g_pNewUISystem != nullptr)
                g_pNewUISystem->Toggle(SEASON3B::INTERFACE_OPTION);
            Core::Input::GamepadService::Instance().PublishHaptic(
                Core::Haptics::HapticEvent::Confirmed, static_cast<double>(SDL_GetTicks()));
            break;
        }
    }

    void HandleMobileFinger(const SDL_Event& event)
    {
        Core::Input::TouchPhase phase = Core::Input::TouchPhase::Move;
        switch (event.type)
        {
        case SDL_EVENT_FINGER_DOWN: phase = Core::Input::TouchPhase::Down; break;
        case SDL_EVENT_FINGER_UP: phase = Core::Input::TouchPhase::Up; break;
        case SDL_EVENT_FINGER_CANCELED: phase = Core::Input::TouchPhase::Cancel; break;
        default: break;
        }

        const Core::Input::TouchSample logicalContact{
            static_cast<std::int64_t>(event.tfinger.fingerID), phase,
            event.tfinger.x * REFERENCE_WIDTH, event.tfinger.y * REFERENCE_HEIGHT,
            static_cast<std::uint64_t>(SDL_GetTicks())};
        if (phase == Core::Input::TouchPhase::Down && !g_mobileGestureMapper.HasActiveContacts())
        {
            HandleMouseMotion(event.tfinger.x * WindowWidth, event.tfinger.y * WindowHeight);
            if (UI::Items::Touch::BeginItemContact(logicalContact)) g_mobileGestureMapper.CaptureNextContact();
        }
        if (phase == Core::Input::TouchPhase::Down)
        {
            const bool worldInput = GetGamepadInputContext() == Core::Input::InputContext::World;
            constexpr float WorldGestureBottom = 0.86f;
            g_mobileGestureMapper.SetWorldGesturesEnabled(worldInput
                && event.tfinger.y < WorldGestureBottom && g_MessageBox->IsEmpty());
        }
        const auto actions = g_mobileGestureMapper.Handle({
            static_cast<std::int64_t>(event.tfinger.fingerID),
            phase,
            event.tfinger.x,
            event.tfinger.y,
            static_cast<std::uint64_t>(SDL_GetTicks()),
        });
        for (const auto& action : actions)
            HandleMobileGestureAction(action);
        UI::Items::Touch::HandleItemContact(logicalContact);
        if (!g_mobileGestureMapper.HasActiveContacts()) UI::Items::Touch::FinishItemContactSequence();
    }
#endif

    void HandleWindowResize(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        UI::Items::Touch::CancelItemContact();
        WindowWidth = width;
        WindowHeight = height;
        CInput::Instance().ResizeScreen(width, height);
        g_fScreenRate_x = static_cast<float>(WindowWidth) / static_cast<float>(REFERENCE_WIDTH);
        g_fScreenRate_y = static_cast<float>(WindowHeight) / static_cast<float>(REFERENCE_HEIGHT);
        OpenglWindowWidth = WindowWidth;
        OpenglWindowHeight = WindowHeight;
        RHI::OnResize(WindowWidth, WindowHeight); // no-op on GL
        ReinitializeFonts();
        // Refresh sprites that persist across scenes (world item labels,
        // balloons, in-world effects) before scene owners reposition/recreate.
        CSprite::RefreshAllResolutionScales();
        UpdateResolutionDependentSystems();
        UpdateCursorClip();
    }

    void HandleFocusChange(bool active)
    {
        if (!active) g_MessageBox->CancelPointerInput();
        if (!active)
            Core::Input::ClearKeyboardPresses();
        Core::Input::GamepadService::Instance().OnFocusChanged(active);
        if (!active)
        {
#if defined(__ANDROID__) || defined(__OHOS__)
            UI::Items::Touch::CancelItemContact();
            UI::Items::Touch::FinishItemContactSequence();
            g_mobileGestureMapper.Reset();
            Core::Input::ResetTouchMouseButtons();
            CancelMobileMouseButton(true);
            CancelMobileMouseButton(false);
#endif
            Core::Input::FocusNavigator::Instance().Clear();
            g_pNewKeyInput->ResetKeyStates();
            CancelLegacyButtonPress();
            g_virtualLeftButtonDown = false;
            g_virtualRightButtonDown = false;
            g_virtualLeftButtonOwnsState = false;
            g_virtualRightButtonOwnsState = false;
            g_controllerInputWasAvailable = false;
            g_controllerKeyboardWasCapturing = false;
            g_bWndActive = false;
            // Release the cursor when losing focus so input can route elsewhere.
            ClipCursor(nullptr);

            if (!g_HasInactiveFpsOverride)
            {
                g_TargetFpsBeforeInactive = GetTargetFps();
                ApplyFrameTimingConfiguration(false);
                g_HasInactiveFpsOverride = true;
            }
            MouseLButton = false;
            MouseLButtonPush = false;
            MouseLButtonPop = false;
            MouseRButton = false;
            MouseRButtonPop = false;
            MouseRButtonPush = false;
            MouseLButtonDBClick = false;
            MouseMButton = false;
            MouseMButtonPop = false;
            MouseMButtonPush = false;
            MouseWheel = 0;
            Core::Input::ClearVirtualKeys();
        }
        else
        {
            g_bWndActive = true;
            if (g_HasInactiveFpsOverride)
            {
                ApplyFrameTimingConfiguration(true);
                g_HasInactiveFpsOverride = false;
            }
            UpdateCursorClip();
        }
    }

    // --- Portable text field input routing (issue #447) -------------------
    // Map the SDL keys a single-line text field reacts to onto the Win32 VK
    // codes the field already understands. Returns 0 for keys it ignores.
    int MapScancodeToEditVk(SDL_Scancode sc)
    {
        switch (sc)
        {
        case SDL_SCANCODE_LEFT:      return VK_LEFT;
        case SDL_SCANCODE_RIGHT:     return VK_RIGHT;
        case SDL_SCANCODE_HOME:      return VK_HOME;
        case SDL_SCANCODE_END:       return VK_END;
        case SDL_SCANCODE_BACKSPACE: return VK_BACK;
        case SDL_SCANCODE_DELETE:    return VK_DELETE;
        case SDL_SCANCODE_RETURN:
        case SDL_SCANCODE_KP_ENTER:  return VK_RETURN;
        case SDL_SCANCODE_TAB:       return VK_TAB;
        default:                     return 0;
        }
    }

    // UTF-8 <-> UTF-16 conversions sized to the input, so text of any length
    // (typed, copied or pasted) round-trips without truncation (issue #447).
    std::wstring Utf8ToWide(const char* utf8)
    {
        if (utf8 == nullptr) return std::wstring();
        const int needed = MultiByteToWideChar(CP_UTF8, 0, utf8, -1, nullptr, 0);
        if (needed <= 1) return std::wstring();  // <=1 means empty or error
        std::wstring wide(needed - 1, L'\0');  // needed includes the null terminator
        MultiByteToWideChar(CP_UTF8, 0, utf8, -1, wide.data(), needed);
        return wide;
    }

    std::string WideToUtf8(const std::wstring& wide)
    {
        if (wide.empty()) return std::string();
        const int needed = WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), -1, nullptr, 0, nullptr, nullptr);
        if (needed <= 1) return std::string();  // <=1 means empty or error
        std::string utf8(needed - 1, '\0');
        WideCharToMultiByte(CP_UTF8, 0, wide.c_str(), -1, utf8.data(), needed, nullptr, nullptr);
        return utf8;
    }

    void FeedPortableTextInput(const char* utf8)
    {
        auto* box = CUITextInputBox::GetFocusedPortable();
        if (box == nullptr || utf8 == nullptr) return;

        const std::wstring wide = Utf8ToWide(utf8);
        if (!wide.empty())
            box->OnTextInput(wide.c_str());
    }

    // Handle a key for the focused portable field. Returns true if consumed.
    bool FeedPortableKey(const SDL_KeyboardEvent& key)
    {
        auto* box = CUITextInputBox::GetFocusedPortable();
        if (box == nullptr) return false;

        const bool ctrl = (key.mod & SDL_KMOD_CTRL) != 0;
        const bool shift = (key.mod & SDL_KMOD_SHIFT) != 0;

        // Clipboard lives in SDL on this side of the boundary, keeping the text
        // field itself free of SDL; the field only exposes selection helpers.
        if (ctrl)
        {
            switch (key.scancode)
            {
            case SDL_SCANCODE_A:
                box->SelectAll();
                return true;
            case SDL_SCANCODE_C:
            case SDL_SCANCODE_X:
            {
                const std::wstring selection = box->GetSelectedText();
                if (!selection.empty())
                {
                    const std::string utf8 = WideToUtf8(selection);
                    if (!utf8.empty())
                    {
                        SDL_SetClipboardText(utf8.c_str());
                        if (key.scancode == SDL_SCANCODE_X)
                            box->DeleteSelection();
                    }
                }
                return true;
            }
            case SDL_SCANCODE_V:
            {
                char* clip = SDL_GetClipboardText();
                if (clip != nullptr)
                {
                    const std::wstring wide = Utf8ToWide(clip);
                    if (!wide.empty())
                        box->OnTextInput(wide.c_str());
                    SDL_free(clip);
                }
                return true;
            }
            default:
                break;
            }
        }

        const int vk = MapScancodeToEditVk(key.scancode);
        if (vk == 0) return false;

        box->OnEditKey(vk, ctrl, shift);
        return true;
    }
}

// Resolution change through SDL (issue #462). SDL owns the window on every
// platform, so resize it via SDL rather than the OS. The old Windows path in
// ApplyResolution() drove Win32 SetWindowPos/ChangeDisplaySettings on g_hWnd,
// which fought SDL: it pins the min/max tracking size of a non-resizable
// window, so a raw SetWindowPos was clamped back and the resolution never
// changed unless a windowed/fullscreen toggle reset the style first.
// SDL_SetWindowSize resizes regardless of the resizable flag and drives the
// same HandleWindowResize update synchronously, so callers can Save() config
// right after and see the size the window actually ended up with.
void MuApplyWindowResolution(unsigned int width, unsigned int height, bool windowed)
{
    if (!g_sdlWindow || width == 0 || height == 0) return;
    const int w = static_cast<int>(width);
    const int h = static_cast<int>(height);

    if (windowed)
    {
        SDL_SetWindowFullscreen(g_sdlWindow, false);
        SDL_SetWindowSize(g_sdlWindow, w, h);
        SDL_SetWindowPosition(g_sdlWindow, SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED);
    }
    else
    {
        // Pick the closest real fullscreen mode so the monitor switches
        // resolution; fall back to borderless desktop if none matches.
        SDL_DisplayMode mode;
        const SDL_DisplayID display = SDL_GetDisplayForWindow(g_sdlWindow);
        const auto& timing = GameConfig::GetInstance().GetFrameTimingSettings();
        const float requestedRefresh = timing.mode == Core::Time::FrameRateMode::DisplayMaximum
            ? static_cast<float>(GetMaximumRefreshForResolution(w, h).hertz)
            : 0.0f;
        if (SDL_GetClosestFullscreenDisplayMode(display, w, h, requestedRefresh, false, &mode))
            SDL_SetWindowFullscreenMode(g_sdlWindow, &mode);
        else
            SDL_SetWindowFullscreenMode(g_sdlWindow, nullptr);
        SDL_SetWindowSize(g_sdlWindow, w, h);
        SDL_SetWindowFullscreen(g_sdlWindow, true);
    }

    // The request is not a guarantee: the closest fullscreen mode can differ
    // from what was asked, the borderless fallback is desktop-sized, and mode
    // switches are asynchronous on some window managers. Settle the request,
    // then resize the game to the size the window really got - callers persist
    // WindowWidth/Height, and config must record what happened, not what was
    // asked for. Logical size, matching what SDL_EVENT_WINDOW_RESIZED carries.
    SDL_SyncWindow(g_sdlWindow);
    int actualW = w, actualH = h;
    SDL_GetWindowSize(g_sdlWindow, &actualW, &actualH);
    HandleWindowResize(actualW, actualH);
}

namespace
{
    void UpdatePortableTextInput()
    {
        static bool textInputActive = false;
        auto* field = CUITextInputBox::GetFocusedPortable();
        const bool requested = CUITextInputBox::ConsumeTextInputRequest();
        if (g_sdlWindow == nullptr) return;
        if (field == nullptr)
        {
            if (textInputActive) SDL_StopTextInput(g_sdlWindow);
            textInputActive = false;
            return;
        }

        int cx, cy, cw, ch;
        if (field->GetCaretArea(cx, cy, cw, ch))
        {
            const SDL_Rect area = {
                static_cast<int>(cx * g_fScreenRate_x),
                static_cast<int>(cy * g_fScreenRate_y),
                static_cast<int>(cw * g_fScreenRate_x),
                static_cast<int>(ch * g_fScreenRate_y) };
            static SDL_Rect lastArea = {};
            if (requested || area.x != lastArea.x || area.y != lastArea.y
                || area.w != lastArea.w || area.h != lastArea.h)
            {
                SDL_SetTextInputArea(g_sdlWindow, &area, 0);
#if defined(__ANDROID__)
                if (textInputActive || requested)
                    Platform::Android::Input::SynchronizeArea(area,
                        field->IsPassword(), field->UseMultiline() != FALSE);
#endif
                lastArea = area;
            }
        }
        if (textInputActive && !requested) return;

        const SDL_PropertiesID properties = SDL_CreateProperties();
        SDL_SetNumberProperty(properties, SDL_PROP_TEXTINPUT_TYPE_NUMBER,
            field->IsPassword() ? SDL_TEXTINPUT_TYPE_TEXT_PASSWORD_HIDDEN : SDL_TEXTINPUT_TYPE_TEXT);
        SDL_SetBooleanProperty(properties, SDL_PROP_TEXTINPUT_AUTOCORRECT_BOOLEAN,
            !field->IsPassword());
        SDL_SetBooleanProperty(properties, SDL_PROP_TEXTINPUT_MULTILINE_BOOLEAN,
            field->UseMultiline() != FALSE);
        textInputActive = SDL_StartTextInputWithProperties(g_sdlWindow, properties);
        SDL_DestroyProperties(properties);
    }

    void HandleKeyboardDown(const SDL_KeyboardEvent& key)
    {
        if (!key.repeat)
            Core::Input::RecordKeyboardPress(key.scancode);
#ifndef _WIN32
        // These mirror what WndProc does from Win32 messages, for the
        // SDL-only input path. On Windows WndProc is still driven (via
        // SDL_SetWindowsMessageHook), so doing them here too would
        // double-fire - guard them off there.
        //
        // Enter is gated through SetEnterPressed: ScanAsyncKeyState
        // suppresses a VK_RETURN press unless this fired that frame
        // (WM_CHAR does it on Windows). Without it Enter never reaches
        // the game (login submit, chat open).
        if (key.scancode == SDL_SCANCODE_RETURN ||
            key.scancode == SDL_SCANCODE_KP_ENTER)
        {
            SetEnterPressed(true);
        }
        // F10 toggles the camera zoom lock (WM_SYSKEYDOWN on Windows,
        // where F10 is a reserved system key). Without it the zoom stays
        // locked and the mouse wheel can never zoom. Edge-triggered.
        if (key.scancode == SDL_SCANCODE_F10 && !key.repeat)
        {
            CameraManager::Instance().ToggleZoomLock();
        }
#endif
        // Navigation/erase/clipboard for the focused portable field (#447).
        FeedPortableKey(key);
    }
}

MSG MainLoop()
{
    constexpr auto target_resolution = 1;
    auto precise = timeBeginPeriod(target_resolution);

    while (!Destroy)
    {
        SDL_Event event;
        int messageProcessed = 0;

        // Per-frame mouse-state reset (was done per-message in WndProc): clear a
        // stale double-click and a pop whose position the cursor has moved off.
        MouseLButtonDBClick = false;
        if (MouseLButtonPop && (g_iMousePopPosition_x != MouseX || g_iMousePopPosition_y != MouseY))
            MouseLButtonPop = false;

        // Pumping SDL also drives the Win32 message hook (-> WndProc) for the
        // input still on it (IME, the legacy EDIT text boxes); mouse and window
        // events are handled here, off the hook.
        while (SDL_PollEvent(&event))
        {
            Core::Input::GamepadService::Instance().HandleEvent(event);
#ifdef _EDITOR
            // Feed every event to the editor's ImGui SDL3 backend (issue #442).
            // Guard on an active context: editor init can fail or be shut down.
            if (ImGui::GetCurrentContext() != nullptr)
                ImGui_ImplSDL3_ProcessEvent(&event);
#endif
            switch (event.type)
            {
            case SDL_EVENT_QUIT:
                Destroy = true;
                break;
            case SDL_EVENT_MOUSE_MOTION:
#if defined(__ANDROID__) || defined(__OHOS__)
                // Mobile finger input is arbitrated by HandleMobileFinger.
                if (event.motion.which == SDL_TOUCH_MOUSEID) break;
#endif
                HandleMouseMotion(event.motion.x, event.motion.y);
                break;
            case SDL_EVENT_MOUSE_BUTTON_DOWN:
            case SDL_EVENT_MOUSE_BUTTON_UP:
#if defined(__ANDROID__) || defined(__OHOS__)
                if (event.button.which == SDL_TOUCH_MOUSEID) break;
#endif
                HandleMouseButton(event);
                break;
            case SDL_EVENT_MOUSE_WHEEL:
                // SDL does not pre-correct flipped (natural) scrolling; invert.
                MouseWheel = (event.wheel.direction == SDL_MOUSEWHEEL_FLIPPED)
                    ? -static_cast<int>(event.wheel.y)
                    : static_cast<int>(event.wheel.y);
                break;
#if defined(__ANDROID__) || defined(__OHOS__)
            case SDL_EVENT_FINGER_DOWN:
            case SDL_EVENT_FINGER_UP:
            case SDL_EVENT_FINGER_MOTION:
            case SDL_EVENT_FINGER_CANCELED:
                HandleMobileFinger(event);
                break;
#endif
            case SDL_EVENT_WINDOW_RESIZED:
                HandleWindowResize(event.window.data1, event.window.data2);
                break;
            case SDL_EVENT_WINDOW_DISPLAY_CHANGED:
                ApplyFrameTimingConfiguration(g_bWndActive);
                break;
            case SDL_EVENT_WINDOW_FOCUS_GAINED:
                HandleFocusChange(true);
                break;
#ifndef _WIN32
            case SDL_EVENT_WINDOW_MOUSE_ENTER:
                // Wayland can deliver the first pointer-enter after the startup
                // cursor-hide, dropping it and leaving the OS cursor over the
                // game's own; re-apply the hide on every enter (issue #462).
                MuApplyCursorVisibility();
                break;
#endif
            case SDL_EVENT_WINDOW_FOCUS_LOST:
                HandleFocusChange(false);
                break;
            case SDL_EVENT_TEXT_INPUT:
                // Committed characters for the focused portable text field (#447).
                FeedPortableTextInput(event.text.text);
                break;
            case SDL_EVENT_TEXT_EDITING:
                // IME composition preview for the focused portable field (#447).
                if (auto* box = CUITextInputBox::GetFocusedPortable())
                    box->OnTextEditing(Utf8ToWide(event.edit.text).c_str());
                break;
            case SDL_EVENT_KEY_DOWN:
                HandleKeyboardDown(event.key);
                break;
            default:
                break;
            }

            ++messageProcessed;
            if (g_MaxMessagePerCycle > 0 && messageProcessed >= g_MaxMessagePerCycle)
            {
                break;
            }
        }

        UpdateGamepadInput();
#if defined(__ANDROID__) || defined(__OHOS__)
        UpdateMobileMouseButtons();
#endif

        UpdatePortableTextInput();

        // Process server packets handed over from the network thread. Replaces
        // the old WM_RECEIVE_BUFFER message round-trip; runs on the main thread.
        Network::IncomingPacketQueue::Instance().DrainTo(ProcessPacketCallback);

        // Run a pending reconnect teardown between frames (self-guards on its
        // pending flag). Replaces the old WM_START_RECONNECT round-trip.
        ReconnectManager::Instance().Begin();

        // Fire any due timers. Replaces the Win32 SetTimer/WM_TIMER dispatch.
        Core::Time::FrameTimerScheduler::Instance().Tick();
#if defined(__ANDROID__) || defined(__OHOS__)
        UI::Items::Touch::TickItemContact(static_cast<std::uint64_t>(SDL_GetTicks()));
#endif

        if (CheckRenderNextFrame())
        {
            if (g_bUseWindowMode || g_bWndActive || g_HasInactiveFpsOverride)
            {
#ifdef _EDITOR
                // F12 key toggle for editor
                static bool wasF12Pressed = false;
                if (Core::Input::IsKeyDown(VK_F12))
                {
                    if (!wasF12Pressed)
                    {
                        g_MuEditorCore.ToggleEditor();
                        fwprintf(stderr, L"[Editor] Toggled: %s\n",
                            g_MuEditorCore.IsEnabled() ? L"ON" : L"OFF");
                        fflush(stderr);
                        wasF12Pressed = true;
                    }
                }
                else
                {
                    wasF12Pressed = false;
                }

                // Update editor UI (must be before RenderScene)
                g_MuEditorCore.Update();
#endif

                // Render game scene (ImGui rendering happens inside before SwapBuffers)
                auto& focusNavigator = Core::Input::FocusNavigator::Instance();
                focusNavigator.BeginFrame();
                RenderScene(g_hDC);
                focusNavigator.EndFrame(
                    static_cast<float>(MouseX),
                    static_cast<float>(MouseY));
                Core::Input::ClearKeyboardPresses();
                Core::Input::ClearTouchMousePresses();
#if defined(MU_ENABLE_VIRTUAL_GAMEPAD_TESTS)
                WriteVirtualGamepadAcceptanceState();
#endif
            }
        }
        else
        {
            // SDL_PollEvent above already drained pending events, so just pace
            // the frame.
            WaitForNextActivity(precise == TIMERR_NOERROR);
        }

    } // while (!Destroy)

    if (precise == TIMERR_NOERROR)
    {
        timeEndPeriod(target_resolution);
    }

    return MSG{};
}

namespace
{
    // The whole interface is authored in a 640x480 reference space and stretched
    // by g_fScreenRate; the text buffer is full-window-resolution. A glyph that is
    // N px tall in the reference must therefore be N*rate px to keep its on-screen
    // proportion -- this is what makes text grow when the window is enlarged and
    // shrink when it is dragged smaller, and keeps every label centered/aligned
    // with its UI art at any size. (The old fixed 1px-per-200px slope left text at
    // ~6 reference px on large windows: tiny copyright/options text that looked
    // offset from its row.)
    constexpr int BASE_FONT_HEIGHT = 12;       // reference UI font (480px-tall space)
    constexpr int BASE_FIX_FONT_HEIGHT = 14;   // reference fixed-width/label font
    constexpr int MIN_FONT_HEIGHT = 9;         // floor for unusually small windows

    struct FontSizes { int uiFontSize; int fixFontSize; };

    int ScaleReferenceFontHeight(int referenceHeight)
    {
        const float verticalScale = (g_fScreenRate_y > 0.0f) ? g_fScreenRate_y : 1.0f;
        int scaled = static_cast<int>(std::lround(referenceHeight * verticalScale));
        if (scaled < MIN_FONT_HEIGHT)
            scaled = MIN_FONT_HEIGHT;
        return scaled;
    }

    FontSizes CalculateFontSizes()
    {
        FontHeight = ScaleReferenceFontHeight(BASE_FONT_HEIGHT);
        const int fixFontHeight = ScaleReferenceFontHeight(BASE_FIX_FONT_HEIGHT);
        return { FontHeight - 1, fixFontHeight - 1 };
    }

#ifdef _WIN32
    // Absolute path of a bundled font file (relative to ./fonts) next to the exe.
    // The curated names in kBundledFonts are ASCII, so the byte-wise widen is safe.
    std::wstring BundledFontFullPath(const char* relative)
    {
        wchar_t exePath[MAX_PATH] = {};
        GetModuleFileNameW(nullptr, exePath, MAX_PATH);
        std::wstring path(exePath);
        path.resize(path.find_last_of(L"\\/") + 1);   // keep the directory + separator
        while (*relative)
            path.push_back(static_cast<wchar_t>(static_cast<unsigned char>(*relative++)));
        return path;
    }
#endif

    // Privately register the TTFs bundled in ./fonts so GDI resolves their face
    // names even when they are not installed system-wide — parity with the Linux
    // GdiText shim, which reads ./fonts directly. FR_PRIVATE scopes the faces to
    // this process, leaving the system font list untouched. No-op off Windows,
    // where bundled fonts are resolved by GdiText. Shares the kBundledFonts table.
    void RegisterBundledFonts()
    {
#ifdef _WIN32
        for (const auto& font : kBundledFonts)
        {
            AddFontResourceExW(BundledFontFullPath(font.regular).c_str(), FR_PRIVATE, nullptr);
            AddFontResourceExW(BundledFontFullPath(font.bold).c_str(),    FR_PRIVATE, nullptr);
        }
#endif
    }

    // Mirrors RegisterBundledFonts so the process leaves no private faces behind.
    void UnregisterBundledFonts()
    {
#ifdef _WIN32
        for (const auto& font : kBundledFonts)
        {
            RemoveFontResourceExW(BundledFontFullPath(font.regular).c_str(), FR_PRIVATE, nullptr);
            RemoveFontResourceExW(BundledFontFullPath(font.bold).c_str(),    FR_PRIVATE, nullptr);
        }
#endif
    }

    HFONT CreateUIFont(int size, int weight)
    {
        // UI font family from config ([UI] Font). When the player leaves this at
        // "Default", the face follows the active UI/data language: CJK scripts
        // need a font that carries their own glyph variants. Tahoma has no CJK
        // coverage, and on systems without a Tahoma font-link entry GDI falls
        // back to the system-locale CJK face (YaHei on zh-CN Windows), which
        // renders Japanese kanji as simplified-Chinese forms (e.g. 巻->卷,
        // indistinguishable from garbled text to a Japanese reader).
        std::wstring sel = GameConfig::GetInstance().GetFontSelection();
        if (sel.empty())
        {
            const std::wstring uiLocale = GameConfig::GetInstance().GetUILocale();
            const std::wstring dataLang = GameConfig::GetInstance().GetLanguageSelection();
#ifdef _WIN32
            constexpr auto platform = Core::Platform::Fonts::UIFontPlatform::Windows;
#else
            constexpr auto platform = Core::Platform::Fonts::UIFontPlatform::Portable;
#endif
            sel = Core::Platform::Fonts::SelectUIFontFamily(sel, uiLocale, dataLang, platform);
        }
        const wchar_t* face = sel.empty() ? L"Tahoma" : sel.c_str();
        return CreateFont(size, 0, 0, 0, weight, 0, 0, 0, DEFAULT_CHARSET,
                          OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_NATURAL_QUALITY,
                          DEFAULT_PITCH | FF_DONTCARE, face);
    }

    void CreateNewFonts(FontSizes sizes)
    {
        g_hFont     = CreateUIFont(sizes.uiFontSize, FW_NORMAL);
        g_hFontBold = CreateUIFont(sizes.uiFontSize, FW_SEMIBOLD);
        g_hFontBig  = CreateUIFont(sizes.uiFontSize * 2, FW_SEMIBOLD);
        g_hFixFont  = CreateUIFont(sizes.fixFontSize, FW_NORMAL);
    }

    void ReinitializeTextRenderer()
    {
        // Recreates the font buffer bitmap with new g_fScreenRate values
        g_pRenderText->Release();
        g_pRenderText->Create(g_hDC);
        g_pRenderText->SetFont(g_hFont);
    }

    void RefreshInventoryEquipmentSlots()
    {
        // Inventory slot positions depend on the text buffer size; MUST run after
        // ReinitializeTextRenderer().
        if (!g_pNewUISystem)
            return;
        auto* pInventory = g_pNewUISystem->GetUI_NewMyInventory();
        if (pInventory)
            pInventory->SetEquipmentSlotInfo();
    }

}

// Reinitialize fonts when window resolution changes
void ReinitializeFonts()
{
    // Save old font handles so we can delete them after the renderer has switched over
    HFONT hOldFont     = g_hFont;
    HFONT hOldFontBold = g_hFontBold;
    HFONT hOldFontBig  = g_hFontBig;
    HFONT hOldFixFont  = g_hFixFont;

    FontSizes sizes = CalculateFontSizes();
    CreateNewFonts(sizes);
    ReinitializeTextRenderer();

    if (hOldFont)     DeleteObject(hOldFont);
    if (hOldFontBold) DeleteObject(hOldFontBold);
    if (hOldFontBig)  DeleteObject(hOldFontBig);
    if (hOldFixFont)  DeleteObject(hOldFixFont);

    CInput::Instance().Create(g_hWnd, WindowWidth, WindowHeight);
    RefreshInventoryEquipmentSlots();
    // Text fields render through g_pRenderText and resolve their font by kind
    // each frame, so they need no per-control rebuild after a resolution change.
}

DWORD GetDesktopBitsPerPel()
{
    DEVMODE dm = {};
    dm.dmSize = sizeof(dm);
    if (EnumDisplaySettings(nullptr, ENUM_CURRENT_SETTINGS, &dm))
        return dm.dmBitsPerPel;
    return 32;
}

void UpdateCursorClip();

namespace Core::Platform
{
#ifdef _WIN32
    static bool g_nativeModalActive = false;

    void BeginNativeModal()
    {
        g_nativeModalActive = true;

        // The game window's WM_SETCURSOR handler drives the per-thread Win32
        // cursor display counter negative on every mouse move (the game draws
        // its own cursor sprite). A same-thread MessageBox inherits that
        // counter, so its pointer stays invisible. Bring it back to zero and
        // put a real arrow up for the duration of the dialog.
        ClipCursor(nullptr);
        while (::ShowCursor(TRUE) < 0)
        {
        }
        ::SetCursor(::LoadCursorW(nullptr, IDC_ARROW));
    }

    void EndNativeModal()
    {
        g_nativeModalActive = false;

        // Restore the hidden system cursor; WM_SETCURSOR keeps balancing it
        // down while the pointer is over the game window.
        ::ShowCursor(FALSE);
        ::SetCursor(::LoadCursorW(nullptr, IDC_ARROW));
        UpdateCursorClip();
    }

    bool IsNativeModalActive()
    {
        return g_nativeModalActive;
    }
#endif // _WIN32
}

void UpdateCursorClip()
{
    // Confine cursor in fullscreen + active only. In windowed mode the user
    // must be able to move the cursor to other windows; when deactivated we
    // must also release so Windows can focus other apps.
    if (Core::Platform::IsNativeModalActive() || !g_hWnd || g_bUseWindowMode || !g_bWndActive)
    {
        ClipCursor(nullptr);
        return;
    }
    RECT client;
    if (!GetClientRect(g_hWnd, &client)) return;
    POINT tl = { client.left, client.top };
    POINT br = { client.right, client.bottom };
    ClientToScreen(g_hWnd, &tl);
    ClientToScreen(g_hWnd, &br);
    RECT clip = { tl.x, tl.y, br.x, br.y };
    ClipCursor(&clip);
}

// Update camera state when window resolution changes
void UpdateResolutionDependentSystems()
{
    // Force camera state update with new viewport dimensions
    // This updates ScreenCenterX/Y and PerspectiveX/Y used for 3D item positioning
    extern CameraState g_Camera;
    float aspectRatio = (float)WindowWidth / (float)WindowHeight;
    CameraProjection::SetupPerspective(g_Camera, g_Camera.FOV, aspectRatio,
                                       g_Camera.ViewNear, g_Camera.ViewFar * RENDER_DISTANCE_MULTIPLIER);

    // Update all 3D UI camera dimensions for proper item rendering
    if (g_pNewUI3DRenderMng)
    {
        g_pNewUI3DRenderMng->UpdateAllCameraDimensions(WindowWidth, WindowHeight);
    }

    // Reposition old-style CWin-based UI for the current scene. Without this,
    // login/character-scene info boxes stay anchored to the old screen size
    // until the player re-enters the scene.
    CUIMng::Instance().RepositionSceneUI();
}

#ifdef _DEBUG
// Set to 1 to enable GL_KHR_debug callback logging (DXP-08 diagnostic soak mode).
// Disabled (0) by default because GL_DEBUG_OUTPUT_SYNCHRONOUS and stack symbolization cause
// severe CPU/GPU stalls during normal development iteration.
#define ENABLE_GL_KHR_DEBUG_CALLBACK 0

#if ENABLE_GL_KHR_DEBUG_CALLBACK
// DXP-08 pre-flip diagnostic: logs every GL_KHR_debug message (Core-profile violations
// included) to MuError.log instead of the driver silently no-oping or hard-failing.
// Registered only when the debug context flag (set alongside SDL_GL_CreateContext, see
// below) is honored by the driver — see the SDL_GL_GetProcAddress null-check at the
// call site, which is the "extension unsupported" fallback.
#include <dbghelp.h>
#pragma comment(lib, "dbghelp.lib")
#include <set>
#include <string>

// DXP-08a attribution pass (temporary): the raw Stage-2 soak logged 1.25M message lines
// with no way to map a message to the call site that produced it. This symbolizes one
// call stack per distinct violation the first time it's seen, then goes quiet on repeats,
// so a single soak yields an actual stack per violation type instead of a guess from
// message text. Remove (or re-gate) once every DXP-08a category is attributed — this is a
// diagnostic aid, not meant to run permanently.
//
// Dedup key is the message TEXT, not (source,type,id): a first attempt keyed on the triple
// and only captured 3 stacks total for a soak with ~24 distinct violation texts, because the
// driver assigns the same id to many unrelated violations (id=1282 alone covers glPushMatrix,
// glColor3f, glBegin, glVertexPointer, and a dozen others) — the id is a coarse GL error
// category, not a per-call-site identifier. The message text is what's actually distinct
// per call site here (confirmed by inspecting the log), so that's the right key.
static void LogSymbolizedStack()
{
    void* frames[32] = {};
    USHORT count = CaptureStackBackTrace(2, 32, frames, nullptr); // skip this fn + GLDebugCallback

    static bool symInitialized = false;
    if (!symInitialized)
    {
        SymSetOptions(SYMOPT_LOAD_LINES | SYMOPT_UNDNAME);
        SymInitialize(GetCurrentProcess(), nullptr, TRUE);
        symInitialized = true;
    }

    char symbolBuffer[sizeof(SYMBOL_INFO) + MAX_SYM_NAME * sizeof(char)] = {};
    SYMBOL_INFO* symbol = reinterpret_cast<SYMBOL_INFO*>(symbolBuffer);
    symbol->SizeOfStruct = sizeof(SYMBOL_INFO);
    symbol->MaxNameLen = MAX_SYM_NAME;

    for (USHORT i = 0; i < count; ++i)
    {
        DWORD64 address = reinterpret_cast<DWORD64>(frames[i]);
        DWORD64 symDisplacement = 0;
        BOOL hasSymbol = SymFromAddr(GetCurrentProcess(), address, &symDisplacement, symbol);

        DWORD lineDisplacement = 0;
        IMAGEHLP_LINE64 line = {};
        line.SizeOfStruct = sizeof(IMAGEHLP_LINE64);
        BOOL hasLine = SymGetLineFromAddr64(GetCurrentProcess(), address, &lineDisplacement, &line);

        if (hasSymbol && hasLine)
            g_ErrorReport.Write(L"    #%u %hs (%hs:%lu)\r\n", i, symbol->Name, line.FileName, line.LineNumber);
        else if (hasSymbol)
            g_ErrorReport.Write(L"    #%u %hs\r\n", i, symbol->Name);
        else
            g_ErrorReport.Write(L"    #%u 0x%p\r\n", i, frames[i]);
    }
}

static void APIENTRY GLDebugCallback(GLenum source, GLenum type, GLuint id, GLenum severity,
                                      GLsizei /*length*/, const GLchar* message, const void* /*userParam*/)
{
    g_ErrorReport.Write(L"[GL_DEBUG] source=0x%04X type=0x%04X id=%u severity=0x%04X: %hs\r\n",
                         source, type, id, severity, message);

    static std::set<std::string> seenMessages;
    if (seenMessages.insert(std::string(message)).second)
    {
        g_ErrorReport.Write(L"  ^ first occurrence of this message -- call stack:\r\n");
        LogSymbolizedStack();
    }
}
// GLP-08: GLDebugCallback's closing brace above was missing -- this whole block has been
// uncompiled dead code since ENABLE_GL_KHR_DEBUG_CALLBACK has always defaulted to 0, so the
// preprocessor skipped it before the compiler could ever see the mismatch. Found while
// temporarily flipping the flag on for a GLP-08 soak test.
#endif // ENABLE_GL_KHR_DEBUG_CALLBACK
#endif // _DEBUG

static void InitializeGameTranslations()
{
    const std::wstring uiLocaleW = GameConfig::GetInstance().GetUILocale();
    const std::string uiLocale(uiLocaleW.begin(), uiLocaleW.end());
    I18N::SetLocale(uiLocale.c_str());
}

#ifdef _WIN32
int APIENTRY WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, PSTR szCmdLine, int nCmdShow)
#else
// Off Windows the Linux entry point (main.cpp) calls this directly.
int WinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, PSTR szCmdLine, int nCmdShow)
#endif
{
    // Pick up the local single-player launch profile (config path, auto-login,
    // solo balance) before any GameConfig access or environment check, so that
    // clients started directly or by an older bundled launcher behave the same
    // as clients started through play.ps1.
    Network::Login::ApplyLaunchProfile();

    wchar_t lpszExeVersion[256] = L"unknown";

    wchar_t* lpszCommandLine = GetCommandLine();
    wchar_t lpszFile[MAX_PATH];
    WORD wVersion[4] = { 0, };
    if (GetFileNameOfFilePath(lpszFile, lpszCommandLine))
    {
        if (GetFileVersion(lpszFile, wVersion))
        {
            mu_swprintf(lpszExeVersion, L"%d.%02d", wVersion[0], wVersion[1]);
            if (wVersion[2] > 0)
            {
                wchar_t lpszMinorVersion[2] = L"a";
                lpszMinorVersion[0] += (wVersion[2] - 1);
                wcscat(lpszExeVersion, lpszMinorVersion);
            }
        }
    }

    g_ErrorReport.Write(L"\r\n");
    g_ErrorReport.WriteLogBegin();
    g_ErrorReport.AddSeparator();
    g_ErrorReport.Write(L"Mu online %ls (%ls) executed. (%d.%d.%d.%d)\r\n", lpszExeVersion, L"Eng", wVersion[0], wVersion[1], wVersion[2], wVersion[3]);

    g_ConsoleDebug->Write(MCD_NORMAL, L"Mu Online (Version: %d.%d.%d.%d)", wVersion[0], wVersion[1], wVersion[2], wVersion[3]);

    g_ErrorReport.WriteCurrentTime();
    ER_SystemInfo si;
    ZeroMemory(&si, sizeof(ER_SystemInfo));
    GetSystemInfo(&si);
    g_ErrorReport.AddSeparator();
    g_ErrorReport.WriteSystemInfo(&si);
    g_ErrorReport.AddSeparator();
    
    g_ErrorReport.Write(L"> To read config.ini.\r\n");

    // Load game settings from INI file first
    GameConfig::GetInstance().Load();
#if defined(__ANDROID__) || defined(__OHOS__)
    g_mobileGestureMapper.SetLeftHanded(GameConfig::GetInstance().GetMobileLeftHanded());
#endif
    InitRenderConfig();

  // Check if animation task pool should be enabled (disabled by default)
  {
      wchar_t configPath[MAX_PATH];
      GetModuleFileNameW(nullptr, configPath, MAX_PATH);
      wchar_t* lastSlash = wcsrchr(configPath, L'\\');
      if (!lastSlash) lastSlash = wcsrchr(configPath, L'/');
      if (lastSlash) *(lastSlash + 1) = L'\0';
      wcscat(configPath, L"config.ini");
      int enableTaskPool = GetPrivateProfileIntW(L"UI", L"EnableAnimationTaskPool", 0, configPath);
      if (enableTaskPool != 0 || wcsstr(GetCommandLineW(), L"--enable-taskpool")) {
          g_bDisableAnimationTaskPool = false;
      }
  }

    // Check for command line server override
    WORD wPortNumber;
    if (GetConnectServerInfo(GetCommandLine(), g_lpszCmdURL, &wPortNumber))
    {
        szServerIpAddress = g_lpszCmdURL;
        g_ServerPort = wPortNumber;
    }
    else
    {
        // Use config.ini settings if no command line override
        static std::wstring serverIPFromConfig = GameConfig::GetInstance().GetServerIP();
        szServerIpAddress = serverIPFromConfig.c_str();
        g_ServerPort = GameConfig::GetInstance().GetServerPort();
    }

    //#ifdef _DEBUG

    m_Username[0] = '\0';
    m_Password[0] = '\0';
    m_SoundOnOff = 1;
    m_MusicOnOff = 1;
    m_Resolution = 0;
    m_RememberMe = 0;

    g_iChatInputType = 1;

    // Apply window settings from INI
    WindowWidth = GameConfig::GetInstance().GetWindowWidth();
    WindowHeight = GameConfig::GetInstance().GetWindowHeight();
    g_bUseWindowMode = GameConfig::GetInstance().GetWindowMode() ? TRUE : FALSE;
    g_bUseFullscreenMode = !g_bUseWindowMode;

    // Apply audio settings from INI — volume 0 = off, >0 = on
    m_SoundOnOff = (GameConfig::GetInstance().GetSoundVolume() > 0) ? 1 : 0;
    m_MusicOnOff = (GameConfig::GetInstance().GetMusicVolume() > 0) ? 1 : 0;

    // Apply login settings from INI
    m_RememberMe = GameConfig::GetInstance().GetRememberMe() ? 1 : 0;
    auto& gameConfig = GameConfig::GetInstance();
    const std::wstring configuredDataLanguage = gameConfig.GetLanguageSelection();
    std::wstring langSelection = ResolveDataLanguageSelection(configuredDataLanguage);
    if (langSelection != configuredDataLanguage)
    {
        gameConfig.SetLanguageSelection(langSelection);
        gameConfig.Save();
        g_ErrorReport.Write(
            L"> Data language '%ls' is unavailable; falling back to '%ls'.\r\n",
            configuredDataLanguage.c_str(), langSelection.c_str());
    }
    wcsncpy_s(g_aszMLSelection, langSelection.c_str(), MAX_LANGUAGE_NAME_LENGTH - 1);
    g_strSelectedML = g_aszMLSelection;
    g_ErrorReport.Write(L"> Data language: %ls (UI locale: %ls).\r\n",
        g_strSelectedML.c_str(), GameConfig::GetInstance().GetUILocale().c_str());

    if (m_RememberMe)
    {
        GameConfig::GetInstance().DecryptCredentials(m_Username, m_Password, _countof(m_Username), _countof(m_Password));
    }

    g_fScreenRate_x = (float)WindowWidth / (float)REFERENCE_WIDTH;
    g_fScreenRate_y = (float)WindowHeight / (float)REFERENCE_HEIGHT;


    pMultiLanguage = new CMultiLanguage(g_strSelectedML);

    if (g_iChatInputType == 1)
        ShowCursor(FALSE);

    // Fullscreen is requested via an SDL window flag below; SDL handles the
    // display-mode change and restores it on teardown.
    g_ErrorReport.Write(L"> Screen size = %d x %d.\r\n", WindowWidth, WindowHeight);

    g_hInst = hInstance;

#if defined(__ANDROID__) || defined(__OHOS__)
    // The gesture mapper owns mobile mouse edges. SDL's automatic conversion
    // also affects GetMouseState, bypassing captured inventory gestures.
    if (!SDL_SetHintWithPriority(SDL_HINT_TOUCH_MOUSE_EVENTS, "0", SDL_HINT_OVERRIDE))
    {
        g_ErrorReport.Write(L"> Cannot disable duplicate SDL touch-mouse input.\r\n");
        return 0;
    }
#endif
    // SDL owns the window and GL context (issue #442).
    if (!SDL_InitSubSystem(SDL_INIT_VIDEO))
    {
        g_ErrorReport.Write(L"> SDL video init failed.\r\n");
        MessageBox(nullptr, L"Windows aplication error!", L"Aplication Error", MB_ICONERROR);
        return 0;
    }

    if (!Core::Input::GamepadService::Instance().Initialize(
            GameConfig::GetInstance().GetGamepadSettings(),
            GameConfig::GetInstance().GetHapticSettings()))
    {
        g_ErrorReport.Write(L"> SDL gamepad init failed: %hs.\r\n", SDL_GetError());
    }

    // DXP-08 Stage G: g_CoreProfile (config.ini [Render] CoreProfile, default 1 as of
    // Stage G) selects the context profile. Core became the default after the DXP-08a/
    // DXP-09 prerequisites were fixed and the debug-callback soak came back clean across
    // every map/panel; compatibility (CoreProfile=0) remains available as a rollback.
    // Core additionally needs an explicit version request (compatibility takes the
    // driver's highest, and must keep doing so -- see the else branch below).
#if defined(__ANDROID__) || defined(__OHOS__)
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_PROFILE_MASK, SDL_GL_CONTEXT_PROFILE_ES);
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_MAJOR_VERSION, 3);
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_MINOR_VERSION, 0);
#else
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_PROFILE_MASK, g_CoreProfile ? SDL_GL_CONTEXT_PROFILE_CORE : SDL_GL_CONTEXT_PROFILE_COMPATIBILITY);
#endif
#if defined(_DEBUG) && ENABLE_GL_KHR_DEBUG_CALLBACK
    // KHR_debug callback (registered below, after context creation) needs the context
    // created with the debug flag to get synchronous, precisely-attributed messages.
    SDL_GL_SetAttribute(SDL_GL_CONTEXT_FLAGS, SDL_GL_CONTEXT_DEBUG_FLAG);
#endif
    SDL_GL_SetAttribute(SDL_GL_DOUBLEBUFFER, 1);
    SDL_GL_SetAttribute(SDL_GL_DEPTH_SIZE, 16);

    SDL_WindowFlags windowFlags = SDL_WINDOW_OPENGL;
    if (g_bUseWindowMode != TRUE)
        windowFlags |= SDL_WINDOW_FULLSCREEN;
#if !MU_PLATFORM_MOBILE
    // Desktop: let the player drag the window corner to resize. No-op while the
    // window is fullscreen; takes effect in windowed mode. Mobile is always
    // fullscreen and must keep its fixed surface.
    windowFlags |= SDL_WINDOW_RESIZABLE;
#endif

    if (g_CoreProfile)
    {
        // GLP-08: request the highest core context the driver will give -- a driver is
        // permitted to hand back a higher version than requested for a core-profile context,
        // but not guaranteed to, and Phase 2/4 need features not in 3.3 (see GLP-08 task notes).
        // Descend only on failure; the {3,3} rung is byte-identical to the pre-GLP-08 fixed
        // request, so a machine where the whole loop somehow misbehaves still ends up exactly
        // where it was before this change.
        static constexpr struct { int major, minor; } kCoreVersionAttempts[] = { {4, 5}, {4, 3}, {3, 3} };
        constexpr int kAttemptCount = sizeof(kCoreVersionAttempts) / sizeof(kCoreVersionAttempts[0]);

        // config.ini [Render] MaxGLVersion caps which rung the loop starts at -- rollback path
        // for a driver that mishandles the descending loop itself. Empty/default tries highest.
        int startIndex = 0;
        if (g_MaxGLVersionMajor > 0)
        {
            while (startIndex < kAttemptCount - 1 &&
                   (kCoreVersionAttempts[startIndex].major > g_MaxGLVersionMajor ||
                    (kCoreVersionAttempts[startIndex].major == g_MaxGLVersionMajor &&
                     kCoreVersionAttempts[startIndex].minor > g_MaxGLVersionMinor)))
            {
                startIndex++;
            }
        }

        for (int i = startIndex; i < kAttemptCount; i++)
        {
            SDL_GL_SetAttribute(SDL_GL_CONTEXT_MAJOR_VERSION, kCoreVersionAttempts[i].major);
            SDL_GL_SetAttribute(SDL_GL_CONTEXT_MINOR_VERSION, kCoreVersionAttempts[i].minor);

            g_sdlWindow = SDL_CreateWindow("MU Online", static_cast<int>(WindowWidth), static_cast<int>(WindowHeight), windowFlags);
            if (g_sdlWindow)
            {
                g_sdlGLContext = SDL_GL_CreateContext(g_sdlWindow);
                if (g_sdlGLContext)
                {
                    g_ErrorReport.Write(L"> GL %d.%d core context created.\r\n", kCoreVersionAttempts[i].major, kCoreVersionAttempts[i].minor);
                    break;
                }
                g_ErrorReport.Write(L"> GL %d.%d core context failed, trying next.\r\n", kCoreVersionAttempts[i].major, kCoreVersionAttempts[i].minor);
                // SDL will not let a context-creation retry reuse a window whose pixel format is
                // already set -- destroy and recreate between attempts.
                SDL_DestroyWindow(g_sdlWindow);
                g_sdlWindow = nullptr;
            }
            else
            {
                g_ErrorReport.Write(L"> SDL_CreateWindow failed for GL %d.%d core, trying next.\r\n", kCoreVersionAttempts[i].major, kCoreVersionAttempts[i].minor);
            }
        }
    }
    else
    {
        // Compatibility profile: unchanged from pre-GLP-08 behavior -- no explicit version
        // request, takes the driver's highest.
        g_sdlWindow = SDL_CreateWindow("MU Online", static_cast<int>(WindowWidth), static_cast<int>(WindowHeight), windowFlags);
        if (g_sdlWindow)
        {
            g_sdlGLContext = SDL_GL_CreateContext(g_sdlWindow);
        }
    }

    if (!g_sdlWindow)
    {
        g_ErrorReport.Write(L"> SDL_CreateWindow failed.\r\n");
        MessageBox(nullptr, L"Windows aplication error!", L"Aplication Error", MB_ICONERROR);
        return 0;
    }

    g_ErrorReport.Write(L"> Start window success.\r\n");

    // Initialize OpenGL viewport dimensions to match window dimensions
    // This ensures they're correct even if WM_SIZE hasn't fired yet or sent wrong values
    OpenglWindowWidth = WindowWidth;
    OpenglWindowHeight = WindowHeight;

#if !MU_PLATFORM_MOBILE
    // Desktop: the configured Width/Height is only a *request*. In fullscreen SDL
    // switches to the real display mode, and a window may come back at a
    // different size before the first SDL_EVENT_WINDOW_RESIZED arrives. Sync to
    // the actual size now so fonts/UI scale correctly from the very first frame,
    // and clamp how small the player can drag a windowed corner.
    SDL_SetWindowMinimumSize(g_sdlWindow, REFERENCE_WIDTH, REFERENCE_HEIGHT);
    {
        int actualWidth = 0;
        int actualHeight = 0;
        SDL_GetWindowSize(g_sdlWindow, &actualWidth, &actualHeight);
        if (actualWidth > 0 && actualHeight > 0 &&
            (static_cast<unsigned int>(actualWidth) != WindowWidth ||
             static_cast<unsigned int>(actualHeight) != WindowHeight))
        {
            WindowWidth = static_cast<unsigned int>(actualWidth);
            WindowHeight = static_cast<unsigned int>(actualHeight);
            OpenglWindowWidth = WindowWidth;
            OpenglWindowHeight = WindowHeight;
            g_fScreenRate_x = static_cast<float>(WindowWidth) / static_cast<float>(REFERENCE_WIDTH);
            g_fScreenRate_y = static_cast<float>(WindowHeight) / static_cast<float>(REFERENCE_HEIGHT);
        }
    }
#endif

    if (!g_sdlGLContext)
    {
        g_ErrorReport.Write(L"OpenGL Create Context Error.\r\n");
        KillGLWindow();
        MessageBox(nullptr, I18N::Game::InstallTheLatestGraphicsCardDriver, L"OpenGL Create Context Error.", MB_OK | MB_ICONEXCLAMATION);
        return FALSE;
    }

    if (!SDL_GL_MakeCurrent(g_sdlWindow, g_sdlGLContext))
    {
        g_ErrorReport.Write(L"OpenGL Make Current Error: %hs.\r\n", SDL_GetError());
        MessageBox(nullptr, L"Unable to activate the OpenGL context.", L"OpenGL Error", MB_OK | MB_ICONEXCLAMATION);
        KillGLWindow();
        return FALSE;
    }

#if defined(__ANDROID__) || defined(__OHOS__)
    // These entry points are core in OpenGL ES 3.0 and are required by the
    // renderer's UBO, integer-attribute and VAO paths. Fail explicitly instead
    // of continuing into a device-specific black screen.
    static constexpr const char* kRequiredGLES3Functions[] = {
        "glBindBufferBase",
        "glGetUniformBlockIndex",
        "glUniformBlockBinding",
        "glGenVertexArrays",
        "glBindVertexArray",
        "glVertexAttribIPointer",
    };
    for (const char* functionName : kRequiredGLES3Functions)
    {
        if (SDL_GL_GetProcAddress(functionName) == nullptr)
        {
            g_ErrorReport.Write(L"OpenGL ES 3.0 entry point missing: %hs.\r\n", functionName);
            SDL_ShowSimpleMessageBox(
                SDL_MESSAGEBOX_ERROR,
                "OpenMU graphics requirement",
                "This device does not provide the OpenGL ES 3.0 features required by OpenMU.",
                g_sdlWindow);
            KillGLWindow();
            return FALSE;
        }
    }
#endif

    RHI::Init(nullptr, static_cast<int>(WindowWidth), static_cast<int>(WindowHeight));

#if defined(_DEBUG) && ENABLE_GL_KHR_DEBUG_CALLBACK
    // DXP-08: register the KHR_debug callback now that a current context exists.
    // glew.h (included via stdafx.h) supplies the PFNGLDEBUGMESSAGECALLBACKPROC
    // typedef and GL_DEBUG_* enums, but glewInit() is never called in this codebase
    // (every other GL entry point is loaded the same manual way, see
    // BMDMeshShader::Init()), so the function pointer is fetched directly.
    {
        PFNGLDEBUGMESSAGECALLBACKPROC fn_glDebugMessageCallback =
            (PFNGLDEBUGMESSAGECALLBACKPROC)SDL_GL_GetProcAddress("glDebugMessageCallback");
        if (fn_glDebugMessageCallback)
        {
            glEnable(GL_DEBUG_OUTPUT);
            glEnable(GL_DEBUG_OUTPUT_SYNCHRONOUS);
            fn_glDebugMessageCallback(GLDebugCallback, nullptr);

            // NVIDIA's GL_DEBUG_SEVERITY_NOTIFICATION chatter (routine "buffer will use
            // VIDEO memory" info per alloc/rebind) runs into the thousands of lines per
            // session and buries real LOW/MEDIUM/HIGH violations — the entire point of
            // this callback during the Core-profile soak. Silence NOTIFICATION only.
            PFNGLDEBUGMESSAGECONTROLPROC fn_glDebugMessageControl =
                (PFNGLDEBUGMESSAGECONTROLPROC)SDL_GL_GetProcAddress("glDebugMessageControl");
            if (fn_glDebugMessageControl)
            {
                fn_glDebugMessageControl(GL_DONT_CARE, GL_DONT_CARE, GL_DEBUG_SEVERITY_NOTIFICATION, 0, nullptr, GL_FALSE);
            }

            g_ErrorReport.Write(L"> GL_KHR_debug callback registered.\r\n");
        }
        else
        {
            g_ErrorReport.Write(L"> GL_KHR_debug unavailable (glDebugMessageCallback not found).\r\n");
        }
    }
#endif // defined(_DEBUG) && ENABLE_GL_KHR_DEBUG_CALLBACK

    // Initialize single-pass GLSL engines (Item Specular & Planar Ground Shadows)
    CItemSpecularShader::Instance().Init();
    CPlanarShadowShader::Instance().Init();
    GlobalUBO::Instance().Create();
    SceneUBO::Instance().Create();
    PassthroughShader::Instance().Create();
    BMDMeshShader::Instance().Create();
    TerrainShader::Instance().Create();

#if defined(__ANDROID__) || defined(__OHOS__)
    if (!GlobalUBO::Instance().IsCreated()
        || !SceneUBO::Instance().IsCreated()
        || !PassthroughShader::Instance().IsCreated()
        || !BMDMeshShader::Instance().IsCreated()
        || !TerrainShader::Instance().IsCreated())
    {
        g_ErrorReport.Write(L"OpenGL ES 3.0 renderer initialization failed.\r\n");
        SDL_ShowSimpleMessageBox(
            SDL_MESSAGEBOX_ERROR,
            "OpenMU graphics error",
            "The OpenMU renderer could not start on this device. Check the game log for shader details.",
            g_sdlWindow);
        TerrainShader::Instance().Destroy();
        BMDMeshShader::Instance().Destroy();
        PassthroughShader::Instance().Destroy();
        SceneUBO::Instance().Destroy();
        GlobalUBO::Instance().Destroy();
        CPlanarShadowShader::Instance().Shutdown();
        CItemSpecularShader::Instance().Shutdown();
        RHI::Shutdown();
        KillGLWindow();
        return FALSE;
    }
#endif

    IR::Create();

#ifdef _WIN32
    // Bridge SDL's native handles so the remaining Win32 code (IME, DirectSound,
    // cursor, the legacy EDIT-control text boxes) keeps working.
    g_hWnd = static_cast<HWND>(SDL_GetPointerProperty(
        SDL_GetWindowProperties(g_sdlWindow), SDL_PROP_WINDOW_WIN32_HWND_POINTER, nullptr));
    g_hDC = GetDC(g_hWnd);
    g_hRC = wglGetCurrentContext();

    // Drive the existing WndProc from SDL's Win32 messages (transitional, #442).
    SDL_SetWindowsMessageHook(Win32MessageHook, nullptr);
#endif // _WIN32

    if ((SDL_GetWindowFlags(g_sdlWindow) & SDL_WINDOW_FULLSCREEN) != 0)
        MuApplyWindowResolution(WindowWidth, WindowHeight, false);

    SDL_RaiseWindow(g_sdlWindow);
    SetFocus(g_hWnd);

#ifndef _WIN32
    // The engine hid the OS cursor (it draws its own) before the SDL video
    // subsystem existed, so that call could not reach SDL. Apply the pending
    // state now that the window is up. On Windows WM_SETCURSOR keeps doing this.
    MuApplyCursorVisibility();
#endif

    g_ErrorReport.Write(L"> OpenGL init success.\r\n");
    g_ErrorReport.AddSeparator();
    g_ErrorReport.WriteOpenGLInfo();
    g_ErrorReport.AddSeparator();
    g_ErrorReport.WriteSoundCardInfo();

    // SDL_CreateWindow already shows the window.

    // Initialize translations with the saved UI locale (defaults to "zh-CN"
    // in the Mainland-local package).
    // The editor still restores its own MuEditorConfig language preference
    // later in its init, which feeds through to I18N::SetLocale as well.
    InitializeGameTranslations();

#ifdef _EDITOR
    // Initialize MU Editor (ImGui SDL3 backend needs the SDL window + GL context).
    g_MuEditorCore.Initialize(g_sdlWindow, g_sdlGLContext);

    // Check for --editor command line flag
    if (szCmdLine && wcsstr(GetCommandLineW(), L"--editor"))
    {
        g_MuEditorCore.SetEnabled(true);
        fwprintf(stderr, L"[Editor] Starting in editor mode (--editor flag detected)\n");
        std::fflush(stderr);
    }
#endif

    g_ErrorReport.WriteImeInfo( g_hWnd);
    g_ErrorReport.AddSeparator();

    InitVSync();
    ApplyFrameTimingConfiguration(true);

    // Make the bundled ./fonts faces resolvable by GDI before the first CreateFont,
    // so a chosen curated font works even without a system-wide install.
    RegisterBundledFonts();
    CreateNewFonts(CalculateFontSizes());

    // Log which UI font was resolved now that the fonts have been created (the
    // discovery is lazy on first CreateFont). Helps diagnose "no UI text".
    g_ErrorReport.AddSeparator();
    g_ErrorReport.WriteFontInfo();
    g_ErrorReport.AddSeparator();

    setlocale(LC_ALL, "english");

    CInput::Instance().Create(g_hWnd, WindowWidth, WindowHeight);

    // Android enters here after its data directory and SDL JNI are ready.
    g_MixRecipeMgr.LoadRecipes();
    g_pNewUISystem->Create();

    // Always initialize audio system so music can be enabled at runtime
    AudioPlayer::Initialize();

    // Always initialize sound so it can be toggled at runtime
    InitDirectSound(g_hWnd);

    {
        int value = AudioPlayer::ClampVolume(GameConfig::GetInstance().GetSoundVolume());
        g_pOption->SetVolumeLevel(value);
        SetEffectVolumeLevel(value);
    }

    auto& timers = Core::Time::FrameTimerScheduler::Instance();
    timers.SetRepeating(HACK_TIMER, 20 * 1000, [] { CheckHack(); });
    timers.SetRepeating(MUHELPER_TIMER, 250 /* ms */,
        [] { MUHelper::CMuHelper::TimerProc(nullptr, 0, MUHELPER_TIMER, 0); });

    srand((unsigned)time(nullptr));

    for (int & i : RandomTable)
        i = rand() % 360;

    RendomMemoryDump = new BYTE[rand() % 100 + 1];
    GateAttribute = new GATE_ATTRIBUTE[MAX_GATES] { };
    SkillAttribute = new SKILL_ATTRIBUTE[MAX_SKILLS] { };
    ItemAttRibuteMemoryDump = new ITEM_ATTRIBUTE[MAX_ITEM + 1024] { };
    ItemAttribute = ((ITEM_ATTRIBUTE*)ItemAttRibuteMemoryDump) + rand() % 1024;
    CharacterMemoryDump = new CHARACTER[MAX_CHARACTERS_CLIENT + 1 + 128] { };
    CharactersClient = ((CHARACTER*)CharacterMemoryDump) + rand() % 128;
    CharacterMachine = new CHARACTER_MACHINE;

    memset(GateAttribute, 0, sizeof(GATE_ATTRIBUTE) * (MAX_GATES));
    memset(ItemAttribute, 0, sizeof(ITEM_ATTRIBUTE) * (MAX_ITEM));
    memset(SkillAttribute, 0, sizeof(SKILL_ATTRIBUTE) * (MAX_SKILLS));
    memset(CharacterMachine, 0, sizeof(CHARACTER_MACHINE));

    CharacterAttribute = &CharacterMachine->Character;
    CharacterMachine->Init();
    Hero = &CharactersClient[0];

    if (g_iChatInputType == 1)
    {
        g_pMercenaryInputBox = new CUIMercenaryInputBox;
        g_pSingleTextInputBox = new CUITextInputBox;
        g_pSinglePasswdInputBox = new CUITextInputBox;
    }

    g_pUIManager = new CUIManager;
    g_pUIMapName = new CUIMapName;	// rozy

    g_BuffSystem = BuffStateSystem::Make();
	AnimationTaskPool::Instance().Initialize();

    g_MapProcess = MapProcess::Make();

    g_petProcess = PetProcess::Make();

    CUIMng::Instance().Create();

    if (g_iChatInputType == 1)
    {
        g_pMercenaryInputBox->Init(g_hWnd);
        g_pSingleTextInputBox->Init(g_hWnd, 200, 20);
        g_pSinglePasswdInputBox->Init(g_hWnd, 200, 20, 9, TRUE);
        g_pSingleTextInputBox->SetState(UISTATE_HIDE);
        g_pSinglePasswdInputBox->SetState(UISTATE_HIDE);

        g_pMercenaryInputBox->SetFont(g_hFont);
        g_pSingleTextInputBox->SetFont(g_hFont);
        g_pSinglePasswdInputBox->SetFont(g_hFont);

        g_bIMEBlock = FALSE;
        HIMC  hIMC = ImmGetContext(g_hWnd);
        ImmSetConversionStatus(hIMC, IME_CMODE_ALPHANUMERIC, IME_SMODE_NONE);
        ImmReleaseContext(g_hWnd, hIMC);
        SaveIMEStatus();
        g_bIMEBlock = TRUE;
    }

#ifdef _WIN32
    if (g_bUseWindowMode == FALSE)
    {
        int nOldVal;
        SystemParametersInfo(SPI_SCREENSAVERRUNNING, 1, &nOldVal, 0);
        SystemParametersInfo(SPI_GETSCREENSAVETIMEOUT, 0, &g_iScreenSaverOldValue, 0);
        SystemParametersInfo(SPI_SETSCREENSAVETIMEOUT, 300 * 60, nullptr, 0);
    }
#endif // _WIN32

    std::thread cpuUsageRecorder(RecordCpuUsage);
    const MSG msg = MainLoop();

    // Teardown that used to run in WM_DESTROY, now after the loop exits (SDL owns
    // the window/GL context, so they must not be destroyed from a message).
    DestroySound();
#ifdef _EDITOR
    // Shut the editor's ImGui backends down while the GL context and SDL window
    // are still alive; the static destructor runs too late (after KillGLWindow).
    g_MuEditorCore.Shutdown();
#endif
    UnregisterBundledFonts();   // mirror the startup registration
    RHI::Shutdown();
    KillGLWindow();
    DestroyWindow();

    // RecordCpuUsage loops on !Destroy, so it exits once the loop above ended.
    // Join it before WinMain returns; a joinable std::thread destroyed unjoined
    // calls std::terminate.
    if (cpuUsageRecorder.joinable())
        cpuUsageRecorder.join();

	AnimationTaskPool::Instance().Shutdown();
    UI::Controller::ControllerKeyboard::Instance().Shutdown();
    SDL_Quit();

    return msg.wParam;
}
