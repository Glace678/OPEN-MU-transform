#pragma once

// Split chunk of legacy Core/Globals/_enum.h (P2).
// Enum names/values/order are unchanged.
#include "Core/Platform/WinCompat.h"  // BYTE and fixed underlying types

namespace COMGEM
{
    enum	_METHOD
    {
        ATTACH = 0,
        DETACH
    };

    enum	eGEMTYPE
    {
        eNOGEM = -1,
        eBLESS = 0,
        eBLESS_C,

        eSOUL,
        eSOUL_C,

        eLIFE,
        eLIFE_C,

        eCREATE,
        eCREATE_C,

        ePROTECT,
        ePROTECT_C,

        eGEMSTONE,
        eGEMSTONE_C,

        eHARMONY,
        eHARMONY_C,

        eCHAOS,
        eCHAOS_C,

        eLOW,
        eLOW_C,

        eUPPER,
        eUPPER_C,

        eGEMTYPE_END = 10,
        eGEMTYPE_END_ALL = 20,
    };
    enum eGEMINDEXTYPE
    {
        eGEM_NAME, eGEM_INDEX, eGEM_END
    };

    enum	_GEMTYPE
    {
        NOGEM = -1,
        CELE = 0,
        SOUL = 1,
        COMCELE = 2,
        COMSOUL = 3
    };

    enum	_COMTYPE
    {
        NOCOM = -1,
        FIRST = 10,
        SECOND = 20,
        THIRD = 30,
        eCOMTYPE_END = 3
    };

    enum	_STATE
    {
        STATE_READY,
        STATE_HOLD,
        STATE_END
    };

    enum	_ERRORTYPE
    {
        NOERR = 0,
        POPERROR_NOTALLOWED,
        COMERROR_NOTALLOWED,
        DEERROR_SOMANY,
        DEERROR_NOTALLOWED,
        ERROR_UNKNOWN,
        ERROR_ALL
    };
};
