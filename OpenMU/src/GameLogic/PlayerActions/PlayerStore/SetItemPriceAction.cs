// <copyright file="SetItemPriceAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.PlayerStore;

using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameLogic.Views.PlayerShop;

/// <summary>
/// Action to set the price of an item in the player shop.
/// </summary>
public class SetItemPriceAction
{
    /// <summary>
    /// Sets the price of an item.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="slot">The slot of the item in the store.</param>
    /// <param name="price">The price.</param>
    public async ValueTask SetPriceAsync(Player player, byte slot, int price)
    {
        ItemPriceResult result;
        if (player.Level < 6)
        {
            result = ItemPriceResult.CharacterLevelTooLow;
        }
        else if (player.ShopStorage?.StoreOpen ?? false)
        {
            // Changing a price while the shop is open would race with a buyer that has already
            // read the old price (TOCTOU); the shop must be closed before prices can change.
            result = ItemPriceResult.Failed;
        }
        else if (slot < InventoryConstants.FirstStoreItemSlotIndex)
        {
            // Only items in the shop storage may be priced; slots below the first store slot
            // belong to the regular inventory.
            result = ItemPriceResult.ItemSlotOutOfRange;
        }
        else
        {
            result = ItemPriceResult.ItemNotFound;

            // Look the item up in the shop storage only, instead of scanning the whole inventory.
            var item = player.ShopStorage?.GetItem(slot);
            if (item is { })
            {
                item.StorePrice = price > 0 ? price : (int?)null;
                result = price >= 0 ? ItemPriceResult.Success : ItemPriceResult.PriceNegative;
            }
        }

        await player.InvokeViewPlugInAsync<IItemPriceSetResponsePlugIn>(p => p.ItemPriceSetResponseAsync(slot, result)).ConfigureAwait(false);
    }
}
