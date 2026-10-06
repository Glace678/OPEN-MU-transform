/**************************************************************************************************
**************************************************************************************************/

#pragma once

#include "Include.h"

class CShopCategory
{
public:
    CShopCategory();
    virtual ~CShopCategory();

    bool SetCategory(std::wstring strdata);

    void SetCategoryFirst();							// īװ ù ° ׷ Ű Ѵ.
    bool GetCategoryNext(int& CategorySeq);				// īװ ȣ ϰ īװ ȣ Ų.

    void SetPackagSeqFirst();							// īװ ϵǾ ִ Ű ù ° ׸ Ű Ѵ.
    bool GetPackagSeqNext(int& PackagSeq);				// Ű ȣ ϰ Ű ȣ Ų.

    void AddPackageSeq(int PackageSeq);
    void ClearPackageSeq();

public:
    int ProductDisplaySeq;								// 1. īװ ȣ
    wchar_t CategroyName[SHOPLIST_LENGTH_CATEGORYNAME];	// 2. īװ ̸
    int EventFlag;										// 3. ̺Ʈ īװ (199:̺Ʈ, 200:Ϲ)
    int OpenFlag;										// 4. (201:, 202: )
    int ParentProductDisplaySeq;						// 5. θ īװ ȣ
    int DisplayOrder;									// 6. 
    int Root;											// 7. ֻ īװ (1: ֻ, 0: )

    std::vector<int> CategoryList;						// īװ "īװ ȣ" 
    std::vector<int>::iterator Categoryiter;

    std::vector<int> PackageList;						// īװ Ե "Ű ȣ" 
    std::vector<int>::iterator Packageiter;
};
