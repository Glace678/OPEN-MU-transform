// <copyright file="OpenStoreAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlayerActions.PlayerStore;

using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.GameLogic.Views.PlayerShop;

/// <summary>
/// Action to open a player store.
/// </summary>
public class OpenStoreAction
{
    /// <summary>
    /// Opens the player store.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="storeName">Name of the store.</param>
    public async ValueTask OpenStoreAsync(Player player, string storeName)
    {
        using var loggerScope = player.Logger.BeginScope(this.GetType());
        var character = player.SelectedCharacter;
        if (character is null || player.ShopStorage is not { } shopStorage)
        {
            player.Logger.LogWarning("OpenStore request failed: No character or shop storage available.");
            return;
        }

        if (player.PlayerState.CurrentState != PlayerState.EnteredWorld || !player.IsAlive)
        {
            player.Logger.LogWarning("OpenStore request failed: Player [{0}] is not alive in EnteredWorld state.", character.Name);
            return;
        }

        using (await shopStorage.StoreLock.LockAsync())
        {
            if (player.SelectedCharacter != character
                || player.PlayerState.CurrentState != PlayerState.EnteredWorld
                || !player.IsAlive)
            {
                return;
            }

            if (!shopStorage.Items.Any() || shopStorage.Items.Any(i => !i.StorePrice.HasValue || i.StorePrice.Value < 0))
            {
                player.Logger.LogWarning("OpenStore request failed: Store is empty or not all store items have a valid price assigned. Player: [{0}], StoreName: [{1}]", character.Name, character.StoreName);
                return;
            }

            if (shopStorage.Items.Any(i => i.ItemOptions.Any(o => o.ItemOption?.OptionType == ItemOptionTypes.HarmonyOption)))
            {
                player.Logger.LogWarning("OpenStore request failed: Items with an harmony option can't be traded. Player: [{0}], StoreName: [{1}]", character.Name, character.StoreName);
                return;
            }

            character.StoreName = storeName;
            shopStorage.StoreOpen = true;
        }

        player.Logger.LogDebug("OpenStore: Player: [{0}], StoreName: [{1}]", character.Name, character.StoreName);
        await player.ForEachWorldObserverAsync<IPlayerShopOpenedPlugIn>(p => p.PlayerShopOpenedAsync(player), true).ConfigureAwait(false);
    }

    /// <summary>
    /// Restores the store's open state after the player entered the game.
    /// </summary>
    /// <param name="player">The player.</param>
    /// <param name="isStoreOpeningAfterEnterSupported">
    /// Whether re-opening the store to other players after entering the game is supported by the player's client.
    /// </param>
    /// <remarks>
    /// If the store was left open before logging out, its name is set, and the client supports it,
    /// the store is properly re-opened and announced to nearby players (and the owner) via
    /// <see cref="OpenStoreAsync"/>. Otherwise, the store is (re-)marked as closed for everyone,
    /// including the owner - it must never appear open only to the owner while staying closed to
    /// everyone else.
    /// </remarks>
    public async ValueTask RestoreAfterEnterWorldAsync(Player player, bool isStoreOpeningAfterEnterSupported)
    {
        if (player.SelectedCharacter is not { } character)
        {
            return;
        }

        if (character.IsStoreOpened
            && !string.IsNullOrWhiteSpace(character.StoreName)
            && isStoreOpeningAfterEnterSupported)
        {
            character.IsStoreOpened = false;
            await this.OpenStoreAsync(player, character.StoreName).ConfigureAwait(false);
        }
        else
        {
            character.IsStoreOpened = false;
        }
    }
}