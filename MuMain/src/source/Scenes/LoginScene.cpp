///////////////////////////////////////////////////////////////////////////////
// LoginScene.cpp - Login scene implementation
///////////////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include <cstring>
#include "Core/Time/FrameScaling.h"
#include "LoginScene.h"
#include "Camera/CameraUtility.h"
#include "Camera/CameraManager.h"
#include "Camera/CameraMove.h"
#include "Audio/DSPlaySound.h"
#include "Render/Textures/ZzzOpenglUtil.h"
#include "Render/Core/RenderConfig.h"
#include "Engine/Object/ZzzObject.h"
#include "Engine/Object/ZzzCharacter.h"
#include "Render/Terrain/ZzzLodTerrain.h"
#include "Engine/Object/ZzzInterface.h"
#include "Render/Effects/ZzzEffect.h"
#include "Engine/AI/GOBoid.h"
#include "GameLogic/Pets/w_PetProcess.h"
#include "World/MapInfra/MapManager.h"
#include "UI/Legacy/UIMng.h"
#include "Core/Input/Input.h"
#include "Core/Input/VirtualGamepadOfflineAcceptance.h"
#include "Network/Server/WSclient.h"
#include "Core/Utilities/Log/muConsoleDebug.h"
#include "I18N/All.h"
#include "Engine/Object/ZzzCharacter.h"
#include "UI/Legacy/UIControls.h"
#include "SceneCommon.h"
#include "Engine/Object/ZzzOpenData.h"
#include "UI/NewUI/NewUISystem.h"
#include "UI/NewUI/Dialogs/NewUICommonMessageBox.h"

// External declarations
extern int DeleteGuildIndex;
extern EGameScene SceneFlag;
extern int g_iChatInputType;
extern CUITextInputBox* g_pSinglePasswdInputBox;
extern float g_fMULogoAlpha;
extern wchar_t m_Username[MAX_USERNAME_SIZE + 1];
extern CHARACTER* Hero;
extern double WorldTime;
extern HFONT g_hFont;
extern wchar_t m_ExeVersion[11];
extern HWND g_hWnd;

#ifdef _EDITOR
extern "C" float DevEditor_GetLoginTerrainDist();
#endif

//=============================================================================
// LoginScene Camera State (local to this file)
//=============================================================================
namespace {

struct LoginCameraState {
    int walkCut = 0;
    int currentCount = -1;
    int currentNumber = 0;
    int currentWalkType = 0;
    vec3_t currentPosition = {0, 0, 0};
    vec3_t currentAngle = {0, 0, 0};
    float currentWalkDelta[6] = {0, 0, 0, 0, 0, 0};

    // Predefined camera animation paths (6 paths × 6 values each)
    static constexpr float WALK_PATHS[6][6] = {
        {0.f, -1000.f, 500.f, -80.f, 0.f, 0.f},
        {0.f, -1100.f, 500.f, -80.f, 0.f, 0.f},
        {0.f, -1100.f, 500.f, -80.f, 0.f, 0.f},
        {0.f, -1100.f, 500.f, -80.f, 0.f, 0.f},
        {0.f, -1100.f, 500.f, -80.f, 0.f, 0.f},
        {200.f, -800.f, 250.f, -87.f, 0.f, -10.f}
    };

    void Reset() {
        currentCount = -1;
        walkCut = 0;
        currentNumber = 0;
        currentWalkType = 0;
    }
};

// File-local camera state instance
LoginCameraState g_loginCamera;

#if defined(__ANDROID__) || defined(__OHOS__)
// Mobile opening-cinematic tap gate.
//
// On phones/tablets the client logs in automatically, so there is no
// "connect" button press that naturally ends the login fly-through. Once the
// server accepts the login we keep the cinematic tour playing and show a
// blinking "tap to start" prompt; the player taps to move on to character
// selection. Desktop keeps its original immediate transition (a manual login
// already has a deliberate button press), so this whole path compiles out
// there and desktop behaviour is untouched.
struct MobileTapGate
{
    static constexpr double GRACE_SECONDS = 0.6;

    bool armed = false;
    double armedAtWorldTime = 0.0;

    void Arm(double nowWorldTime)
    {
        if (!armed)
        {
            armed = true;
            armedAtWorldTime = nowWorldTime;
        }
    }

    void Reset()
    {
        armed = false;
        armedAtWorldTime = 0.0;
    }

