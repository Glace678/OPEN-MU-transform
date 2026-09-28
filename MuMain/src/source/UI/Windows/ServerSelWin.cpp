//*****************************************************************************
// Desc: implementation of the CServerSelWin class.
//*****************************************************************************

#include "stdafx.h"
#include "ServerSelWin.h"
#include "Core/Input/Input.h"
#include "UI/Legacy/UIMng.h"
#include "App/Platform/Windows/Local.h"
#include "Render/Textures/ZzzOpenglUtil.h"
#include "Render/Models/ZzzBMD.h"
#include "Engine/Object/ZzzObject.h"
#include "Engine/Object/ZzzCharacter.h"
#include "I18N/All.h"

#include "UI/Legacy/UIControls.h"

#include "UI/NewUI/NewUISystem.h"
#include "Network/Server/ServerListManager.h"
#include "Network/Login/LocalAutoLogin.h"
#include "Audio/DSPlaySound.h"

#define	SSW_GAP_WIDTH	28
#define	SSW_GAP_HEIGHT	5
#define	SSW_GB_POS_X	16
#define	SSW_GB_POS_Y	19




using namespace SEASON3A;

CServerSelWin::CServerSelWin()
{
}

CServerSelWin::~CServerSelWin()
{
}

void CServerSelWin::Create()
{
    CWin::Create(0, 0, -2);

    m_iSelectServerBtnIndex = -1;

    int i;

    for (i = 0; i < SSW_SERVER_G_MAX; ++i)
    {
        m_aServerGroupBtn[i].Create(SERVER_GROUP_BTN_WIDTH, SERVER_GROUP_BTN_HEIGHT, BITMAP_LOG_IN, 4, 2, 1, -1, 3);
        CWin::RegisterButton(&m_aServerGroupBtn[i]);
    }

    for (i = 0; i < SSW_SERVER_MAX; ++i)
    {
        m_aServerBtn[i].Create(SERVER_BTN_WIDTH, SERVER_BTN_HEIGHT, BITMAP_LOG_IN + 1, 3, 2, 1);
        CWin::RegisterButton(&m_aServerBtn[i]);
        m_aServerGauge[i].Create(160, 4, BITMAP_LOG_IN + 2);
    }

    SImgInfo aiiDeco[2] =
    {
        { BITMAP_LOG_IN + 3, 0, 0, 68, 95 },
        { BITMAP_LOG_IN + 3, 68, 0, 68, 95 }
    };
    m_aBtnDeco[0].Create(&aiiDeco[0], 8, 19);
    m_aBtnDeco[1].Create(&aiiDeco[1], 60, 19);

    SImgInfo aiiArrow[2] =
    {
        { BITMAP_LOG_IN + 3, 136, 0, 23, 29 },
        { BITMAP_LOG_IN + 3, 136, 30, 23, 29 }
    };
    m_aArrowDeco[0].Create(&aiiArrow[0], 1, 2);
    m_aArrowDeco[1].Create(&aiiArrow[1], 23, 2);

    SImgInfo aiiDescBg[WE_BG_MAX] =
    {
        { BITMAP_LOG_IN + 11, 0, 0, 4, 4 },
        { BITMAP_LOG_IN + 12, 0, 0, 512, 6 },
        { BITMAP_LOG_IN + 12, 0, 6, 512, 6 },
        { BITMAP_LOG_IN + 13, 0, 0, 3, 4 },
        { BITMAP_LOG_IN + 13, 3, 0, 3, 4 }
    };
    m_winDescription.Create(aiiDescBg, 1, 10);
    m_winDescription.SetLine(10);

    {
        static const DWORD registerBtnColors[3] =
        {
            CLRDW_BR_GRAY, CLRDW_BR_GRAY, CLRDW_WHITE
        };
        m_aBtnRegister.Create(86, 26, BITMAP_LOG_IN + 1, 3, 2, 1);
        m_aBtnRegister.SetText(I18N::Game::AccountRegister, const_cast<DWORD*>(registerBtnColors));
        CWin::RegisterButton(&m_aBtnRegister);
    }

    const float descriptionArtHeight = m_winDescription.GetHeight() / CSprite::ResolutionScaleY();
    CWin::SetSizeArt((SERVER_GROUP_BTN_WIDTH + SSW_GAP_WIDTH) * 2 + SERVER_BTN_WIDTH,
        SERVER_BTN_HEIGHT * SSW_SERVER_MAX + SSW_GAP_HEIGHT * 2 + SERVER_GROUP_BTN_HEIGHT
            + descriptionArtHeight);
}

