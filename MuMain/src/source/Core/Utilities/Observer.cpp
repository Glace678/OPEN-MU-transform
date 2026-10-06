//*****************************************************************************
// File: Observer.cpp
//
// Desc: implementation of the CObserver, CSubject class.
//
// producer: Ahn Sang-Kyu
//*****************************************************************************

#include "stdafx.h"
#include "Core/Utilities/Observer.h"

//*****************************************************************************
// CObserver
//*****************************************************************************

//////////////////////////////////////////////////////////////////////
// Construction/Destruction
//////////////////////////////////////////////////////////////////////

CObserver::CObserver()
{
}

CObserver::~CObserver()
{
}

//*****************************************************************************
// CSubject
//*****************************************************************************

//////////////////////////////////////////////////////////////////////
// Construction/Destruction
//////////////////////////////////////////////////////////////////////

CSubject::CSubject()
{
}

CSubject::~CSubject()
{
}

//*****************************************************************************
// Function: Attach()
// Description: Adds to the observer list.
//			   (Best called when a subject child class is created.)
// Parameter: pObserver	: Pointer to the observer object.
//*****************************************************************************
void CSubject::Attach(CObserver* pObserver)
{
    m_ObserverList.AddTail(pObserver);
}

//*****************************************************************************
// Function: Detach()
// Description: Removes from the observer list.
//			   (Best called before releasing a subject child class.)
// Parameter: pObserver	: Pointer to the observer object.
//*****************************************************************************
void CSubject::Detach(CObserver* pObserver)
{
    NODE* pPos = m_ObserverList.Find(pObserver);
    if (pPos)
        m_ObserverList.RemoveAt(pPos);
}

//*****************************************************************************
// Function: Notify()
// Description: Iterates the list and calls the observer's UpdateData(this).
//			   (Call whenever the subject's contents change.)
//*****************************************************************************
void CSubject::Notify()
{
    CObserver* pObserver;
    NODE* pPos = m_ObserverList.GetHeadPosition();
    while (pPos)
    {
        pObserver = (CObserver*)m_ObserverList.GetNext(pPos);
        pObserver->UpdateData(this);
    }
}