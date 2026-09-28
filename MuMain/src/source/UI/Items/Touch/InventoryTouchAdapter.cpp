#include "stdafx.h"
#include "UI/Items/Touch/InventoryTouchAdapter.h"
#include "UI/Items/Touch/ItemTouchController.h"
#include "UI/Items/Touch/QuickItemTouchLayout.h"
#include "UI/Input/InterfaceInputPolicy.h"
#include "UI/NewUI/NewUISystem.h"
#include "UI/Legacy/UIManager.h"
#include "UI/Legacy/UIControls.h"
#include "Scenes/SceneCore.h"
#include "App/Platform/Windows/Winmain.h"
#include "Engine/Object/ZzzInventory.h"
#include "World/MapInfra/MapManager.h"

namespace UI::Items::Touch
{
    namespace
    {
        using namespace SEASON3B;

        std::optional<std::uint64_t> CurrentContext()
        {
            if (SceneFlag != MAIN_SCENE || !g_bWndActive || !Hero || Hero->Dead
                || !g_pNewUISystem || !g_pNewItemMng || !g_pNewUI3DRenderMng || EquipmentItem
                || !g_MessageBox || !g_MessageBox->IsEmpty()
                || CUITextInputBox::GetFocusedPortable()) return std::nullopt;
            const UI::Input::LayerState layerState{
                false, g_pSkillList && g_pSkillList->IsSelectionOpen(),
                PartyNumber > 0, gMapManager.InBattleCastle()};
            std::uint64_t context = 0;
            for (int key = INTERFACE_BEGIN + 1; key < INTERFACE_END; ++key)
            {
                if (!g_pNewUISystem->IsVisible(key) || !UI::Input::BlocksWorldInput(key, layerState)) continue;
                if (key == SEASON3B::INTERFACE_INVENTORY) context |= 1;
                else if (key == SEASON3B::INTERFACE_INVENTORY_EXT) context |= 2;
                else return std::nullopt;
            }
            if (g_pUIManager)
                for (DWORD key = ::INTERFACE_FRIEND; key < INTERFACE_MAX_COUNT; ++key)
                    if (g_pUIManager->IsOpen(key)) return std::nullopt;
            return context;
        }

        CNewUIInventoryCtrl* FindControlAt(float x, float y)
        {
            if (!g_pMyInventory || !g_pMyInventory->IsVisible()) return nullptr;
            auto* control = g_pMyInventory->GetInventoryCtrl();
            if (control && control->IsVisible() && control->CheckPtInRect(static_cast<int>(x), static_cast<int>(y)))
                return control;
            return g_pMyInventoryExt ? g_pMyInventoryExt->FindTouchControlAt(static_cast<int>(x), static_cast<int>(y)) : nullptr;
        }

        CNewUIInventoryCtrl* ResolveControl(const ItemTouchToken& token)
        {
            if (!g_pMyInventory) return nullptr;
            auto* control = token.index < MAX_MY_INVENTORY_INDEX ? g_pMyInventory->GetInventoryCtrl()
                : (g_pMyInventoryExt ? g_pMyInventoryExt->FindTouchControlByIndex(token.index) : nullptr);
            return reinterpret_cast<std::uintptr_t>(control) == token.owner ? control : nullptr;
        }

        class InventoryTarget final : public ItemTouchTarget
        {
        public:
            std::optional<ItemTouchToken> Capture(float x, float y) override
            {
                const auto context = CurrentContext();
                if (!context || CNewUIInventoryCtrl::GetPickedItem()) return std::nullopt;
                if (auto* control = FindControlAt(x, y))
                {
                    ITEM* item = control->FindItemAtPt(static_cast<int>(x), static_cast<int>(y));
                    if (!item) return std::nullopt;
                    ItemTouchToken token;
                    token.owner = reinterpret_cast<std::uintptr_t>(control);
                    token.key = item->Key;
                    token.index = control->GetIndexByItem(item);
                    token.context = *context;
                    return control->CanTouchItem(token.key, token.index) ? std::optional{token} : std::nullopt;
                }
                return CaptureQuickSlot(x, y, *context);
            }

