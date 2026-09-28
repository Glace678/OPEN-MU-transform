#include "stdafx.h"

#ifdef KJH_ADD_INGAMESHOP_UI_SYSTEM
#include "SoloShopCatalog.h"
#include "InGameShopSystem.h"
#include "I18N/All.h"

namespace GameShop::SoloCatalog
{
    namespace
    {
        constexpr int RootCategory = 10;
        constexpr int FirstCategory = 13;
        constexpr int LastCategory = 17;
        constexpr int NormalEvent = 200;
        constexpr int OpenCategory = 201;
        constexpr int NormalPackage = 170;
        constexpr int Purchasable = 182;
        constexpr int Giftable = 184;
        constexpr int NotCapsule = 177;
        constexpr int ActivePackage = 181;
        constexpr int CreditCash = 508;
        constexpr int AutomaticCash = 669;
        constexpr int StorageGroup = 673;
        constexpr int QuantityProperty = 142;
        constexpr int RequiredProperty = 145;
        constexpr int ActiveProduct = 144;
        constexpr int AccountShared = 518;
        constexpr int QuantityPropertySequence = 7;
        constexpr int QuantityUnit = 680;
        constexpr int CreditCashSequence = 2;
        constexpr int CatalogStartYear = 100;
        constexpr int CatalogEndYear = 199;
        constexpr int CatalogEndMonth = 11;
        constexpr int CatalogEndDay = 31;
        constexpr int EquipmentCategory = 13;
        constexpr int MaterialsCategory = 14;
        constexpr int SkillsCategory = 15;
        constexpr int PetsCategory = 16;
        constexpr int ConsumablesCategory = 17;

        void AddCategory(CShopList& catalog, int id, const wchar_t* name, bool root)
        {
            CShopCategory category;
            category.ProductDisplaySeq = id;
            StringCchCopy(category.CategroyName, std::size(category.CategroyName), name);
            category.EventFlag = NormalEvent;
            category.OpenFlag = OpenCategory;
            category.ParentProductDisplaySeq = RootCategory;
            category.DisplayOrder = root ? 1 : id - FirstCategory + 1;
            category.Root = root ? 1 : 0;
            catalog.GetCategoryListPtr()->Append(category);
        }

        CShopPackage CreatePackage(const Offer& offer)
        {
            CShopPackage package;
            package.ProductDisplaySeq = offer.category;
            package.ViewOrder = offer.id;
            package.PackageProductSeq = offer.id;
            StringCchCopy(package.PackageProductName, std::size(package.PackageProductName), offer.name.data());
            package.PackageProductType = NormalPackage;
            package.Price = offer.price;
            StringCchCopy(package.Description, std::size(package.Description), offer.name.data());
            package.Caution[0] = L'\0';
            package.SalesFlag = Purchasable;
            package.GiftFlag = Giftable;
            package.StartDate = {};
            package.EndDate = {};
            package.StartDate.tm_year = CatalogStartYear;
            package.StartDate.tm_mday = 1;
            package.EndDate.tm_year = CatalogEndYear;
            package.EndDate.tm_mon = CatalogEndMonth;
            package.EndDate.tm_mday = CatalogEndDay;
            package.CapsuleFlag = NotCapsule;
            package.CapsuleCount = 1;
            StringCchCopy(package.ProductCashName, std::size(package.ProductCashName), L"WCoin(C)");
            StringCchCopy(package.PricUnitName, std::size(package.PricUnitName), L"WCoin(C)");
            package.DeleteFlag = ActivePackage;
            package.EventFlag = NormalEvent;
            package.ProductAmount = 1;
            StringCchPrintf(package.InGamePackageID, std::size(package.InGamePackageID), L"%u", offer.itemCode);
            package.ProductCashSeq = CreditCashSequence;
            package.PriceCount = 1;
            package.DeductMileageFlag = false;
            package.CashType = CreditCash;
            package.CashTypeFlag = AutomaticCash;
            package.SetSingleProduct(offer.id);
            return package;
        }

