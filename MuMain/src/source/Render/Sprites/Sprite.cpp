//*****************************************************************************
// File: Sprite.cpp
//*****************************************************************************

#include "stdafx.h"
#include "Render/Sprites/Sprite.h"

#include "Core/Input/Input.h"

#include "Render/Textures/ZzzOpenglUtil.h"
#include "Render/Core/RenderConfig.h"
#include "Render/Core/ImmediateRenderer.h"
#include "Render/Shaders/PassthroughShader.h"

#include "Core/Platform/CrtDbg.h"

std::set<CSprite*> CSprite::s_registry;

CSprite::CSprite()
{
    m_aFrameTexCoord = NULL;
    m_pTexture = NULL;

    m_fArtScaleX = m_fArtScaleY = 1.0f;
    m_fScaleX = m_fScaleY = 1.0f;
    m_fArtPosX = m_fArtPosY = 0.0f;
    m_fArtWidth = m_fArtHeight = 0.0f;

    s_registry.insert(this);
}

CSprite::~CSprite()
{
    s_registry.erase(this);
    Release();
}

void CSprite::Release()
{
    m_pTexture = NULL;
    SAFE_DELETE_ARRAY(m_aFrameTexCoord);
}

void CSprite::RefreshAllResolutionScales()
{
    for (CSprite* pSprite : s_registry)
    {
        pSprite->m_fScaleX = pSprite->m_fArtScaleX * ResolutionScaleX();
        pSprite->m_fScaleY = pSprite->m_fArtScaleY * ResolutionScaleY();
        pSprite->m_fScrHeight = (float)REFERENCE_HEIGHT / pSprite->m_fArtScaleY;

        // Rebuild the quad from stored art position/size. Texture coordinates
        // are left untouched (tile expansion was applied once at SetSize).
        pSprite->ApplyArtPosition(pSprite->m_fArtPosX, pSprite->m_fArtPosY, XY);
        pSprite->ApplyArtSize(pSprite->m_fArtWidth, pSprite->m_fArtHeight, XY,
            false);
    }
}