            bool Validate(const ItemTouchToken& token, bool dragging) override
            {
                const auto context = CurrentContext();
                if (!context || *context != token.context) return false;
                if (token.kind == ItemTouchKind::QuickSlot)
                {
                    int index = -1;
                    DWORD key = 0;
                    return g_pMainFrame && reinterpret_cast<std::uintptr_t>(g_pMainFrame) == token.owner
                        && g_pMainFrame->GetTouchItemSlotIdentity(token.quickSlot, index, key)
                        && index == token.index && key == token.key;
                }
                auto* control = ResolveControl(token);
                if (!control || !control->IsVisible() || control->IsLocked() || control->IsRepairMode()
                    || !g_pMyInventory || g_pMyInventory->GetRepairMode() != REPAIR_MODE_OFF) return false;
                return dragging ? control->OwnsTouchItemDrag(token.draggedKey, token.index)
                    : control->CanTouchItem(token.key, token.index);
            }

            void Select(const ItemTouchToken& token) override
            {
                if (token.kind == ItemTouchKind::InventoryItem)
                    if (auto* control = ResolveControl(token)) control->SelectTouchItem(token.key, token.index);
            }

            void Use(const ItemTouchToken& token) override
            {
                if (!Validate(token, false)) return;
                if (token.kind == ItemTouchKind::QuickSlot)
                {
                    g_pMainFrame->UseTouchItemSlot(token.quickSlot);
                    return;
                }
                CNewUIInventoryActionController action;
                action.SetContext(g_pMyInventory);
                action.UseTouchItem(ResolveControl(token), token.key, token.index);
            }

            bool BeginDrag(ItemTouchToken& token) override
            {
                if (!Validate(token, false)) return false;
                auto* control = ResolveControl(token);
                if (!control || !control->BeginTouchItemDrag(token.key, token.index)) return false;
                // Picked items are real duplicates with a new client-only key.
                token.draggedKey = CNewUIInventoryCtrl::GetPickedItem()->GetItem()->Key;
                return true;
            }

            bool PlaceDrag(const ItemTouchToken& token, float x, float y) override
            {
                if (!Validate(token, true)) return false;
                auto* target = FindControlAt(x, y);
                if (!target) return false;
                CNewUIInventoryActionController action;
                action.SetContext(g_pMyInventory);
                return action.PlaceTouchItem(target);
            }

            void RestoreDrag(const ItemTouchToken& token) override
            {
                if (auto* control = ResolveControl(token)) control->RestoreTouchItemDrag(token.draggedKey, token.index);
            }

        private:
            std::optional<ItemTouchToken> CaptureQuickSlot(float x, float y, std::uint64_t context)
            {
                if (!g_pMainFrame || !g_pMainFrame->IsVisible()) return std::nullopt;
                const int slot = QuickItemSlotAt(x, y);
                if (slot < 0) return std::nullopt;
                ItemTouchToken token;
                token.kind = ItemTouchKind::QuickSlot;
                token.owner = reinterpret_cast<std::uintptr_t>(g_pMainFrame);
                token.quickSlot = slot;
                token.context = context;
                DWORD key = 0;
                g_pMainFrame->GetTouchItemSlotIdentity(slot, token.index, key);
                token.key = key;
                return token;
            }
        };

        struct TouchRuntime
        {
            InventoryTarget target;
            ItemTouchController controller{target};
        };

        TouchRuntime& Runtime()
        {
            static TouchRuntime runtime;
            return runtime;
        }
    }

    bool BeginItemContact(const Core::Input::TouchSample& contact) { return Runtime().controller.Begin(contact); }
    void HandleItemContact(const Core::Input::TouchSample& contact) { Runtime().controller.Handle(contact); }
    void TickItemContact(std::uint64_t nowMs) { Runtime().controller.Tick(nowMs); }
    void FinishItemContactSequence() { Runtime().controller.FinishContactSequence(); }
    void CancelItemContact() { Runtime().controller.Cancel(); }
    void CancelItemContactForOwner(SEASON3B::CNewUIInventoryCtrl* owner)
    {
        if (Runtime().controller.Owns(reinterpret_cast<std::uintptr_t>(owner))) CancelItemContact();
    }
}
