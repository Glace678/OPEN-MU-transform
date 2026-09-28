//*****************************************************************************
// File: CharInfoBalloon.cpp
//*****************************************************************************

#include "stdafx.h"
#include "CharInfoBalloon.h"
#include "CharacterBalloonLayout.h"
#include "Render/Textures/ZzzOpenglUtil.h"
#include "Engine/Object/ZzzInterface.h"
#include "UI/Legacy/UIControls.h"
#include "CharacterManager.h"
#include "I18N/All.h"

#include <algorithm>
#include <array>
#include <cwchar>
#include <cmath>

#include "Camera/CameraProjection.h"

namespace
{
    template <std::size_t N>
    void CopyWideString(wchar_t (&destination)[N], const wchar_t* source)
    {
        if (source == nullptr)
        {
            destination[0] = L'\0';
            return;
        }

        std::wcsncpy(destination, source, N - 1);
        destination[N - 1] = L'\0';
    }

    struct GuildStatusText
    {
        std::uint8_t status;
        int textIndex;
    };

    constexpr std::array<GuildStatusText, 5> kGuildStatusTexts{ {
        {   0, 1330 },
        {  32, 1302 },
        {  64, 1301 },
        { 128, 1300 },
        { 255, 488 },
    } };

    DWORD ResolveNameColor(std::uint8_t controlCode)
    {
        if (controlCode & CTLCODE_01BLOCKCHAR)
            return ARGB(255, 0, 255, 255);
        if (controlCode & (CTLCODE_02BLOCKITEM | CTLCODE_10ACCOUNT_BLOCKITEM))
            return CLRDW_BR_ORANGE;
        if (controlCode & CTLCODE_04FORTV)
            return CLRDW_WHITE;
        if (controlCode & (CTLCODE_08OPERATOR | CTLCODE_20OPERATOR))
            return ARGB(255, 255, 0, 0);

        return CLRDW_WHITE;
    }

    int ResolveGuildTextIndex(std::uint8_t guildStatus)
    {
        const auto it = std::lower_bound(
            kGuildStatusTexts.begin(),
            kGuildStatusTexts.end(),
            guildStatus,
            [](const GuildStatusText& entry, std::uint8_t status) { return entry.status < status; });

        return (it != kGuildStatusTexts.end() && it->status == guildStatus) ? it->textIndex : 0;
    }
}

CCharInfoBalloon::CCharInfoBalloon() : m_pCharInfo(nullptr)
{
    I18N::RegisterLocaleObserver(&CCharInfoBalloon::OnLocaleChanged, this);
}

CCharInfoBalloon::~CCharInfoBalloon()
{
    I18N::UnregisterLocaleObserver(&CCharInfoBalloon::OnLocaleChanged, this);
}

void CCharInfoBalloon::OnLocaleChanged(void* ctx) noexcept
{
    auto* self = static_cast<CCharInfoBalloon*>(ctx);
    if (self->m_pCharInfo != nullptr)
    {
        self->SetInfo();
    }
}

void CCharInfoBalloon::Create(CHARACTER* pCharInfo)
{
    CSprite::Create(118, 54, BITMAP_LOG_IN + 7, 0, nullptr, 59, 54);

    m_pCharInfo = pCharInfo;
    m_dwNameColor = 0;
    std::fill(std::begin(m_szName), std::end(m_szName), L'\0');
    std::fill(std::begin(m_szGuild), std::end(m_szGuild), L'\0');
    std::fill(std::begin(m_szClass), std::end(m_szClass), L'\0');
}

void CCharInfoBalloon::Render()
{
    if (m_pCharInfo == nullptr || !CSprite::m_bShow)
        return;

    UpdateLayout();
    CSprite::Render();
    RenderLines();
}

void CCharInfoBalloon::MeasureLines(int& maxWidth, int& maxHeight)
{
    g_pRenderText->SetFont(g_hFixFont);
    SIZE textSize{};
    maxWidth = maxHeight = 0;
    for (const auto* text : { m_szName, m_szGuild, m_szClass })
    {
        GetTextExtentPoint32(g_pRenderText->GetFontDC(), text, lstrlen(text), &textSize);
        maxWidth = std::max(maxWidth, static_cast<int>(textSize.cx));
        maxHeight = std::max(maxHeight, static_cast<int>(textSize.cy));
    }
}