void CSprite::Create(int nOrgWidth, int nOrgHeight, int nTexID, int nMaxFrame, SFrameCoord* aFrameCoord, int nDatumX, int nDatumY, bool bTile, int nSizingDatums, float fScaleX, float fScaleY)
{
    Release();

    m_fOrgWidth = (float)nOrgWidth;
    m_fOrgHeight = (float)nOrgHeight;
    m_nTexID = nTexID;
    m_pTexture = Bitmaps.FindTexture(m_nTexID);

    m_fArtScaleX = fScaleX;
    m_fArtScaleY = fScaleY;
    m_fScaleX = m_fArtScaleX * ResolutionScaleX();
    m_fScaleY = m_fArtScaleY * ResolutionScaleY();

    // Art-space screen height: REFERENCE_HEIGHT divided by the owner-requested
    // art Y multiplier. (Equivalent to the legacy WindowHeight / effective Y.)
    m_fScrHeight = (float)REFERENCE_HEIGHT / m_fArtScaleY;

    m_aScrCoord[LT].fX = 0.0f;
    m_aScrCoord[LT].fY = m_fScrHeight;
    m_aScrCoord[LB].fX = 0.0f;
    m_aScrCoord[LB].fY = m_fScrHeight - m_fOrgHeight;
    m_aScrCoord[RB].fX = m_fOrgWidth;
    m_aScrCoord[RB].fY = m_fScrHeight - m_fOrgHeight;
    m_aScrCoord[RT].fX = m_fOrgWidth;
    m_aScrCoord[RT].fY = m_fScrHeight;

    m_fArtPosX = m_fArtPosY = 0.0f;
    m_fArtWidth = m_fOrgWidth;
    m_fArtHeight = m_fOrgHeight;

    m_nNowFrame = -1;

    if (-1 < m_nTexID)
    {
        m_aTexCoord[LT].fTU = 0.5f / m_pTexture->Width;
        m_aTexCoord[LT].fTV = 0.5f / m_pTexture->Height;
        m_aTexCoord[LB].fTU = 0.5f / m_pTexture->Width;
        m_aTexCoord[LB].fTV = (m_fOrgHeight - 0.5f) / m_pTexture->Height;
        m_aTexCoord[RB].fTU = (m_fOrgWidth - 0.5f) / m_pTexture->Width;
        m_aTexCoord[RB].fTV = (m_fOrgHeight - 0.5f) / m_pTexture->Height;
        m_aTexCoord[RT].fTU = (m_fOrgWidth - 0.5f) / m_pTexture->Width;
        m_aTexCoord[RT].fTV = 0.5f / m_pTexture->Height;

        if (NULL != aFrameCoord)
        {
            _ASSERT(0 < nMaxFrame);

            m_nMaxFrame = nMaxFrame;

            m_aFrameTexCoord = new STexCoord[m_nMaxFrame];

            for (int i = 0; i < nMaxFrame; ++i)
            {
                m_aFrameTexCoord[i].fTU = ((float)aFrameCoord[i].nX + 0.5f) / m_pTexture->Width;
                m_aFrameTexCoord[i].fTV = ((float)aFrameCoord[i].nY + 0.5f) / m_pTexture->Height;
            }

            m_nStartFrame = m_nEndFrame = 0;
            SetNowFrame(0);
            m_bTile = false;
        }
        else
        {
            m_nMaxFrame = 0;
            m_nStartFrame = m_nEndFrame = -1;
            m_bTile = bTile;
        }
    }
    else
    {
        ::memset(m_aTexCoord, 0, sizeof(STexCoord) * POS_MAX);

        m_nMaxFrame = 0;
        m_nStartFrame = m_nEndFrame = -1;
        m_bTile = false;
    }

    m_byAlpha = m_byRed = m_byGreen = m_byBlue = 255;

    m_fDatumX = (float)nDatumX;
    m_fDatumY = (float)nDatumY;

    m_bRepeat = false;
    m_dDelayTime = m_dDeltaTickSum = 0.0;
    m_nSizingDatums = nSizingDatums;
    m_bShow = false;
}

void CSprite::Create(SImgInfo* pImgInfo, int nDatumX, int nDatumY, bool bTile,
    int nSizingDatums, float fScaleX, float fScaleY)
{
    if (pImgInfo->nX == 0 && pImgInfo->nY == 0)
        Create(pImgInfo->nWidth, pImgInfo->nHeight, pImgInfo->nTexID, 0, NULL, nDatumX, nDatumY, bTile, nSizingDatums, fScaleX, fScaleY);
    else
    {
        SFrameCoord frameCoord = { pImgInfo->nX, pImgInfo->nY };
        Create(pImgInfo->nWidth, pImgInfo->nHeight, pImgInfo->nTexID, 1,
            &frameCoord, nDatumX, nDatumY, bTile, nSizingDatums, fScaleX,
            fScaleY);
    }
}

void CSprite::SetPosition(int nXCoord, int nYCoord, CHANGE_PRAM eChangedPram)
{
    float fArtX = (float)nXCoord / ResolutionScaleX();
    float fArtY = (float)nYCoord / ResolutionScaleY();
    SetPositionArt(fArtX, fArtY, eChangedPram);
}

void CSprite::SetPositionArt(float fXCoord, float fYCoord, CHANGE_PRAM eChangedPram)
{
    if (eChangedPram & X)
        m_fArtPosX = fXCoord;
    if (eChangedPram & Y)
        m_fArtPosY = fYCoord;

    ApplyArtPosition(fXCoord, fYCoord, eChangedPram);
}

