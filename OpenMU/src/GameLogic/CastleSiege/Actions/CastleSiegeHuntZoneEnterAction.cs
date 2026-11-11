// <copyright file="CastleSiegeHuntZoneEnterAction.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.CastleSiege.Actions;

using MUnique.OpenMU.GameLogic.PlayerActions;

/// <summary>
/// Enters the Castle Siege hunting zone.
/// </summary>
public sealed class CastleSiegeHuntZoneEnterAction
{
    private const short GuardsmanNumber = 224;
    private readonly CastleSiegeTaxProvider _taxProvider = new();

    /// <summary>
    /// Tries to charge the configured entry fee and warp the player to the Land of Trials.
    /// </summary>
    /// <param name="player">The entering player.</param>
    /// <param name="context">The Castle Siege context.</param>
    /// <returns><see langword="true"/> when the player entered the map.</returns>
    public async ValueTask<bool> EnterAsync(Player player, CastleSiegeContext? context)
    {
        if (context is not { Configuration.Enabled: true }
            || !player.IsAlive
            || (player.PlayerState.CurrentState != PlayerState.NpcDialogOpened && player.PlayerState.CurrentState != PlayerState.EnteredWorld)
            || player.OpenedNpc?.Definition.Number != GuardsmanNumber
            || player.CurrentMap?.Definition.Number != context.Configuration.CastleSiegeMapDefinition?.Number
            || context.Configuration.LandOfTrialsMapDefinition is not { } targetMap
            || targetMap.ExitGates.FirstOrDefault() is not { } targetGate)
        {
            return false;
        }

        if (targetMap.TryGetRequirementError(player, out var errorMessage))
        {
            await player.ShowBlueMessageAsync(errorMessage).ConfigureAwait(false);
            return false;
        }

        var (paid, paidFee) = await this._taxProvider.TryPayHuntEntryFeeWithAmountAsync(player, context).ConfigureAwait(false);
        if (!paid)
        {
            var fee = await this._taxProvider.GetHuntEntryFeeAsync(player, context).ConfigureAwait(false);
            if (fee > player.Money)
            {
                await player.ShowLocalizedBlueMessageAsync(
                        nameof(PlayerMessage.NotEnoughMoneyToEnter),
                        targetMap.Name.GetTranslation(player.Culture))
                    .ConfigureAwait(false);
            }

            return false;
        }

        var previousOpenedNpc = player.OpenedNpc;
        player.OpenedNpc = null;
        var warped = false;
        try
        {
            warped = await player.TryWarpToAsync(targetGate).ConfigureAwait(false);
            return warped;
        }
        finally
        {
            if (!warped)
            {
                if (player.PlayerState.CurrentState == PlayerState.NpcDialogOpened && player.OpenedNpc is null)
                {
                    player.OpenedNpc = previousOpenedNpc;
                }

                await this._taxProvider.RevertHuntEntryFeeAsync(player, context, paidFee).ConfigureAwait(false);
            }
        }
    }
}
