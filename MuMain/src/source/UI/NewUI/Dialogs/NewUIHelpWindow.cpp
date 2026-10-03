// NewUIHelpWindow.cpp: implementation of the CNewUIHelpWindow class.
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include "UI/NewUI/Dialogs/NewUIHelpWindow.h"
#include "UI/NewUI/NewUISystem.h"
#include "Engine/Object/ZzzInventory.h"
#include "Audio/DSPlaySound.h"
#include "Core/Haptics/Haptics.h"
#include "Core/Input/GamepadService.h"
#include "I18N/All.h"
#include "Data/GameConfig/GameConfig.h"

#include <SDL3/SDL.h>

#include <array>

using namespace SEASON3B;

namespace
{
    constexpr int FirstHelpPage = 0;

    // Page order: reference pages 0..3, then the onboarding sequence:
    // welcome, keyboard/mouse, touch, gamepad, and a closing page.
    constexpr int TutorialWelcomePage = 4;
    constexpr int TutorialDesktopPage = 5;
    constexpr int TutorialTouchPage = 6;
    constexpr int TutorialGamepadPage = 7;
    constexpr int TutorialFinishPage = 8;
    constexpr int LastHelpPage = TutorialFinishPage;

    void PublishHelpHaptic(Core::Haptics::HapticEvent event)
    {
        Core::Input::GamepadService::Instance().PublishHaptic(
            event, static_cast<double>(SDL_GetTicks()));
    }
}

SEASON3B::CNewUIHelpWindow::CNewUIHelpWindow()
    : m_iIndex(0), m_isOnboarding(false)
{
    m_pNewUIMng = NULL;
    m_Pos.x = 0;
    m_Pos.y = 0;
}

SEASON3B::CNewUIHelpWindow::~CNewUIHelpWindow()
{
    Release();
}

bool SEASON3B::CNewUIHelpWindow::Create(CNewUIManager* pNewUIMng, int x, int y)
{
    if (NULL == pNewUIMng)
        return false;

    m_pNewUIMng = pNewUIMng;
    m_pNewUIMng->AddUIObj(SEASON3B::INTERFACE_HELP, this);

    SetPos(x, y);

    Show(false);

    return true;
}

void SEASON3B::CNewUIHelpWindow::Release()
{
    if (m_pNewUIMng)
    {
        m_pNewUIMng->RemoveUIObj(this);
        m_pNewUIMng = NULL;
    }
}

void SEASON3B::CNewUIHelpWindow::SetPos(int x, int y)
{
    m_Pos.x = x;
    m_Pos.y = y;
}

bool SEASON3B::CNewUIHelpWindow::UpdateMouseEvent()
{
    return true;
}

bool SEASON3B::CNewUIHelpWindow::UpdateKeyEvent()
{
    if (!g_pNewUISystem->IsVisible(SEASON3B::INTERFACE_HELP))
        return true;

    if (HandleGamepadInput())
        return false;

    if (IsPress(VK_F1) || IsPress(VK_NEXT))
    {
        NextPage();
        return false;
    }

    if (IsPress(VK_PRIOR))
    {
        PreviousPage();
        return false;
    }

    if (IsPress(VK_ESCAPE))
    {
        Close();
        return false;
    }

    return true;
}

bool SEASON3B::CNewUIHelpWindow::Update()
{
    return true;
}

