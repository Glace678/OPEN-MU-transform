#include "stdafx.h"
#include "UI/NewUI/Inventory/NewUIInventoryCtrl.h"
#include "UI/NewUI/Inventory/NewUIInventoryActionController.h"

bool SEASON3B::CNewUIInventoryCtrl::CanTouchItem(DWORD key, int index)
{
    if (!IsVisible() || IsLocked() || IsRepairMode() || GetPickedItem()
        || m_StorageType != STORAGE_TYPE::INVENTORY || (m_pOwner && !m_pOwner->IsEnabled())) return false;
    ITEM* item = FindItem(index);
    return item && item->Key == key && GetIndexByItem(item) == index;
}

bool SEASON3B::CNewUIInventoryCtrl::SelectTouchItem(DWORD key, int index)
{
    if (!CanTouchItem(key, index)) return false;
    m_iPointedSquareIndex = index;
    m_EventState = EVENT_HOVER;
    UpdateItemToolTip(FindItem(index));
    return true;
}

bool SEASON3B::CNewUIInventoryCtrl::BeginTouchItemDrag(DWORD key, int index)
{
    if (!CanTouchItem(key, index)) return false;
    ITEM* item = FindItem(index);
    if (!CreatePickedItem(this, item))
    {
        DeletePickedItem();
        return false;
    }
    DeleteItemToolTip();
    m_EventState = EVENT_PICKING;
    RemoveItem(item);
    return true;
}

bool SEASON3B::CNewUIInventoryCtrl::OwnsTouchItemDrag(DWORD key, int index) const
{
    auto* picked = GetPickedItem();
    return picked && picked->GetOwnerInventory() == this && picked->GetItem()
        && picked->GetItem()->Key == key && picked->GetSourceLinealPos() == index;
}

void SEASON3B::CNewUIInventoryCtrl::RestoreTouchItemDrag(DWORD key, int index)
{
    if (!OwnsTouchItemDrag(key, index)) return;
    // Do not overwrite an item installed by a server refresh during the drag.
    if (FindItem(index))
    {
        DeletePickedItem();
        RequestInventoryRefresh();
    }
    else
        BackupPickedItem();
}

bool SEASON3B::CNewUIInventoryActionController::UseTouchItem(CNewUIInventoryCtrl* control, DWORD key, int index) const
{
    if (!m_pContext || !control || m_pContext->GetRepairMode() != REPAIR_MODE_OFF
        || !control->CanTouchItem(key, index)) return false;
    // No equip, transfer, sell or drop fallback, including rejected uses.
    return TryConsumeItem(control, control->FindItem(index), index);
}

bool SEASON3B::CNewUIInventoryActionController::PlaceTouchItem(CNewUIInventoryCtrl* control) const
{
    if (!m_pContext || !control || !control->IsVisible() || control->IsLocked()
        || control->IsRepairMode() || control->GetStorageType() != STORAGE_TYPE::INVENTORY
        || m_pContext->GetRepairMode() != REPAIR_MODE_OFF) return false;
    return HandlePickedItemPlacement(control);
}
