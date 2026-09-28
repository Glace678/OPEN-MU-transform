//*****************************************************************************
// File: Sprite.h
//*****************************************************************************

#if !defined(AFX_SPRITE_H__1696B800_F6E1_4AB2_AA02_F67BBA8EFD2E__INCLUDED_)
#define AFX_SPRITE_H__1696B800_F6E1_4AB2_AA02_F67BBA8EFD2E__INCLUDED_

#pragma once

#include <set>

#include "Render/Textures/ZzzTexture.h"
#include "UI/Widgets/UIBaseDef.h"

#define	SPR_SIZING_DATUMS_LT	0x00
#define	SPR_SIZING_DATUMS_LB	0x01
#define	SPR_SIZING_DATUMS_RT	0x02
#define	SPR_SIZING_DATUMS_RB	0x03
#define	IS_SIZING_DATUMS_R(v)	(v & 0x02)
#define	IS_SIZING_DATUMS_B(v)	(v & 0x01)

class CSprite
{
protected:
    // Every live sprite registers itself so resolution changes can refresh
    // its effective scale without the scene/window owners re-Creating it.
    static std::set<CSprite*> s_registry;

    float		m_fScrHeight;

    BITMAP_t* m_pTexture;
    int			m_nTexID;
    float		m_fOrgWidth;
    float		m_fOrgHeight;

    SScrCoord	m_aScrCoord[POS_MAX];
    STexCoord	m_aTexCoord[POS_MAX];
    float		m_fDatumX;
    float		m_fDatumY;

    BYTE		m_byAlpha;
    BYTE		m_byRed;
    BYTE		m_byGreen;
    BYTE		m_byBlue;

    int			m_nMaxFrame;
    int			m_nNowFrame;
    int			m_nStartFrame;
    int			m_nEndFrame;
    STexCoord* m_aFrameTexCoord;
    bool		m_bRepeat;
    double		m_dDelayTime;
    double		m_dDeltaTickSum;

    // Scale model:
    //   m_fArtScaleX/Y - explicit art-space multiplier requested by the owner
    //     (legacy title/credit scenes used this to stretch their art canvases).
    //   resolution scale - g_fScreenRate_x/y, maps 640x480 reference art space
    //     to the actual window size (e.g. 4.5 on a 2880-wide window).
    //   m_fScaleX/Y - the EFFECTIVE scale used at render time.
    // Coordinates are stored in ART space (reference 640x480, bottom-left
    // origin); the legacy device-pixel public API divides by the resolution
    // scale on input and multiplies on output, so positioning callers keep
    // working in device pixels while the art renders crisp at any resolution.
    float		m_fArtScaleX;
    float		m_fArtScaleY;
    float		m_fScaleX;
    float		m_fScaleY;
    float		m_fArtPosX;
    float		m_fArtPosY;
    float		m_fArtWidth;
    float		m_fArtHeight;
    bool		m_bTile;
    int			m_nSizingDatums;
    bool		m_bShow;

public:
    CSprite();
    virtual ~CSprite();

    static float ResolutionScaleX()
    {
        return 0.0f < g_fScreenRate_x ? g_fScreenRate_x : 1.0f;
    }
    static float ResolutionScaleY()
    {
        return 0.0f < g_fScreenRate_y ? g_fScreenRate_y : 1.0f;
    }
    // Recompute every sprite's effective scale/art screen height after the
    // window resolution changed. Safe to call with sprites in any state.
    static void RefreshAllResolutionScales();

    void Release();
    void Create(int nOrgWidth, int nOrgHeight, int nTexID = -1,
        int nMaxFrame = 0, SFrameCoord* aFrameCoord = NULL, int nDatumX = 0,
        int nDatumY = 0, bool bTile = false,
        int nSizingDatums = SPR_SIZING_DATUMS_LT, float fScaleX = 1.0f,
        float fScaleY = 1.0f);
    void Create(SImgInfo* pImgInfo, int nDatumX = 0, int nDatumY = 0,
        bool bTile = false, int nSizingDatums = SPR_SIZING_DATUMS_LT,
        float fScaleX = 1.0f, float fScaleY = 1.0f);

    // Position/size in DEVICE pixels (legacy callers use this).
    void SetPosition(int nXCoord, int nYCoord, CHANGE_PRAM eChangedPram = XY);
    void SetSize(int nWidth, int nHeight, CHANGE_PRAM eChangedPram = XY);

    // Position/size directly in ART/reference units (used by composite art
    // canvases such as the title/credit/loading pictures).
    void SetPositionArt(float fXCoord, float fYCoord, CHANGE_PRAM eChangedPram = XY);
    void SetSizeArt(float fWidth, float fHeight, CHANGE_PRAM eChangedPram = XY);

    int GetXPos() const
    {
        return int(m_aScrCoord[LT].fX * m_fScaleX);
    }
    int GetYPos() const
    {
        return int((m_fScrHeight - m_aScrCoord[LT].fY) * m_fScaleY);
    }
    int GetWidth() const
    {
        return int((m_aScrCoord[RT].fX - m_aScrCoord[LT].fX) * m_fScaleX);
    }
    int GetHeight() const
    {
        return int((m_aScrCoord[LT].fY - m_aScrCoord[LB].fY) * m_fScaleY);
    }
    int GetTexID() { return m_nTexID; };
    int GetTexWidth()
    {
        return -1 < m_nTexID ? (int)m_pTexture->Width : 0;
    }
    int GetTexHeight()
    {
        return -1 < m_nTexID ? (int)m_pTexture->Height : 0;
    }

    float GetScaleX() const { return m_fScaleX; }
    float GetScaleY() const { return m_fScaleY; }
    float GetArtScaleX() const { return m_fArtScaleX; }
    float GetArtScaleY() const { return m_fArtScaleY; }
    float GetScrHeight() const { return m_fScrHeight; }
    void Show(bool bShow = true) { m_bShow = bShow; }
    bool IsShow() const { return m_bShow; }
    int GetSizingDatums() { return m_nSizingDatums; }
    BOOL PtInSprite(long lXPos, long lYPos);
    BOOL CursorInObject();
    void SetAlpha(BYTE byAlpha) { m_byAlpha = byAlpha; }
    BYTE GetAlpha() { return m_byAlpha; }
    void SetColor(BYTE byRed, BYTE byGreen, BYTE byBlue)
    {
        m_byRed = byRed;	m_byGreen = byGreen;	m_byBlue = byBlue;
    }
    void SetAction(int nStartFrame, int nEndFrame, double dDelayTime = 0.0,
        bool bRepeat = true);
    void SetNowFrame(int nFrame);
    void Update(double dDeltaTick = 0.0);
    void Render();

protected:
    void ApplyArtPosition(float fXCoord, float fYCoord, CHANGE_PRAM eChangedPram);
    void ApplyArtSize(float fWidth, float fHeight, CHANGE_PRAM eChangedPram,
        bool bAdjustTexCoords);
};

#endif // !defined(AFX_SPRITE_H__1696B800_F6E1_4AB2_AA02_F67BBA8EFD2E__INCLUDED_)
