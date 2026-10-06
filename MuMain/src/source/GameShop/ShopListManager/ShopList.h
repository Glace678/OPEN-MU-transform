/**************************************************************************************************

Top-level object for the script list.

Holds the category list, package list, and product (attribute) list.

**************************************************************************************************/

#pragma once

#include "ShopPackage.h"
#include "ShopProduct.h"

#include "ShopCategoryList.h"
#include "ShopPackageList.h"
#include "ShopProductList.h"

class CShopList
{
public:
    CShopList();
    virtual ~CShopList();

    WZResult LoadCategroy(const wchar_t* szFilePath);
    WZResult LoadPackage(const wchar_t* szFilePath);
    WZResult LoadProduct(const wchar_t* szFilePath);

    CShopCategoryList* GetCategoryListPtr() { return m_CategoryListPtr; };	// Gets the category list.
    CShopPackageList* GetPackageListPtr() { return m_PackageListPtr; };		// Gets the package list.
    CShopProductList* GetProductListPtr() { return m_ProductListPtr; };		// Gets the product (attribute) list.

    void SetCategoryListPtr(CShopCategoryList* CategoryListPtr);
    void SetPackageListPtr(CShopPackageList* PackagePtr);
    void SetProductListPtr(CShopProductList* ProductListPtr);

private:
    CShopCategoryList* m_CategoryListPtr;
    CShopPackageList* m_PackageListPtr;
    CShopProductList* m_ProductListPtr;

    FILE_ENCODE IsFileEncodingUtf8(const wchar_t* szFilePath);
    std::wstring GetDecodedString(const char* buffer, FILE_ENCODE encode);
};