#pragma once

#include <string>
#include <string_view>

namespace UI::Notices
{
    // Translate only recognized stock server announcements for the active UI
    // locale. Custom announcements and player-authored text remain verbatim.
    std::wstring LocalizeServerNotice(std::wstring_view text);
}