void CServerSelWin::PreRelease()
{
    int i;

    for (i = 0; i < SSW_SERVER_MAX; ++i)
    {
        m_aServerGauge[i].Release();
    }

    for (i = 0; i < 2; ++i)
    {
        m_aBtnDeco[i].Release();
        m_aArrowDeco[i].Release();
    }

    m_winDescription.Release();
}

void CServerSelWin::SetPosition(int nXCoord, int nYCoord)
{
    SetPositionArt((float)nXCoord / CSprite::ResolutionScaleX(),
        (float)nYCoord / CSprite::ResolutionScaleY());
}

void CServerSelWin::SetPositionArt(float fArtX, float fArtY)
{
    CWin::SetPositionArt(fArtX, fArtY);

    const float rateX = CSprite::ResolutionScaleX();
    const float rateY = CSprite::ResolutionScaleY();

    const float nServerGBtnWidth = m_aServerGroupBtn[0].GetWidth() / rateX;
    const float nServerGBtnHeight = m_aServerGroupBtn[0].GetHeight() / rateY;
    const float nServerBtnWidth = m_aServerBtn[0].GetWidth() / rateX;
    const float nServerBtnHeight = m_aServerBtn[0].GetHeight() / rateY;
    const float nDescGgHeight = m_winDescription.GetHeight() / rateY;
    const float winW = CWin::GetWidth() / rateX;
    const float winH = CWin::GetHeight() / rateY;
    float nBtnPosY;
    int i;

    const float nServerGBtnBasePosY = fArtY + winH
        - (nServerGBtnHeight * 11 + SSW_GAP_HEIGHT * 2 + nDescGgHeight);
    const float nRServerGBtnPosX = fArtX + nServerGBtnWidth + nServerBtnWidth
        + (SSW_GAP_WIDTH * 2);

    int icntServreGroup = 0;
    m_aServerGroupBtn[icntServreGroup++].SetPositionArt(
        fArtX + (winW - nServerGBtnWidth) / 2,
        fArtY + winH - nServerGBtnHeight - SSW_GAP_HEIGHT - nDescGgHeight);

    for (i = 0; i < SSW_LEFT_SERVER_G_MAX; i++)
    {
        nBtnPosY = nServerGBtnBasePosY + nServerGBtnHeight * i;
        m_aServerGroupBtn[icntServreGroup++].SetPositionArt(fArtX, nBtnPosY);
    }

    for (i = 0; i < SSW_RIGHT_SERVER_G_MAX; i++)
    {
        nBtnPosY = nServerGBtnBasePosY + nServerGBtnHeight * i;
        m_aServerGroupBtn[icntServreGroup++].SetPositionArt(nRServerGBtnPosX, nBtnPosY);
    }

    m_winDescription.SetPositionArt(
        fArtX - ((m_winDescription.GetWidth() / rateX - winW) / 2),
        fArtY + winH - nDescGgHeight);

    m_aBtnDeco[0].SetPositionArt(
        m_aServerGroupBtn[1].GetXPos() / rateX,
        m_aServerGroupBtn[1].GetYPos() / rateY);
    m_aBtnDeco[1].SetPositionArt(
        m_aServerGroupBtn[SSW_LEFT_SERVER_G_MAX + 1].GetXPos() / rateX
            + nServerGBtnWidth,
        m_aServerGroupBtn[SSW_LEFT_SERVER_G_MAX + 1].GetYPos() / rateY);

    // Empty top-left corner of the window frame.
    m_aBtnRegister.SetPositionArt(fArtX + 16, fArtY + 12);
}

