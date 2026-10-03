//*****************************************************************************
// File: UIMng.cpp
//*****************************************************************************

#include "stdafx.h"
#include "UIMng.h"
#include "Core/Input/Input.h"
#include "Audio/DSPlaySound.h"
#include "Render/Sprites/Sprite.h"
#include "UI/Widgets/GaugeBar.h"
#include "Render/Textures/ZzzOpenglUtil.h"
#include "Engine/Object/ZzzInfomation.h"
#include "Render/Models/ZzzBMD.h"
#include "Engine/Object/ZzzObject.h"
#include "Engine/Object/ZzzCharacter.h"
#include "Engine/Object/ZzzInterface.h"

#include "UIControls.h"
#include "Network/Server/ServerListManager.h"

#ifdef _EDITOR
#include "../MuEditor/Core/MuEditorCore.h"
#endif

#define	DOCK_EXTENT		10

//#define	UIM_TS_BG_BLACK		0
#define	UIM_TS_BACK0		0
#define	UIM_TS_BACK1		1
#define	UIM_TS_121518		3
#define UIM_TS_BACK2		5
#define UIM_TS_BACK3		6
#define UIM_TS_BACK4		7
#define UIM_TS_BACK5		8
#define UIM_TS_BACK6		9
#define UIM_TS_BACK7		10
#define UIM_TS_BACK8		11
#define UIM_TS_BACK9		12
#define	UIM_TS_MAX			13

CUIMng::CUIMng()
{
    m_asprTitle = NULL;
    m_pgbLoding = NULL;
    m_pLoadingScene = NULL;
}

CUIMng::~CUIMng()
{
}

CUIMng& CUIMng::Instance()
{
    static CUIMng s_UIMng;
    return s_UIMng;
}

