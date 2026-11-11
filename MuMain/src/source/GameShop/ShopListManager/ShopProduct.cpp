//************************************************************************
//
// Decompiled by @myheart, @synth3r
// <https://forum.ragezone.com/members/2000236254.html>
//
//
// FILE: ShopProduct.cpp
//
//

#include "stdafx.h"
#ifdef KJH_ADD_INGAMESHOP_UI_SYSTEM
#include "ShopProduct.h"
#include "StringToken.h"

#include <iterator>

CShopProduct::CShopProduct()  // OK
{
    this->ProductSeq = 0;
    memset(this->ProductName, 0, sizeof(this->ProductName));
    memset(this->PropertyName, 0, sizeof(this->PropertyName));
    memset(this->Value, 0, sizeof(this->Value));
    memset(this->UnitName, 0, sizeof(this->UnitName));
    this->Price        = 0;
    this->PriceSeq     = 0;
    this->PropertyType = 0;
    this->MustFlag     = 0;
    this->vOrder       = 0;
    this->DeleteFlag   = 0;
    this->StorageGroup = 0;
    this->ShareFlag    = 0;
    memset(this->InGamePackageID, 0, sizeof(this->InGamePackageID));
    this->PropertySeq = 0;
    this->ProductType = 0;
    this->UnitType    = 0;
}
CShopProduct::~CShopProduct()  // OK
{}

bool CShopProduct::SetProduct(std::wstring strdata)  // OK
{
    if (strdata.empty())
        return 0;

    CStringToken token(strdata, L"@");

    if (token.countTokens() < 17)
        return 0;

    this->ProductSeq = _wtoi(token.nextToken().c_str());
    StringCchCopy(this->ProductName, std::size(this->ProductName), token.nextToken().c_str());
    StringCchCopy(this->PropertyName, std::size(this->PropertyName), token.nextToken().c_str());
    StringCchCopy(this->Value, std::size(this->Value), token.nextToken().c_str());
    StringCchCopy(this->UnitName, std::size(this->UnitName), token.nextToken().c_str());
    this->Price        = _wtoi(token.nextToken().c_str());
    this->PriceSeq     = _wtoi(token.nextToken().c_str());
    this->PropertyType = _wtoi(token.nextToken().c_str());
    this->MustFlag     = _wtoi(token.nextToken().c_str());
    this->vOrder       = _wtoi(token.nextToken().c_str());
    this->DeleteFlag   = _wtoi(token.nextToken().c_str());
    this->StorageGroup = _wtoi(token.nextToken().c_str());
    this->ShareFlag    = _wtoi(token.nextToken().c_str());
    StringCchCopy(this->InGamePackageID, std::size(this->InGamePackageID), token.nextToken().c_str());
    this->PropertySeq = _wtoi(token.nextToken().c_str());
    this->ProductType = _wtoi(token.nextToken().c_str());
    this->UnitType    = _wtoi(token.nextToken().c_str());

    return 1;
}
#endif