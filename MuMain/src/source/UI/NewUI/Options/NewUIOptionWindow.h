// NewUIOptionWindow.h: interface for the CNewUIOptionWindow class.
//
//////////////////////////////////////////////////////////////////////

#if !defined(AFX_NEWUIOPTIONWINDOW_H__1469FA1D_7C15_4AFE_AD6E_59C303E72BC0__INCLUDED_)
#define AFX_NEWUIOPTIONWINDOW_H__1469FA1D_7C15_4AFE_AD6E_59C303E72BC0__INCLUDED_

#pragma once

#include "UI/NewUI/NewUIManager.h"
#include "UI/NewUI/Inventory/NewUIMyInventory.h"
#include "UI/NewUI/Widgets/NewUIComboBox.h"
#include "Core/Input/GamepadTypes.h"

#include <cstdint>

namespace SEASON3B
{
    class CNewUIOptionWindow : public CNewUIObj
    {
#ifdef KJH_ADD_INGAMESHOP_UI_SYSTEM
    public:
#endif // KJH_ADD_INGAMESHOP_UI_SYSTEM
        enum IMAGE_LIST
        {
            IMAGE_OPTION_FRAME_BACK = CNewUIMessageBoxMng::IMAGE_MSGBOX_BACK,
            IMAGE_OPTION_BTN_CLOSE = CNewUIMessageBoxMng::IMAGE_MSGBOX_BTN_CLOSE,
            IMAGE_OPTION_FRAME_DOWN = CNewUIMyInventory::IMAGE_INVENTORY_BACK_BOTTOM,

            IMAGE_OPTION_FRAME_UP = BITMAP_OPTION_BEGIN,
            IMAGE_OPTION_FRAME_LEFT,
            IMAGE_OPTION_FRAME_RIGHT,
            IMAGE_OPTION_LINE,
            IMAGE_OPTION_POINT,
            IMAGE_OPTION_BTN_CHECK,
            IMAGE_OPTION_EFFECT_BACK,
            IMAGE_OPTION_EFFECT_COLOR,
            IMAGE_OPTION_VOLUME_BACK,
            IMAGE_OPTION_VOLUME_COLOR,
        };

    public:
        CNewUIOptionWindow();
        virtual ~CNewUIOptionWindow();

        bool Create(CNewUIManager* pNewUIMng, int x, int y);
        void Release();

        void SetPos(int x, int y);

        bool UpdateMouseEvent();
        bool UpdateKeyEvent();
        bool Update();
        bool Render();

        float GetLayerDepth();	//. 10.5f
        float GetKeyEventOrder();	// 10.f;

        void OpenningProcess();
        void ClosingProcess();

        void SetAutoAttack(bool bAuto);
        bool IsAutoAttack();
        void SetWhisperSound(bool bSound);
        bool IsWhisperSound();
        void SetSlideHelp(bool bHelp);
        bool IsSlideHelp();
        void SetVolumeLevel(int iVolume);
        int GetVolumeLevel();
        void SetRenderLevel(int iRender);
        int GetRenderLevel();
        void SetRenderAllEffects(bool bRenderAllEffects);
        bool GetRenderAllEffects();
        bool IsDisplayChangePending() const { return m_bDisplayChangePending; }

    private:
        void LoadImages();
        void UnloadImages();

        void SetButtonInfo();

        void RenderFrame();
        void RenderContents();
        void RenderButtons();

        // UpdateMouseEvent helpers
        bool ProcessMouseEvent();
        void RegisterFocusNodes();
        void RegisterStandardFocusNodes();
        void RegisterDisplayConfirmationFocusNodes();
        void RegisterRestartPromptFocusNodes();
        void HandleFocusedAdjustment();
        void RememberCurrentFocus();
        void FocusOptionControl(std::uint32_t focusId);
        void UpdateMappingCapture();
        void ApplyCapturedMapping(int row, Core::Input::GamepadControl control);
        void UpdateRestartPromptMouseEvent();
        void AcceptRestartForLanguage();
        void RenderRestartPrompt();
        void RenderFocusHighlight();
        CNewUIComboBox* FindOpenCombo();
        void HandleCheckboxInputs();
        bool HandleVolumeSlider(int& level, int yOffset);
        bool HandleIntegerSlider(int& value, int minimum, int maximum,
                                 int xLocal, int yLocal, int width, int wheelStep);
        void HandleAdvancedInputs();
        void OnSoundVolumeChanged();
        void OnMusicVolumeChanged();
        void HandleRenderLevelSlider();
        void ApplyGamepadSettings();
        void ApplyHapticSettings();
        void PublishSettingHaptic();
        void BeginDisplayChange(unsigned int width, unsigned int height, bool windowed);
        void AcceptDisplayChange();
        void RevertDisplayChange();
        void UpdateDisplayChange();
        void UpdateDisplayChangeMouseEvent();
        void RenderDisplayChangeConfirmation();
        void PersistCurrentDisplaySettings();

