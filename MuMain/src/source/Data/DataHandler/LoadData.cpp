// LoadData.cpp: implementation of the CLoadData class.
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include "LoadData.h"

#include <codecvt>
#include <cstring>
#include <locale>
#include <string>
#ifdef _WIN32
#include <shlwapi.h>
#endif

#include "Render/Sprites/GlobalBitmap.h"

#include "Render/Models/ZzzBMD.h"
#include "Render/Textures/ZzzTexture.h"

CLoadData gLoadData;

CLoadData::CLoadData()  // OK
{}

CLoadData::~CLoadData()  // OK
{}

void CLoadData::AccessModel(int Type, const wchar_t* Dir, const wchar_t* FileName, int i)
{
    if (!Models || Type < 0 || Type >= MAX_MODELS || !Dir || !FileName)
    {
        return;
    }

    wchar_t Name[128] = {};
    if (i == -1)
        swprintf(Name, sizeof(Name) / sizeof(Name[0]), L"%ls.bmd", FileName);
    else if (i < 10)
        swprintf(Name, sizeof(Name) / sizeof(Name[0]), L"%ls0%d.bmd", FileName, i);
    else
        swprintf(Name, sizeof(Name) / sizeof(Name[0]), L"%ls%d.bmd", FileName, i);
    Name[(sizeof(Name) / sizeof(Name[0])) - 1] = L'\0';

    bool Success = false;

    Models[Type].m_iBMDSeqID = Type;

    Success = Models[Type].Open2(Dir, Name);

    if (Success == false && (wcscmp(FileName, L"Monster") == 0 || wcscmp(FileName, L"Player") == 0 ||
                             wcscmp(FileName, L"PlayerTest") == 0 || wcscmp(FileName, L"Angel") == 0))
    {
        wchar_t Text[256] = {};
        swprintf(Text, sizeof(Text) / sizeof(Text[0]), L"%ls file does not exist.", Name);
        Text[(sizeof(Text) / sizeof(Text[0])) - 1] = L'\0';
        MessageBox(g_hWnd, Text, NULL, MB_OK);
        SendMessage(g_hWnd, WM_DESTROY, 0, 0);
    }
}

void CLoadData::OpenTexture(int Model, const wchar_t* SubFolder, int Wrap, int Type, bool Check)
{
    (void)Check;
    if (!Models || Model < 0 || Model >= MAX_MODELS)
    {
        return;
    }

    BMD* pModel = &Models[Model];
    if (pModel->NumMeshs <= 0 || !pModel->Textures || !pModel->IndexTexture)
    {
        return;
    }

    for (int i = 0; i < pModel->NumMeshs; i++)
    {
        Texture_t*   pTexture = &pModel->Textures[i];
        const size_t rawLen   = strnlen(pTexture->FileName, sizeof(pTexture->FileName));
        if (rawLen == 0)
        {
            pModel->IndexTexture[i] = BITMAP_UNKNOWN;
            continue;
        }

        const int wcharsNum =
            MultiByteToWideChar(CP_UTF8, 0, pTexture->FileName, static_cast<int>(rawLen), NULL, 0);
        if (wcharsNum <= 0)
        {
            pModel->IndexTexture[i] = BITMAP_UNKNOWN;
            continue;
        }

        std::wstring textureFileName(static_cast<size_t>(wcharsNum), L'\0');
        if (MultiByteToWideChar(CP_UTF8, 0, pTexture->FileName, static_cast<int>(rawLen), textureFileName.data(),
                                wcharsNum) <= 0)
        {
            pModel->IndexTexture[i] = BITMAP_UNKNOWN;
            continue;
        }

        std::wstring fullPath = L"Data\\";
        if (SubFolder)
        {
            fullPath += SubFolder;
        }
        fullPath += textureFileName;

        wchar_t __ext[_MAX_EXT] = {
            0,
        };
        _wsplitpath(textureFileName.c_str(), NULL, NULL, NULL, __ext);
        if (rawLen >= 3 && pTexture->FileName[0] == 'h' && pTexture->FileName[1] == 'i' && pTexture->FileName[2] == 'd')
        {
            pModel->IndexTexture[i] = BITMAP_HIDE;
        }
        else if (tolower(__ext[1]) == 't')  // TGA
        {
            pModel->IndexTexture[i] = Bitmaps.LoadImage(fullPath.c_str(), GL_NEAREST, Wrap);
        }
        else if (tolower(__ext[1]) == 'j')  // JPG
        {
            pModel->IndexTexture[i] = Bitmaps.LoadImage(fullPath.c_str(), Type, Wrap);
        }
        else
        {
            pModel->IndexTexture[i] = BITMAP_UNKNOWN;
        }

        bool isSkin = (rawLen >= 3 && pTexture->FileName[0] == 's' && pTexture->FileName[1] == 'k' &&
                       pTexture->FileName[2] == 'i') ||
                      !wcsnicmp(textureFileName.c_str(), L"level", 5);
        bool isHair = rawLen >= 4 && pTexture->FileName[0] == 'h' && pTexture->FileName[1] == 'a' &&
                      pTexture->FileName[2] == 'i' && pTexture->FileName[3] == 'r';

        if (isSkin || isHair)
        {
            BITMAP_t* pBitmap = pModel->IndexTexture[i] != BITMAP_UNKNOWN
                                    ? Bitmaps.FindTexture(pModel->IndexTexture[i])
                                    : Bitmaps.FindTextureByName(textureFileName.c_str());

            if (pBitmap)
            {
                pBitmap->IsSkin = isSkin;
                pBitmap->IsHair = isHair;
            }
        }

        if (pModel->IndexTexture[i] == BITMAP_UNKNOWN)
        {
            if (auto pBitmap = Bitmaps.FindTextureByName(textureFileName.c_str()))
            {
                // we try to find an already loaded texture based on the file name
                Bitmaps.LoadImage(pBitmap->BitmapIndex, pBitmap->FileName);
                pModel->IndexTexture[i] = pBitmap->BitmapIndex;
            }
            else
            {
                wchar_t szErrorMsg[256] = {
                    0,
                };
                swprintf(szErrorMsg, sizeof(szErrorMsg) / sizeof(szErrorMsg[0]),
                         L"OpenTexture Failed: %ls of %.32hs", fullPath.c_str(), pModel->Name);
                szErrorMsg[(sizeof(szErrorMsg) / sizeof(szErrorMsg[0])) - 1] = L'\0';
#ifdef FOR_WORK
                PopUpErrorCheckMsgBox(szErrorMsg);
#else   // FOR_WORK
                PopUpErrorCheckMsgBox(szErrorMsg, true);
#endif  // FOR_WORK
            }
        }
    }
}