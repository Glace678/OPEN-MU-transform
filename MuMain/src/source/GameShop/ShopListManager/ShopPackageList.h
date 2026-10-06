/**************************************************************************************************

Object holding the entire package list.

Package objects can be retrieved sequentially using an iterator.
Package objects can be retrieved by package number.

**************************************************************************************************/

#pragma once

#include "ShopPackage.h"
#include <map>

class CShopPackageList
{
public:
    CShopPackageList(void);
    ~CShopPackageList(void);

    int GetSize();
    void Clear();

    virtual void Append(CShopPackage package);

    void SetFirst();											// Points to the first package in the package list.
    bool GetNext(CShopPackage& package);						// Returns the current package object and points to the next one.

    bool GetValueByKey(int nKey, CShopPackage& package);		// Retrieves the package object by package number.
    bool GetValueByIndex(int nIndex, CShopPackage& package);	// Retrieves the package object by index number.

    bool SetPacketLeftCount(int PackageSeq, int nCount);

protected:
    std::map<int, CShopPackage> m_Packages;
    std::map<int, CShopPackage>::iterator m_iter;
    std::vector<int> m_PackageIndex;
};
