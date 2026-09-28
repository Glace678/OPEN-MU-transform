//*****************************************************************************
// File: OptionWin.cpp
//*****************************************************************************

#include "stdafx.h"
#include "UI/Windows/OptionWin.h"
#include "Core/Input/Input.h"
#include "UI/Legacy/UIMng.h"
#include "Render/Models/ZzzBMD.h"
#include "Engine/Object/ZzzInfomation.h"
#include "Engine/Object/ZzzObject.h"
#include "Engine/Object/ZzzCharacter.h"
#include "Engine/Object/ZzzInterface.h"
#include "Scenes/SceneCore.h"
#include "Audio/DSPlaySound.h"
#include "UI/Legacy/UIControls.h"
#include "UI/NewUI/NewUISystem.h"
#include "I18N/All.h"

#define	OW_BTN_GAP		25
#define	OW_SLD_GAP		48




COptionWin::COptionWin()
{
}

COptionWin::~COptionWin()
{
}

void COptionWin::Create()
{
    CInput& rInput = CInput::Instance();
    // Modal dim backdrop sized to the art canvas; hit-test size stays device.
    CWin::Create(REFERENCE_WIDTH, REFERENCE_HEIGHT);
    m_Size.cx = int(REFERENCE_WIDTH * CSprite::ResolutionScaleX());
    m_Size.cy = int(REFERENCE_HEIGHT * CSprite::ResolutionScaleY());

    SImgInfo aiiBack[WE_BG_MAX] =
    {
        { BITMAP_SYS_WIN, 0, 0, 128, 128 },
        { BITMAP_OPTION_WIN, 0, 0, 213, 65 },
        { BITMAP_SYS_WIN + 2, 0, 0, 213, 43 },
        { BITMAP_SYS_WIN + 3, 0, 0, 5, 8 },
        { BITMAP_SYS_WIN + 4, 0, 0, 5, 8 }
    };
    m_winBack.Create(aiiBack, 1, 30);
    m_winBack.SetLine(30);

    for (int i = 0; i <= OW_BTN_SLIDE_HELP; ++i)
    {
        m_aBtn[i].Create(16, 16, BITMAP_CHECK_BTN, 2, 0, 0, -1, 1, 1, 1);
        CWin::RegisterButton(&m_aBtn[i]);
    }

    DWORD adwBtnClr[4] = { CLRDW_BR_GRAY, CLRDW_BR_GRAY, CLRDW_WHITE, 0 };
    m_aBtn[OW_BTN_CLOSE].Create(108, 30, BITMAP_TEXT_BTN, 4, 2, 1);
    m_aBtn[OW_BTN_CLOSE].SetText(I18N::Game::Close388, adwBtnClr);
    CWin::RegisterButton(&m_aBtn[OW_BTN_CLOSE]);

    SImgInfo iiThumb = { BITMAP_SLIDER, 0, 0, 13, 13 };
    SImgInfo iiBack = { BITMAP_SLIDER + 2, 0, 0, 98, 13 };
    SImgInfo iiGauge = { BITMAP_SLIDER + 1, 0, 0, 4, 7 };
    RECT rcGauge = { 3, 3, 95, 10 };

    for (int i = 0; i < OW_SLD_MAX; ++i)
        m_aSlider[i].Create(&iiThumb, &iiBack, &iiGauge, &rcGauge);

    m_aSlider[OW_SLD_EFFECT_VOL].SetSlideRange(9);
    m_aSlider[OW_SLD_RENDER_LV].SetSlideRange(4);

    SetPosition((rInput.GetScreenWidth() - m_winBack.GetWidth()) / 2,
        (rInput.GetScreenHeight() - m_winBack.GetHeight()) / 2);

    UpdateDisplay();
}

void COptionWin::PreRelease()
{
    m_winBack.Release();
    for (int i = 0; i < OW_SLD_MAX; ++i)
        m_aSlider[i].Release();
}

void COptionWin::SetPosition(int nXCoord, int nYCoord)
{
    // Window base is device-space; all offsets/sizes are authored art units.
    const float backArtX = (float)nXCoord / CSprite::ResolutionScaleX();
    const float backArtY = (float)nYCoord / CSprite::ResolutionScaleY();
    m_winBack.SetPositionArt(backArtX, backArtY);

    const float winArtX = m_winBack.GetXPos() / CSprite::ResolutionScaleX();
    const float winArtY = m_winBack.GetYPos() / CSprite::ResolutionScaleY();
    const float winArtW = m_winBack.GetWidth() / CSprite::ResolutionScaleX();

    float nBtnPosX = winArtX + 52.0f;
    float nBtnGap = (float)OW_BTN_GAP
        + m_aBtn[0].GetHeight() / CSprite::ResolutionScaleY();
    float nBtnPosBaseTop = winArtY + 52.0f;
    for (int i = 0; i <= OW_BTN_SLIDE_HELP; ++i)
        m_aBtn[i].SetPositionArt(nBtnPosX, nBtnPosBaseTop + i * nBtnGap);

    m_aBtn[OW_BTN_CLOSE].SetPositionArt(
        winArtX + (winArtW - m_aBtn[OW_BTN_CLOSE].GetWidth()
            / CSprite::ResolutionScaleX()) / 2.0f,
        winArtY + 301.0f);

    float nSldGap = (float)OW_SLD_GAP
        + m_aSlider[0].GetHeight() / CSprite::ResolutionScaleY();
    float nSldPosBaseTop = m_aBtn[OW_BTN_SLIDE_HELP].GetYPos()
        / CSprite::ResolutionScaleY()
        + m_aBtn[0].GetHeight() / CSprite::ResolutionScaleY() + OW_SLD_GAP;
    for (int i = 0; i < OW_SLD_MAX; ++i)
        m_aSlider[i].SetPositionArt(nBtnPosX, nSldPosBaseTop + i * nSldGap);
}

