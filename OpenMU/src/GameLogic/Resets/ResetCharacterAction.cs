// <copyright file="ResetCharacterAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.Resets;

using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions;
using MUnique.OpenMU.GameLogic.Views.Character;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameLogic.Views.Login;
using MUnique.OpenMU.GameLogic.Views.NPC;

/// <summary>
/// Action to reset a character.
/// </summary>
public class ResetCharacterAction
{
    private readonly Player _player;
    private readonly NonPlayerCharacter? _npc;
    private readonly LogoutAction _logoutAction = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetCharacterAction"/> class.
    /// </summary>
    /// <param name="player">Player to reset.</param>
    /// <param name="npc">NPC which the player talks to to initiate the reset action.</param>
    public ResetCharacterAction(Player player, NonPlayerCharacter? npc = null)
    {
        this._player = player;
        this._npc = npc;
    }

    /// <summary>
    /// Reset specific character.
    /// </summary>
    public async ValueTask ResetCharacterAsync()
    {
        ResetConfiguration? completedConfiguration = null;
        await this._player.RunPersistenceExclusiveAsync(async () =>
        {
            var resetFeature = this._player.GameContext.FeaturePlugIns.GetPlugIn<ResetFeaturePlugIn>();
            if (resetFeature is null)
            {
                await this.ShowMessageAsync(nameof(PlayerMessage.ResetNotEnabled)).ConfigureAwait(false);
                return;
            }

            if (this._player.PlayerState.CurrentState != PlayerState.EnteredWorld && this._npc is null)
            {
                await this.ShowMessageAsync(nameof(PlayerMessage.CantResetWithOpenedWindows)).ConfigureAwait(false);
                return;
            }

            if (this._player.Attributes is null || this._player.SelectedCharacter is null)
            {
                await this.ShowMessageAsync(nameof(PlayerMessage.NotEnteredTheGame)).ConfigureAwait(false);
                return;
            }

            var configuration = resetFeature.Configuration;
            if (configuration is null)
            {
                await this.ShowMessageAsync(nameof(PlayerMessage.ResetNotConfigured)).ConfigureAwait(false);
                return;
            }

            var resetProgression = ResetProgressionCalculator.Calculate(this.GetResetCount(), (int)this._player.Attributes[Stats.PointsPerReset], configuration);

            if (this._player.Level < configuration.RequiredLevel)
            {
                await this.ShowMessageAsync(nameof(PlayerMessage.RequiredLevelForReset), configuration.RequiredLevel).ConfigureAwait(false);
                return;
            }

            if (configuration.ResetLimit > 0 && resetProgression.NextResetCount > configuration.ResetLimit)
            {
                await this.ShowMessageAsync(nameof(PlayerMessage.MaximumResetsReached), configuration.ResetLimit).ConfigureAwait(false);
                return;
            }

            if (!await this.TryConsumeResetCostsAsync(configuration, resetProgression).ConfigureAwait(false))
            {
                return;
            }

            this._player.Attributes[Stats.Resets] = resetProgression.NextResetCount;
            this._player.Attributes[Stats.Level] = configuration.LevelAfterReset;
            this._player.SelectedCharacter.Experience = 0;
            this.UpdateStats(configuration, resetProgression);
            if (configuration.MoveHome)
            {
                await this.MoveHomeAsync(configuration).ConfigureAwait(false);
            }

            completedConfiguration = configuration;
        }).ConfigureAwait(false);

        if (completedConfiguration is null)
        {
            return;
        }

        if (completedConfiguration.LogOut)
        {
            await this._logoutAction.LogoutAsync(this._player, LogoutType.BackToCharacterSelection).ConfigureAwait(false);
        }
        else
        {
            await this.UpdateClientStatsAsync(completedConfiguration).ConfigureAwait(false);
        }
    }

    private async ValueTask ShowMessageAsync(string messageKey, params object?[] args)
    {
        var message = this._player.GetLocalizedMessage(messageKey, args);

        if (this._npc is null)
        {
            await this._player.ShowBlueMessageAsync(message).ConfigureAwait(false);
            return;
        }

        await this._player.InvokeViewPlugInAsync<IShowMessageOfObjectPlugIn>(p => p.ShowMessageOfObjectAsync(message, this._npc)).ConfigureAwait(false);
    }

    private int GetResetCount()
    {
        return (int)this._player.Attributes![Stats.Resets];
    }

    private static int GetStackUnitCount(Item item)
    {
        return item.IsStackable() ? Math.Max(0, (int)item.Durability) : 1;
    }