void CUIMng::CreateTitleSceneUI()
{
    ReleaseTitleSceneUI();

    // The title artwork is one 1280x1024 composite canvas (top/bottom strips
    // are 400-wide pieces, the middle art is 512-wide pieces). Map that whole
    // canvas into the 640x480 reference art space once; the resolution scale
    // then takes it crisply to the actual window size.
    constexpr float kCanvasSX = (float)REFERENCE_WIDTH / 1280.0f;  // 0.5
    constexpr float kCanvasSY = (float)REFERENCE_HEIGHT / 1024.0f; // 0.46875

    m_asprTitle = new CSprite[UIM_TS_MAX];

    m_asprTitle[UIM_TS_BACK0].Create(int(400 * kCanvasSX), int(69 * kCanvasSY),
        BITMAP_TITLE);
    m_asprTitle[UIM_TS_BACK0].SetPositionArt(0, 0);

    m_asprTitle[UIM_TS_BACK1].Create(int(400 * kCanvasSX), int(69 * kCanvasSY),
        BITMAP_TITLE + 1);
    m_asprTitle[UIM_TS_BACK1].SetPositionArt(400 * kCanvasSX, 0);

    m_asprTitle[UIM_TS_BACK2].Create(int(400 * kCanvasSX), int(100 * kCanvasSY),
        BITMAP_TITLE + 6);
    m_asprTitle[UIM_TS_BACK2].SetPositionArt(0, 500 * kCanvasSY);

    m_asprTitle[UIM_TS_BACK3].Create(int(400 * kCanvasSX), int(100 * kCanvasSY),
        BITMAP_TITLE + 7);
    m_asprTitle[UIM_TS_BACK3].SetPositionArt(400 * kCanvasSX, 500 * kCanvasSY);

    m_asprTitle[UIM_TS_BACK4].Create(int(512 * kCanvasSX), int(512 * kCanvasSY),
        BITMAP_TITLE + 8);
    m_asprTitle[UIM_TS_BACK4].SetPositionArt(0, 119 * kCanvasSY);

    m_asprTitle[UIM_TS_BACK5].Create(int(512 * kCanvasSX), int(512 * kCanvasSY),
        BITMAP_TITLE + 9);
    m_asprTitle[UIM_TS_BACK5].SetPositionArt(512 * kCanvasSX, 119 * kCanvasSY);

    m_asprTitle[UIM_TS_BACK6].Create(int(256 * kCanvasSX), int(512 * kCanvasSY),
        BITMAP_TITLE + 10);
    m_asprTitle[UIM_TS_BACK6].SetPositionArt(1024 * kCanvasSX, 119 * kCanvasSY);

    m_asprTitle[UIM_TS_BACK7].Create(int(512 * kCanvasSX), int(223 * kCanvasSY),
        BITMAP_TITLE + 11);
    m_asprTitle[UIM_TS_BACK7].SetPositionArt(0, (512 + 119) * kCanvasSY);

    m_asprTitle[UIM_TS_BACK8].Create(int(512 * kCanvasSX), int(223 * kCanvasSY),
        BITMAP_TITLE + 12);
    m_asprTitle[UIM_TS_BACK8].SetPositionArt(512 * kCanvasSX,
        (512 + 119) * kCanvasSY);

    m_asprTitle[UIM_TS_BACK9].Create(int(256 * kCanvasSX), int(223 * kCanvasSY),
        BITMAP_TITLE + 13);
    m_asprTitle[UIM_TS_BACK9].SetPositionArt(1024 * kCanvasSX,
        (512 + 119) * kCanvasSY);


    m_asprTitle[UIM_TS_121518].Create(int(256 * kCanvasSX),
        int(206 * kCanvasSY), BITMAP_TITLE + 3);
    m_asprTitle[UIM_TS_121518].SetPositionArt(544 * kCanvasSX,
        60 * kCanvasSY);

    m_pgbLoding = new CGaugeBar;

    RECT rc = { 0, 0, int(656 * kCanvasSX), int(15 * kCanvasSY) };
    m_pgbLoding->Create(int(4 * kCanvasSX), int(15 * kCanvasSY),
        BITMAP_TITLE + 5, &rc);

    m_pgbLoding->SetPositionArt(72 * kCanvasSX, 540 * kCanvasSY);
    for (int i = 0; i < UIM_TS_MAX; ++i)
    {
        m_asprTitle[i].Show();
    }
    m_pgbLoding->Show();
    m_asprTitle[UIM_TS_121518].Show(false);
    m_nScene = UIM_SCENE_TITLE;
}

void CUIMng::ReleaseTitleSceneUI()
{
    SAFE_DELETE_ARRAY(m_asprTitle);
    SAFE_DELETE(m_pgbLoding);

    m_nScene = UIM_SCENE_NONE;
}

void CUIMng::RenderTitleSceneUI(HDC hDC, DWORD dwNow, DWORD dwTotal)
{
    ::BeginOpengl();
    ::ClearColorAndDepthBuffers();
    ::BeginBitmap();

    for (int i = 0; i < UIM_TS_MAX; ++i)
    {
        if (i == 2)
            continue;
        m_asprTitle[i].Render();
    }

    m_pgbLoding->SetValue(dwNow, dwTotal);
    m_pgbLoding->Render();

    ::EndBitmap();
    ::EndOpengl();
    ::FlushGL();
#ifdef _EDITOR
    // Always render ImGui (shows "Open Editor" button when closed, or full UI when open)
    g_MuEditorCore.Render();
#endif
    PlatformSwapBuffers();
}

void CUIMng::Create()
{
    m_bCursorOnUI = false;
    m_bBlockCharMove = false;
    m_bWinActive = false;
    m_nScene = UIM_SCENE_NONE;

    return;
}

void CUIMng::RemoveWinList()
{
    CWin* pWin;
    while (m_WinList.GetCount())
    {
        pWin = (CWin*)m_WinList.RemoveHead();
        pWin->Release();
    }
}

void CUIMng::Release()
{
    RemoveWinList();

    m_CharInfoBalloonMng.Release();

    m_nScene = UIM_SCENE_NONE;
}

