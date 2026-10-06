/*******************************************************************************
*	Author : Jin Hyejin
*	Date : 2009.06.10
*	Contents : WZResult - processing result object
*******************************************************************************/

#pragma once

#include "GameShop/ShopListManager/interface/WZResult/ErrorCodeDefine.h"
#define	MAX_ERROR_MESSAGE	1024

class WZResult
{
public:
    // Constructor, Destructor
    WZResult();
    ~WZResult();

    // public Function

    WZResult& operator=(const WZResult& result);
    BOOL		IsSuccess();
    TCHAR* GetErrorMessage();
    DWORD		GetErrorCode();
    DWORD		GetWindowErrorCode();

    void SetSuccessResult();
    void SetResult(DWORD dwErrorCode,
        DWORD dwWindowErrorCode,
        const TCHAR* szFormat,
        ...);

    static WZResult BuildSuccessResult();
    static WZResult BuildResult(DWORD dwErrorCode,
        DWORD dwWindowErrorCode,
        const TCHAR* szFormat,
        ...);

private:
    // Member Object
    DWORD	m_dwErrorCode;
    DWORD	m_dwWindowErrorCode;
    TCHAR	m_szErrorMessage[MAX_ERROR_MESSAGE];
};
