//*****************************************************************************
// File: LoadingScene.cpp
//*****************************************************************************

#include "stdafx.h"

#include "LoadingScene.h"

#include "Core/Input/Input.h"
#include "UI/Legacy/UIMng.h"
#include "Render/Textures/ZzzOpenglUtil.h"
#include "Render/Textures/ZzzTexture.h"
#include "SceneCore.h"
#include "Engine/Object/ZzzInterface.h"
#include "SceneCommon.h"
#include "UI/NewUI/Dialogs/ReconnectDialog.h"


#ifdef _EDITOR
#include "Core/MuEditorCore.h"
#endif

CLoadingScene::CLoadingScene()
{
}

CLoadingScene::~CLoadingScene()
{
}

void CLoadingScene::Create()
{
    // Loading artwork is a tiled 800x600 canvas; map it into the 640x480
    // reference art space and let the resolution scale handle window sizing.
    constexpr float kCanvasSX = (float)REFERENCE_WIDTH / 800.0f;  // 0.8
    constexpr float kCanvasSY = (float)REFERENCE_HEIGHT / 600.0f; // 0.8

    const int tileW = int(400 * kCanvasSX);
    const int mainH = int(512 * kCanvasSY);
    const int stripH = int(88 * kCanvasSY);
    const int anHeight[LDS_BACK_MAX] = { mainH, mainH, stripH, stripH };
    for (int i = 0; i < LDS_BACK_MAX; ++i)
    {
        m_asprBack[i].Create(tileW, anHeight[i], BITMAP_TITLE + i);
        m_asprBack[i].Show(true);
    }

    m_asprBack[1].SetPositionArt(400 * kCanvasSX, 0, X);
    m_asprBack[2].SetPositionArt(0, 512 * kCanvasSY, Y);
    m_asprBack[3].SetPositionArt(400 * kCanvasSX, 512 * kCanvasSY);
}

void CLoadingScene::Release()
{
    for (int i = 0; i < LDS_BACK_MAX; ++i)
        m_asprBack[i].Release();
}

void CLoadingScene::Render()
{
    for (int i = 0; i < LDS_BACK_MAX; ++i)
    {
        m_asprBack[i].Render();
    }
}

// External variables
extern int LoadingWorld;
extern bool FogEnable;
extern EGameScene SceneFlag;

void LoadingScene(HDC hDC)
{
    g_ConsoleDebug->Write(MCD_NORMAL, L"LoadingScene_Start");

    CUIMng& rUIMng = CUIMng::Instance();
    if (!InitLoading)
    {
        LoadingWorld = 9999999;

        InitLoading = true;
        LoadBitmap(L"Interface\\LSBg01.JPG", BITMAP_TITLE, GL_LINEAR);
        LoadBitmap(L"Interface\\LSBg02.JPG", BITMAP_TITLE + 1, GL_LINEAR);
        LoadBitmap(L"Interface\\LSBg03.JPG", BITMAP_TITLE + 2, GL_LINEAR);
        LoadBitmap(L"Interface\\LSBg04.JPG", BITMAP_TITLE + 3, GL_LINEAR);

        ::StopMp3(MUSIC_LOGIN_THEME);

        rUIMng.m_pLoadingScene = new CLoadingScene;
        rUIMng.m_pLoadingScene->Create();
    }

    FogEnable = true;
    ::BeginOpengl();
    ::ClearColorAndDepthBuffers();
    ::BeginBitmap();

    rUIMng.m_pLoadingScene->Render();

    ::EndBitmap();
    ::EndOpengl();
    ::FlushGL();
#ifdef _EDITOR
    // Always render ImGui (shows "Open Editor" button when closed, or full UI when open)
    g_MuEditorCore.Render();

    // Render game cursor on top of ImGui if not hovering UI
    extern bool g_bRenderGameCursor;
    if (g_bRenderGameCursor)
    {
        BeginBitmap();
        RenderCursor();
        EndBitmap();
    }
#endif
    UI::Reconnect::RenderDialog();
    PlatformSwapBuffers();

    SAFE_DELETE(rUIMng.m_pLoadingScene);

    SceneFlag = MAIN_SCENE;
    for (int i = 0; i < 4; ++i)
        ::DeleteBitmap(BITMAP_TITLE + i);

    ::ClearInput();

    g_ConsoleDebug->Write(MCD_NORMAL, L"LoadingScene_End");
}