void CUIMng::CreateLoginScene()
{
    RemoveWinList();

    m_CharInfoBalloonMng.Release();

    CInput& rInput = CInput::Instance();
    const float rateX = CSprite::ResolutionScaleX();
    const float rateY = CSprite::ResolutionScaleY();
    // 800x600-authored constants mapped to the 640x480 art canvas.
    constexpr float kSceneCanvas = (float)REFERENCE_WIDTH / 800.0f;

    m_MsgWin.Create();
    m_WinList.AddHead(&m_MsgWin);
    m_MsgWin.SetPosition(
        int((REFERENCE_WIDTH - 352) / 2.0f * rateX),
        int((REFERENCE_HEIGHT - 113) / 2.0f * rateY));

    m_SysMenuWin.Create();
    m_WinList.AddHead(&m_SysMenuWin);

    m_OptionWin.Create();
    m_WinList.AddHead(&m_OptionWin);

    m_LoginMainWin.Create();
    m_WinList.AddHead(&m_LoginMainWin);

    const float artBaseY = 567.0f * kSceneCanvas;
    m_LoginMainWin.SetPositionArt(30.0f * kSceneCanvas,
        artBaseY - m_LoginMainWin.GetHeight() / rateY - 11.0f * kSceneCanvas);

    m_ServerSelWin.Create();
    m_WinList.AddHead(&m_ServerSelWin);
    m_ServerSelWin.SetPosition(
        int((REFERENCE_WIDTH - m_ServerSelWin.GetWidth() / rateX) / 2.0f * rateX),
        int((REFERENCE_HEIGHT - m_ServerSelWin.GetHeight() / rateY) / 2.0f * rateY));

    m_LoginWin.Create();
    m_WinList.AddHead(&m_LoginWin);
    // Vertically centered in the art canvas.
    m_LoginWin.SetPosition(
        int((REFERENCE_WIDTH - m_LoginWin.GetWidth() / rateX) / 2.0f * rateX),
        int((REFERENCE_HEIGHT - m_LoginWin.GetHeight() / rateY) / 2.0f * rateY));

    m_RegisterWin.Create();
    m_WinList.AddHead(&m_RegisterWin);
    m_RegisterWin.SetPosition(
        int((REFERENCE_WIDTH - m_RegisterWin.GetWidth() / rateX) / 2.0f * rateX),
        int((REFERENCE_HEIGHT - m_RegisterWin.GetHeight() / rateY) / 2.0f * rateY));
    m_RegisterWin.Show(FALSE);  // created hidden; opened from the server/login windows

    m_PasswordServiceWin.Create();
    m_WinList.AddHead(&m_PasswordServiceWin);
    m_PasswordServiceWin.SetPosition(
        int((REFERENCE_WIDTH - m_PasswordServiceWin.GetWidth() / rateX) / 2.0f * rateX),
        int((REFERENCE_HEIGHT - m_PasswordServiceWin.GetHeight() / rateY) / 2.0f * rateY));
    m_PasswordServiceWin.Show(FALSE);  // opened from the login window

    m_CreditWin.Create();
    m_WinList.AddHead(&m_CreditWin);

    m_bSysMenuWinShow = false;
    m_nScene = UIM_SCENE_LOGIN;
}