bool SEASON3B::CNewUIHelpWindow::Render()
{
    EnableAlphaTest();
    glColor4f(1.0f, 1.0f, 1.0f, 1.0f);

    // Reference-bind to the global arrays in ZzzInventory.cpp. A naive
    // `extern wchar_t TextList[50][100];` here would resolve to
    // SEASON3B::TextList (the reference defined in UIManager.cpp) because
    // this function is in the SEASON3B namespace -- and the linker stores
    // that reference as a 4-byte read-only pointer, so writing to it crashes.
    wchar_t (&TextList)[50][100] = ::TextList;
    int (&TextListColor)[50] = ::TextListColor;
    int (&TextBold)[50] = ::TextBold;

    if (m_iIndex >= TutorialWelcomePage)
    {
        RenderTutorialPage();
    }
    else if (m_iIndex == 0)
    {
        int iTextNum = 0;

        mu_swprintf(TextList[iTextNum], L"\n");
        iTextNum++;

        wcscpy(TextList[iTextNum], I18N::Game::KeyFunction);
        TextListColor[iTextNum] = TEXT_COLOR_BLUE;
        TextBold[iTextNum] = true;
        iTextNum++;

        mu_swprintf(TextList[iTextNum], L"\n");
        iTextNum++;

        // Render F1-F4 entries (I18N::Game::Lookup(121..124)) first.
        for (int i = 0; i < 4; ++i)
        {
            wcscpy(TextList[iTextNum], I18N::Game::Lookup(121 + i));
            TextListColor[iTextNum] = TEXT_COLOR_WHITE;
            TextBold[iTextNum] = false;
            iTextNum++;
        }

        // Insert engine-added camera and MU Helper hotkey entries between F4
        // and the rest of the shipped entries.
        const wchar_t* const extraHelpLines[] = {
            I18N::Game::F8ToggleMonsterHPBar,
            I18N::Game::F9Toggle3DCamera,
            I18N::Game::F10LockUnlockCameraZoom,
            I18N::Game::F11ResetCameraView,
            I18N::Game::HomeToggleMUHelper,
            I18N::Game::JToggleChatCommands,
        };
        for (const wchar_t* line : extraHelpLines)
        {
            wcsncpy_s(TextList[iTextNum], line, 99);
            TextListColor[iTextNum] = TEXT_COLOR_WHITE;
            TextBold[iTextNum] = false;
            iTextNum++;
        }

        // Render the remaining shipped entries (I18N::Game::Lookup(125..139)).
        for (int i = 4; i < 19; ++i)
        {
            wcscpy(TextList[iTextNum], I18N::Game::Lookup(121 + i));
            TextListColor[iTextNum] = TEXT_COLOR_WHITE;
            TextBold[iTextNum] = false;
            iTextNum++;
        }

        mu_swprintf(TextList[iTextNum], L"\n");
        iTextNum++;

        RenderTipTextList(1, 1, iTextNum, 0, RT3_SORT_CENTER);
    }
    else if (m_iIndex == 1)
    {
        int iTextNum = 0;

        mu_swprintf(TextList[iTextNum], L"\n");
        iTextNum++;

        wcscpy(TextList[iTextNum], I18N::Game::ChattingInstructions);
        TextListColor[iTextNum] = TEXT_COLOR_BLUE;
        TextBold[iTextNum] = true;
        iTextNum++;

        mu_swprintf(TextList[iTextNum], L"\n");
        iTextNum++;

        for (int i = 0; i < 16; ++i)
        {
            wcscpy(TextList[iTextNum], I18N::Game::Lookup(141 + i));
            TextListColor[iTextNum] = TEXT_COLOR_WHITE;
            TextBold[iTextNum] = false;
            iTextNum++;
        }

        mu_swprintf(TextList[iTextNum], L"\n");
        iTextNum++;

        RenderTipTextList(1, 1, iTextNum, 0, RT3_SORT_CENTER);
    }
    else if (m_iIndex == 2)
    {
        int iTextNum = 0;

        mu_swprintf(TextList[iTextNum], L"\n");
        iTextNum++;

        wcscpy(TextList[iTextNum], I18N::Game::AMPM);
        TextListColor[iTextNum] = TEXT_COLOR_BLUE;
        TextBold[iTextNum] = true;
        iTextNum++;

        for (int i = 0; i < 24; ++i)
        {
            wcscpy(TextList[iTextNum], I18N::Game::Lookup(2422 + i));
            TextListColor[iTextNum] = TEXT_COLOR_WHITE;
            TextBold[iTextNum] = false;
            iTextNum++;
        }

        mu_swprintf(TextList[iTextNum], L"\n");
        iTextNum++;

        RenderTipTextList(1, 1, iTextNum, 0, RT3_SORT_LEFT);
    }
    else if (m_iIndex == 3)
    {
        int iTextNum = 0;

        mu_swprintf(TextList[iTextNum], L"\n");
        iTextNum++;

        wcscpy(TextList[iTextNum], I18N::Game::ChaosCastleDevilSSquare);
        TextListColor[iTextNum] = TEXT_COLOR_BLUE;
        TextBold[iTextNum] = true;
        iTextNum++;

        for (int i = 0; i < 18; ++i)
        {
            wcscpy(TextList[iTextNum], I18N::Game::Lookup(2447 + i));
            if (i == 0 || i == 8 || i == 9)
            {
                TextListColor[iTextNum] = TEXT_COLOR_BLUE;
                TextBold[iTextNum] = true;
                iTextNum++;
            }
            else
            {
                TextListColor[iTextNum] = TEXT_COLOR_WHITE;
                TextBold[iTextNum] = false;
                iTextNum++;
            }
        }

        mu_swprintf(TextList[iTextNum], L"\n");
        iTextNum++;

        RenderTipTextList(1, 1, iTextNum, 0, RT3_SORT_LEFT);
    }

    DisableAlphaBlend();
    return true;
}

float SEASON3B::CNewUIHelpWindow::GetLayerDepth()
{
    return 8.3f;
}

float SEASON3B::CNewUIHelpWindow::GetKeyEventOrder()
{
    return 10.f;
}