void CServerSelWin::SetServerBtnPosition()
{
    if (m_iSelectServerBtnIndex == -1)
        return;

    const float rateX = CSprite::ResolutionScaleX();
    const float rateY = CSprite::ResolutionScaleY();

    float nServerBtnPosX = m_aServerGroupBtn[1].GetXPos() / rateX
        + m_aServerGroupBtn[0].GetWidth() / rateX + SSW_GAP_WIDTH;

    float nServerBtnHeight = m_aServerBtn[0].GetHeight() / rateY;

    float nLServerGBtnHeightSum = m_aServerGroupBtn[1].GetHeight() / rateY * 10;

    float nServerBtnHeightSum = nServerBtnHeight * m_icntServer;

    float nLServerGBtnTop = m_aServerGroupBtn[1].GetYPos() / rateY;

    float nServerBtnBasePosY = nLServerGBtnHeightSum > nServerBtnHeightSum
        ? nLServerGBtnTop
        : nLServerGBtnTop - (nServerBtnHeightSum - nLServerGBtnHeightSum);

    for (int i = 0; i < m_pSelectServerGroup->GetServerSize(); i++)
    {
        m_aServerBtn[i].SetPositionArt(
            nServerBtnPosX, nServerBtnBasePosY + nServerBtnHeight * i);
        m_aServerGauge[i].SetPositionArt(
            m_aServerBtn[i].GetXPos() / rateX + SSW_GB_POS_X,
            m_aServerBtn[i].GetYPos() / rateY + SSW_GB_POS_Y);
    }
}

void CServerSelWin::SetArrowSpritePosition()
{
    if (m_iSelectServerBtnIndex == -1)
        return;

    const float rateX = CSprite::ResolutionScaleX();
    const float rateY = CSprite::ResolutionScaleY();
    const float groupBtnArtWidth = SERVER_GROUP_BTN_WIDTH;

    if ((m_iSelectServerBtnIndex >= 0) && (m_iSelectServerBtnIndex <= SSW_LEFT_SERVER_G_MAX))
    {
        m_aArrowDeco[0].SetPositionArt(
            m_aServerGroupBtn[m_iSelectServerBtnIndex].GetXPos() / rateX + groupBtnArtWidth,
            m_aServerGroupBtn[m_iSelectServerBtnIndex].GetYPos() / rateY);
    }
    else if ((m_iSelectServerBtnIndex > SSW_LEFT_SERVER_G_MAX) && (m_iSelectServerBtnIndex < SSW_SERVER_G_MAX))
    {
        m_aArrowDeco[1].SetPositionArt(
            m_aServerGroupBtn[m_iSelectServerBtnIndex].GetXPos() / rateX,
            m_aServerGroupBtn[m_iSelectServerBtnIndex].GetYPos() / rateY);
    }
}

