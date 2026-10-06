/*******************************************************************************
* : 2009.06.10
* : Download ʿ 
*******************************************************************************/

#pragma once

#include "Core/Platform/WinInet.h"
#define DL_DEFAULT_BUFFER_SIZE			4096

typedef enum _DownloaderType
{
    FTP,
    HTTP,
} DownloaderType;

class DownloadFileInfo
{
public:
    // Constructor, Destructor

    DownloadFileInfo();
    ~DownloadFileInfo();

    // Get Function
    TCHAR* GetFileName();
    TCHAR* GetLocalFilePath();
    TCHAR* GetRemoteFilePath();
    TCHAR* GetTargetDirPath();
    ULONGLONG	GetFileLength();
    // Set Function
    void	SetFilePath(TCHAR* szFileName,
        TCHAR* szLocalFilePath,
        TCHAR* szRemoteFilePath,
        TCHAR* szTargerDirPath);
    void	SetFileLength(ULONGLONG uFileLength);

private:
    // Member Object

        // ̸
    TCHAR		m_szFileName[MAX_PATH];
    // ü 
    TCHAR		m_szLocalFilePath[MAX_PATH];
    // Ʈ ü 
    TCHAR		m_szRemoteFilePath[INTERNET_MAX_URL_LENGTH];
    // ġ Ǯ 
    TCHAR		m_szTargerDirPath[MAX_PATH];
    ULONGLONG	m_uFileLength;
};

class DownloadServerInfo
{
public:
    // Constructor, Destructor

    DownloadServerInfo();
    ~DownloadServerInfo();

    // Get Function

        // ּ 
    TCHAR* GetServerURL();
    TCHAR* GetUserID();
    TCHAR* GetPassword();
    // Ʈ ȣ 
    INTERNET_PORT	GetPort();
    // Ÿε Ÿ 
    DownloaderType	GetDownloaderType();
    DWORD			GetReadBufferSize();
    // ĿƮ ŸӾƿ 
    DWORD			GetConnectTimeout();
    //  
    BOOL			IsOverWrite();
    // нú 
    BOOL			IsPassive();

    // Set Function

    void			SetServerInfo(TCHAR* szServerURL,
        INTERNET_PORT		nPort,
        TCHAR* szUserID,
        TCHAR* szPassword);
    // ٿε Ÿ 
    void			SetDownloaderType(DownloaderType dwDownloaderType);
    void			SetReadBufferSize(DWORD dwReadBufferSize);
    //  
    void			SetOverWrite(BOOL bOverWrite);
    // нú 
    void			SetPassiveMode(BOOL bPassive);
    // ĿƮ ŸӾƿ 
    void			SetConnectTimeout(DWORD dwConnectTimeout);

private:
    // Member Object

        // Server ּ
    TCHAR						m_szServerURL[INTERNET_MAX_URL_LENGTH];
    TCHAR						m_szUserID[INTERNET_MAX_USER_NAME_LENGTH];
    // Password
    TCHAR						m_szPassword[INTERNET_MAX_PASSWORD_LENGTH];
    // Ʈ default = INTERNET_DEFAULT_FTP_PORT (21)
    INTERNET_PORT				m_nPort;
    // ٿδ Ÿ - 
    DownloaderType				m_DownloaderType;
    // ٿε Ŷ default = 4096
    DWORD						m_dwReadBufferSize;
    // Local File  default = TRUE
    BOOL						m_bOverWrite;
    // Passive default = FALSE
    BOOL						m_bPassive;
    // ĿƮ ŸӾƿ
    DWORD						m_dwConnectTimeout;
};
