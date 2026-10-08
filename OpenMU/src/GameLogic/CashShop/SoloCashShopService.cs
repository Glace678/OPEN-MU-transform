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
    /// <param name="player">The player attempting the transaction.</param>
    /// <returns><c>true</c> when the player is allowed to use the shop; otherwise, <c>false</c>.</returns>
    public static bool CanUse(Player player) =>
        SoloBalance.IsEnabled(player.GameContext.Configuration)
        && player.Account is not null && !player.IsTemplatePlayer
        && player.SelectedCharacter is not null && player.Inventory is not null && player.CurrentMap is not null
        && player.IsAlive && player.PlayerState.CurrentState == PlayerState.EnteredWorld
        && player.IsAtSafezone();

    /// <summary>Initializes the shop data durably before reporting it to the client.</summary>
    /// <param name="player">The player opening the shop.</param>
    /// <returns>The shop result code.</returns>
    public ValueTask<byte> InitializeAsync(Player player) =>
        player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!CanUse(player))
            {
                return SoloCashShopResult.InvalidPlayerState;
            }

            var state = SoloCashShopState.Read(player.Account!.SoloCashShopData);
            return string.IsNullOrEmpty(player.Account.SoloCashShopData)
                ? await CommitAsync(player, state).ConfigureAwait(false) : SoloCashShopResult.Success;
        });

    /// <summary>Buys an exact, validated catalog offer into account storage.</summary>
    /// <param name="player">The player making the purchase.</param>
    /// <param name="offerId">The requested offer identifier.</param>
    /// <param name="category">The requested offer category.</param>
    /// <param name="priceId">The client price identifier; must be 0 or match <paramref name="offerId"/>.</param>
    /// <param name="itemCode">The requested item code.</param>
    /// <param name="coin">The currency the client claims to pay with.</param>
    /// <param name="mileage">The mileage the client claims to spend.</param>
    /// <param name="recipient">An optional character name on this account who receives the item.</param>
    /// <param name="message">An optional gift message.</param>
    /// <returns>The shop result code.</returns>
    public ValueTask<byte> BuyAsync(Player player, uint offerId, uint category, uint priceId, ushort itemCode, uint coin, byte mileage, string recipient = "", string message = "") =>
        player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!CanUse(player))
            {
                return SoloCashShopResult.InvalidPlayerState;
            }

            var offer = SoloCashShopCatalog.GetOffers(player.GameContext.Configuration).FirstOrDefault(o => o.Id == offerId);
            if (offer is null
                || offer.Category != category
                || offer.ItemCode != itemCode
                || (priceId != 0 && priceId != offer.Id))
            {
                return SoloCashShopResult.OfferNotFound;
            }

            if (coin != SoloCashShopCatalog.CoinIndex || mileage != 0)
            {
                return SoloCashShopResult.WrongCurrency;
            }

            // Single-player gifts stay within this account; never mutate another live player's context.
            if (recipient.Length > 0 && !(player.Account!.Characters ?? []).Any(c => string.Equals(c.Name, recipient, StringComparison.Ordinal)))
            {
                return SoloCashShopResult.UnknownRecipient;
            }

            var state = SoloCashShopState.Read(player.Account!.SoloCashShopData);
            if (state.Credit < offer.Price)
            {
                return SoloCashShopResult.GenericFailure;
            }

            if (state.Entries.Count >= SoloCashShopState.StorageCapacity || state.NextId == int.MaxValue)
            {
                return SoloCashShopResult.StorageFull;
            }

            state.Credit -= offer.Price;
            state.Entries.Add(new SoloCashShopState.Entry(
                state.NextId++,
                offer.Id,
                offer.ItemCode,
                offer.Level,
                offer.Durability,
                offer.Price,
                recipient,
                recipient.Length == 0 ? string.Empty : player.SelectedCharacter!.Name,
                message,
                offer.HasSkill));
            return await CommitAsync(player, state).ConfigureAwait(false);
        });

    /// <summary>Exchanges earned gold for local credit, without a payment provider.</summary>
    /// <param name="player">The player exchanging gold for credit.</param>
    /// <returns>The shop result code.</returns>
    public ValueTask<byte> ExchangeAsync(Player player) =>
        player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!CanUse(player))
            {
                return SoloCashShopResult.InvalidPlayerState;
            }

            var state = SoloCashShopState.Read(player.Account!.SoloCashShopData);
            if (state.Credit > SoloCashShopState.MaximumCredit - ExchangeCredit || player.Money < ExchangeGold)
            {
                return SoloCashShopResult.GenericFailure;
            }

            state.Credit += ExchangeCredit;
            return await CommitAsync(player, state, goldCost: ExchangeGold).ConfigureAwait(false);
        });

    /// <summary>Delivers a stored item exactly once, or leaves it intact when the bag is full.</summary>
    /// <param name="player">The player claiming the stored item.</param>
    /// <param name="storageId">The stored-item identifier.</param>
    /// <param name="itemId">The item identifier sent by the client; must match the stored entry.</param>
    /// <param name="itemCode">The item code sent by the client.</param>
    /// <param name="productType">The product type code sent by the client.</param>
    /// <returns>The shop result code.</returns>
    public ValueTask<byte> ClaimAsync(Player player, uint storageId, uint itemId, ushort itemCode, byte productType) =>
        player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!CanUse(player))
            {
                return SoloCashShopResult.Unavailable;
            }

            var state = SoloCashShopState.Read(player.Account!.SoloCashShopData);
            var entry = FindOwnedEntry(player, state, storageId, itemId, productType);
            if (entry is null || entry.ItemCode != itemCode)
            {
                return SoloCashShopResult.GenericFailure;
            }

            var definition = player.GameContext.Configuration.Items.FirstOrDefault(
                d => d.Group == itemCode / SoloCashShopCatalog.ItemCodesPerGroup
                     && d.Number == itemCode % SoloCashShopCatalog.ItemCodesPerGroup);
            if (definition is null
                || definition.IsQuestItem
                || definition.IsBoundToCharacter
                || (definition.StorageLimitPerCharacter > 0
                    && player.Inventory!.Items.Count(i => i.Definition == definition) >= definition.StorageLimitPerCharacter))
            {
                return SoloCashShopResult.Unavailable;
            }

            var item = player.PersistenceContext.CreateNew<Item>();
            item.Definition = definition;
            item.Level = entry.Level;
            item.Durability = entry.Durability;
            item.HasSkill = entry.HasSkill;
            if (!await player.Inventory!.AddItemAsync(item).ConfigureAwait(false))
            {
                player.PersistenceContext.Detach(item);
                return SoloCashShopResult.InventoryFull;
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
    /// <param name="player">The player deleting the stored item.</param>
    /// <param name="storageId">The stored-item identifier.</param>
    /// <param name="itemId">The item identifier sent by the client.</param>
    /// <param name="productType">The product type code sent by the client.</param>
    /// <returns>The shop result code.</returns>
    public ValueTask<byte> DeleteAsync(Player player, uint storageId, uint itemId, byte productType) =>
        player.RunPersistenceExclusiveAsync(async () =>
        {
            if (!CanUse(player))
            {
                return SoloCashShopResult.GenericFailure;
            }

            var state = SoloCashShopState.Read(player.Account!.SoloCashShopData);
            var entry = FindOwnedEntry(player, state, storageId, itemId, productType);
            if (entry is null)
            {
                return SoloCashShopResult.GenericFailure;
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
        // The deduction below is a raw subtraction inside an explicit rollback.
        // Guard affordability first so an underfunded purchase can't drive Money
        // negative (which TryRemoveMoney would otherwise have rejected).
        if (goldCost > 0 && player.Money < goldCost)
        {
            return SoloCashShopResult.GenericFailure;
        }

        var previousData = player.Account!.SoloCashShopData;
        var previousGold = player.Money;
        player.Account.SoloCashShopData = state.Serialize();

        // Avoid publishing a new gold balance before the transaction is committed.
        player.SelectedCharacter!.Inventory!.Money -= goldCost;
        try
        {
            if (await player.SaveProgressAsync().ConfigureAwait(false))
            {
                // The deduction wrote Inventory.Money directly (it bypasses Player.Money setter,
                // which is what publishes the balance), so refresh the client view explicitly.
                if (goldCost > 0)
                {
                    await player.InvokeViewPlugInAsync<MUnique.OpenMU.GameLogic.Views.Inventory.IUpdateMoneyPlugIn>(
                        p => p.UpdateMoneyAsync()).ConfigureAwait(false);
                }

                return SoloCashShopResult.Success;
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

        return SoloCashShopResult.InternalError;
    }
}