        CShopProduct CreateProduct(const Offer& offer)
        {
            CShopProduct product;
            product.ProductSeq = offer.id;
            StringCchCopy(product.ProductName, std::size(product.ProductName), offer.name.data());
            StringCchCopy(product.PropertyName, std::size(product.PropertyName), L"Quantity");
            StringCchPrintf(product.Value, std::size(product.Value), L"%u", offer.quantity);
            StringCchCopy(product.UnitName, std::size(product.UnitName), L"");
            product.Price = offer.price;
            product.PriceSeq = offer.id;
            product.PropertyType = QuantityProperty;
            product.MustFlag = RequiredProperty;
            product.vOrder = 1;
            product.DeleteFlag = ActiveProduct;
            product.StorageGroup = StorageGroup;
            product.ShareFlag = AccountShared;
            StringCchPrintf(product.InGamePackageID, std::size(product.InGamePackageID), L"%u", offer.itemCode);
            product.PropertySeq = QuantityPropertySequence;
            product.ProductType = CInGameShopSystem::IGS_GOODS_TYPE_CONSUMPTION;
            product.UnitType = QuantityUnit;
            return product;
        }
    }

    void Initialize(CShopList& catalog)
    {
        catalog.GetCategoryListPtr()->Clear();
        catalog.GetPackageListPtr()->Clear();
        catalog.GetProductListPtr()->Clear();
        AddCategory(catalog, RootCategory, I18N::Game::SoloShopTitle, true);
        AddCategory(catalog, EquipmentCategory, I18N::Game::SoloShopEquipment, false);
        AddCategory(catalog, MaterialsCategory, I18N::Game::SoloShopMaterials, false);
        AddCategory(catalog, SkillsCategory, I18N::Game::SoloShopSkills, false);
        AddCategory(catalog, PetsCategory, I18N::Game::SoloShopPets, false);
        AddCategory(catalog, ConsumablesCategory, I18N::Game::SoloShopConsumables, false);
    }

    bool Append(CShopList& catalog, const Offer& offer)
    {
        constexpr int MaximumItemCode = 16 * 512;
        constexpr int MaximumItemLevel = 15;
        constexpr int MaximumPrice = 1000000;
        if (offer.itemCode >= MaximumItemCode || offer.level > MaximumItemLevel
            || offer.id != static_cast<std::uint32_t>(offer.itemCode) * 16 + offer.level + 1
            || offer.category < FirstCategory || offer.category > LastCategory
            || offer.price <= 0 || offer.price > MaximumPrice || offer.quantity == 0 || offer.name[0] == L'\0')
        {
            return false;
        }

        CShopPackage existing;
        if (catalog.GetPackageListPtr()->GetValueByKey(offer.id, existing))
        {
            return false;
        }

        catalog.GetPackageListPtr()->Append(CreatePackage(offer));
        catalog.GetCategoryListPtr()->InsertPackage(offer.category, offer.id);
        catalog.GetProductListPtr()->Append(CreateProduct(offer));
        return true;
    }
}

void CInGameShopSystem::BeginSoloCatalog(int count)
{
    m_soloCatalogReady = false;
    m_soloExpectedOffers = count > 0 && count <= GameShop::SoloCatalog::MaximumOffers ? count : -1;
    m_soloReceivedOffers = 0;
    auto* catalog = m_ShopManager.GetListPtr();
    GameShop::SoloCatalog::Initialize(*catalog);
    m_pCategoryList = catalog->GetCategoryListPtr();
    m_pPackageList = catalog->GetPackageListPtr();
    m_pProductList = catalog->GetProductListPtr();
}

void CInGameShopSystem::AppendSoloOffer(const GameShop::SoloCatalog::Offer& offer)
{
    if (m_soloExpectedOffers < 0 || m_soloReceivedOffers >= m_soloExpectedOffers
        || !GameShop::SoloCatalog::Append(*m_ShopManager.GetListPtr(), offer))
    {
        m_soloExpectedOffers = -1;
        return;
    }
    ++m_soloReceivedOffers;
}

void CInGameShopSystem::FinishSoloCatalog()
{
    m_soloCatalogReady = m_soloExpectedOffers > 0 && m_soloReceivedOffers == m_soloExpectedOffers;
}
#endif
