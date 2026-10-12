// <copyright file="ItemCraftAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.Items;

using System.Collections.Concurrent;
using MUnique.OpenMU.DataModel.Configuration.ItemCrafting;
using MUnique.OpenMU.GameLogic.Views.NPC;

/// <summary>
/// The action to craft items with crafting NPCs.
/// </summary>
public class ItemCraftAction
{
    // A4-H2: this cache is shared across all players (the action is a single long-lived instance),
    // so it must be thread-safe. A plain Dictionary with a TryGetValue-then-Add check is not: two
    // players crafting the same ItemCrafting concurrently could race into a corrupted dictionary.
    private readonly ConcurrentDictionary<ItemCrafting, IItemCraftingHandler> _craftingHandlerCache = new();

    /// <summary>
    /// Mixes the items at the currently opened crafting NPC.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="mixTypeId">The mix type identifier.</param>
    /// <param name="socketSlot">The socket slot.</param>
    public async ValueTask MixItemsAsync(Player player, byte mixTypeId, byte socketSlot)
    {
        var npcStats = player.OpenedNpc?.Definition;

        var crafting = npcStats?.ItemCraftings.FirstOrDefault(c => c.Number == mixTypeId);
        if (crafting is null)
        {
            await player.InvokeViewPlugInAsync<IShowItemCraftingResultPlugIn>(p => p.ShowResultAsync(CraftingResult.IncorrectMixItems, null)).ConfigureAwait(false);
            return;
        }

        var craftingHandler = this._craftingHandlerCache.GetOrAdd(crafting, this.CreateCraftingHandler);

        (CraftingResult, Item?) result;
        try
        {
            result = await craftingHandler.DoMixAsync(player, socketSlot).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // The fee and some materials may already have been consumed, so report the real error.
            // Reporting "missing materials" here both misleads the player and hides data loss.
            player.Logger.LogError(
                exception,
                "Chaos machine crafting {Crafting} failed after partial consumption for player {Player}.",
                crafting,
                player.Name);
            result = (CraftingResult.LackingMixItems, null);
        }

        var itemList = player.TemporaryStorage?.Items.ToList() ?? new List<Item>();
        await player.InvokeViewPlugInAsync<IShowItemCraftingResultPlugIn>(p => p.ShowResultAsync(result.Item1, itemList.Count > 1 ? null : result.Item2)).ConfigureAwait(false);
        await player.InvokeViewPlugInAsync<IShowMerchantStoreItemListPlugIn>(
            p => p.ShowMerchantStoreItemListAsync(
                itemList,
                npcStats!.NpcWindow == NpcWindow.PetTrainer && result.Item1 != CraftingResult.Success ? StoreKind.ResurrectionFailed : StoreKind.ChaosMachine))
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Finds the relevant <see cref="ItemCrafting"/> by testing the mix items against every crafting's item requirements.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <returns>The relevant <see cref="ItemCrafting"/>.</returns>
    public ItemCrafting? FindAppropriateCraftingByItems(Player player)
    {
        if (player.OpenedNpc?.Definition is { } npc)
        {
            // Wing crafting is similar but has one extra requirement (chaos weapon) than
            // chaos weapon crafting, so we set a descending order so it gets checked first
            foreach (var itemCrafting in npc.ItemCraftings.OrderByDescending(c => c.Number))
            {
                var craftingHandler = this._craftingHandlerCache.GetOrAdd(itemCrafting, this.CreateCraftingHandler);

                if (craftingHandler.TryGetRequiredItems(player, out _, out _) is null)
                {
                    return itemCrafting;
                }
            }
        }

        return null;
    }

    private IItemCraftingHandler CreateCraftingHandler(ItemCrafting crafting)
    {
        if (!string.IsNullOrWhiteSpace(crafting.ItemCraftingHandlerClassName))
        {
            var type = Type.GetType(crafting.ItemCraftingHandlerClassName);
            if (type != null)
            {
                if (type.BaseType == typeof(SimpleItemCraftingHandler))
                {
                    return (IItemCraftingHandler)Activator.CreateInstance(type, crafting.SimpleCraftingSettings)!;
                }

                return (IItemCraftingHandler)Activator.CreateInstance(type)!;
            }

            throw new ArgumentException($"Item crafting handler '{crafting.ItemCraftingHandlerClassName}' not found.", nameof(crafting));
        }

        if (crafting.SimpleCraftingSettings != null)
        {
            return new SimpleItemCraftingHandler(crafting.SimpleCraftingSettings);
        }

        throw new ArgumentException("No simple crafting settings or item crafting handler name specified.", nameof(crafting));
    }
}
