#include "stdafx.h"
#include <doctest.h>
#include "UI/NewUI/Inventory/NewUIInventoryActionController.h"

using namespace SEASON3B;

namespace
{
    struct Effects { int tooltips{}, pickups{}, removes{}, restores{}, refreshes{}, uses{}, placements{}; } effects;
    bool failPickup{};
    struct Context final : IInventoryActionContext
    {
        REPAIR_MODE repair{REPAIR_MODE_OFF};
        REPAIR_MODE GetRepairMode() const override { return repair; }
        bool IsEquipable(int, ITEM*) const override { return false; }
        void ResetMouseRButton() override { FAIL("Touch use reached a generic right click"); }
        void ResetMouseLButton() override { }
        int FindEmptySlot(ITEM*) const override { return -1; }
        bool IsRepairEnableLevel() const override { return false; }
    };
    void Add(CNewUIInventoryCtrl& control, DWORD key, int column = 0, short type = 1)
    {
        ITEM item; item.Key = key; item.Type = type;
        REQUIRE(control.AddItem(column, 0, &item));
    }
}

// Renderer/item-manager/packet doubles surround the actual production touch
// methods compiled from InventoryTouchOperations.cpp and the real class header.
CNewUIPickedItem* CNewUIInventoryCtrl::ms_pPickedItem = nullptr;
CNewUIInventoryCtrl::CNewUIInventoryCtrl() { Init(); }
CNewUIInventoryCtrl::~CNewUIInventoryCtrl()
{
    if (ms_pPickedItem && ms_pPickedItem->GetOwnerInventory() == this) DeletePickedItem();
    for (auto* item : m_vecItem) delete item;
}
void CNewUIInventoryCtrl::Init()
{
    m_pOwner = nullptr; m_StorageType = STORAGE_TYPE::INVENTORY;
    m_nColumn = 8; m_nRow = 8; m_nIndexOffset = 12;
    m_bShow = true; m_bLock = false; m_bRepairMode = false;
    m_EventState = EVENT_NONE; m_iPointedSquareIndex = -1; m_pToolTipItem = nullptr;
    effects = {}; failPickup = false;
}
bool CNewUIInventoryCtrl::IsVisible() const { return m_bShow; }
bool CNewUIInventoryCtrl::IsLocked() const { return m_bLock; }
bool CNewUIInventoryCtrl::IsRepairMode() { return m_bRepairMode; }
void CNewUIInventoryCtrl::HideInventory() { m_bShow = false; }
void CNewUIInventoryCtrl::LockInventory() { m_bLock = true; }
void CNewUIInventoryCtrl::SetRepairMode(bool repair) { m_bRepairMode = repair; }
CNewUIInventoryCtrl::EVENT_STATE CNewUIInventoryCtrl::GetEventState() { return m_EventState; }
int CNewUIInventoryCtrl::GetPointedSquareIndex() { return m_iPointedSquareIndex; }
int CNewUIInventoryCtrl::GetIndex(int column, int row) { return m_nIndexOffset + column + row * m_nColumn; }
int CNewUIInventoryCtrl::GetIndexByItem(ITEM* item) { return GetIndex(item->x, item->y); }
ITEM* CNewUIInventoryCtrl::FindItem(int index)
{
    for (auto* item : m_vecItem) if (GetIndexByItem(item) == index) return item;
    return nullptr;
}
bool CNewUIInventoryCtrl::AddItem(int column, int row, ITEM* item)
{
    if (FindItem(GetIndex(column, row))) return false;
    auto* copy = new ITEM(*item); copy->x = column; copy->y = row; m_vecItem.push_back(copy); return true;
}
void CNewUIInventoryCtrl::RemoveItem(ITEM* item)
{
    ++effects.removes;
    m_vecItem.erase(std::remove(m_vecItem.begin(), m_vecItem.end(), item), m_vecItem.end()); delete item;
}
void CNewUIInventoryCtrl::UpdateItemToolTip(ITEM* item) { m_pToolTipItem = item; ++effects.tooltips; }
void CNewUIInventoryCtrl::DeleteItemToolTip() { m_pToolTipItem = nullptr; }
void CNewUIInventoryCtrl::RequestInventoryRefresh() const { ++effects.refreshes; }
void CNewUIInventoryCtrl::Render3D() { }
CNewUIPickedItem* CNewUIInventoryCtrl::GetPickedItem() { return ms_pPickedItem; }
bool CNewUIInventoryCtrl::CreatePickedItem(CNewUIInventoryCtrl* source, ITEM* item)
{
    ++effects.pickups;
    ms_pPickedItem = new CNewUIPickedItem;
    if (failPickup) { delete ms_pPickedItem; ms_pPickedItem = nullptr; return false; }  // #35: release on failed pickup
    return ms_pPickedItem->Create(nullptr, source, item);
}
void CNewUIInventoryCtrl::DeletePickedItem() { delete ms_pPickedItem; ms_pPickedItem = nullptr; }
void CNewUIInventoryCtrl::BackupPickedItem()
{
    ++effects.restores;
    auto* owner = ms_pPickedItem->GetOwnerInventory();
    auto* item = ms_pPickedItem->GetItem();
    if (owner->AddItem(item->x, item->y, item)) DeletePickedItem();
}
CNewUIPickedItem::CNewUIPickedItem() : m_pNewItemMng(nullptr), m_pSrcInventory(nullptr), m_pPickedItem(nullptr) { }
CNewUIPickedItem::~CNewUIPickedItem() { delete m_pPickedItem; }
bool CNewUIPickedItem::Create(CNewUIItemMng*, CNewUIInventoryCtrl* source, ITEM* item)
{
    m_pSrcInventory = source; m_pPickedItem = new ITEM(*item); m_pPickedItem->Key += 1000; return true;
}
CNewUIInventoryCtrl* CNewUIPickedItem::GetOwnerInventory() const { return m_pSrcInventory; }
ITEM* CNewUIPickedItem::GetItem() const { return m_pPickedItem; }
int CNewUIPickedItem::GetSourceLinealPos() { return m_pSrcInventory->GetIndexByItem(m_pPickedItem); }
void CNewUIPickedItem::Render3D() { }
CNewUIInventoryActionController::CNewUIInventoryActionController() : m_pContext(nullptr) { }
void CNewUIInventoryActionController::SetContext(IInventoryActionContext* context) { m_pContext = context; }
bool CNewUIInventoryActionController::TryConsumeItem(CNewUIInventoryCtrl*, ITEM* item, int) const
{
    if (item->Type == 1 || item->Type == 2) { ++effects.uses; return true; }
    return false;
}
bool CNewUIInventoryActionController::HandlePickedItemPlacement(CNewUIInventoryCtrl*) const { ++effects.placements; return true; }

