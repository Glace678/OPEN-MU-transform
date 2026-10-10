// Definitions for the external symbols the compiled NewUIItemMng.cpp references
// but that are irrelevant to the ParseItemData bounds under test.
#include "stdafx.h"

void SetItemAttributes(ITEM* /*ip*/)
{
    // no-op: attribute table is not exercised by the parse-bounds tests.
}

bool SocketItemMgrStub::IsSocketItem(ITEM* /*p*/)
{
    return false;
}

SocketItemMgrStub g_SocketItemMgr;