void CServerSelWin::UpdateDisplay()
{
    m_pSelectServerGroup = NULL;
    m_icntServerGroup = 0;
    m_icntServer = 0;
    m_icntLeftServerGroup = 0;
    m_icntRightServerGroup = 0;
    m_bTestServerBtn = false;

    DWORD adwServerGBtnClr[BTN_IMG_MAX] =
    {
        CLRDW_BR_GRAY, CLRDW_BR_GRAY, CLRDW_WHITE, 0,
        CLRDW_BR_GRAY, CLRDW_BR_GRAY, CLRDW_WHITE, 0
    };

    DWORD adwServerBtnClr[4][4] =
    {
        { CLRDW_BR_GRAY, CLRDW_BR_GRAY, CLRDW_WHITE, 0 },
        { CLRDW_YELLOW, CLRDW_YELLOW, CLRDW_BR_YELLOW, 0 },
        { CLRDW_ORANGE, CLRDW_ORANGE, CLRDW_BR_ORANGE, 0 },
        { CLRDW_ORANGE, CLRDW_ORANGE, CLRDW_BR_ORANGE, 0 },
    };

    m_icntServerGroup = g_ServerListManager->GetServerGroupSize();

    if (m_icntServerGroup < 1)
    {
#if defined(_WIN32)
        if (FILE* fp = ::fopen("debug_ssw.txt", "w"))
        {
            ::fprintf(fp, "ZERO GROUPS total=%d win pos=%d,%d size=%d,%d\n",
                g_ServerListManager->GetTotalServer(),
                m_ptPos.x, m_ptPos.y, m_Size.cx, m_Size.cy);
            ::fclose(fp);
        }
#endif
        return;
    }

    CServerGroup* pServerGroup = NULL;

    g_ServerListManager->SetFirst();

    while (g_ServerListManager->GetNext(pServerGroup))
    {
        if (pServerGroup->m_iWidthPos == CServerGroup::SBP_CENTER)
        {
            if (m_bTestServerBtn == true)
                continue;

            m_aServerGroupBtn[0].SetText(pServerGroup->m_szName, adwServerGBtnClr);
            pServerGroup->m_iBtnPos = 0;
            m_bTestServerBtn = true;
        }
        else if (pServerGroup->m_iWidthPos == CServerGroup::SBP_LEFT)
        {
            if (m_icntLeftServerGroup >= SSW_LEFT_SERVER_G_MAX)
                continue;

            m_aServerGroupBtn[m_icntLeftServerGroup + 1].SetText(pServerGroup->m_szName, adwServerGBtnClr);
            pServerGroup->m_iBtnPos = m_icntLeftServerGroup + 1;

            m_icntLeftServerGroup++;
        }
        else if (pServerGroup->m_iWidthPos == CServerGroup::SBP_RIGHT)
        {
            if (m_icntRightServerGroup >= SSW_RIGHT_SERVER_G_MAX)
                continue;

            m_aServerGroupBtn[SSW_LEFT_SERVER_G_MAX + m_icntRightServerGroup + 1].SetText(pServerGroup->m_szName, adwServerGBtnClr);
            pServerGroup->m_iBtnPos = SSW_LEFT_SERVER_G_MAX + m_icntRightServerGroup + 1;

            m_icntRightServerGroup++;
        }
    }

    ShowServerGBtns();
    ShowDecoSprite();

    memset(m_szDescription, 0, sizeof(char) * SSW_DESC_LINE_MAX * SSW_DESC_ROW_MAX);

    if (m_iSelectServerBtnIndex != -1)
    {
        m_pSelectServerGroup = g_ServerListManager->GetServerGroupByBtnPos(m_iSelectServerBtnIndex);
    }

    if (m_pSelectServerGroup == NULL)
    {
#if defined(_WIN32)
        if (FILE* fp = ::fopen("debug_ssw.txt", "w"))
        {
            ::fprintf(fp, "win pos=%d,%d size=%d,%d selected=-1 groups=%d\n",
                m_ptPos.x, m_ptPos.y, m_Size.cx, m_Size.cy, m_icntServerGroup);
            for (int d = 0; d < SSW_SERVER_G_MAX; ++d)
            {
                if (m_aServerGroupBtn[d].IsShow())
                {
                    ::fprintf(fp, "group[%d] x=%d y=%d w=%d h=%d\n", d,
                        m_aServerGroupBtn[d].GetXPos(), m_aServerGroupBtn[d].GetYPos(),
                        m_aServerGroupBtn[d].GetWidth(), m_aServerGroupBtn[d].GetHeight());
                }
            }
            if (m_aBtnRegister.IsShow())
            {
                ::fprintf(fp, "register x=%d y=%d w=%d h=%d\n",
                    m_aBtnRegister.GetXPos(), m_aBtnRegister.GetYPos(),
                    m_aBtnRegister.GetWidth(), m_aBtnRegister.GetHeight());
            }
            ::fclose(fp);
        }
#endif
        return;
    }

    m_icntServer = m_pSelectServerGroup->GetServerSize();

    if (m_icntServer < 1)
        return;

    CServerInfo* pServerInfo = NULL;

    m_pSelectServerGroup->SetFirst();

    int icntServer = 0;
    while (m_pSelectServerGroup->GetNext(pServerInfo))
    {
        m_aServerBtn[icntServer].SetText(pServerInfo->m_bName, adwServerBtnClr[pServerInfo->m_byNonPvP]);
        m_aServerGauge[icntServer].SetValue(pServerInfo->m_iPercent, 100);
        icntServer++;
    }

    ::SeparateTextIntoLines(m_pSelectServerGroup->m_szDescription, m_szDescription[0], SSW_DESC_LINE_MAX, SSW_DESC_ROW_MAX);

    SetArrowSpritePosition();
    SetServerBtnPosition();
    ShowArrowSprite();
    ShowServerBtns();

#if defined(_WIN32)
    // TEMP DEBUG: dump live button rects for UI navigation verification.
    if (FILE* fp = nullptr; (fp = ::fopen("debug_ssw.txt", "w")) != nullptr)
    {
        ::fprintf(fp, "win pos=%d,%d size=%d,%d selected=%d groups=%d servers=%d\n",
            m_ptPos.x, m_ptPos.y, m_Size.cx, m_Size.cy,
            m_iSelectServerBtnIndex, m_icntServerGroup, m_icntServer);
        for (int d = 0; d < SSW_SERVER_G_MAX; ++d)
        {
            if (m_aServerGroupBtn[d].IsShow())
            {
                ::fprintf(fp, "group[%d] x=%d y=%d w=%d h=%d\n", d,
                    m_aServerGroupBtn[d].GetXPos(), m_aServerGroupBtn[d].GetYPos(),
                    m_aServerGroupBtn[d].GetWidth(), m_aServerGroupBtn[d].GetHeight());
            }
        }
        for (int d = 0; d < SSW_SERVER_MAX; ++d)
        {
            if (m_aServerBtn[d].IsShow())
            {
                ::fprintf(fp, "server[%d] x=%d y=%d w=%d h=%d\n", d,
                    m_aServerBtn[d].GetXPos(), m_aServerBtn[d].GetYPos(),
                    m_aServerBtn[d].GetWidth(), m_aServerBtn[d].GetHeight());
            }
        }
        if (m_aBtnRegister.IsShow())
        {
            ::fprintf(fp, "register x=%d y=%d w=%d h=%d\n",
                m_aBtnRegister.GetXPos(), m_aBtnRegister.GetYPos(),
                m_aBtnRegister.GetWidth(), m_aBtnRegister.GetHeight());
        }
        ::fclose(fp);
    }
#endif
}

