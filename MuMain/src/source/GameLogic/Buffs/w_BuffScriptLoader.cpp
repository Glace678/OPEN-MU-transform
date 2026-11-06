// BuffScriptLoader.cpp: implementation of the CBuffScriptLoader class.
//
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include "Core/Utilities/ReadScript.h"
#include "UI/Legacy/UIManager.h"
#include "GameLogic/Items/ItemAddOptioninfo.h"
#include "GameLogic/Buffs/BuffScriptValidation.h"
#include "w_BuffScriptLoader.h"

//////////////////////////////////////////////////////////////////////
// Construction/Destruction
//////////////////////////////////////////////////////////////////////

namespace
{
// 64-bit file positioning: MSVC/MinGW expose _fseeki64/_ftelli64, POSIX uses
// fseeko/ftello (off_t is 64-bit on the supported Linux/Apple/Android targets).
int SeekFile64(FILE* fp, int64_t offset, int origin)
{
#ifdef _WIN32
    return _fseeki64(fp, offset, origin);
#else
    return fseeko(fp, static_cast<off_t>(offset), origin);
#endif
}

int64_t TellFile64(FILE* fp)
{
#ifdef _WIN32
    return _ftelli64(fp);
#else
    return static_cast<int64_t>(ftello(fp));
#endif
}

void CutTokenString(wchar_t* pcCuttoken, std::list<std::wstring>& out)
{
    if (wcslen(pcCuttoken) == 0)
        return;

    int cutpos = 0;

    for (int i = 0; i < MAX_DESCRIPT_LENGTH; ++i)
    {
        if (pcCuttoken[i] == '/' || pcCuttoken[i] == 0)
        {
            wchar_t Temp[MAX_DESCRIPT_LENGTH] = {
                0,
            };
            wcsncpy(Temp, pcCuttoken + cutpos, i - cutpos);
            out.push_back(Temp);
            cutpos = i + 1;

            if (pcCuttoken[i] == 0)
                return;
        }
    }
}
}

BuffInfo::BuffInfo()
    : s_ItemType(255), s_ItemIndex(255), s_BuffIndex(0), s_BuffEffectType(0), s_BuffClassType(0), s_NoticeType(0),
      s_ClearType(0)
{
    memset(&s_BuffName, 0, sizeof(s_BuffName));
    memset(&s_BuffDescript, 0, sizeof(s_BuffDescript));
}

BuffInfo::~BuffInfo() {}

BuffScriptLoaderPtr BuffScriptLoader::Make()
{
    BuffScriptLoaderPtr info(new BuffScriptLoader());
    return info;
}

BuffScriptLoader::BuffScriptLoader()
{
    std::wstring filename = L"data/local/" + g_strSelectedML + L"/BuffEffect_" + g_strSelectedML + L".bmd";

    if (!Load(filename))
    {
        assert(0);
    }
}

BuffScriptLoader::~BuffScriptLoader() {}

