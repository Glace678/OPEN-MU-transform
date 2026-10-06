/*******************************************************************************
* : 2009.07.07
* : Connecter Interface
*******************************************************************************/

#pragma once

#include "GameShop/ShopListManager/interface/WZResult/WZResult.h"
#include "GameShop/ShopListManager/interface/DownloadInfo.h"

class IConnecter
{
public:
    // Constructor, Destructor

    IConnecter(DownloadServerInfo* pServerInfo,
        DownloadFileInfo* pFileInfo)
        : m_pServerInfo(pServerInfo),
        m_pFileInfo(pFileInfo)
    {};
    ~IConnecter() {};

    // abstract Function

    virtual WZResult		CreateSession(HINTERNET& hSession) = 0;
    // ĿƮ
    virtual WZResult		CreateConnection(HINTERNET& hSession,
        HINTERNET& hConnection) = 0;
    // ٿε & 
    virtual WZResult		OpenRemoteFile(HINTERNET& hConnection,
        HINTERNET& hRemoteFile,
        ULONGLONG& nFileLength) = 0;
    // Ʈ б
    virtual WZResult		ReadRemoteFile(HINTERNET& hRemoteFile,
        BYTE* byReadBuffer,
        DWORD* dwBytesRead) = 0;

protected:
    // Member Object

    WZResult 				m_Result;
    // ٿε ü
    DownloadServerInfo* m_pServerInfo;
    // ٿε ü
    DownloadFileInfo* m_pFileInfo;
};
