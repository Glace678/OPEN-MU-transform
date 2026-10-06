/*******************************************************************************
*	Author : Jin Hyejin
*	Date : 2009.07.07
*	Contents : HTTP Connecter
*******************************************************************************/

#pragma once

#include "GameShop\ShopListManager\interface\IConnecter.h"

class HTTPConnecter : public IConnecter
{
public:
    // Constructor, Destructor

    HTTPConnecter(DownloadServerInfo* pServerInfo,
        DownloadFileInfo* pFileInfo);
    ~HTTPConnecter();

    // abstract Function

        //						Session
    virtual WZResult		CreateSession(HINTERNET& hSession);
    //						Connection
    virtual WZResult		CreateConnection(HINTERNET& hSession,
        HINTERNET& hConnection);
    //						Open download file & get size
    virtual WZResult		OpenRemoteFile(HINTERNET& hConnection,
        HINTERNET& hRemoteFile,
        ULONGLONG& nFileLength);
    //						Read remote file
    virtual WZResult		ReadRemoteFile(HINTERNET& hRemoteFile,
        BYTE* byReadBuffer,
        DWORD* dwBytesRead);
};
