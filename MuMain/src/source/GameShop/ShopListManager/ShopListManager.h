/*
Date: 2009-07-24
Author: Moon Sanghyun
Summary: Object used to manage the shop list
*/

#pragma once

#include "Include.h"
#include "ListManager.h"
#include "ShopList.h"

class CShopListManager : public CListManager
{
public:
    CShopListManager();
    virtual ~CShopListManager();

    CShopList* GetListPtr() { return m_ShopList; };

private:
    CShopList* m_ShopList;

    WZResult		LoadScript(bool bDonwLoad);
};