TEST_CASE("inventory touch actual control selects first touch without a preexisting hover frame")
{
    CNewUIInventoryCtrl control; Add(control, 20);
    CHECK(control.GetEventState() == CNewUIInventoryCtrl::EVENT_NONE);
    CHECK(control.SelectTouchItem(20, 12));
    CHECK(control.GetEventState() == CNewUIInventoryCtrl::EVENT_HOVER);
    CHECK(control.GetPointedSquareIndex() == 12);
    CHECK(effects.tooltips == 1); CHECK(effects.pickups == 0); CHECK(control.FindItem(12));
}

TEST_CASE("inventory touch actual control rejects stale keys indices locks repair and hidden owners")
{
    for (int state = 0; state < 5; ++state)
    {
        CNewUIInventoryCtrl control; Add(control, 20);
        DWORD key = 20; int index = 12;
        if (state == 0) key = 21; else if (state == 1) index = 13;
        else if (state == 2) control.LockInventory(); else if (state == 3) control.SetRepairMode(true);
        else control.HideInventory();
        CHECK_FALSE(control.BeginTouchItemDrag(key, index));
        CHECK_FALSE(control.SelectTouchItem(key, index));
        CHECK(effects.pickups == 0); CHECK(control.FindItem(12));
    }
}

TEST_CASE("inventory touch actual drag duplicates removes before release and restores once")
{
    CNewUIInventoryCtrl control; Add(control, 20);
    CHECK(control.BeginTouchItemDrag(20, 12));
    CHECK(control.GetEventState() == CNewUIInventoryCtrl::EVENT_PICKING);
    CHECK_FALSE(control.FindItem(12)); REQUIRE(control.GetPickedItem());
    const DWORD cloneKey = control.GetPickedItem()->GetItem()->Key;
    CHECK(cloneKey != 20); CHECK(control.OwnsTouchItemDrag(cloneKey, 12));
    CHECK_FALSE(control.OwnsTouchItemDrag(20, 12));
    control.RestoreTouchItemDrag(cloneKey, 12); control.RestoreTouchItemDrag(cloneKey, 12);
    REQUIRE(control.FindItem(12)); CHECK(control.FindItem(12)->Key == cloneKey);
    CHECK(effects.removes == 1); CHECK(effects.restores == 1); CHECK_FALSE(control.GetPickedItem());
}

TEST_CASE("inventory touch failed picked creation preserves the original and cleans the partial pick")
{
    CNewUIInventoryCtrl control; Add(control, 20); failPickup = true;
    CHECK_FALSE(control.BeginTouchItemDrag(20, 12));
    CHECK(control.FindItem(12)); CHECK_FALSE(control.GetPickedItem()); CHECK(effects.removes == 0);
}

TEST_CASE("inventory touch cancelled drag cannot overwrite a server replacement in its original slot")
{
    CNewUIInventoryCtrl control; Add(control, 20); REQUIRE(control.BeginTouchItemDrag(20, 12));
    const DWORD cloneKey = control.GetPickedItem()->GetItem()->Key;
    Add(control, 99); control.RestoreTouchItemDrag(cloneKey, 12);
    REQUIRE(control.FindItem(12)); CHECK(control.FindItem(12)->Key == 99);
    CHECK(effects.restores == 0); CHECK(effects.refreshes == 1); CHECK_FALSE(control.GetPickedItem());
}

TEST_CASE("inventory touch actual action entry consumes or learns but has no right-click fallback")
{
    for (short type : {0, 1, 2})
    {
        CNewUIInventoryCtrl control; Add(control, 20, 0, type);
        Context context; CNewUIInventoryActionController action; action.SetContext(&context);
        CHECK(action.UseTouchItem(&control, 20, 12) == (type != 0));
        CHECK(effects.uses == (type != 0 ? 1 : 0)); CHECK(effects.pickups == 0); CHECK(effects.removes == 0);
        CHECK(effects.placements == 0); CHECK(control.FindItem(12));
        CHECK_FALSE(action.UseTouchItem(&control, 21, 12));
        context.repair = REPAIR_MODE_ON; CHECK_FALSE(action.UseTouchItem(&control, 20, 12));
    }
}

TEST_CASE("inventory touch actual placement is restricted to unlocked normal inventory")
{
    CNewUIInventoryCtrl control; Context context; CNewUIInventoryActionController action;
    action.SetContext(&context); CHECK(action.PlaceTouchItem(&control)); CHECK(effects.placements == 1);
    control.LockInventory(); CHECK_FALSE(action.PlaceTouchItem(&control)); CHECK(effects.placements == 1);
}
