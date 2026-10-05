#pragma once

// Buff/item index cast helper macros, extracted from the legacy aggregate
// Core/Globals/_define.h (P2 disassembly).

#include "Core/Platform/WinCompat.h"  // DWORD

#define BUFFINDEX( buff )				static_cast<eBuffState>(buff)
#define BUFFTIMEINDEX( timetype )		static_cast<eBuffTimeType>(timetype)
#define ITEMINDEX( type, index )        static_cast<DWORD>((type*MAX_ITEM_INDEX)+index)