    // Returns true once the player taps, after a short grace window so a tap
    // already in flight when login completed does not instantly skip the
    // cinematic. Any tap while the gate owns the screen is consumed (the edge
    // cleared) so no underlying login control also acts on it.
    bool ConsumeTap(double nowWorldTime)
    {
        if (!MouseLButtonPop)
            return false;

        MouseLButtonPop = false;

        if (!armed || (nowWorldTime - armedAtWorldTime) < GRACE_SECONDS)
            return false;

        return true;
    }
};

MobileTapGate g_mobileTapGate;

// Reference-space Y (of the 640x480 UI canvas) for the centered tap prompt;
// sits in the lower-middle, clear of the top logo and the bottom copyright.
constexpr int MOBILE_TAP_PROMPT_Y = 352;
#endif

}  // namespace

//=============================================================================
// Accessor functions for external use
//=============================================================================

int GetLoginCameraCount() {
    return g_loginCamera.currentCount;
}

int GetLoginCameraWalkCut() {
    return g_loginCamera.walkCut;
}

//=============================================================================
// LoginScene Implementation
//=============================================================================

void DeleteCharacter()
{
    if (SelectedHero < 0 || SelectedHero >= MAX_CHARACTERS_PER_ACCOUNT)
    {
        return;
    }

    int characterToDelete = SelectedHero;
    SelectedHero = -1;

    if (g_iChatInputType == 1)
    {
        g_pSinglePasswdInputBox->GetText(InputText[0]);
        g_pSinglePasswdInputBox->SetText(NULL);
        g_pSinglePasswdInputBox->SetState(UISTATE_HIDE);
    }

    CurrentProtocolState = REQUEST_DELETE_CHARACTER;
    SocketClient->ToGameServer()->SendDeleteCharacter(CharactersClient[characterToDelete].ID, InputText[0]);

    PlayBuffer(SOUND_MENU01);

    ClearInput();
    InputEnable = false;
}

void MoveCharacterCamera(vec3_t Origin, vec3_t Position, vec3_t Angle)
{
    vec3_t TransformPosition;
    g_Camera.Angle[0] = 0.f;
    g_Camera.Angle[1] = 0.f;
    g_Camera.Angle[2] = Angle[2];
    float Matrix[3][4];
    AngleMatrix(g_Camera.Angle, Matrix);
    VectorIRotate(Position, Matrix, TransformPosition);
    VectorAdd(Origin, TransformPosition, g_Camera.Position);
    g_Camera.Angle[0] = Angle[0];
}

/**
 * @brief Initializes login camera to starting position and angle.
 */
static void InitializeLoginCamera()
{
    for (int i = 0; i < 3; i++)
    {
        g_loginCamera.currentPosition[i] = LoginCameraState::WALK_PATHS[0][i];
        g_loginCamera.currentAngle[i] = LoginCameraState::WALK_PATHS[0][i + 3];
    }
    g_loginCamera.currentNumber = 1;
    g_loginCamera.currentWalkType = 1;

    for (int i = 0; i < 3; i++)
    {
        g_loginCamera.currentWalkDelta[i] = (LoginCameraState::WALK_PATHS[g_loginCamera.currentNumber][i] - g_loginCamera.currentPosition[i]) / 128;
        g_loginCamera.currentWalkDelta[i + 3] = (LoginCameraState::WALK_PATHS[g_loginCamera.currentNumber][i + 3] - g_loginCamera.currentAngle[i]) / 128;
    }
}

/**
 * @brief Calculates movement delta for transitioning to target waypoint.
 */
static void CalculateWalkDelta()
{
    for (int i = 0; i < 3; i++)
    {
        g_loginCamera.currentWalkDelta[i] = (LoginCameraState::WALK_PATHS[g_loginCamera.currentNumber][i] - g_loginCamera.currentPosition[i]) / 128;
        g_loginCamera.currentWalkDelta[i + 3] = (LoginCameraState::WALK_PATHS[g_loginCamera.currentNumber][i + 3] - g_loginCamera.currentAngle[i]) / 128;
    }
}

/**
 * @brief Selects next camera waypoint based on scene state.
 */
static void SelectNextWaypoint()
{
    if (SceneFlag == LOG_IN_SCENE)
    {
        g_loginCamera.currentNumber = rand() % 4 + 1;
        g_loginCamera.currentWalkType = rand() % 2;
    }
    else
    {
        g_loginCamera.currentNumber = 5;
        g_loginCamera.currentWalkType = 0;
    }
}

/**
 * @brief Updates camera waypoint transition timing and selection.
 */
