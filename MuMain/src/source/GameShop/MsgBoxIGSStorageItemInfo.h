// MsgBoxIGSStorageItemInfo.h: interface for the MsgBoxIGSUseItem class.
//
//////////////////////////////////////////////////////////////////////

#if !defined(AFX_MSGBOXIGSUSEITEM_H__5E717B05_9D6D_4E85_B168_47D5EEA59CF7__INCLUDED_)
#define AFX_MSGBOXIGSUSEITEM_H__5E717B05_9D6D_4E85_B168_47D5EEA59CF7__INCLUDED_

#if _MSC_VER > 1000
#pragma once
#endif // _MSC_VER > 1000

#ifdef KJH_ADD_INGAMESHOP_UI_SYSTEM

#include "UI/Legacy/UIControls.h"
#include "UI/NewUI/Dialogs/NewUIMessageBox.h"
#include "UI/NewUI/Dialogs/NewUICommonMessageBox.h"

using namespace SEASON3B;

class CMsgBoxIGSStorageItemInfo : public CNewUIMessageBoxBase, public INewUI3DRenderObj
{
public:
    enum IMAGE_IGS_STORAGE_ITEM_INFO
    {
        IMAGE_IGS_BUTTON = BITMAP_IGS_MSGBOX_BUTTON,				// In-game shop button
        IMAGE_IGS_FRAME = BITMAP_IGS_MSGBOX_STORAGE_ITEM,		// Main Frame
    };

    enum IMAGESIZE_IGS_STORAGE_ITEM_INFO
    {
        IMAGE_IGS_WINDOW_WIDTH = 640,	// In-game shop background size
        IMAGE_IGS_WINDOW_HEIGHT = 429,
        IMAGE_IGS_FRAME_WIDTH = 210,	// Message box Size
        IMAGE_IGS_FRAME_HEIGHT = 202,
        IMAGE_IGS_BTN_WIDTH = 52,		// Button Size
        IMAGE_IGS_BTN_HEIGHT = 26,
    };

    // Relative coordinates on the message box
    enum IGS_STORAGE_ITEM_INFO_POS
    {
        IGS_BTN_OK_POS_X = 43,	// Button
        IGS_BTN_CANCEL_POS_X = 115,
        IGS_BTN_POS_Y = 168,
        IGS_TEXT_TITLE_POS_Y = 10,	// Title
        IGS_TEXT_ITEM_NAME_POS_Y = 100,	// ItemName
        IGS_TEXT_ITEM_INFO_POS_X = 30,	// ItemInfo
        IGS_TEXT_ITEM_INFO_NUM_POS_Y = 124,
        IGS_TEXT_ITEM_INFO_PERIOD_POS_Y = 140,
        IGS_TEXT_ITEM_INFO_WIDTH = 150,
        IGS_3DITEM_POS_X = 56,		// 3D item render start position (BOX reference)
        IGS_3DITEM_POS_Y = 34,
        IGS_3DITEM_WIDTH = 97,		// 3D item render space setting
        IGS_3DITEM_HEIGHT = 60,
    };

public:
    CMsgBoxIGSStorageItemInfo();
    ~CMsgBoxIGSStorageItemInfo();

    bool Create(float fPriority = 3.f);
    void Release();
    bool Update();
    bool Render();

    bool IsVisible() const;

    void Render3D();

    void Initialize(int iStorageSeq, int iStorageItemSeq, WORD wItemCode, char szItemType,
        wchar_t* pszName, wchar_t* pszNum, wchar_t* pszPeriod);

    static CALLBACK_RESULT LButtonUp(class CNewUIMessageBoxBase* pOwner, const leaf::xstreambuf& xParam);
    static CALLBACK_RESULT OKButtonDown(class CNewUIMessageBoxBase* pOwner, const leaf::xstreambuf& xParam);
    static CALLBACK_RESULT CancelButtonDown(class CNewUIMessageBoxBase* pOwner, const leaf::xstreambuf& xParam);

private:
    void SetAddCallbackFunc();
    void SetButtonInfo();

    void RenderFrame();
    void RenderTexts();
    void RenderButtons();

    void LoadImages();
    void UnloadImages();

private:
    // buttons
    CNewUIMessageBoxButton m_BtnUse;
    CNewUIMessageBoxButton m_BtnCancel;

    int		m_iStorageSeq;			// Storage sequence number
    int		m_iStorageItemSeq;		// Storage product sequence number
    WORD	m_wItemCode;			// Item code

    wchar_t m_szName[MAX_TEXT_LENGTH];		// Item name
    wchar_t m_szNum[MAX_TEXT_LENGTH];
    wchar_t m_szPeriod[MAX_TEXT_LENGTH];
    char    m_szItemType;	// Product type (C : Cash, P : Product)
};

////////////////////////////////////////////////////////////////////
// LayOut
////////////////////////////////////////////////////////////////////
class CMsgBoxIGSStorageItemInfoLayout : public TMsgBoxLayout<CMsgBoxIGSStorageItemInfo>
{
public:
    bool SetLayout();
};

#endif // KJH_ADD_INGAMESHOP_UI_SYSTEM

#endif // !defined(AFX_MSGBOXIGSUSEITEM_H__5E717B05_9D6D_4E85_B168_47D5EEA59CF7__INCLUDED_)
