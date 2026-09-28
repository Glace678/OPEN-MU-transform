// <copyright file="SoloCashShopService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.CashShop;

using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic.Views.Inventory;

/// <summary>
/// Local shop transactions, serialized with the player's other actions and progress saves.
/// Only the solo profile is permitted to create or spend local credit.
/// </summary>
public sealed class SoloCashShopService
{
    /// <summary>Gold paid by one explicit exchange action.</summary>
    public const int ExchangeGold = 1000;

    /// <summary>Local credit granted by one exchange action.</summary>
    public const int ExchangeCredit = 100;

    /// <summary>Checks whether a shop transaction is safe at the current player state.</summary>
    public static bool CanUse(Player player) =>
        SoloBalance.IsEnabled(player.GameContext.Configuration)
        && player.Account is not null && !player.IsTemplatePlayer
        && player.SelectedCharacter is not null && player.Inventory is not null && player.CurrentMap is not null
        && player.IsAlive && player.PlayerState.CurrentState == PlayerState.EnteredWorld
        && player.IsAtSafezone();

    /// <summary>Initializes the grant durably before reporting it to the client.</summary>
    public ValueTask<byte> InitializeAsync(Player player) =>
        player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!CanUse(player))
            {
                return (byte)6;
            }

            var state = SoloCashShopState.Read(player.Account!.SoloCashShopData);
            return string.IsNullOrEmpty(player.Account.SoloCashShopData)
                ? await CommitAsync(player, state).ConfigureAwait(false) : (byte)0;
        });

    /// <summary>Buys an exact, validated catalog offer into account storage.</summary>
    public ValueTask<byte> BuyAsync(Player player, uint offerId, uint category, uint priceId, ushort itemCode, uint coin, byte mileage, string recipient = "", string message = "") =>
        player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!CanUse(player))
            {
                return (byte)6;
            }

            var offer = SoloCashShopCatalog.GetOffers(player.GameContext.Configuration).FirstOrDefault(o => o.Id == offerId);
            if (offer is null || offer.Category != category || offer.ItemCode != itemCode || priceId != 0 && priceId != offer.Id)
            {
                return (byte)4;
            }

            if (coin != SoloCashShopCatalog.CoinIndex || mileage != 0)
            {
                return (byte)9;
            }

            // Single-player gifts stay within this account; never mutate another live player's context.
            if (recipient.Length > 0 && !(player.Account!.Characters ?? []).Any(c => string.Equals(c.Name, recipient, StringComparison.Ordinal)))
            {
                return (byte)10;
            }

            var state = SoloCashShopState.Read(player.Account!.SoloCashShopData);
            if (state.Credit < offer.Price)
            {
                return (byte)1;
            }

            if (state.Entries.Count >= SoloCashShopState.StorageCapacity || state.NextId == int.MaxValue)
            {
                return (byte)2;
            }

            state.Credit -= offer.Price;
            state.Entries.Add(new SoloCashShopState.Entry(
                state.NextId++, offer.Id, offer.ItemCode, offer.Level, offer.Durability, offer.Price,
                recipient, recipient.Length == 0 ? "" : player.SelectedCharacter!.Name, message, offer.HasSkill));
            return await CommitAsync(player, state).ConfigureAwait(false);
        });

    /// <summary>Exchanges earned gold for local credit, without a payment provider.</summary>
    public ValueTask<byte> ExchangeAsync(Player player) =>
        player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!CanUse(player))
            {
                return (byte)6;
            }

            var state = SoloCashShopState.Read(player.Account!.SoloCashShopData);
            if (state.Credit > SoloCashShopState.MaximumCredit - ExchangeCredit || player.Money < ExchangeGold)
            {
                return (byte)1;
            }

            state.Credit += ExchangeCredit;
            return await CommitAsync(player, state, goldCost: ExchangeGold).ConfigureAwait(false);
        });

    /// <summary>Delivers a stored item exactly once, or leaves it intact when the bag is full.</summary>
    public ValueTask<byte> ClaimAsync(Player player, uint storageId, uint itemId, ushort itemCode, byte productType) =>
        player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!CanUse(player))
            {
                return (byte)22;
            }

            var state = SoloCashShopState.Read(player.Account!.SoloCashShopData);
            var entry = FindOwnedEntry(player, state, storageId, itemId, productType);
            if (entry is null || entry.ItemCode != itemCode)
            {
                return (byte)1;
            }

            var definition = player.GameContext.Configuration.Items.FirstOrDefault(d => d.Group == itemCode / 512 && d.Number == itemCode % 512);
            if (definition is null || definition.IsQuestItem || definition.IsBoundToCharacter
                || definition.StorageLimitPerCharacter > 0 && player.Inventory!.Items.Count(i => i.Definition == definition) >= definition.StorageLimitPerCharacter)
            {
                return (byte)22;
            }

            var item = player.PersistenceContext.CreateNew<Item>();
            item.Definition = definition;
            item.Level = entry.Level;
            item.Durability = entry.Durability;
            item.HasSkill = entry.HasSkill;
            if (!await player.Inventory!.AddItemAsync(item).ConfigureAwait(false))
            {
                player.PersistenceContext.Detach(item);
                return (byte)21;
            }

            state.Entries.Remove(entry);
            var result = await CommitAsync(player, state, item).ConfigureAwait(false);
            if (result == 0)
            {
                await player.InvokeViewPlugInAsync<IItemAppearPlugIn>(p => p.ItemAppearAsync(item)).ConfigureAwait(false);
            }

            return result;
        });

    /// <summary>Deletes an owned unclaimed item, without refunding spent currency.</summary>
    public ValueTask<byte> DeleteAsync(Player player, uint storageId, uint itemId, byte productType) =>
        player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!CanUse(player))
            {
                return (byte)1;
            }

            var state = SoloCashShopState.Read(player.Account!.SoloCashShopData);
            var entry = FindOwnedEntry(player, state, storageId, itemId, productType);
            if (entry is null)
            {
                return (byte)1;
            }

            state.Entries.Remove(entry);
            return await CommitAsync(player, state).ConfigureAwait(false);
        });

    private static SoloCashShopState.Entry? FindOwnedEntry(Player player, SoloCashShopState state, uint storageId, uint itemId, byte productType) =>
        productType != (byte)'P' || storageId != itemId ? null
            : state.Entries.FirstOrDefault(e => e.Id == storageId
                && (e.Recipient.Length == 0 || e.Recipient == player.SelectedCharacter!.Name));

    private static async ValueTask<byte> CommitAsync(Player player, SoloCashShopState state, Item? addedItem = null, int goldCost = 0)
    {
        var previousData = player.Account!.SoloCashShopData;
        var previousGold = player.Money;
        player.Account.SoloCashShopData = state.Serialize();
        // Avoid publishing a new gold balance before the transaction is committed.
        player.SelectedCharacter!.Inventory!.Money -= goldCost;
        try
        {
            if (await player.SaveProgressAsync().ConfigureAwait(false))
            {
                return 0;
            }
        }
        catch (Exception exception)
        {
            player.Logger.LogError(exception, "Solo shop transaction could not be saved.");
        }

        player.Account.SoloCashShopData = previousData;
        player.SelectedCharacter.Inventory.Money = previousGold;
        if (addedItem is not null)
        {
            await player.Inventory!.RemoveItemAsync(addedItem).ConfigureAwait(false);
            player.PersistenceContext.Detach(addedItem);
        }

        return 255;
    }
}