static void UpdateCameraWaypoint()
{
    g_loginCamera.currentCount++;

    bool shouldTransition = (g_loginCamera.walkCut == 0 && g_loginCamera.currentCount >= 40) ||
                           (g_loginCamera.walkCut > 0 && g_loginCamera.currentCount >= 128);

    if (shouldTransition)
    {
        g_loginCamera.currentCount = 0;

        if (g_loginCamera.walkCut == 0)
        {
            g_loginCamera.walkCut = 1;
        }
        else
        {
            SelectNextWaypoint();
        }

        CalculateWalkDelta();
    }
}

/**
 * @brief Interpolates camera position/angle towards the current target waypoint.
 *
 * Two walk modes, selected per waypoint via WALK_PATHS[...][6]:
 *  - Type 0 (smooth): exponential ease toward target (divide by SMOOTH_EASING_FACTOR each frame).
 *  - Type 1 (linear): step by a precomputed per-frame delta (see CalculateWalkDelta).
 *
 * Both modes update all 3 position components AND all 3 angle components so camera
 * rotation tracks the waypoint pose, not just X/Y translation.
 */
static void InterpolateCameraMovement()
{
    constexpr int SMOOTH_EASING_FACTOR = 6;
    const int target = g_loginCamera.currentNumber;

    if (g_loginCamera.currentWalkType == 0)
    {
        for (int i = 0; i < 3; i++)
        {
            g_loginCamera.currentPosition[i] += (LoginCameraState::WALK_PATHS[target][i] - g_loginCamera.currentPosition[i]) / SMOOTH_EASING_FACTOR;
            g_loginCamera.currentAngle[i]    += (LoginCameraState::WALK_PATHS[target][i + 3] - g_loginCamera.currentAngle[i]) / SMOOTH_EASING_FACTOR;
        }
    }
    else
    {
        for (int i = 0; i < 3; i++)
        {
            g_loginCamera.currentPosition[i] += g_loginCamera.currentWalkDelta[i];
            g_loginCamera.currentAngle[i]    += g_loginCamera.currentWalkDelta[i + 3];
        }
    }
}

/**
 * @brief Updates login scene camera animation along predefined waypoint path.
 */
void MoveCamera()
{
    if (CCameraMove::GetInstancePtr()->IsTourMode())
    {
        return;
    }

    if (g_loginCamera.currentCount == -1)
    {
        InitializeLoginCamera();
    }

    UpdateCameraWaypoint();
    InterpolateCameraMovement();

    g_Camera.FOV = 45.f;
    vec3_t Position;
    Vector(0.f, 0.f, 0.f, Position);
    MoveCharacterCamera(Position, g_loginCamera.currentPosition, g_loginCamera.currentAngle);
}

void CreateLogInScene()
{
    EnableMainRender = true;
    gMapManager.WorldActive = WD_73NEW_LOGIN_SCENE;

    if (!gMapManager.LoadWorld(gMapManager.WorldActive)) return;

    OpenLogoSceneData();

    CUIMng::Instance().CreateLoginScene();

    CurrentProtocolState = REQUEST_JOIN_SERVER;
    if (!Core::Input::IsVirtualGamepadOfflineLoginAcceptance())
        CreateSocket(szServerIpAddress, g_ServerPort);

    GuildInputEnable = false;
    TabInputEnable = false;
    GoldInputEnable = false;
    InputEnable = true;
    ClearInput();

    if (g_iChatInputType == 0)
    {
        wcscpy_s(InputText[0], 256, m_Username);
        InputLength[0] = wcslen(InputText[0]);
        InputTextMax[0] = MAX_USERNAME_SIZE;
        if (InputLength[0] == 0)	InputIndex = 0;
        else InputIndex = 1;
    }
    InputNumber = 2;
    InputTextHide[1] = 1;

    // FIX: Enable tour mode with offset correction
    // Tour mode waypoints work well for movement, but need position offset
    // Offset is applied in CCameraMove::GetCurrentCameraPos()
    CCameraMove::GetInstancePtr()->PlayCameraWalk(Hero->Object.Position, 1000);
    CCameraMove::GetInstancePtr()->SetTourMode(TRUE, FALSE, 0);  // Start from waypoint 0

    MoveMainCamera();

    g_fMULogoAlpha = 0;

    ::PlayMp3(MUSIC_LOGIN_THEME);

    g_ErrorReport.Write(L"> Login Scene init success.\r\n");
}