void CUIMng::CreateCharacterScene()
{
    RemoveWinList();

    m_CharInfoBalloonMng.Create();

    CInput& rInput = CInput::Instance();

    const float rateX = CSprite::ResolutionScaleX();
    const float rateY = CSprite::ResolutionScaleY();

    m_MsgWin.Create();
    m_WinList.AddHead(&m_MsgWin);
    m_MsgWin.SetPosition(
        int((REFERENCE_WIDTH - 352) / 2.0f * rateX),
        int((REFERENCE_HEIGHT - 113) / 2.0f * rateY));

    m_ServerMsgWin.Create();
    m_WinList.AddHead(&m_ServerMsgWin);
    // 800x600-authored constants mapped to the 640x480 art canvas.
    constexpr float kCharSceneCanvas = (float)REFERENCE_WIDTH / 800.0f;
    m_ServerMsgWin.SetPosition(
        int(10.0f * kCharSceneCanvas * rateX),
        int((31.0f + 10.0f) * kCharSceneCanvas * rateY));

    m_SysMenuWin.Create();
    m_WinList.AddHead(&m_SysMenuWin);

    m_OptionWin.Create();
    m_WinList.AddHead(&m_OptionWin);

    m_CharSelMainWin.Create();
    m_WinList.AddHead(&m_CharSelMainWin);
    const float nArtBaseY = 567.0f * kCharSceneCanvas;
    m_CharSelMainWin.SetPositionArt(22.0f * kCharSceneCanvas,
        nArtBaseY - m_CharSelMainWin.GetHeight() / CSprite::ResolutionScaleY()
            - 11.0f * kCharSceneCanvas);

    m_CharMakeWin.Create();
    m_WinList.AddHead(&m_CharMakeWin);

    m_CharMakeWin.SetPosition(
        int((REFERENCE_WIDTH - 454) / 2.0f * rateX),
        int((REFERENCE_HEIGHT - 406) / 2.0f * rateY));

    m_CharSelMainWin.UpdateDisplay();
    m_CharInfoBalloonMng.UpdateDisplay();

    ShowWin(&m_CharSelMainWin);

    m_bSysMenuWinShow = false;
    m_nScene = UIM_SCENE_CHARACTER;
}

void CUIMng::CreateMainScene()
{
    RemoveWinList();

    m_CharInfoBalloonMng.Release();

    m_nScene = UIM_SCENE_MAIN;
}

void CUIMng::RepositionSceneUI()
{
    // A lightweight SetPosition sweep isn't enough: CSprite caches
    // m_fScrHeight = WindowHeight at Create() time and uses it for the
    // Y-flipped coordinate math in SetPosition(). When the window resizes,
    // every sprite's cached screen height is stale, so a pure SetPosition
    // call lands the windows in the wrong place.
    //
    // The only clean way to refresh that cache is to re-Create the sprites,
    // which is exactly what the scene's Create*Scene() function does. But
    // that also resets each window's m_bShow flag, so we snapshot the
    // current visibility here and restore it right after.
    if (m_nScene == UIM_SCENE_LOGIN)
    {
        const bool wasShown_MsgWin       = m_MsgWin.IsShow();
        const bool wasShown_SysMenuWin   = m_SysMenuWin.IsShow();
        const bool wasShown_OptionWin    = m_OptionWin.IsShow();
        const bool wasShown_LoginMainWin = m_LoginMainWin.IsShow();
        const bool wasShown_ServerSelWin = m_ServerSelWin.IsShow();
        const bool wasShown_LoginWin     = m_LoginWin.IsShow();
        const bool wasShown_RegisterWin  = m_RegisterWin.IsShow();
        const bool wasShown_PasswordServiceWin = m_PasswordServiceWin.IsShow();
        const bool wasShown_CreditWin    = m_CreditWin.IsShow();
        const auto loginEntry = m_LoginWin.CaptureResizeState();
        if (loginEntry.focus != CLoginWin::EntryFocus::None)
            CUITextInputBox::ReleaseFocus();

        CreateLoginScene();

        // Restore visibility BEFORE re-populating dynamic windows: child
        // elements like server/group buttons read `CWin::m_bShow` of their
        // parent when `UpdateDisplay()` decides which sub-elements to show.
        // If the parent is still hidden at that moment, nothing renders.
        if (wasShown_MsgWin)       ShowWin(&m_MsgWin);
        if (wasShown_SysMenuWin)   ShowWin(&m_SysMenuWin);
        if (wasShown_OptionWin)    ShowWin(&m_OptionWin);
        if (wasShown_LoginMainWin) ShowWin(&m_LoginMainWin);
        if (wasShown_ServerSelWin) ShowWin(&m_ServerSelWin);
        if (wasShown_LoginWin)     ShowWin(&m_LoginWin);
        if (wasShown_RegisterWin)  ShowWin(&m_RegisterWin);
        if (wasShown_PasswordServiceWin) ShowWin(&m_PasswordServiceWin);
        if (wasShown_CreditWin)    ShowWin(&m_CreditWin);
        m_LoginWin.RestoreResizeState(loginEntry);

        // Re-populate the server / server-group buttons from the existing
        // network-side data. Create() clears the button labels, so without
        // this the server list and groups render empty after a resolution
        // change.
        m_ServerSelWin.UpdateDisplay();
    }
    else if (m_nScene == UIM_SCENE_CHARACTER)
    {
        // CreateCharacterScene() ends with an explicit ShowWin(&m_CharSelMainWin)
        // so visibility of the main panel is already preserved. Other character-
        // scene windows (msg box, server msg, char make) are shown on demand
        // by game events, matching the fresh-scene state.
        CreateCharacterScene();
    }
    // MainScene uses the new UI system which resizes itself; nothing to do.
}

