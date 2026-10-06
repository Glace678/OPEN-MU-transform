/**************************************************************************************************

Object holding the entire category list.

Category objects can be retrieved sequentially using an iterator.
Category objects can be retrieved by category number.

**************************************************************************************************/

#pragma once

#include "ShopCategory.h"
#include <map>

class CShopCategoryList
{
public:
    CShopCategoryList(void);
    ~CShopCategoryList(void);

    void Clear();

    int GetSize();
    virtual void Append(CShopCategory category);

    void SetFirst();											// Points to the first category in the category list.
    bool GetNext(CShopCategory& category);						// Returns the current category object and points to the next one.

    bool GetValueByKey(int nKey, CShopCategory& category);		// Retrieves a category object by category sequence number.
    bool GetValueByIndex(int nIndex, CShopCategory& category);	// Retrieves a category object by index number.

    bool InsertPackage(int Category, int Package);
    bool RefreshPackageSeq(int Category, int PackageSeqs[], int PackageCount);

protected:
    std::map<int, CShopCategory> m_Categroys;				// Category object map
    std::map<int, CShopCategory>::iterator m_Categoryiter;	// Category iterator
    std::vector<int> m_CategoryIndex;						// Category number index list
};
