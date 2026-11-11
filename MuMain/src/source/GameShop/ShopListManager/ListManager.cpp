//************************************************************************
//
// Decompiled by @myheart, @synth3r
// <https://forum.ragezone.com/members/2000236254.html>
//
//
// FILE: ListManager.cpp
//
//

#include "stdafx.h"
#ifdef KJH_ADD_INGAMESHOP_UI_SYSTEM
#include "ListManager.h"
#include <cerrno>
#ifdef _WIN32
#include <process.h>
#endif

#include <iterator>

CListManager::CListManager()  // OK
{
    this->m_pFTPDownLoader = NULL;
    this->m_ListManagerInfo.m_strServerIP.clear();
    this->m_ListManagerInfo.m_strUserID.clear();
    this->m_ListManagerInfo.m_strPWD.clear();
    this->m_ListManagerInfo.m_strRemotePath.clear();
    this->m_ListManagerInfo.m_strLocalPath.clear();
    this->m_vScriptFiles.clear();
}

CListManager::~CListManager()  // OK
{
    CFTPFileDownLoader* oldDownloader = NULL;
    {
        std::lock_guard<std::mutex> lock(this->m_DownloaderMutex);
        oldDownloader          = this->m_pFTPDownLoader;
        this->m_pFTPDownLoader = NULL;
    }
    if (oldDownloader != NULL)
    {
        oldDownloader->Break();
        SafeDelete(oldDownloader);
    }
}

void CListManager::SetListManagerInfo(DownloaderType type, const wchar_t* ServerIP, const wchar_t* UserID,
                                      const wchar_t* Pwd, const wchar_t* RemotePath, const wchar_t* LocalPath,
                                      CListVersionInfo Version, DWORD dwDownloadMaxTime)
{
    unsigned short port = 80;

    if (type == FTP)
        port = 21;

    this->SetListManagerInfo(type, ServerIP, port, UserID, Pwd, RemotePath, LocalPath, FTP_MODE_ACTIVE, Version,
                             dwDownloadMaxTime);
}