void CSprite::ApplyArtPosition(float fXCoord, float fYCoord, CHANGE_PRAM eChangedPram)
{
    if (eChangedPram & X)
    {
        float fWidth = m_aScrCoord[RT].fX - m_aScrCoord[LT].fX;

        if (IS_SIZING_DATUMS_R(m_nSizingDatums))
        {
            m_aScrCoord[RT].fX = m_aScrCoord[RB].fX = fXCoord + m_fOrgWidth - m_fDatumX;
            m_aScrCoord[LT].fX = m_aScrCoord[LB].fX = m_aScrCoord[RT].fX - fWidth;
        }
        else
        {
            m_aScrCoord[LT].fX = m_aScrCoord[LB].fX = fXCoord - m_fDatumX;
            m_aScrCoord[RT].fX = m_aScrCoord[RB].fX = m_aScrCoord[LT].fX + fWidth;
        }
    }

    if (eChangedPram & Y)
    {
        float fHeight = m_aScrCoord[LT].fY - m_aScrCoord[LB].fY;

        if (IS_SIZING_DATUMS_B(m_nSizingDatums))
        {
            m_aScrCoord[LB].fY = m_aScrCoord[RB].fY = m_fScrHeight - fYCoord - m_fOrgHeight + m_fDatumY;

            m_aScrCoord[LT].fY = m_aScrCoord[RT].fY = m_aScrCoord[LB].fY + fHeight;
        }
        else
        {
            m_aScrCoord[LT].fY = m_aScrCoord[RT].fY = m_fScrHeight - fYCoord + m_fDatumY;
            m_aScrCoord[LB].fY = m_aScrCoord[RB].fY = m_aScrCoord[LT].fY - fHeight;
        }
    }
}

void CSprite::SetSize(int nWidth, int nHeight, CHANGE_PRAM eChangedPram)
{
    SetSizeArt((float)nWidth / ResolutionScaleX(),
        (float)nHeight / ResolutionScaleY(), eChangedPram);
}

void CSprite::SetSizeArt(float fWidth, float fHeight, CHANGE_PRAM eChangedPram)
{
    if (eChangedPram & X)
        m_fArtWidth = fWidth;
    if (eChangedPram & Y)
        m_fArtHeight = fHeight;

    ApplyArtSize(fWidth, fHeight, eChangedPram, true);
}

void CSprite::ApplyArtSize(float fWidth, float fHeight, CHANGE_PRAM eChangedPram,
    bool bAdjustTexCoords)
{
    if (eChangedPram & X)
    {
        if (IS_SIZING_DATUMS_R(m_nSizingDatums))
        {
            m_aScrCoord[LT].fX = m_aScrCoord[LB].fX = m_aScrCoord[RT].fX - fWidth;
            if (bAdjustTexCoords && m_bTile)
                m_aTexCoord[LT].fTU = m_aTexCoord[LB].fTU = m_aTexCoord[RT].fTU - fWidth / m_pTexture->Width;
        }
        else
        {
            m_aScrCoord[RT].fX = m_aScrCoord[RB].fX = m_aScrCoord[LT].fX + fWidth;
            if (bAdjustTexCoords && m_bTile)
                m_aTexCoord[RT].fTU = m_aTexCoord[RB].fTU = fWidth / m_pTexture->Width;
        }
    }
    if (eChangedPram & Y)
    {
        if (IS_SIZING_DATUMS_B(m_nSizingDatums))
        {
            m_aScrCoord[LT].fY = m_aScrCoord[RT].fY = m_aScrCoord[LB].fY + fHeight;
            if (bAdjustTexCoords && m_bTile)
                m_aTexCoord[LT].fTV = m_aTexCoord[RT].fTV = m_aTexCoord[LB].fTV - fHeight / m_pTexture->Height;
        }
        else
        {
            m_aScrCoord[LB].fY = m_aScrCoord[RB].fY = m_aScrCoord[LT].fY - fHeight;
            if (bAdjustTexCoords && m_bTile)
                m_aTexCoord[LB].fTV = m_aTexCoord[RB].fTV = fHeight / m_pTexture->Height;
        }
    }
}

BOOL CSprite::PtInSprite(long lXPos, long lYPos)
{
    if (!m_bShow)
        return FALSE;

    POINT pt = Core::Platform::MakePoint(lXPos, lYPos);

    RECT rc = Core::Platform::MakeRect(
        long(m_aScrCoord[LT].fX * m_fScaleX),
        long((m_fScrHeight - m_aScrCoord[LT].fY) * m_fScaleY),
        long(m_aScrCoord[RB].fX * m_fScaleX),
        long((m_fScrHeight - m_aScrCoord[RB].fY) * m_fScaleY)
    );

    return ::PtInRect(&rc, pt);
}