void CServerSelWin::Show(bool bShow)
{
    CWin::Show(bShow);
    m_aBtnRegister.Show(bShow);
}

void CServerSelWin::ShowServerGBtns()
{
    int i;

    if (m_bTestServerBtn == true)
    {
        m_aServerGroupBtn[0].Show(CWin::m_bShow);
    }
    else
    {
        m_aServerGroupBtn[0].Show(false);
    }

    for (i = 1; i < m_icntLeftServerGroup + 1; i++)
    {
        m_aServerGroupBtn[i].Show(CWin::m_bShow);
    }
    for (; i < SSW_LEFT_SERVER_G_MAX; ++i)
    {
        m_aServerGroupBtn[i].Show(false);
    }

    for (i = SSW_LEFT_SERVER_G_MAX + 1; i < SSW_RIGHT_SERVER_G_MAX + 1 + m_icntRightServerGroup; i++)
    {
        m_aServerGroupBtn[i].Show(CWin::m_bShow);
    }
    for (; i < SSW_SERVER_G_MAX; i++)
    {
        m_aServerGroupBtn[i].Show(false);
    }
}

void CServerSelWin::ShowDecoSprite()
{
    if (m_icntLeftServerGroup > 0)
    {
        m_aBtnDeco[0].Show(CWin::m_bShow);
    }
    else
    {
        m_aBtnDeco[0].Show(false);
    }

    if (m_icntRightServerGroup > 0)
    {
        m_aBtnDeco[1].Show(CWin::m_bShow);
    }
    else
    {
        m_aBtnDeco[1].Show(false);
    }
}

void CServerSelWin::ShowArrowSprite()
{
    if ((m_iSelectServerBtnIndex >= 0) && (m_iSelectServerBtnIndex <= SSW_LEFT_SERVER_G_MAX))
    {
        m_aArrowDeco[0].Show(CWin::m_bShow);
        m_aArrowDeco[1].Show(false);
    }
    else if ((m_iSelectServerBtnIndex > SSW_LEFT_SERVER_G_MAX) && (m_iSelectServerBtnIndex < SSW_SERVER_G_MAX))
    {
        m_aArrowDeco[0].Show(false);
        m_aArrowDeco[1].Show(CWin::m_bShow);
    }
    else
    {
        m_aArrowDeco[0].Show(false);
        m_aArrowDeco[1].Show(false);
    }
}