void CListManager::SetListManagerInfo(DownloaderType type, const wchar_t* ServerIP, unsigned short PortNum,
                                      const wchar_t* UserID, const wchar_t* Pwd, const wchar_t* RemotePath,
                                      const wchar_t* LocalPath, FTP_SERVICE_MODE ftpMode, CListVersionInfo Version,
                                      DWORD dwDownloadMaxTime)  // OK
{
    this->m_ListManagerInfo.m_DownloaderType    = type;
    this->m_ListManagerInfo.m_nPortNum          = PortNum;
    this->m_ListManagerInfo.m_ftpMode           = ftpMode;
    this->m_ListManagerInfo.m_strLocalPath      = LocalPath ? LocalPath : L"";
    this->m_ListManagerInfo.m_strRemotePath     = RemotePath ? RemotePath : L"";
    this->m_ListManagerInfo.m_strPWD            = Pwd ? Pwd : L"";
    this->m_ListManagerInfo.m_strServerIP       = ServerIP ? ServerIP : L"";
    this->m_ListManagerInfo.m_strUserID         = UserID ? UserID : L"";
    this->m_ListManagerInfo.m_Version           = Version;
    this->m_ListManagerInfo.m_dwDownloadMaxTime = dwDownloadMaxTime;

    if (!this->m_ListManagerInfo.m_strLocalPath.empty() &&
        GetFileAttributes(this->m_ListManagerInfo.m_strLocalPath.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        CreateDirectory(this->m_ListManagerInfo.m_strLocalPath.c_str(), 0);
    }

    if (this->m_ListManagerInfo.m_strLocalPath.empty() ||
        (this->m_ListManagerInfo.m_strLocalPath.back() != L'\\' &&
         this->m_ListManagerInfo.m_strLocalPath.back() != L'/'))
    {
        this->m_ListManagerInfo.m_strLocalPath += L"\\";
    }

    if (this->m_ListManagerInfo.m_strRemotePath.empty() ||
        (this->m_ListManagerInfo.m_strRemotePath.back() != L'/' &&
         this->m_ListManagerInfo.m_strRemotePath.back() != L'\\'))
    {
        this->m_ListManagerInfo.m_strRemotePath += L"/";
    }
}

WZResult CListManager::LoadScriptList(bool bDonwLoad)  // OK
{
    this->m_Result.BuildSuccessResult();

    if (!bDonwLoad && this->IsScriptFileExist())
    {
        goto JUMP;
    }

    this->m_Result = this->FileDownLoad();

    if (!this->m_Result.IsSuccess())
    {
        this->DeleteScriptFiles();
        return this->m_Result;
    }

    if (this->IsScriptFileExist())
    {
    JUMP:
        this->m_Result = this->LoadScript(bDonwLoad);

        if (!this->m_Result.IsSuccess())
            this->DeleteScriptFiles();
    }
    else
    {
        this->DeleteScriptFiles();

        this->m_Result.SetResult(2, 0, L"File Size Zero");
    }

    return this->m_Result;
}

bool CListManager::IsScriptFileExist()  // OK
{
    std::wstring path = this->GetScriptPath();

    for (std::vector<std::wstring>::iterator it = this->m_vScriptFiles.begin(); it != this->m_vScriptFiles.end(); it++)
    {
        std::wstring file_path = path + *(it);

        if (GetFileAttributes(file_path.c_str()) == INVALID_FILE_ATTRIBUTES)
        {
            return 0;
        }
    }

    return 1;
}

std::wstring CListManager::GetScriptPath()  // OK
{
    TCHAR buff[MAX_PATH] = {0};

    StringCchPrintf(buff, std::size(buff), L"%03d.%04d.%03d", m_ListManagerInfo.m_Version.Zone,
                    m_ListManagerInfo.m_Version.year, m_ListManagerInfo.m_Version.yearId);

    std::wstring path = this->m_ListManagerInfo.m_strLocalPath;
    path += buff;
    path += L"\\";

    return path;
}

void CListManager::DeleteScriptFiles()  // OK
{
    std::wstring path = this->GetScriptPath();

    for (std::vector<std::wstring>::iterator it = this->m_vScriptFiles.begin(); it != this->m_vScriptFiles.end(); it++)
    {
        std::wstring file_path = path + (*it);

        DeleteFile(file_path.c_str());
    }
}

WZResult CListManager::FileDownLoad()  // OK
{
#ifndef _WIN32
    // The watchdog thread guards a WinInet download that is excluded off
    // Windows (issue #462); DownLoadFiles fails immediately there, so run it
    // inline.
    this->m_Result = this->FileDownLoadImpl();
#else
    if (this->m_ListManagerInfo.m_dwDownloadMaxTime > 0)
    {
        unsigned int ThreadID = 0;

        const uintptr_t uThread = _beginthreadex(0, 0, CListManager::RunFileDownLoadThread, this, 0, &ThreadID);

        if (uThread == 0)
        {
            const DWORD dwErr = GetLastError() ? GetLastError() : static_cast<DWORD>(errno);
            this->m_Result.BuildResult(8, dwErr, L"Fail : _beginthreadex");
        }
        else
        {
            HANDLE      hHandle = reinterpret_cast<HANDLE>(uThread);
            const DWORD dwWait  = WaitForSingleObject(hHandle, this->m_ListManagerInfo.m_dwDownloadMaxTime);
            const DWORD dwErr   = (dwWait == WAIT_FAILED) ? GetLastError() : ERROR_SUCCESS;

            if (dwWait != WAIT_OBJECT_0)
            {
                {
                    std::lock_guard<std::mutex> lock(this->m_DownloaderMutex);
                    if (this->m_pFTPDownLoader != NULL)
                    {
                        this->m_pFTPDownLoader->Break();
                    }
                }

                WaitForSingleObject(hHandle, INFINITE);

                CFTPFileDownLoader* finishedDownloader = NULL;
                {
                    std::lock_guard<std::mutex> lock(this->m_DownloaderMutex);
                    finishedDownloader     = this->m_pFTPDownLoader;
                    this->m_pFTPDownLoader = NULL;
                }
                SafeDelete(finishedDownloader);

                CloseHandle(hHandle);

                if (dwWait == WAIT_TIMEOUT)
                {
                    this->m_Result.BuildResult(1, 0, L"Time Out!");
                }
                else
                {
                    this->m_Result.BuildResult(8, dwErr, L"Fail : WaitForSingleObject");
                }
            }
            else
            {
                CloseHandle(hHandle);
            }
        }
    }
    else
    {
        this->m_Result = this->FileDownLoadImpl();
    }
#endif  // _WIN32

    return this->m_Result;
}

WZResult CListManager::FileDownLoadImpl()  // OK
{
    CFTPFileDownLoader* oldDownloader = NULL;
    CFTPFileDownLoader* newDownloader = new CFTPFileDownLoader;
    {
        std::lock_guard<std::mutex> lock(this->m_DownloaderMutex);
        oldDownloader          = this->m_pFTPDownLoader;
        this->m_pFTPDownLoader = newDownloader;
    }

    if (oldDownloader != NULL)
    {
        oldDownloader->Break();
        SafeDelete(oldDownloader);
    }

    return newDownloader->DownLoadFiles(
        this->m_ListManagerInfo.m_DownloaderType, this->m_ListManagerInfo.m_strServerIP,
        this->m_ListManagerInfo.m_nPortNum, this->m_ListManagerInfo.m_strUserID, this->m_ListManagerInfo.m_strPWD,
        this->m_ListManagerInfo.m_strRemotePath, this->m_ListManagerInfo.m_strLocalPath,
        this->m_ListManagerInfo.m_ftpMode == FTP_MODE_PASSIVE, this->m_ListManagerInfo.m_Version, this->m_vScriptFiles);
}

unsigned int __stdcall CListManager::RunFileDownLoadThread(LPVOID pParam)  // OK
{
    auto* p = reinterpret_cast<CListManager*>(pParam);

    p->m_Result = p->FileDownLoadImpl();

    return 0;
}
#endif