BOOL CSprite::CursorInObject()
{
    CInput& rInput = CInput::Instance();

    return PtInSprite(rInput.GetCursorX(), rInput.GetCursorY());
}

void CSprite::SetAction(int nStartFrame, int nEndFrame, double dDelayTime,
    bool bRepeat)
{
    if (1 >= m_nMaxFrame)
        return;

    _ASSERT(nStartFrame <= nEndFrame && nStartFrame >= 0
        && nEndFrame < m_nMaxFrame);

    m_nStartFrame = m_nNowFrame = nStartFrame;
    m_nEndFrame = nEndFrame;
    m_bRepeat = bRepeat;
    m_dDelayTime = dDelayTime;
}

void CSprite::SetNowFrame(int nFrame)
{
    if (NULL == m_aFrameTexCoord || nFrame == m_nNowFrame)
        return;

    if (nFrame < m_nStartFrame || nFrame > m_nEndFrame)
        return;

    m_nNowFrame = nFrame;

    float fTUWidth = m_aTexCoord[RT].fTU - m_aTexCoord[LT].fTU;
    float fTVHeight = m_aTexCoord[LB].fTV - m_aTexCoord[LT].fTV;

    m_aTexCoord[LT] = m_aFrameTexCoord[m_nNowFrame];

    m_aTexCoord[RT].fTU = m_aFrameTexCoord[m_nNowFrame].fTU + fTUWidth;
    m_aTexCoord[RT].fTV = m_aFrameTexCoord[m_nNowFrame].fTV;

    m_aTexCoord[LB].fTU = m_aFrameTexCoord[m_nNowFrame].fTU;
    m_aTexCoord[LB].fTV = m_aFrameTexCoord[m_nNowFrame].fTV + fTVHeight;

    m_aTexCoord[RB].fTU = m_aTexCoord[RT].fTU;
    m_aTexCoord[RB].fTV = m_aTexCoord[LB].fTV;
}

void CSprite::Update(double dDeltaTick)
{
    if (!m_bShow)
        return;

    if (1 >= m_nMaxFrame)
        return;

    m_dDeltaTickSum += dDeltaTick;

    if (m_dDeltaTickSum >= m_dDelayTime)
    {
        int nFrame = m_nNowFrame;

        if (m_bRepeat)
            nFrame = ++nFrame > m_nEndFrame ? m_nStartFrame : nFrame;
        else
            nFrame = ++nFrame > m_nEndFrame ? m_nEndFrame : nFrame;

        SetNowFrame(nFrame);

        m_dDeltaTickSum = 0.0f;
    }
}

void CSprite::Render()
{
    if (!m_bShow)
        return;

    if (-1 < m_nTexID)
    {
        BindTexture(m_nTexID);
        PassthroughShader::Instance().SetUseTexture(true);
        IR::Begin(GL_TRIANGLE_FAN);
        IR::Color4ub(m_byRed, m_byGreen, m_byBlue, m_byAlpha);
        for (int i = LT; i < POS_MAX; ++i)
        {
            IR::TexCoord2f(m_aTexCoord[i].fTU, m_aTexCoord[i].fTV);
            IR::Vertex2f(m_aScrCoord[i].fX * m_fScaleX,
                m_aScrCoord[i].fY * m_fScaleY);
        }
        IR::End();
    }
    else
    {
        PassthroughShader::Instance().SetUseTexture(false);
        IR::Begin(GL_TRIANGLE_FAN);
        IR::Color4ub(m_byRed, m_byGreen, m_byBlue, m_byAlpha);
        for (int i = LT; i < POS_MAX; ++i)
        {
            IR::Vertex2f(m_aScrCoord[i].fX * m_fScaleX,
                m_aScrCoord[i].fY * m_fScaleY);
        }
        IR::End();
    }
}