    private async ValueTask<bool> TryConsumeResetCostsAsync(ResetConfiguration configuration, ResetProgression resetProgression)
    {
        var requiredItems = await this.GetRequiredItemsToConsumeAsync(configuration, resetProgression.RequiredItemAmount).ConfigureAwait(false);
        if (requiredItems is null)
        {
            return false;
        }

        if (this._player.Money < resetProgression.RequiredZen)
        {
            await this.ShowMessageAsync(nameof(PlayerMessage.NotEnoughMoneyForReset), resetProgression.RequiredZen).ConfigureAwait(false);
            return false;
        }

        // Consume the items first and the money last, so a failure while removing an item can never
        // leave the player with the price paid but the items still missing. If anything below fails,
        // the already removed items are restored before the exception or the failure result is reported.
        var consumedItems = new List<Item>();
        try
        {
            var remainingToConsume = resetProgression.RequiredItemAmount;
            foreach (var item in requiredItems)
            {
                if (remainingToConsume <= 0)
                {
                    break;
                }

                var itemUnits = GetStackUnitCount(item);
                if (item.IsStackable() && itemUnits > remainingToConsume)
                {
                    item.Durability -= (byte)remainingToConsume;
                    remainingToConsume = 0;
                    await this._player.InvokeViewPlugInAsync<IItemDurabilityChangedPlugIn>(p => p.ItemDurabilityChangedAsync(item, false)).ConfigureAwait(false);
                }
                else
                {
                    var snapshot = this._player.PersistenceContext.CreateNew<Item>();
                    snapshot.AssignValues(item);
                    snapshot.ItemSlot = item.ItemSlot;
                    consumedItems.Add(snapshot);
                    remainingToConsume -= itemUnits;
                    await this._player.DestroyInventoryItemAsync(item).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            await this.RestoreConsumedItemsAsync(consumedItems).ConfigureAwait(false);
            throw;
        }

        if (resetProgression.RequiredZen > 0
            && (!this._player.TryRemoveMoney(resetProgression.RequiredZen)
                || this._player.Money < 0))
        {
            await this.RestoreConsumedItemsAsync(consumedItems).ConfigureAwait(false);
            await this.ShowMessageAsync(nameof(PlayerMessage.NotEnoughMoneyForReset), resetProgression.RequiredZen).ConfigureAwait(false);
            return false;
        }

        return true;
    }

    private async ValueTask RestoreConsumedItemsAsync(IEnumerable<Item> consumedItems)
    {
        foreach (var consumedItem in consumedItems)
        {
            var restoredItem = this._player.PersistenceContext.CreateNew<Item>();
            restoredItem.AssignValues(consumedItem);
            if (this._player.Inventory is { } inventory
                && await inventory.AddItemAsync(consumedItem.ItemSlot, restoredItem).ConfigureAwait(false))
            {
                continue;
            }

            this._player.Logger.LogError(
                "Reset rollback could not restore item {Definition} (level {Level}, durability {Durability}) in slot {Slot}.",
                consumedItem.Definition?.Name,
                consumedItem.Level,
                consumedItem.Durability,
                consumedItem.ItemSlot);
        }
    }

    private async ValueTask<IList<Item>?> GetRequiredItemsToConsumeAsync(ResetConfiguration configuration, int requiredItemAmount)
    {
        if (requiredItemAmount <= 0 || configuration.RequiredResetItem is null)
        {
            return [];
        }

        if (this._player.Inventory is null)
        {
            return null;
        }

        var requiredDefinition = configuration.RequiredResetItem;
        var candidateItems = this._player.Inventory.Items
            .Where(item => item.ItemSlot > InventoryConstants.LastEquippableItemSlotIndex
                           && item.Definition is { } definition
                           && definition.Group == requiredDefinition.Group
                           && definition.Number == requiredDefinition.Number
                           && GetStackUnitCount(item) > 0)
            .ToList();
        var availableAmount = candidateItems.Sum(GetStackUnitCount);
        if (availableAmount < requiredItemAmount)
        {
            await this.ShowMessageAsync(
                nameof(PlayerMessage.NotEnoughItemsForReset),
                requiredItemAmount,
                configuration.RequiredResetItem.Name).ConfigureAwait(false);
            return null;
        }

        return candidateItems;
    }

    private void UpdateStats(ResetConfiguration configuration, ResetProgression resetProgression)
    {
        if (configuration.ResetStats)
        {
            this._player.SelectedCharacter!.CharacterClass!.StatAttributes
                .Where(s => s.IncreasableByPlayer)
                .ForEach(s => this._player.Attributes![s.Attribute] = s.BaseValue);
        }

        if (configuration.ReplacePointsPerReset)
        {
            this._player.SelectedCharacter!.LevelUpPoints = resetProgression.TotalPointsAfterReset;
        }
        else
        {
            this._player.SelectedCharacter!.LevelUpPoints = (int)Math.Min(
                int.MaxValue,
                (long)this._player.SelectedCharacter!.LevelUpPoints + resetProgression.PointsForReset);
        }
    }

    private async ValueTask MoveHomeAsync(ResetConfiguration configuration)
    {
        var homeMapDef = this._player.SelectedCharacter!.CharacterClass!.HomeMap;
        if (homeMapDef is { }
            && await this._player.GameContext.GetMapAsync((ushort)homeMapDef.Number).ConfigureAwait(false) is { SafeZoneSpawnGate: { } spawnGate })
        {
            if (!configuration.LogOut)
            {
                await this._player.WarpToAsync(spawnGate).ConfigureAwait(false);
                return;
            }

            var spawnPoint = spawnGate.GetRandomPoint();
            this._player.SelectedCharacter.PositionX = spawnPoint.X;
            this._player.SelectedCharacter.PositionY = spawnPoint.Y;
            this._player.SelectedCharacter.CurrentMap = spawnGate.Map;
            this._player.Rotation = spawnGate.Direction;
        }
    }

    private async ValueTask UpdateClientStatsAsync(ResetConfiguration configuration)
    {
        if (configuration.ResetStats)
        {
            await this._player.InvokeViewPlugInAsync<IUpdateCharacterBaseStatsPlugIn>(p => p.UpdateCharacterBaseStatsAsync()).ConfigureAwait(false);
        }

        await this._player.InvokeViewPlugInAsync<IUpdateLevelPlugIn>(p => p.UpdateLevelAsync()).ConfigureAwait(false);
    }
}