void CServerSelWin::ShowServerBtns()
{
    if (m_iSelectServerBtnIndex == -1)
    {
        m_winDescription.Show(false);
        return;
    }

    int i;
    for (i = 0; i < m_icntServer; i++)
    {
        m_aServerBtn[i].Show(CWin::m_bShow);
        m_aServerGauge[i].Show(CWin::m_bShow);
    }
    for (; i < SSW_SERVER_MAX; i++)
    {
        m_aServerBtn[i].Show(false);
        m_aServerGauge[i].Show(false);
    }

    m_winDescription.Show(CWin::m_bShow);
}

bool CServerSelWin::CursorInWin(int nArea)
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

void CServerSelWin::UpdateWhileActive(double dDeltaTick)
{
    int i;

    if (m_aBtnRegister.IsClick())
    {
        PlayBuffer(SOUND_CLICK01);
        CUIMng::Instance().m_LoginWin.OpenAccountService(Network::Login::AccountPortalView::Register);
        return;
    }

    for (i = 0; i < SSW_SERVER_G_MAX; i++)
    {
        if (m_aServerGroupBtn[i].IsClick())
        {
            if (m_iSelectServerBtnIndex != -1)
            {
                m_aServerGroupBtn[m_iSelectServerBtnIndex].SetCheck(false);
            }

            m_aServerGroupBtn[i].SetCheck(true);
            m_iSelectServerBtnIndex = i;

            SocketClient->ToConnectServer()->SendServerListRequest();
        }
    }

    if (m_pSelectServerGroup == NULL)
        return;

    CServerInfo* pServerInfo = NULL;
    for (i = 0; i < m_icntServer; i++)
    {
        if (m_aServerBtn[i].IsClick())
        {
            pServerInfo = m_pSelectServerGroup->GetServerInfo(i);

            if (pServerInfo == NULL)
                return;

            if (pServerInfo->m_iPercent < 100)
            {
                CUIMng::Instance().HideWin(this);

                // Drive the local auto-login state machine on the manual path too,
                // otherwise it stays in SelectingServer and the account/password
                // window appears after choosing the (localhost) server.
                Network::Login::LocalAutoLogin::Instance().ServerSelected();
                SocketClient->ToConnectServer()->SendConnectionInfoRequest(static_cast<uint16_t>(pServerInfo->m_iConnectIndex));
                g_pSystemLogBox->AddText(I18N::Game::ConnectingToTheServer, SEASON3B::TYPE_SYSTEM_MESSAGE);
                g_pSystemLogBox->AddText(I18N::Game::PleaseWait, SEASON3B::TYPE_SYSTEM_MESSAGE);

                //if (m_pSelectServerGroup->m_iSequence == 0)
                //{
                //    bTestServer = true;
                //}

                g_ServerListManager->SetSelectServerInfo(m_pSelectServerGroup->m_szName, pServerInfo->m_iIndex, pServerInfo->m_byNonPvP);

                break;
            }
            else if (pServerInfo->m_iPercent < 128)
            {
                CUIMng::Instance().PopUpMsgWin(MESSAGE_SERVER_BUSY);
            }
        }
    }
}

void CServerSelWin::RenderControls()
{
    int i = 0;

    g_pRenderText->SetFont(g_hFixFont);
    g_pRenderText->SetTextColor(CLRDW_WHITE);
    g_pRenderText->SetBgColor(0);

    CWin::RenderButtons();

    if (m_pSelectServerGroup != NULL)
    {
        for (i = 0; i < m_icntServer; i++)
        {
            m_aServerGauge[i].Render();
        }

        if (m_pSelectServerGroup->m_bPvPServer == true)
        {
            g_pRenderText->SetTextColor(ARGB(255, 255, 255, 255));
            g_pRenderText->RenderText(90, 164 - 60, I18N::Game::SinceHelheimServer);
            g_pRenderText->RenderText(90, 164 - 45, I18N::Game::TendsToBeCrowded);
            g_pRenderText->RenderText(90, 164 - 30, I18N::Game::WeRecommendThatYouUseOtherServers);
        }
    }
}