    private:
        CNewUIManager* m_pNewUIMng;
        POINT						m_Pos;

        CNewUIButton m_BtnClose;

        bool m_bAutoAttack;
        bool m_bWhisperSound;
        bool m_bSlideHelp;
        int m_iVolumeLevel;     // Sound volume (0=off, 10=max)
        int m_iMusicLevel;      // Music volume (0=off, 10=max)
        int m_iRenderLevel;
        bool m_bRenderAllEffects;
        int m_iResolutionIndex;
        bool m_bWindowedMode;
        int m_iLanguageIndex;
        int m_iFontIndex;
        int m_iFrameRateIndex;
        int m_iFrameRateFps;       // mobile frame-rate slider: selected FPS (min..m_iFrameRateMaxFps)
        int m_iFrameRateMaxFps;    // mobile frame-rate slider: detected device ceiling
        bool m_bVSync;
        bool m_bGamepadEnabled;
        int m_iStickDeadZonePercent;
        int m_iTriggerDeadZonePercent;
        int m_iPointerSpeed;
        bool m_bInvertPointerY;
        int m_iGamepadActionIndex;
        int m_iGamepadControlIndex;
        bool m_bHapticsEnabled;
        int m_iHapticIntensity;
        bool m_bCombatHaptics;
        bool m_bUIHaptics;
        bool m_bTransactionHaptics;
        bool m_bDisplayChangePending = false;
        bool m_bPreviousWindowedMode = true;
        unsigned int m_uPreviousWindowWidth = 0;
        unsigned int m_uPreviousWindowHeight = 0;
        std::uint64_t m_uDisplayChangeDeadlineMs = 0;

        // Controller-driven Yes/No prompt for the post-language-switch restart.
        bool m_bRestartPromptPending = false;
        // >= 0 while waiting for a physical control to bind to this mapping row.
        int m_iMappingCaptureRow = -1;

        // Set when a combo consumes a click; swallows the rest of that mouse-hold
        // so the release can't fall through to the Close button (see UpdateMouseEvent).
        bool m_bSwallowClickHold = false;
        std::uintptr_t m_uPreviousFocusOwner = 0;
        std::uint32_t m_uPreviousFocusId = 0;
        std::uint32_t m_uActiveAdjustmentFocusId = 0;
        int m_iHeldAdjustmentDirection = 0;
        std::uint64_t m_uNextAdjustmentRepeatMs = 0;

        CNewUIComboBox m_ResolutionCombo;
        CNewUIComboBox m_LanguageCombo;
        CNewUIComboBox m_FontCombo;
        CNewUIComboBox m_FrameRateCombo;
        CNewUIComboBox m_GamepadActionCombo;
        CNewUIComboBox m_GamepadControlCombo;

        void ApplyResolution();
        int FindCurrentResolutionIndex();
        void InitResolutionCombo();
        void SyncResolutionComboToWindow();
        void ApplyWindowModeToggle();

        void ApplyLanguage();
        int FindCurrentLanguageIndex();
        void InitLanguageCombo();

        void ApplyFont();
        int FindCurrentFontIndex();
        void InitFontCombo();

        void ApplyFrameRate();
        int FindCurrentFrameRateIndex();
        void InitFrameRateCombo();

        // Mobile (Android / HarmonyOS) frame-rate slider: 30 FPS .. device max.
        int DetectMaximumFrameRate() const;
        void InitFrameRateSlider();
        bool HandleFrameRateSlider();
        void ApplyFrameRateSlider();
        void RenderFrameRateSlider();

        void InitGamepadMappingCombos();
        void ApplyGamepadActionSelection();
        void ApplyGamepadControlSelection();
        void ResetGamepadBindings();
    };
}

#endif // !defined(AFX_NEWUIOPTIONWINDOW_H__1469FA1D_7C15_4AFE_AD6E_59C303E72BC0__INCLUDED_)
