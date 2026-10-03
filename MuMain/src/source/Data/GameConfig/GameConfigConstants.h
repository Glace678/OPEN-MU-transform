#pragma once

namespace CfgSections
{
    inline constexpr wchar_t CfgSectionWindow[]     = L"Window";
    inline constexpr wchar_t CfgSectionGraphics[]   = L"Graphics";
    inline constexpr wchar_t CfgSectionAudio[]      = L"Audio";
    inline constexpr wchar_t CfgSectionUI[]         = L"UI";
    inline constexpr wchar_t CfgSectionLogin[]      = L"LOGIN";
    inline constexpr wchar_t CfgSectionConnectionSettings[] = L"CONNECTION SETTINGS";
    inline constexpr wchar_t CfgSectionCamera[] = L"Camera";
    inline constexpr wchar_t CfgSectionRender[] = L"Render";
    inline constexpr wchar_t CfgSectionInput[] = L"Input";
    inline constexpr wchar_t CfgSectionHaptics[] = L"Haptics";
}

namespace CfgKeys
{
    // Window
    inline constexpr wchar_t CfgKeyWidth[]      = L"Width";
    inline constexpr wchar_t CfgKeyHeight[]     = L"Height";
    inline constexpr wchar_t CfgKeyWindowed[]   = L"Windowed";

    // Audio — volume 0 = off, >0 = on (no separate Enabled flag).
    inline constexpr wchar_t CfgKeySoundVolume[]  = L"SoundVolume";
    inline constexpr wchar_t CfgKeyMusicVolume[] = L"MusicVolume";

    // Login
    inline constexpr wchar_t CfgKeyRememberMe[]        = L"RememberMe";
    inline constexpr wchar_t CfgKeySavePassword[]      = L"SavePassword";
    inline constexpr wchar_t CfgKeyLanguage[]          = L"Language";
    inline constexpr wchar_t CfgKeyEncryptedUsername[] = L"EncryptedUsername";
    inline constexpr wchar_t CfgKeyEncryptedPassword[] = L"EncryptedPassword";

    // Connection
    inline constexpr wchar_t CfgKeyServerIP[]   = L"ServerIP";
    inline constexpr wchar_t CfgKeyServerPort[] = L"ServerPort";
    inline constexpr wchar_t CfgKeyAccountPortalUrl[] = L"AccountPortalUrl";

    // UI
    inline constexpr wchar_t CfgKeyUILocale[] = L"Locale";
    inline constexpr wchar_t CfgKeyFont[]     = L"Font";

    // Camera
    inline constexpr wchar_t CfgKeyZoom[] = L"Zoom";

    // Render
    // DXP-08: Core Profile GL context flip. 0 = compatibility (rollback), 1 = core.
    inline constexpr wchar_t CfgKeyCoreProfile[] = L"CoreProfile";
    // GLP-08: ceiling on the requested core-profile GL context version, e.g. "4.3". Empty
    // (default) tries the highest of {4.5, 4.3, 3.3} the driver will grant. Rollback path for a
    // driver that mishandles the descending attempt loop.
    inline constexpr wchar_t CfgKeyMaxGLVersion[] = L"MaxGLVersion";
    // Groups particle draws by texture so consecutive sprites merge into one IR batch instead
    // of one draw each. Only reorders runs whose result cannot depend on draw order; 0 keeps
    // the historical slot order. Opt-in until it has been visually checked on real effects.
    inline constexpr wchar_t CfgKeySortParticleDraws[] = L"SortParticleDraws";
    inline constexpr wchar_t CfgKeyFrameRateMode[] = L"FrameRateMode";
    inline constexpr wchar_t CfgKeyFrameRateLimit[] = L"FrameRateLimit";
    inline constexpr wchar_t CfgKeyFrameRateLimitMilliHz[] = L"FrameRateLimitMilliHz";
    inline constexpr wchar_t CfgKeyBackgroundFrameRate[] = L"BackgroundFrameRate";
    inline constexpr wchar_t CfgKeyVSync[] = L"VSync";
    inline constexpr wchar_t CfgKeyRenderLevel[] = L"RenderLevel";
    inline constexpr wchar_t CfgKeyRenderAllEffects[] = L"RenderAllEffects";

    inline constexpr wchar_t CfgKeyGamepadEnabled[] = L"GamepadEnabled";
    inline constexpr wchar_t CfgKeyStickDeadZonePercent[] = L"StickDeadZonePercent";
    inline constexpr wchar_t CfgKeyTriggerDeadZonePercent[] = L"TriggerDeadZonePercent";
    inline constexpr wchar_t CfgKeyPointerSpeed[] = L"PointerSpeed";
    inline constexpr wchar_t CfgKeyInvertPointerY[] = L"InvertPointerY";
    inline constexpr wchar_t CfgKeyHandedness[] = L"Handedness";