CWin* CUIMng::SetActiveWin(CWin* pWin)
{
    CWin* pBeforeActWin = (CWin*)m_WinList.GetHead();

    if (pBeforeActWin == NULL)
        return NULL;

    if (pBeforeActWin->IsActive())
        pBeforeActWin->Active(FALSE);
    else
        pBeforeActWin = NULL;

    if (pWin->IsShow())
    {
        if (!m_WinList.RemoveAt(m_WinList.Find(pWin)))
            return NULL;

        m_bWinActive = true;
        m_WinList.AddHead(pWin);
    }

    return pBeforeActWin;
}

void CUIMng::ShowWin(CWin* pWin)
{
    pWin->Show(TRUE);
    SetActiveWin(pWin);
}

void CUIMng::HideWin(CWin* pWin)
{
    if (!m_WinList.RemoveAt(m_WinList.Find(pWin)))
        return;

    pWin->Show(FALSE);
    pWin->Active(FALSE);
    m_WinList.AddTail(pWin);

    pWin = (CWin*)m_WinList.GetHead();
    if (pWin->IsShow())
        m_bWinActive = true;
}

void CUIMng::CheckDockWin()
{
    NODE* position = m_WinList.GetHeadPosition();
    if (NULL == position)
        return;

    CWin* pMovWin = (CWin*)m_WinList.GetNext(position);

    if (pMovWin->GetState() != WS_MOVE)
        return;

    pMovWin->SetDocking(false);

    RECT rcMovWin = { pMovWin->GetTempXPos(), pMovWin->GetTempYPos(),
        pMovWin->GetTempXPos() + pMovWin->GetWidth(),
        pMovWin->GetTempYPos() + pMovWin->GetHeight() };

    RECT rcDock[4] =
    {
        { rcMovWin.left - DOCK_EXTENT, rcMovWin.top - DOCK_EXTENT,
            rcMovWin.left + DOCK_EXTENT, rcMovWin.top + DOCK_EXTENT },
        { rcMovWin.right - DOCK_EXTENT, rcMovWin.top - DOCK_EXTENT,
            rcMovWin.right + DOCK_EXTENT, rcMovWin.top + DOCK_EXTENT },
        { rcMovWin.left - DOCK_EXTENT, rcMovWin.bottom - DOCK_EXTENT,
            rcMovWin.left + DOCK_EXTENT, rcMovWin.bottom + DOCK_EXTENT },
        { rcMovWin.right - DOCK_EXTENT, rcMovWin.bottom - DOCK_EXTENT,
            rcMovWin.right + DOCK_EXTENT, rcMovWin.bottom + DOCK_EXTENT }
    };

    CInput& rInput = CInput::Instance();

    POINT pt[4] = { { 0, 0 }, { rInput.GetScreenWidth(), 0 },
        { 0, rInput.GetScreenHeight() },
        { rInput.GetScreenWidth(), rInput.GetScreenHeight() } };

    if (::PtInRect(&rcDock[0], pt[0]))
    {
        pMovWin->SetPosition(pt[0].x, pt[0].y);
        pMovWin->SetDocking(true);
    }
    else if (::PtInRect(&rcDock[1], pt[1]))
    {
        pMovWin->SetPosition(pt[1].x - pMovWin->GetWidth(), pt[1].y);
        pMovWin->SetDocking(true);
    }
    else if (::PtInRect(&rcDock[2], pt[2]))
    {
        pMovWin->SetPosition(pt[2].x, pt[2].y - pMovWin->GetHeight());
        pMovWin->SetDocking(true);
    }
    else if (::PtInRect(&rcDock[3], pt[3]))
    {
        pMovWin->SetPosition(pt[3].x - pMovWin->GetWidth(),
            pt[3].y - pMovWin->GetHeight());
        pMovWin->SetDocking(true);
    }
    else if (rcDock[0].top < 0 && rcDock[0].bottom > 0)
    {
        pMovWin->SetPosition(rcMovWin.left, 0);
        pMovWin->SetDocking(true);
    }
    else if (rcDock[2].top < pt[2].y && rcDock[2].bottom > pt[2].y)
    {
        pMovWin->SetPosition(rcMovWin.left, pt[2].y - pMovWin->GetHeight());
        pMovWin->SetDocking(true);
    }
    else if (rcDock[0].left < 0 && rcDock[0].right > 0)
    {
        pMovWin->SetPosition(0, rcMovWin.top);
        pMovWin->SetDocking(true);
    }
    else if (rcDock[1].left < pt[1].x && rcDock[1].right > pt[1].x)
    {
        pMovWin->SetPosition(pt[1].x - pMovWin->GetWidth(), rcMovWin.top);
        pMovWin->SetDocking(true);
    }

    BOOL bEdgeDocking = FALSE;
    int i, j, nXCoord, nYCoord;
    CWin* pWin;

    while (position)
    {
        pWin = (CWin*)m_WinList.GetNext(position);
        if (!pWin->IsShow())
            continue;

        pt[0].x = pWin->GetXPos();
        pt[0].y = pWin->GetYPos();
        pt[1].x = pWin->GetXPos() + pWin->GetWidth();
        pt[1].y = pt[0].y;
        pt[2].x = pt[0].x;
        pt[2].y = pWin->GetYPos() + pWin->GetHeight();
        pt[3].x = pt[1].x;
        pt[3].y = pt[2].y;

        for (i = 0; i < 4; i++)
        {
            for (j = 0; j < 4; j++)
            {
                if (i != j && ::PtInRect(&rcDock[i], pt[j]))
                {
                    bEdgeDocking = TRUE;
                    goto DOCKING;
                }
            }
        }

        if (pt[0].x < rcDock[1].left && pt[1].x > rcDock[0].right)
        {
            nXCoord = rcMovWin.left;
            if (pt[2].y > rcDock[0].top && pt[2].y < rcDock[0].bottom)
            {
                if (SetDockWinPosition(pMovWin, nXCoord, pt[2].y))
                    continue;
            }
            else if (pt[0].y > rcDock[2].top && pt[0].y < rcDock[2].bottom)
            {
                if (SetDockWinPosition(pMovWin,
                    nXCoord, pt[0].y - pMovWin->GetHeight()))
                    continue;
            }
        }
        else if (pt[0].y < rcDock[2].top && pt[2].y > rcDock[0].bottom)
        {
            nYCoord = rcMovWin.top;
            if (pt[1].x > rcDock[0].left && pt[1].x < rcDock[0].right)
            {
                if (SetDockWinPosition(pMovWin, pt[1].x, nYCoord))
                    continue;
            }
            else if (pt[0].x > rcDock[1].left && pt[0].x < rcDock[1].right)
            {
                if (SetDockWinPosition(pMovWin,
                    pt[0].x - pMovWin->GetWidth(), nYCoord))
                    continue;
            }
        }
    }

DOCKING:
    if (bEdgeDocking)
    {
        switch (j)
        {
        case 0:
            switch (i)
            {
            case 1:
                nXCoord = pWin->GetXPos() - pMovWin->GetWidth();
                nYCoord = pWin->GetYPos();
                break;
            case 2:
                nXCoord = pWin->GetXPos();
                nYCoord = pWin->GetYPos() - pMovWin->GetHeight();
                break;
            case 3:
                nXCoord = pWin->GetXPos() - pMovWin->GetWidth();
                nYCoord = pWin->GetYPos() - pMovWin->GetHeight();
            }
            break;

        case 1:
            switch (i)
            {
            case 0:
                nXCoord = pWin->GetXPos() + pWin->GetWidth();
                nYCoord = pWin->GetYPos();
                break;
            case 2:
                nXCoord = pWin->GetXPos() + pWin->GetWidth();
                nYCoord = pWin->GetYPos() - pMovWin->GetHeight();
                break;
            case 3:
                nXCoord = pWin->GetXPos() + pWin->GetWidth()
                    - pMovWin->GetWidth();
                nYCoord = pWin->GetYPos() - pMovWin->GetHeight();
            }
            break;

        case 2:
            switch (i)
            {
            case 0:
                nXCoord = pWin->GetXPos();
                nYCoord = pWin->GetYPos() + pWin->GetHeight();
                break;
            case 1:
                nXCoord = pWin->GetXPos() - pMovWin->GetWidth();
                nYCoord = pWin->GetYPos() + pWin->GetHeight();
                break;
            case 3:
                nXCoord = pWin->GetXPos() - pMovWin->GetWidth();
                nYCoord = pWin->GetYPos() + pWin->GetHeight()
                    - pMovWin->GetHeight();
            }
            break;

        case 3:
            switch (i)
            {
            case 0:
                nXCoord = pWin->GetXPos() + pWin->GetWidth();
                nYCoord = pWin->GetYPos() + pWin->GetHeight();
                break;
            case 1:
                nXCoord = pWin->GetXPos() + pWin->GetWidth()
                    - pMovWin->GetWidth();
                nYCoord = pWin->GetYPos() + pWin->GetHeight();
                break;
            case 2:
                nXCoord = pWin->GetXPos() + pWin->GetWidth();
                nYCoord = pWin->GetYPos() + pWin->GetHeight()
                    - pMovWin->GetHeight();
            }
        }
        SetDockWinPosition(pMovWin, nXCoord, nYCoord);
    }
}

