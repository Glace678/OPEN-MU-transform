// <copyright file="BuyRequestAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.PlayerStore;

using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.Interfaces;

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

            using var itemContext = requestedPlayer.GameContext.PersistenceContextProvider.CreateNewTradeContext();
            itemContext.Attach(item);
            await requestedPlayer.ShopStorage.RemoveItemAsync(item).ConfigureAwait(false);
            await requestedPlayer.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
            await requestedPlayer.InvokeViewPlugInAsync<IItemSoldByPlayerShopPlugIn>(p => p.ItemSoldByPlayerShopAsync(slot, player)).ConfigureAwait(false);
            await requestedPlayer.InvokeViewPlugInAsync<IItemRemovedPlugIn>(p => p.RemoveItemAsync(slot)).ConfigureAwait(false);

            item.ItemSlot = (byte)freeslot;
            item.StorePrice = null;

            // The item is already removed from the seller's storage and the money has moved, so
            // the add to the buyer's inventory must succeed. Re-check inside the lock (the free
            // slot could have been taken by a non-packet reward) and undo the whole transfer if not.
            if (player.Inventory is null
                || player.Inventory.CheckInvSpace(item) is null
                || !await player.Inventory.AddItemAsync(item).ConfigureAwait(false))
            {
                // Refund the buyer and debit the seller back, then return the item to its shop slot.
                if (!player.TryRemoveMoney(itemPrice))
                {
                    player.Logger.LogError("Could not debit back the buyer while compensating a failed shop purchase of item {Item}.", item);
                }

                if (!requestedPlayer.TryRemoveMoney(itemPrice))
                {
                    player.Logger.LogError("Could not refund the seller while compensating a failed shop purchase of item {Item}; manual intervention required.", item);
                }

                item.StorePrice = itemPrice;
                item.ItemSlot = slot;
                await requestedPlayer.ShopStorage.AddItemAsync(slot, item).ConfigureAwait(false);
                await requestedPlayer.InvokeViewPlugInAsync<IItemSoldByPlayerShopPlugIn>(p => p.ItemSoldByPlayerShopAsync(slot, player)).ConfigureAwait(false);
                await player.InvokeViewPlugInAsync<IPlayerShopBuyRequestResultPlugIn>(p => p.ShowResultAsync(requestedPlayer, ItemBuyResult.MoneyOverflowOrNotEnoughSpace, null)).ConfigureAwait(false);
                await player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.InventoryNotEnoughSpace)).ConfigureAwait(false);
                return;
            }

            requestedPlayer.PersistenceContext.Detach(item);
            await itemContext.SaveChangesAsync().ConfigureAwait(false);
            player.PersistenceContext.Attach(item);
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
}