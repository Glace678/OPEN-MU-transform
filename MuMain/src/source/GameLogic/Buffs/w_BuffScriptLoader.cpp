// BuffScriptLoader.cpp: implementation of the CBuffScriptLoader class.
//
//////////////////////////////////////////////////////////////////////

#include "stdafx.h"

#include <limits>

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
    void CutTokenString(wchar_t* pcCuttoken, std::list<std::wstring>& out)
    {
        if (wcslen(pcCuttoken) == 0) return;

        int cutpos = 0;

        for (int i = 0; i < MAX_DESCRIPT_LENGTH; ++i)
        {
            if (pcCuttoken[i] == '/' || pcCuttoken[i] == 0)
            {
                wchar_t Temp[MAX_DESCRIPT_LENGTH] = { 0, };
                wcsncpy(Temp, pcCuttoken + cutpos, i - cutpos);
                out.push_back(Temp);
                cutpos = i + 1;

                if (pcCuttoken[i] == 0) return;
            }
        }
    }

    // Fatal error exit shared by Load()'s guards: release buffer/file, log
    // "<file> - <reason>", show a message box and ask the window to close.
    void AbortLoad(FILE* fp, const std::wstring& fileName, const wchar_t* reason, BYTE* buffer = nullptr)
    {
        delete[] buffer;
        if (fp != nullptr)
        {
            fclose(fp);
        }

        wchar_t text[256];
        mu_swprintf(text, L"%ls - %ls", fileName.c_str(), reason);
        g_ErrorReport.Write(text);
        MessageBox(g_hWnd, text, NULL, MB_OK);
        SendMessage(g_hWnd, WM_DESTROY, 0, 0);
    }
}

BuffInfo::BuffInfo() : s_ItemType(255), s_ItemIndex(255), s_BuffIndex(0),
s_BuffEffectType(0), s_BuffClassType(0), s_NoticeType(0), s_ClearType(0)
{
    memset(&s_BuffName, 0, sizeof(s_BuffName));
    memset(&s_BuffDescript, 0, sizeof(s_BuffDescript));
}

BuffInfo::~BuffInfo()
{
}

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

BuffScriptLoader::~BuffScriptLoader()
{
}

bool BuffScriptLoader::Load(const std::wstring& pchFileName)
{
    FILE* fp = _wfopen(pchFileName.c_str(), L"rb");

    if (fp != NULL)
    {
        DWORD structsize = sizeof(_BUFFINFO);

        DWORD listsize = 0;
        if (fread(&listsize, sizeof(DWORD), 1, fp) != 1)
        {
            // XC-12: record count unreadable -- do not allocate/parse garbage.
            AbortLoad(fp, pchFileName, L"File truncated (record count unreadable).");
            return true;
        }

        // XC-12: bound the caller-supplied record count BEFORE allocating and
        // multiplying (overflow-safe), instead of trusting a file value.
        // Rules live in BuffScriptValidation.h so they can be unit tested.
        if (!BuffScriptValidation::IsValidRecordCount(listsize, structsize))
        {
            wchar_t reason[128];
            mu_swprintf(reason, L"File corrupted (invalid record count: %u).", listsize);
            AbortLoad(fp, pchFileName, reason);
            return true;
        }

        const DWORD bufferSize = structsize * listsize;
        BYTE* Buffer = new BYTE[bufferSize];
        if (fread(Buffer, bufferSize, 1, fp) != 1)
        {
            // XC-12: short read -- checksum on uninitialized tail would be
            // meaningless.
            AbortLoad(fp, pchFileName, L"File truncated (record data).", Buffer);
            return true;
        }

        DWORD dwCheckSum = 0;
        if (fread(&dwCheckSum, sizeof(DWORD), 1, fp) != 1)
        {
            // XC-12: checksum domain missing -- cannot validate the data.
            AbortLoad(fp, pchFileName, L"File truncated (checksum).", Buffer);
            return true;
        }

        fclose(fp);
        if (dwCheckSum != GenerateCheckSum2(Buffer, bufferSize, 0xE2F1))
        {
            // fp is already closed; return so Buffer is not freed again below.
            AbortLoad(nullptr, pchFileName, L"File corrupted.", Buffer);
            return true;
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
                buffinfo.s_BuffIndex = tempbuffinfo.s_BuffIndex;
                buffinfo.s_BuffEffectType = tempbuffinfo.s_BuffEffectType;
                buffinfo.s_ItemType = tempbuffinfo.s_ItemType;
                buffinfo.s_ItemIndex = tempbuffinfo.s_ItemIndex;

                CMultiLanguage::ConvertFromUtf8(buffinfo.s_BuffName, tempbuffinfo.s_BuffName, MAX_BUFF_NAME_LENGTH);
                CMultiLanguage::ConvertFromUtf8(buffinfo.s_BuffDescript, tempbuffinfo.s_BuffDescript, MAX_DESCRIPT_LENGTH);

                buffinfo.s_BuffClassType = tempbuffinfo.s_BuffClassType;
                buffinfo.s_NoticeType = tempbuffinfo.s_NoticeType;
                buffinfo.s_ClearType = tempbuffinfo.s_ClearType;

                CutTokenString(buffinfo.s_BuffDescript, buffinfo.s_BuffDescriptlist);
                m_Info.insert(std::make_pair(static_cast<eBuffState>(buffinfo.s_BuffIndex), buffinfo));

                pSeek += structsize;
            }
        }
        delete[] Buffer;
    }
    else
    {
        AbortLoad(fp, pchFileName, L"File not exist.");
    }

    return true;
}

const BuffInfo BuffScriptLoader::GetBuffinfo(eBuffState type) const
{
    if (type >= eBuff_Count) return BuffInfo();

    auto iter = m_Info.find(type);

    if (iter != m_Info.end())
    {
        return (*iter).second;
    }

    return BuffInfo();
}

eBuffClass BuffScriptLoader::IsBuffClass(eBuffState type) const
{
    if (type >= eBuff_Count) return eBuffClass_Count;

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
#endif // KJH_PBG_ADD_INGAMESHOP_SYSTEM