void NewMoveLogInScene()
{
    if (!InitLogIn)
    {
        InitLogIn = true;
        CreateLogInScene();
#if defined(__ANDROID__) || defined(__OHOS__)
        g_mobileTapGate.Reset();
#endif
    }

    if (!CUIMng::Instance().m_CreditWin.IsShow())
    {
        InitTerrainLight();
        MoveObjects();
        MoveMounts();
        MoveLeaves();

        // Update camera BEFORE checking character visibility
        MoveCamera();

        // Now check character visibility with updated frustum
        MoveCharactersClient();

        MoveEffects();
        MoveJoints();
        MoveParticles();
        MoveBoids();
        ThePetProcess().UpdatePets();
    }

    // ESC menu toggle is handled by CUIMng::Update()
    if (RECEIVE_LOG_IN_SUCCESS == CurrentProtocolState)
    {
        bool proceedToCharacterSelect = true;
#if defined(__ANDROID__) || defined(__OHOS__)
        // Hold the opening cinematic until the player taps the screen. The
        // tour camera keeps animating (the Move* calls above) until then.
        g_mobileTapGate.Arm(WorldTime);
        if (!g_mobileTapGate.ConsumeTap(WorldTime))
            proceedToCharacterSelect = false;
        else
            g_mobileTapGate.Reset();
#endif

        if (proceedToCharacterSelect)
        {
            g_ErrorReport.Write(L"> Request Character list\r\n");

            CCameraMove::GetInstancePtr()->SetTourMode(FALSE);

            // Tear down the login scene data before asking the server for the
            // account characters, otherwise a fast reply can be cleared again.
            ReleaseLogoSceneData();

            SceneFlag = CHARACTER_SCENE;
            CurrentProtocolState = REQUEST_CHARACTERS_LIST;
            SocketClient->ToGameServer()->SendRequestCharacterList(g_pMultiLanguage->GetLanguage());
        }
    }

    g_ConsoleDebug->UpdateMainScene();
}