void SEASON3B::CNewUIHelpWindow::OpenningProcess()
{
    // Manual Help always opens at the reference index and never marks onboarding.
    m_isOnboarding = false;
    m_iIndex = 0;
}

void SEASON3B::CNewUIHelpWindow::ClosingProcess()
{
}

void SEASON3B::CNewUIHelpWindow::StartTutorial()
{
    m_isOnboarding = true;
    m_iIndex = TutorialWelcomePage;
}

void SEASON3B::CNewUIHelpWindow::AutoUpdateIndex()
{
    NextPage();
}

void SEASON3B::CNewUIHelpWindow::NextPage()
{
    if (!g_pNewUISystem->IsVisible(SEASON3B::INTERFACE_HELP))
        return;

    if (m_iIndex >= LastHelpPage)
    {
        Close();
        return;
    }

    ++m_iIndex;
    PlayBuffer(SOUND_CLICK01);
    PublishHelpHaptic(Core::Haptics::HapticEvent::FocusMoved);
}

void SEASON3B::CNewUIHelpWindow::PreviousPage()
{
    if (!g_pNewUISystem->IsVisible(SEASON3B::INTERFACE_HELP))
        return;

    if (m_iIndex <= FirstHelpPage)
        return;

    --m_iIndex;
    PlayBuffer(SOUND_CLICK01);
    PublishHelpHaptic(Core::Haptics::HapticEvent::FocusMoved);
}

void SEASON3B::CNewUIHelpWindow::Close()
{
    g_pNewUISystem->Hide(SEASON3B::INTERFACE_HELP);
    PlayBuffer(SOUND_CLICK01);

    if (m_isOnboarding)
    {
        // Finishing (or skipping) the first-launch onboarding records it so it
        // does not show again. Players can reopen the pages manually via Help.
        m_isOnboarding = false;
        GameConfig::GetInstance().SetTutorialCompleted(true);
        PublishHelpHaptic(Core::Haptics::HapticEvent::Confirmed);
    }
    else
    {
        PublishHelpHaptic(Core::Haptics::HapticEvent::Cancelled);
    }
}

bool SEASON3B::CNewUIHelpWindow::HandleGamepadInput()
{
    const auto& frame = Core::Input::GamepadService::Instance().LastFrameState();
    const auto action = [&frame](Core::Input::InputAction inputAction)
        -> const Core::Input::InputActionState&
    {
        return frame.actions[static_cast<std::size_t>(inputAction)];
    };

    if (action(Core::Input::InputAction::Cancel).pressed)
    {
        Close();
        return true;
    }
    if (action(Core::Input::InputAction::PreviousPage).pressed)
    {
        PreviousPage();
        return true;
    }
    if (action(Core::Input::InputAction::NextPage).pressed
        || action(Core::Input::InputAction::Confirm).pressed)
    {
        NextPage();
        return true;
    }
    return false;
}

void SEASON3B::CNewUIHelpWindow::RenderTutorialPage()
{
    wchar_t (&textList)[50][100] = ::TextList;
    int (&textListColor)[50] = ::TextListColor;
    int (&textBold)[50] = ::TextBold;

    constexpr int HeadingColor = TEXT_COLOR_BLUE;
    constexpr int BodyColor = TEXT_COLOR_WHITE;
    int textNumber = 0;

    auto blank = [&]()
    {
        mu_swprintf(textList[textNumber], L"\n");
        ++textNumber;
    };
    auto heading = [&](const wchar_t* text)
    {
        wcscpy(textList[textNumber], text);
        textListColor[textNumber] = HeadingColor;
        textBold[textNumber] = true;
        ++textNumber;
    };
    auto body = [&](const wchar_t* text)
    {
        wcsncpy_s(textList[textNumber], text, 99);
        textListColor[textNumber] = BodyColor;
        textBold[textNumber] = false;
        ++textNumber;
    };

    blank();
    heading(I18N::Game::TutorialTitle);
    blank();

    switch (m_iIndex)
    {
    case TutorialWelcomePage:
        body(I18N::Game::TutorialWelcome);
        break;
    case TutorialDesktopPage:
        body(I18N::Game::TutorialDesktopControls);
        blank();
        body(I18N::Game::TutorialKeyboardControls);
        break;
    case TutorialTouchPage:
        body(I18N::Game::TutorialTouchControls);
        blank();
        body(I18N::Game::TutorialTouchGestures);
        break;
    case TutorialGamepadPage:
        body(I18N::Game::TutorialGamepadControls);
        blank();
        body(I18N::Game::TutorialGamepadSettings);
        break;
    case TutorialFinishPage:
        body(I18N::Game::TutorialFinish);
        break;
    }

    blank();
    body(I18N::Game::TutorialPaging);

    RenderTipTextList(1, 1, textNumber, 0, RT3_SORT_CENTER);
}
