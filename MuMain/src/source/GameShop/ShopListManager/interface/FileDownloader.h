/*******************************************************************************
* : 2009.06.10
* : FileDownloader
* File ٿε 
*******************************************************************************/

#pragma once

#include "GameShop/ShopListManager/interface/IConnecter.h"
#include "GameShop/ShopListManager/interface/IDownloaderStateEvent.h"

class FileDownloader
{
public:
    // Constructor, Destructor

    FileDownloader(IDownloaderStateEvent* pStateEvent,
        DownloadServerInfo* pServerInfo,
        DownloadFileInfo* pFileInfo);
    ~FileDownloader();

    // public Function

        // ٿε 
    void				Break();
    // ٿε : , ĿƮ, ó
    WZResult			DownloadFile();

private:
    // private Function

    BOOL				CanBeContinue();
    void				Release();

    // Ŀ 
    IConnecter* CreateConnecter();
    // ó
    WZResult 			CreateConnection();
    static unsigned int __stdcall RunConnectThread(LPVOID pParam);
    WZResult 			Connection();

    // ó
    WZResult 			TransferRemoteFile();

    WZResult 			CreateLocalFile();
    // ٿε б
    WZResult 			ReadRemoteFile(BYTE* byReadBuffer, DWORD* dwBytesRead);
    WZResult 			WriteLocalFile(BYTE* byReadBuffer, DWORD dwBytesRead);

    // ٿε ̺Ʈ 
    void				SendStartedDownloadFileEvent(ULONGLONG nFileLength);
    // ٿε Ϸ ̺Ʈ 
    void				SendCompletedDownloadFileEvent(WZResult wzResult);
    // ٿε Ȳ ̺Ʈ : Ŷ 
    void				SendProgressDownloadFileEvent(ULONGLONG nTotalBytesRead);

    // Member Object

        // ٿε ÷
    volatile BOOL				m_bBreak;
    WZResult 					m_Result;

    // ٿε ̺Ʈ ü
    IDownloaderStateEvent* m_pStateEvent;
    // ٿε ü
    DownloadServerInfo* m_pServerInfo;
    // ٿε ü
    DownloadFileInfo* m_pFileInfo;
    // Ŀ
    IConnecter* m_pConnecter;

    // WinINet ڵ
    HINTERNET					m_hSession;
    // WinINet Ŀ ڵ
    HINTERNET					m_hConnection;
    // ڵ
    HINTERNET					m_hRemoteFile;
    // ڵ
    HANDLE						m_hLocalFile;
    ULONGLONG					m_nFileLength;
};