bool NewRenderLogInScene(HDC hDC)
{
    if (!InitLogIn) return false;

    FogEnable = true;

    vec3_t pos;
    VectorCopy(g_Camera.Position, pos);
    if (CCameraMove::GetInstancePtr()->IsCameraMove())
    {
        VectorCopy(g_Camera.Position, pos);
    }

    MoveMainCamera();

    // Play login music (called every frame — PlayMp3 no-ops if already playing)
    ::PlayMp3(MUSIC_LOGIN_THEME);

    int Width, Height;

    glColor3f(1.f, 1.f, 1.f);

    Height = REFERENCE_HEIGHT;
    Width = GetScreenWidth();
    SetClearColor(0.f, 0.f, 0.f, 1.f);

    // Set ViewFar BEFORE BeginOpengl so the projection matrix covers the full render distance
#ifdef _EDITOR
    g_Camera.ViewFar = DevEditor_GetLoginTerrainDist();
#else
    g_Camera.ViewFar = LoginSceneCameraDefaults::RENDER_TERRAIN_DIST;
#endif
    g_Camera.ViewNear = 100.f;  // Push near plane out to preserve z-buffer precision

    BeginOpengl(0, 25, REFERENCE_WIDTH, 430);

    // LoginScene doesn't call CreateFrustrum (DefaultCamera tour mode angles differ from
    // legacy hardcoded values). Instead, TestFrustrum2D is bypassed for LOG_IN_SCENE and
    // we reset iteration bounds to cover the full terrain so stale bounds from other scenes
    // don't restrict the render loop.
    ResetFrustrumBoundsFullTerrain();

    if (!CUIMng::Instance().m_CreditWin.IsShow())
    {
        RenderTerrain(false);
        RenderObjects();
        RenderCharactersClient();
        RenderMount();

        RenderJoints();
        RenderEffects();
        CheckSprites();
        RenderLeaves();
        RenderBoids();
        RenderObjects_AfterCharacter();
        ThePetProcess().RenderPets();
    }

    BeginSprite();
    RenderSprites();
    RenderParticles();
    EndSprite();
    BeginBitmap();

    if (CCameraMove::GetInstancePtr()->IsTourMode())
    {
        g_fMULogoAlpha += Core::Time::ScaleLinearStep(0.02f, FPS_ANIMATION_FACTOR);
        if (g_fMULogoAlpha > 10.0f) g_fMULogoAlpha = 10.0f;

        EnableAlphaBlend();
        glColor4f(g_fMULogoAlpha - 0.3f, g_fMULogoAlpha - 0.3f, g_fMULogoAlpha - 0.3f, g_fMULogoAlpha - 0.3f);
        RenderBitmap(BITMAP_LOG_IN + 17, 320.0f - 128.0f * 0.8f, 25.0f, 256.0f * 0.8f, 128.0f * 0.8f);
        EnableAlphaTest();
        glColor4f(g_fMULogoAlpha, g_fMULogoAlpha, g_fMULogoAlpha, g_fMULogoAlpha);
        RenderBitmap(BITMAP_LOG_IN + 16, 320.0f - 128.0f * 0.8f, 25.0f, 256.0f * 0.8f, 128.0f * 0.8f);
    }

    SIZE Size;
    wchar_t Text[100];

    g_pRenderText->SetFont(g_hFont);

    InputTextWidth = 256;
    glColor3f(0.8f, 0.7f, 0.6f);
    g_pRenderText->SetTextColor(255, 255, 255, 255);
    g_pRenderText->SetBgColor(0, 0, 0, 128);

    wcscpy_s(Text, 100, I18N::Game::CCopyright2001Webzen);
    GetTextExtentPoint32(g_pRenderText->GetFontDC(), Text, lstrlen(Text), &Size);
    g_pRenderText->RenderText(335 - Size.cx * REFERENCE_WIDTH / WindowWidth, REFERENCE_HEIGHT - Size.cy * REFERENCE_HEIGHT / WindowHeight - 1, Text);

    wcscpy_s(Text, 100, I18N::Game::AllRightsReserved);

    GetTextExtentPoint32(g_pRenderText->GetFontDC(), Text, lstrlen(Text), &Size);
    g_pRenderText->RenderText(335, REFERENCE_HEIGHT - Size.cy * REFERENCE_HEIGHT / WindowHeight - 1, Text);

    swprintf_s(Text, 100, I18N::Game::VerS, m_ExeVersion);

    GetTextExtentPoint32(g_pRenderText->GetFontDC(), Text, lstrlen(Text), &Size);
    g_pRenderText->RenderText(0, REFERENCE_HEIGHT - Size.cy * REFERENCE_HEIGHT / WindowHeight - 1, Text);

#if defined(__ANDROID__) || defined(__OHOS__)
    // Blinking "tap to start" prompt while the opening cinematic is waiting
    // for a tap. Centered across the full reference width so it stays centered
    // on every screen size/aspect ratio; the pulse is driven by real
    // WorldTime so it blinks at the same rate regardless of frame rate.
    if (g_mobileTapGate.armed)
    {
        const float pulse = 0.5f + 0.5f * sinf(static_cast<float>(WorldTime) * 3.0f);
        const BYTE promptAlpha = static_cast<BYTE>(110 + 145 * pulse);

        g_pRenderText->SetFont(g_hFont);
        g_pRenderText->SetTextColor(255, 255, 255, promptAlpha);
        g_pRenderText->SetBgColor(0, 0, 0, static_cast<BYTE>(90 * pulse + 30));

        const char* tapLocale = I18N::GetCurrentLocale();
        const bool chinesePrompt = tapLocale != nullptr && strncmp(tapLocale, "zh", 2) == 0;
        g_pRenderText->RenderText(
            0, MOBILE_TAP_PROMPT_Y, chinesePrompt ? L"点击屏幕开始" : L"Tap to Start",
            REFERENCE_WIDTH, 0, RT3_SORT_CENTER);

        g_pRenderText->SetTextColor(255, 255, 255, 255);
        g_pRenderText->SetBgColor(0, 0, 0, 128);
    }
#endif

    RenderInfomation();

#ifdef ENABLE_EDIT
    RenderDebugWindow();
#endif

    // Handle option window in login/character scenes (can't use full g_pNewUISystem update)
    if (g_pNewUISystem->IsVisible(SEASON3B::INTERFACE_OPTION))
    {
        g_pOption->UpdateMouseEvent();
        g_pOption->UpdateKeyEvent();
        g_pOption->Render();
    }

    // Drive the NewUI message box here too (same reason as the option window):
    // the login scene skips the full NewUI update, so a confirmation dialog such
    // as the "Remember Password" prompt would otherwise never update or draw.
    if (!g_MessageBox->IsEmpty())
    {
        g_MessageBox->UpdateMouseEvent();
        g_MessageBox->UpdateKeyEvent();
        g_MessageBox->Update();
        g_MessageBox->Render();
    }

    // The OS cursor is hidden while the game window owns pointer input. Keep
    // the game cursor above login-scene overlays such as Options and message
    // boxes; drawing it in RenderInfomation() placed it underneath them.
    RenderCursor();

    EndBitmap();

    EndOpengl();

    return true;
}
