#pragma once

#include <atomic>
#include <mutex>
#include "Include.h"

class CFTPFileDownLoader
{
public:
    CFTPFileDownLoader();
    virtual ~CFTPFileDownLoader();

public:
    WZResult DownLoadFiles(DownloaderType type, std::wstring strServerIP, unsigned short PortNum,
                           std::wstring strUserName, std::wstring strPWD, std::wstring strRemotepath,
                           std::wstring strlocalpath, bool bPassiveMode, CListVersionInfo Version,
                           std::vector<std::wstring> vScriptFiles);

    void Break();

    FileDownloader* GetFileDownloader()
    {
        std::lock_guard<std::mutex> lock(m_DownloaderMutex);
        return m_pFileDownloader;
    }

private:
    BOOL              CreateFolder(std::wstring strFilePath);
    std::atomic<bool> m_Break;
    std::mutex        m_DownloaderMutex;
    FileDownloader*   m_pFileDownloader;
};