    inline constexpr wchar_t CfgKeyHapticsEnabled[] = L"Enabled";
    inline constexpr wchar_t CfgKeyHapticsIntensity[] = L"Intensity";
    inline constexpr wchar_t CfgKeyHapticsCombat[] = L"Combat";
    inline constexpr wchar_t CfgKeyHapticsUI[] = L"UI";
    inline constexpr wchar_t CfgKeyHapticsTransaction[] = L"Transaction";

    // Tutorial
    inline constexpr wchar_t CfgKeyTutorialCompleted[] = L"TutorialCompleted";
}

namespace CfgDefaults
{
    inline constexpr int  CfgDefaultWindowWidth  = 1024;
    inline constexpr int  CfgDefaultWindowHeight = 768;
    inline constexpr bool CfgDefaultWindowed     = false;

    inline constexpr int  CfgDefaultSoundVolume = 5;
    inline constexpr int  CfgDefaultMusicVolume = 5;

    inline constexpr bool CfgDefaultRememberMe = false;
    inline constexpr bool CfgDefaultSavePassword = false;
    inline constexpr wchar_t CfgDefaultLanguage[] = L"Eng";
    inline constexpr wchar_t CfgDefaultEncryptedUsername[] = L"";
    inline constexpr wchar_t CfgDefaultEncryptedPassword[] = L"";

    inline constexpr wchar_t CfgDefaultServerIP[] = L"127.0.0.1";
    inline constexpr int CfgDefaultServerPort = 44406;

    inline constexpr int CfgDefaultZoom = 1735;  // OrbitalCamera DEFAULT_RADIUS — matches Default-cam camera-to-Hero distance

    // I18N locale code. English remains the resource fallback, while this
    // Mainland-local package starts new profiles in Simplified Chinese.
    inline constexpr wchar_t CfgDefaultUILocale[] = L"zh-CN";

    // UI font family name. Empty = each platform's built-in default (Tahoma on
    // Windows, fontconfig "sans-serif" on Linux), so the look is unchanged until
    // the user picks a font. Any value is passed through as the GDI face name.
    inline constexpr wchar_t CfgDefaultFont[] = L"";

    // DXP-08 Stage G: flipped to default-on after DXP-08a/DXP-09 prerequisites were fixed and
    // soak-confirmed clean under CoreProfile=1 (2026-08-01). Set CoreProfile=0 in config.ini to
    // opt back into the compatibility-profile rollback path.
    inline constexpr bool CfgDefaultCoreProfile = true;

    // GLP-08: empty = no cap, try the highest core context available.
    inline constexpr wchar_t CfgDefaultMaxGLVersion[] = L"";

    // Off until the reordering has been eyeballed against real effects on target hardware.
    inline constexpr bool CfgDefaultSortParticleDraws = false;

    inline constexpr wchar_t CfgDefaultFrameRateMode[] = L"DisplayMaximum";
    inline constexpr int CfgDefaultFrameRateLimit = 60;
    inline constexpr int CfgDefaultFrameRateLimitMilliHz = 60000;
    inline constexpr int CfgDefaultBackgroundFrameRate = 30;
    inline constexpr bool CfgDefaultVSync = false;
    inline constexpr int CfgDefaultRenderLevel = 4;
    inline constexpr bool CfgDefaultRenderAllEffects = true;

    inline constexpr bool CfgDefaultGamepadEnabled = true;
    inline constexpr int CfgDefaultStickDeadZonePercent = 18;
    inline constexpr int CfgDefaultTriggerDeadZonePercent = 8;
    inline constexpr int CfgDefaultPointerSpeed = 520;
    inline constexpr bool CfgDefaultInvertPointerY = false;
    inline constexpr wchar_t CfgDefaultHandedness[] = L"right";

    inline constexpr bool CfgDefaultHapticsEnabled = true;
    inline constexpr int CfgDefaultHapticsIntensity = 70;
    inline constexpr bool CfgDefaultHapticsCombat = true;
    inline constexpr bool CfgDefaultHapticsUI = true;
    inline constexpr bool CfgDefaultHapticsTransaction = true;

    // First launch shows the tutorial until the player finishes it.
    inline constexpr bool CfgDefaultTutorialCompleted = false;
}
