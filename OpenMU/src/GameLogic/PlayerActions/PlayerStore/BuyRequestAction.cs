// <copyright file="BuyRequestAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.PlayerStore;

using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Interfaces;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Action to buy an item from another player shop.
/// </summary>
public class BuyRequestAction
{
    private readonly CloseStoreAction _closeStoreAction = new();

    /// <summary>
    /// Buys the item from another player shop.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="requestedPlayer">The requested player.</param>
    /// <param name="slot">The slot.</param>
    public async ValueTask BuyItemAsync(Player player, Player requestedPlayer, byte slot)
    {
        using var loggerScope = player.Logger.BeginScope(this.GetType());

        // A player must not buy from their own shop: the money would only move between their own
        // pockets while the item jumps out of the shop storage into the inventory, bypassing the
        // "close the shop before taking items back" flow.
        if (ReferenceEquals(player, requestedPlayer))
        {
            player.Logger.LogWarning("Player {Character} tried to buy an item from their own shop.", player.SelectedCharacter?.Name);
            await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.NameMismatchOrPriceMissing, null)).ConfigureAwait(false);
            return;
        }

        if (requestedPlayer.IsTemplatePlayer || player.IsTemplatePlayer)
        {
            await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.ItemBlock, null)).ConfigureAwait(false);
            return;
        }

        if (!(requestedPlayer.ShopStorage?.StoreOpen ?? false))
        {
            player.Logger.LogDebug("Store not open, Character {0}", requestedPlayer.SelectedCharacter?.Name);
            await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.ShopNotOpened, null)).ConfigureAwait(false);
            return;
        }

        if (slot < InventoryConstants.FirstStoreItemSlotIndex)
        {
            player.Logger.LogWarning("Store Slot too low: {0}, possible hacker", slot);
            await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.InvalidShopSlot, null)).ConfigureAwait(false);
            return;
        }

        var item = requestedPlayer.ShopStorage.GetItem(slot);
        if (item?.StorePrice is null)
        {
            player.Logger.LogDebug("Item unavailable, Slot {0}", slot);
            await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.NameMismatchOrPriceMissing, null)).ConfigureAwait(false);
            return;
        }

        // The authoritative price is read inside the store lock below. Reading it here and
        // using it for the money deduction would race with the seller changing the price (TOCTOU).

        // Check Inv Space
        var freeslot = player.Inventory?.CheckInvSpace(item);
        if (freeslot is null)
        {
            await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.MoneyOverflowOrNotEnoughSpace, null)).ConfigureAwait(false);
            await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.InventoryNotEnoughSpace)).ConfigureAwait(false);
            return;
        }

        bool itemSold = false;
        using (await requestedPlayer.ShopStorage.StoreLock.LockAsync())
        {
            if (!requestedPlayer.ShopStorage.StoreOpen)
            {
                player.Logger.LogDebug("Store not open anymore, Character {0}", requestedPlayer.SelectedCharacter?.Name);
                await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.ShopNotOpened, null)).ConfigureAwait(false);
                return;
            }

            item = requestedPlayer.ShopStorage.GetItem(slot);
            if (item?.StorePrice is not { } itemPrice)
            {
                await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.NameMismatchOrPriceMissing, null)).ConfigureAwait(false);
                return;
            }

            // Re-check buyer inventory space inside StoreLock before touching money or shop items.
            var lockedFreeSlot = player.Inventory?.CheckInvSpace(item);
            if (lockedFreeSlot is null || player.Inventory is null)
            {
                await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.MoneyOverflowOrNotEnoughSpace, null)).ConfigureAwait(false);
                await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.InventoryNotEnoughSpace)).ConfigureAwait(false);
                return;
            }

            // Re-check the money against the price read inside the lock.
            if (player.Money < itemPrice)
            {
                await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.LackOfMoney, null)).ConfigureAwait(false);
                return;
            }

            player.Logger.LogDebug("BuyRequest, Item Price: {0}", itemPrice);
            if (!player.TryRemoveMoney(itemPrice))
            {
                await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.LackOfMoney, null)).ConfigureAwait(false);
                return;
            }

            if (!requestedPlayer.TryAddMoney(itemPrice))
            {
                // Seller cannot receive the zen (cap reached): refund and abort before touching the item.
                player.TryAddMoney(itemPrice);
                await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.MoneyOverflowOrNotEnoughSpace, null)).ConfigureAwait(false);
                await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.SellerInventoryFull)).ConfigureAwait(false);
                return;
            }

            var addedToBuyer = false;
            var detachedFromSeller = false;
            var tradeCommitted = false;
            try
            {
                using var itemContext = requestedPlayer.GameContext.PersistenceContextProvider.CreateNewTradeContext();
                itemContext.Attach(item);
                await requestedPlayer.ShopStorage.RemoveItemAsync(item).ConfigureAwait(false);

                item.ItemSlot = lockedFreeSlot.Value;
                item.StorePrice = null;

                addedToBuyer = await player.Inventory.AddItemAsync(item).ConfigureAwait(false);
                if (!addedToBuyer)
                {
                    await RollbackFailedPurchaseAsync(player, requestedPlayer, item, slot, itemPrice, addedToBuyer, detachedFromSeller).ConfigureAwait(false);
                    await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.MoneyOverflowOrNotEnoughSpace, null)).ConfigureAwait(false);
                    await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.InventoryNotEnoughSpace)).ConfigureAwait(false);
                    return;
                }

                detachedFromSeller = requestedPlayer.PersistenceContext.Detach(item);

                // Register both wallet balances on the trade context so the single SaveChanges below
                // commits the item transfer AND the buyer deduction + seller credit atomically in one DB
                // transaction.
                await RegisterWalletBalanceAsync(itemContext, player, player.Money).ConfigureAwait(false);
                await RegisterWalletBalanceAsync(itemContext, requestedPlayer, requestedPlayer.Money).ConfigureAwait(false);

                tradeCommitted = await itemContext.SaveChangesAsync().ConfigureAwait(false);
                if (!tradeCommitted)
                {
                    await RollbackFailedPurchaseAsync(player, requestedPlayer, item, slot, itemPrice, addedToBuyer, detachedFromSeller).ConfigureAwait(false);
                    await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.ItemBlock, null)).ConfigureAwait(false);
                    return;
                }
            }
            catch (Exception ex) when (!tradeCommitted)
            {
                player.Logger.LogError(ex, "Unexpected error while committing player store purchase of item {Item} from {Seller} to {Buyer}; rolling back.", item, requestedPlayer, player);
                await RollbackFailedPurchaseAsync(player, requestedPlayer, item, slot, itemPrice, addedToBuyer, detachedFromSeller).ConfigureAwait(false);
                await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.ItemBlock, null)).ConfigureAwait(false);
                return;
            }

            player.PersistenceContext.Attach(item);

            // Explicitly persist both players' own contexts. The wallet rows were already committed
            // in the trade transaction above; this also flushes any other in-flight change and means
            // the settlement does not rely on a later periodic save.
            await SavePlayerAfterPurchaseAsync(player).ConfigureAwait(false);
            await SavePlayerAfterPurchaseAsync(requestedPlayer).ConfigureAwait(false);

            await requestedPlayer.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            await requestedPlayer.InvokeViewPlugInAsync<IItemSoldByPlayerShopPlugIn>(p => p.ItemSoldByPlayerShopAsync(slot, player)).ConfigureAwait(false);
            await requestedPlayer.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(slot)).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.Success, item)).ConfigureAwait(false);
            await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            itemSold = true;

            player.GameContext.PlugInManager.GetPlugInPoint<IItemSoldToOtherPlayerPlugIn>()?.ItemSold(requestedPlayer, item, player);
        }

        if (itemSold)
        {
            if (requestedPlayer.ShopStorage.Items.Any())
            {
                // this update may be sent to other players as well which are currently looking at the store
                await player.InvokeViewPlugInAsync<Views.PlayerShop.IShowShopItemListPlugIn>(p => p.ShowShopItemListAsync(requestedPlayer, true)).ConfigureAwait(false);
            }
            else
            {
                await this._closeStoreAction.CloseStoreAsync(requestedPlayer).ConfigureAwait(false);
            }
        }
    }

    private static async ValueTask RegisterWalletBalanceAsync(IContext itemContext, Player player, int newMoney)
    {
        if (player.SelectedCharacter?.Inventory is not { } inventory)
        {
            return;
        }

        var storageId = inventory.GetId();
        if (storageId == Guid.Empty)
        {
            return;
        }

        await itemContext.AttachMoneyBalanceAsync(storageId, newMoney).ConfigureAwait(false);
    }

    private static async ValueTask RollbackFailedPurchaseAsync(
        Player buyer,
        Player seller,
        Item item,
        byte originalSlot,
        int itemPrice,
        bool addedToBuyer,
        bool detachedFromSeller)
    {
        if (addedToBuyer && buyer.Inventory is { } buyerInventory)
        {
            await buyerInventory.RemoveItemAsync(item).ConfigureAwait(false);
        }

        if (detachedFromSeller)
        {
            seller.PersistenceContext.Attach(item);
        }

        item.StorePrice = itemPrice;
        item.ItemSlot = originalSlot;

        if (seller.ShopStorage is { } shopStorage)
        {
            var restored = shopStorage is ShopStorage concreteShopStorage
                ? await concreteShopStorage.RestoreItemInternalAsync(originalSlot, item).ConfigureAwait(false)
                : await RestoreViaInterfaceAsync(shopStorage, originalSlot, item).ConfigureAwait(false);
            if (!restored && !shopStorage.ItemStorage.Items.Contains(item))
            {
                shopStorage.ItemStorage.Items.Add(item);
                seller.IsEconomyPersistencePending = true;
            }
        }

        if (!buyer.TryAddMoney(itemPrice))
        {
            buyer.Logger.LogError("Could not refund the buyer while compensating a failed shop purchase of item {Item}.", item);
        }

        if (!seller.TryRemoveMoney(itemPrice))
        {
            buyer.Logger.LogError("Could not debit back the seller while compensating a failed shop purchase of item {Item}; manual intervention required.", item);
        }

        await buyer.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
        await seller.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
    }

    private static async ValueTask<bool> RestoreViaInterfaceAsync(IShopStorage shopStorage, byte slot, Item item)
    {
        var wasOpen = shopStorage.StoreOpen;
        try
        {
            shopStorage.StoreOpen = false;
            return await shopStorage.AddItemAsync(slot, item).ConfigureAwait(false);
        }
        finally
        {
            shopStorage.StoreOpen = wasOpen;
        }
    }

    private static async ValueTask SavePlayerAfterPurchaseAsync(Player targetPlayer)
    {
        try
        {
            if (!await targetPlayer.SaveProgressAsync().ConfigureAwait(false))
            {
                targetPlayer.IsEconomyPersistencePending = true;
                targetPlayer.Logger.LogWarning("SaveProgressAsync returned false after committed store purchase for {Player}; marked economy persistence pending.", targetPlayer);
            }
        }
        catch (Exception ex)
        {
            targetPlayer.IsEconomyPersistencePending = true;
            targetPlayer.Logger.LogError(ex, "SaveProgressAsync failed after committed store purchase for {Player}; marked economy persistence pending.", targetPlayer);
        }
    }
}