void COptionWin::Show(bool bShow)
{
    CWin::Show(bShow);

    m_winBack.Show(bShow);
    for (int i = 0; i < OW_BTN_MAX; ++i)
        m_aBtn[i].Show(bShow);
    for (int i = 0; i < OW_SLD_MAX; ++i)
        m_aSlider[i].Show(bShow);
}

bool COptionWin::CursorInWin(int nArea)
{
    if (!CWin::m_bShow)
        return false;

    switch (nArea)
    {
    case WA_MOVE:
        return false;
    }

    return CWin::CursorInWin(nArea);
}

void COptionWin::UpdateDisplay()
{
    m_aBtn[OW_BTN_AUTO_ATTACK].SetCheck(g_pOption->IsAutoAttack());
    m_aBtn[OW_BTN_WHISPER_ALARM].SetCheck(g_pOption->IsWhisperSound());
    m_aBtn[OW_BTN_SLIDE_HELP].SetCheck(g_pOption->IsSlideHelp());
    m_aSlider[OW_SLD_EFFECT_VOL].SetSlidePos(g_pOption->GetVolumeLevel());
    m_aSlider[OW_SLD_RENDER_LV].SetSlidePos(g_pOption->GetRenderLevel());
}

void COptionWin::UpdateWhileActive(double dDeltaTick)
{
    for (int i = 0; i < OW_SLD_MAX; ++i)
        m_aSlider[i].Update(dDeltaTick);

    if (m_aBtn[OW_BTN_AUTO_ATTACK].IsClick())
    {
        g_pOption->SetAutoAttack(m_aBtn[OW_BTN_AUTO_ATTACK].IsCheck());
    }
    else if (m_aBtn[OW_BTN_WHISPER_ALARM].IsClick())
    {
        g_pOption->SetWhisperSound(m_aBtn[OW_BTN_WHISPER_ALARM].IsCheck());
    }
    else if (m_aBtn[OW_BTN_SLIDE_HELP].IsClick())
    {
        g_pOption->SetSlideHelp(m_aBtn[OW_BTN_SLIDE_HELP].IsCheck());
    }
    else if (m_aBtn[OW_BTN_CLOSE].IsClick())
    {
        CUIMng::Instance().HideWin(this);
        CUIMng::Instance().SetSysMenuWinShow(false);
    }
    else if (m_aSlider[OW_SLD_EFFECT_VOL].GetState())
    {
        int nSlidePos = m_aSlider[OW_SLD_EFFECT_VOL].GetSlidePos();

        if (g_pOption->GetVolumeLevel() != nSlidePos)
        {
            g_pOption->SetVolumeLevel(nSlidePos);
            ::SetEffectVolumeLevel(g_pOption->GetVolumeLevel());
        }
    }
    else if (m_aSlider[OW_SLD_RENDER_LV].GetState())
    {
        int nSlidePos = m_aSlider[OW_SLD_RENDER_LV].GetSlidePos();
        if (g_pOption->GetRenderLevel() != nSlidePos)
        {
            g_pOption->SetRenderLevel(nSlidePos);
        }
    }
    else if (CInput::Instance().IsKeyDown(VK_ESCAPE))
    {
        ::PlayBuffer(SOUND_CLICK01);
        CUIMng::Instance().HideWin(this);
        CUIMng::Instance().SetSysMenuWinShow(false);
    }
}

void COptionWin::RenderControls()
{
    m_winBack.Render();

    g_pRenderText->SetFont(g_hFixFont);
    g_pRenderText->SetTextColor(CLRDW_WHITE);
    g_pRenderText->SetBgColor(0);
    g_pRenderText->RenderText(int(m_winBack.GetXPos() / g_fScreenRate_x),
        int(m_winBack.GetYPos() / g_fScreenRate_y) + 10,
        I18N::Game::Option385, m_winBack.GetWidth() / g_fScreenRate_x, 0, RT3_SORT_CENTER);

    const wchar_t* apszBtnText[3] =
    { I18N::Game::AutomaticAttack, I18N::Game::BeepSoundForWhispering, I18N::Game::SlideHelp };
    for (int i = 0; i <= OW_BTN_SLIDE_HELP; ++i)
    {
        g_pRenderText->RenderText(int(m_aBtn[i].GetXPos() / g_fScreenRate_x) + 24,
            int(m_aBtn[i].GetYPos() / g_fScreenRate_y) + 4, apszBtnText[i]);
    }

    int nTextPosY;
    const wchar_t* apszSldText[OW_SLD_MAX] = { I18N::Game::Volume, I18N::Game::EffectLimitation };
    int anVal[OW_SLD_MAX] = { g_pOption->GetVolumeLevel(), g_pOption->GetRenderLevel() * 2 + 5 };

    wchar_t szVal[3];

    for (int i = 0; i < OW_SLD_MAX; ++i)
    {
        nTextPosY = int(m_aSlider[i].GetYPos() / g_fScreenRate_y) - 18;
        g_pRenderText->RenderText(int(m_aSlider[i].GetXPos() / g_fScreenRate_x), nTextPosY, apszSldText[i]);

        ::_itow(anVal[i], szVal, 10);
        g_pRenderText->RenderText(int(m_aSlider[i].GetXPos() / g_fScreenRate_x) + 85, nTextPosY, szVal);

        m_aSlider[i].Render();
    }

    CWin::RenderButtons();
}