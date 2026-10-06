/*******************************************************************************
*	Author : Jin Hyejin
*	Date : 2009.06.10
*	Contents : Interface - Download State Event virtual class
*				Object to receive download state notifications
*				Overridden by programs that use this library
*******************************************************************************/

#pragma once

class IDownloaderStateEvent
{
public:
    // Constructor, Destructor

    IDownloaderStateEvent() {};
    virtual ~IDownloaderStateEvent() {};

    // abstract Function

        //				Download start event handler
    virtual void	OnStartedDownloadFile(TCHAR* szFileName, ULONGLONG uFileLength) = 0;
    //				Download progress event handler : per packet
    virtual void	OnProgressDownloadFile(TCHAR* szFileName, ULONGLONG uDownloadFileLength) = 0;
    //				Download completion event handler
    virtual void	OnCompletedDownloadFile(TCHAR* szFileName, WZResult wzResult) = 0;
};