bool BuffScriptLoader::Load(const std::wstring& pchFileName)
{
    FILE* fp = _wfopen(pchFileName.c_str(), L"rb");

    if (fp != NULL)
    {
        DWORD structsize = sizeof(_BUFFINFO);

        DWORD listsize = 0;
        if (fread(&listsize, sizeof(DWORD), 1, fp) != 1)
        {
            fclose(fp);
            return false;
        }

        if (!BuffScriptValidation::IsValidRecordCount(listsize, structsize))
        {
            fclose(fp);
            wchar_t Text[256];
            mu_swprintf(Text, L"%ls - Invalid buff record count (%u).", pchFileName.c_str(), listsize);
            g_ErrorReport.Write(Text);
            MessageBox(g_hWnd, Text, NULL, MB_OK);
            SendMessage(g_hWnd, WM_DESTROY, 0, 0);
            return false;
        }

        uint64_t totalBytes64 = static_cast<uint64_t>(structsize) * static_cast<uint64_t>(listsize);

        SeekFile64(fp, 0, SEEK_END);
        int64_t fileSize = TellFile64(fp);
        SeekFile64(fp, sizeof(DWORD), SEEK_SET);

        if (fileSize < static_cast<int64_t>(sizeof(DWORD) + totalBytes64 + sizeof(DWORD)))
        {
            fclose(fp);
            wchar_t Text[256];
            mu_swprintf(Text, L"%ls - File size smaller than declared records.", pchFileName.c_str());
            g_ErrorReport.Write(Text);
            MessageBox(g_hWnd, Text, NULL, MB_OK);
            SendMessage(g_hWnd, WM_DESTROY, 0, 0);
            return false;
        }

        BYTE* Buffer = new BYTE[totalBytes64];
        if (fread(Buffer, static_cast<size_t>(totalBytes64), 1, fp) != 1)
        {
            delete[] Buffer;
            fclose(fp);
            return false;
        }

        DWORD dwCheckSum = 0;
        if (fread(&dwCheckSum, sizeof(DWORD), 1, fp) != 1)
        {
            delete[] Buffer;
            fclose(fp);
            return false;
        }

        fclose(fp);
        if (dwCheckSum != GenerateCheckSum2(Buffer, static_cast<DWORD>(totalBytes64), 0xE2F1))
        {
            wchar_t Text[256];
            mu_swprintf(Text, L"%ls - File corrupted.", pchFileName.c_str());
            g_ErrorReport.Write(Text);
            MessageBox(g_hWnd, Text, NULL, MB_OK);
            SendMessage(g_hWnd, WM_DESTROY, 0, 0);
        }
        else
        {
            BYTE* pSeek = Buffer;

            for (DWORD i = 0; i < listsize; i++)
            {
                _BUFFINFO tempbuffinfo;

                BuxConvert(pSeek, structsize);
                memcpy(&tempbuffinfo, pSeek, structsize);

                BuffInfo buffinfo;
                buffinfo.s_BuffIndex      = tempbuffinfo.s_BuffIndex;
                buffinfo.s_BuffEffectType = tempbuffinfo.s_BuffEffectType;
                buffinfo.s_ItemType       = tempbuffinfo.s_ItemType;
                buffinfo.s_ItemIndex      = tempbuffinfo.s_ItemIndex;

                CMultiLanguage::ConvertFromUtf8(buffinfo.s_BuffName, tempbuffinfo.s_BuffName, MAX_BUFF_NAME_LENGTH);
                CMultiLanguage::ConvertFromUtf8(buffinfo.s_BuffDescript, tempbuffinfo.s_BuffDescript,
                                                MAX_DESCRIPT_LENGTH);

                buffinfo.s_BuffClassType = tempbuffinfo.s_BuffClassType;
                buffinfo.s_NoticeType    = tempbuffinfo.s_NoticeType;
                buffinfo.s_ClearType     = tempbuffinfo.s_ClearType;

                CutTokenString(buffinfo.s_BuffDescript, buffinfo.s_BuffDescriptlist);
                m_Info.insert(std::make_pair(static_cast<eBuffState>(buffinfo.s_BuffIndex), buffinfo));

                pSeek += structsize;
            }
        }
        delete[] Buffer;
    }
    else
    {
        wchar_t Text[256];
        mu_swprintf(Text, L"%ls - File not exist.", pchFileName.c_str());
        g_ErrorReport.Write(Text);
        MessageBox(g_hWnd, Text, NULL, MB_OK);
        SendMessage(g_hWnd, WM_DESTROY, 0, 0);
    }

    return true;
}

const BuffInfo BuffScriptLoader::GetBuffinfo(eBuffState type) const
{
    if (type >= eBuff_Count)
        return BuffInfo();

    auto iter = m_Info.find(type);

    if (iter != m_Info.end())
    {
        return (*iter).second;
    }

    return BuffInfo();
}

eBuffClass BuffScriptLoader::IsBuffClass(eBuffState type) const
{
    if (type >= eBuff_Count)
        return eBuffClass_Count;

    auto iter = m_Info.find(type);

    if (iter != m_Info.end())
    {
        return static_cast<eBuffClass>((*iter).second.s_BuffClassType);
    }
    else
    {
        return eBuffClass_Count;
    }
}

#ifdef KJH_PBG_ADD_INGAMESHOP_SYSTEM
int BuffScriptLoader::GetBuffIndex(int iItemCode)
{
    auto iter = m_Info.begin();

    int iterItemCode = 0;

    while (iter != m_Info.end())
    {
        iterItemCode = ITEMINDEX(iter->second.s_ItemType, iter->second.s_ItemIndex);

        if (iterItemCode == iItemCode)
            return iter->second.s_BuffIndex;

        iter++;
    }

    return -1;
}

int BuffScriptLoader::GetBuffType(int iItemCode)
{
    auto iter = m_Info.begin();

    int iterItemCode = 0;

    while (iter != m_Info.end())
    {
        iterItemCode = ITEMINDEX(iter->second.s_ItemType, iter->second.s_ItemIndex);

        if (iterItemCode == iItemCode)
            return iter->second.s_BuffEffectType;

        iter++;
    }

    return -1;
}
#endif  // KJH_PBG_ADD_INGAMESHOP_SYSTEM