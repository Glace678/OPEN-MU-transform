//////////////////////////////////////////////////////////////////////////
//  UIGateKeeper.cpp
//////////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include "UIGateKeeper.h"

#include "Network/Server/WSclient.h"


CUIGateKeeper::CUIGateKeeper()
{
    m_bPublic = false;
    m_byType = TOUCH_TYPE_NONE;
    m_nEntranceFee = 0;
    m_iAddEntranceFee = 0;
    m_iMaxEnteranceFee = 0;

    m_iViewEntranceFee = m_nEntranceFee;
}

CUIGateKeeper::~CUIGateKeeper()
{
}

void CUIGateKeeper::SendPublicSetting()
{
    SocketClient->ToGameServer()->SendCastleSiegeHuntingZoneEntranceSetting(m_bPublic ^ true);
}

void CUIGateKeeper::SendEnteranceFee()
{
    SocketClient->ToGameServer()->SendCastleSiegeTaxChangeRequest(3, m_iViewEntranceFee);
}

void CUIGateKeeper::EnteranceFeeUp()
{
    // #MG-12: saturated add; avoid signed overflow on a hostile step.
    if (m_iAddEntranceFee <= 0) return;
    if (m_iViewEntranceFee > m_iMaxEnteranceFee - m_iAddEntranceFee)
        m_iViewEntranceFee = m_iMaxEnteranceFee;
    else
        m_iViewEntranceFee += m_iAddEntranceFee;
}

void CUIGateKeeper::EnteranceFeeDown()
{
    // #MG-12: saturated subtract; avoid signed underflow on a hostile step.
    if (m_iAddEntranceFee <= 0) return;
    if (m_iViewEntranceFee < m_iAddEntranceFee)
        m_iViewEntranceFee = 0;
    else
        m_iViewEntranceFee -= m_iAddEntranceFee;
}

void CUIGateKeeper::SendEnter()
{
    SocketClient->ToGameServer()->SendCastleSiegeHuntingZoneEnterRequest(m_iViewEntranceFee);
}