int CCharInfoBalloon::NearestNeighborDistance(int centerArtX) const
{
    constexpr float LabelAnchorHeight = 350.f;
    int distance = static_cast<int>(WindowWidth);
    for (int slot = 0; slot < MAX_CHARACTERS_PER_ACCOUNT; ++slot)
    {
        const auto& character = CharactersClient[slot];
        if (&character == m_pCharInfo || !character.Object.Live) continue;
        vec3_t position;
        VectorCopy(character.Object.Position, position);
        position[2] += LabelAnchorHeight;
        int x, y;
        CameraProjection::WorldToScreen(g_Camera, position, &x, &y);
        distance = std::min(distance, static_cast<int>(std::abs(x - centerArtX) * CSprite::ResolutionScaleX()));
    }
    return distance;
}

void CCharInfoBalloon::UpdateLayout()
{
    constexpr float LabelAnchorHeight = 350.f;
    vec3_t afPos;
    VectorCopy(m_pCharInfo->Object.Position, afPos);
    afPos[2] += LabelAnchorHeight;
    int nPosX, nPosY;
    CameraProjection::WorldToScreen(g_Camera, afPos, &nPosX, &nPosY);
    int textWidth, textHeight;
    MeasureLines(textWidth, textHeight);
    const auto layout = UI::CharacterSelection::LayoutBalloon(
        static_cast<int>(WindowWidth), static_cast<int>(WindowHeight),
        CSprite::ResolutionScaleX(), CSprite::ResolutionScaleY(),
        int(nPosX * CSprite::ResolutionScaleX()), int(nPosY * CSprite::ResolutionScaleY()),
        textWidth, textHeight, NearestNeighborDistance(nPosX));
    m_textTop = layout.textTop;
    m_lineHeight = layout.lineHeight;
    CSprite::SetSize(layout.width, layout.height);
    m_fDatumX = layout.width / (2.f * CSprite::ResolutionScaleX());
    m_fDatumY = layout.height / CSprite::ResolutionScaleY();
    CSprite::SetPosition(layout.centerX, layout.bottomY);
}

void CCharInfoBalloon::RenderLines()
{
    g_pRenderText->SetFont(g_hFixFont);
    g_pRenderText->SetBgColor(0);

    const int spriteX = CSprite::GetXPos();
    const int spriteY = CSprite::GetYPos();
    const int spriteW = CSprite::GetWidth();

    const int nTextPosX = int(spriteX / g_fScreenRate_x);

    const wchar_t* lines[] = { m_szName, m_szGuild, m_szClass };
    const DWORD colors[] = { m_dwNameColor, CLRDW_WHITE, CLRDW_BR_ORANGE };
    constexpr int horizontalPadding = 6;
    for (size_t line = 0; line < std::size(lines); ++line)
    {
        g_pRenderText->SetTextColor(colors[line]);
        g_pRenderText->RenderText(nTextPosX + horizontalPadding,
            int((spriteY + m_textTop + line * m_lineHeight) / g_fScreenRate_y),
            lines[line], spriteW / g_fScreenRate_x - horizontalPadding * 2,
            m_lineHeight / g_fScreenRate_y, RT3_SORT_CENTER_FIT);
    }
}

void CCharInfoBalloon::SetInfo()
{
    if (m_pCharInfo == nullptr)
        return;

    if (!m_pCharInfo->Object.Live)
    {
        CSprite::m_bShow = false;
        return;
    }

    CSprite::m_bShow = true;

    m_dwNameColor = ResolveNameColor(m_pCharInfo->CtlCode);

    CopyWideString(m_szName, m_pCharInfo->ID);

    const int guildTextIndex = ResolveGuildTextIndex(m_pCharInfo->GuildStatus);
    mu_swprintf_s(m_szGuild, L"(%ls)", I18N::Game::Lookup(guildTextIndex));
    mu_swprintf_s(m_szClass, L"%ls %d",
        gCharacterManager.GetCharacterClassText(m_pCharInfo->Class),
        m_pCharInfo->Level);
}