bool CUIMng::SetDockWinPosition(CWin* pMoveWin, int nDockX, int nDockY)
{
    CInput& rInput = CInput::Instance();
    RECT rcDummy;
    RECT rcScreen = { 0, 0, rInput.GetScreenWidth(), rInput.GetScreenHeight() };
    RECT rcMoveWin = { nDockX, nDockY,
        nDockX + pMoveWin->GetWidth(), nDockY + pMoveWin->GetHeight() };

    if (::IntersectRect(&rcDummy, &rcScreen, &rcMoveWin))
    {
        pMoveWin->SetPosition(nDockX, nDockY);
        pMoveWin->SetDocking(true);
        return true;
    }

    return false;
}

void CUIMng::Update(double dDeltaTick)
{
    if (UIM_SCENE_NONE == m_nScene || m_WinList.IsEmpty())
        return;

    if (m_bWinActive)
    {
        CWin* pWin = (CWin*)m_WinList.GetHead();
        if (pWin->IsShow())
        {
            pWin->Active(true);
            m_bWinActive = false;
        }
    }

    CInput& rInput = CInput::Instance();

    // ESC toggles system menu in login/character scenes
    if (rInput.IsKeyDown(VK_ESCAPE))
    {
        extern EGameScene SceneFlag;
        if (SceneFlag == LOG_IN_SCENE || SceneFlag == CHARACTER_SCENE)
        {
            if (m_SysMenuWin.IsShow())
            {
                HideWin(&m_SysMenuWin);
            }
            else if (!m_MsgWin.IsShow() && !m_OptionWin.IsShow()
                     && !m_LoginWin.IsShow() && !m_CreditWin.IsShow()
                     && !m_CharMakeWin.IsShow())
            {
                ::PlayBuffer(SOUND_CLICK01);
                ShowWin(&m_SysMenuWin);
            }
        }
    }

    CWin* pWin;
    NODE* position;

    m_bCursorOnUI = false;

    if (rInput.IsLBtnDn())
    {
        bool bWinClick = false;
        position = m_WinList.GetHeadPosition();
        while (position)
        {
            pWin = (CWin*)m_WinList.GetNext(position);

            if (pWin->CursorInWin(WA_ALL))
            {
                SetActiveWin(pWin);
                bWinClick = true;
                break;
            }
        }

        if (!bWinClick)
        {
            pWin = (CWin*)m_WinList.GetHead();
            pWin->Active(false);
        }
    }
    else if (rInput.IsLBtnUp())
    {
        m_bBlockCharMove = false;
    }
    int nlist = m_WinList.GetCount();
    std::vector<CWin*> apTempWin(nlist);

    position = m_WinList.GetHeadPosition();
    for (int i = 0; i < nlist; ++i)
    {
        apTempWin[i] = (CWin*)m_WinList.GetNext(position);
        apTempWin[i]->ActiveBtns(false);
    }

    position = m_WinList.GetHeadPosition();
    while (position)
    {
        pWin = (CWin*)m_WinList.GetNext(position);
        if (pWin->CursorInWin(WA_ALL))
        {
            pWin->ActiveBtns(true);
            break;
        }
    }

    for (int i = 0; i < nlist; ++i)
    {
        apTempWin[i]->Update(dDeltaTick);
    }

    //	CheckKey();
    CheckDockWin();

    position = m_WinList.GetHeadPosition();
    while (position)
    {
        pWin = (CWin*)m_WinList.GetNext(position);

        switch (pWin->GetState())
        {
        case WS_ETC:
            m_bCursorOnUI = true;
            break;

        case WS_MOVE:
            //			eCursorActType = CURSOR_M;
            m_bCursorOnUI = true;
            break;

        case WS_EXTEND_UP:
            //			eCursorActType = CURSOR_V;
            m_bCursorOnUI = true;
            break;

        case WS_EXTEND_DN:
            //			eCursorActType = CURSOR_V;
            m_bCursorOnUI = true;
            break;
        }

        if (m_bCursorOnUI)
            break;

        if (pWin->CursorInWin(WA_ALL))
        {
            m_bCursorOnUI = true;
            break;
        }
    }
}

void CUIMng::Render()
{
    if (UIM_SCENE_NONE == m_nScene)
        return;

    m_CharInfoBalloonMng.Render();

    CWin* pWin;
    NODE* position = m_WinList.GetTailPosition();
    while (position)
    {
        pWin = (CWin*)m_WinList.GetPrev(position);
        pWin->Render();
    }
}

void CUIMng::PopUpMsgWin(int nMsgCode, wchar_t* pszMsg)
{
    if (UIM_SCENE_NONE == m_nScene || UIM_SCENE_TITLE == m_nScene || UIM_SCENE_LOADING == m_nScene)
        return;

    if (UIM_SCENE_MAIN == m_nScene)	return;

    m_MsgWin.PopUp(nMsgCode, pszMsg);
}

void CUIMng::AddServerMsg(wchar_t* pszMsg)
{
    if (UIM_SCENE_CHARACTER != m_nScene)
        return;

    m_ServerMsgWin.AddMsg(pszMsg);
}
