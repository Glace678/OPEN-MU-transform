// NewUIBloodCastle.h: interface for the CNewUIPartyInfo class.
//
//////////////////////////////////////////////////////////////////////

#ifndef _NEWUIBLOODCASTLE_H_
#define _NEWUIBLOODCASTLE_H_

#pragma once

#include "UI/NewUI/NewUIBase.h"
#include "UI/NewUI/NewUIManager.h"

namespace SEASON3B
{
    class CNewUIBloodCastle : public CNewUIObj
    {
    public:
        enum IMAGE_LIST
        {
            IMAGE_BLOODCASTLE_TIME_WINDOW = BITMAP_INTERFACE_NEW_BLOODCASTLE_BEGIN	// newui_Figure_blood.tga (124, 81)
        };

    private:
        enum BLOODCASTLE_TIME_WINDOW_SIZE
        {
            BLOODCASTLE_TIME_WINDOW_WIDTH = 124,
            BLOODCASTLE_TIME_WINDOW_HEIGHT = 81,
        };

        enum
        {
            BC_TIME_STATE_NORMAL = 1,
            BC_TIME_STATE_IMMINENCE,
        };

        enum
        {
            MAX_KILL_MONSTER = 65535,
        };

    private:
        CNewUIManager* m_pNewUIMng;
        POINT						m_Pos;

        wchar_t				m_szTime[256];		// Time
        int							m_iTime;			// Time
        int							m_iTimeState;		// Time state (default, imminent)
        int							m_iMaxKillMonster;	// Number of monsters to kill
        int							m_iKilledMonster;	// Monsters killed so far

    public:
        CNewUIBloodCastle();
        virtual ~CNewUIBloodCastle();

        bool Create(CNewUIManager* pNewUIMng, int x, int y);
        void Release();

        void SetPos(int x, int y);

        bool UpdateMouseEvent();
        bool UpdateKeyEvent();
        bool Update();
        bool Render();

        bool BtnProcess();

        float GetLayerDepth();	//. 1.2f

        void OpenningProcess();
        void ClosingProcess();

    private:
        void LoadImages();
        void UnloadImages();

    public:
        void SetTime(int m_iTime);
        void SetKillMonsterStatue(int iKilled, int iMaxKill);
    };
}

#endif // _NEWUIBLOODCASTLE_H_
