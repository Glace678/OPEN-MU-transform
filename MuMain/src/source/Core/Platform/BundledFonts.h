// Curated UI fonts shipped in the client's ./fonts directory.
//
// Single source of truth shared by both platforms: the Linux GdiText shim
// (Core/Platform/GdiText.cpp) resolves a chosen face by reading the bundled file
// directly, and the Windows startup (App/Platform/Windows/Winmain.cpp) privately
// registers the same files with GDI (AddFontResourceEx) so the face resolves by
// name even without a system-wide install. The `family` names match what the
// options UI lists (UI/NewUI/Options/NewUIOptionWindow.cpp) and what config stores;
// locale defaults also select the bundled JP/TC faces. Paths are relative to
// the executable directory on desktop and the extracted data root on mobile.
#pragma once

struct BundledFont
{
    const char* family;
    const char* regular;
    const char* bold;
};

inline constexpr BundledFont kBundledFonts[] = {
    { "Liberation Sans", "fonts/LiberationSans-Regular.ttf", "fonts/LiberationSans-Bold.ttf" },
    { "DejaVu Sans",     "fonts/DejaVuSans.ttf",             "fonts/DejaVuSans-Bold.ttf" },
    { "Noto Sans CJK SC", "fonts/NotoSansCJKsc-Regular.otf", "fonts/NotoSansCJKsc-Bold.otf" },
    { "Noto Sans CJK JP", "fonts/NotoSansCJKjp-Regular.otf", "fonts/NotoSansCJKjp-Bold.otf" },
    { "Noto Sans CJK TC", "fonts/NotoSansCJKtc-Regular.otf", "fonts/NotoSansCJKtc-Bold.otf" },
};
