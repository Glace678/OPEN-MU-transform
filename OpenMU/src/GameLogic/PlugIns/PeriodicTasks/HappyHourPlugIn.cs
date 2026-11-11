// <copyright file="HappyHourPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// This plugin enables Happy Hour feature.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.HappyHourPlugIn_Name), Description = nameof(PlugInResources.HappyHourPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("6542E452-9780-45B8-85AE-4036422E9A6E")]
public class HappyHourPlugIn : PeriodicTaskBasePlugIn<HappyHourConfiguration, PeriodicTaskGameServerState>, ISupportDefaultCustomConfiguration, IPlayerStateChangedPlugIn
{
    /// <summary>
    /// Per-context Happy Hour multiplier elements added to players' <see cref="Stats.ExperienceRate"/>
    /// and <see cref="Stats.MasterExperienceRate"/>.
    /// </summary>
    private readonly ConditionalWeakTable<IGameContext, SimpleElement> _multipliersByContext = new();

    /// <inheritdoc />
    public object CreateDefaultConfig() => HappyHourConfiguration.Default;

    /// <inheritdoc />
    public async ValueTask PlayerStateChangedAsync(Player player, State previousState, State currentState)
    {
        try
        {
            var multiplier = this.GetMultiplier(player.GameContext);
            if (currentState != PlayerState.EnteredWorld)
            {
                player.Attributes?.RemoveElement(multiplier, Stats.ExperienceRate);
                player.Attributes?.RemoveElement(multiplier, Stats.MasterExperienceRate);
            }

            if (previousState != PlayerState.CharacterSelection || currentState != PlayerState.EnteredWorld)
            {
                return;
            }

            var state = this.GetStateByGameContext(player.GameContext);
            multiplier.Value = state.State == PeriodicTaskState.Started
                ? (this.Configuration?.ExperienceMultiplier ?? 1.0f)
                : 1.0f;

            player.Attributes?.RemoveElement(multiplier, Stats.ExperienceRate);
            player.Attributes?.RemoveElement(multiplier, Stats.MasterExperienceRate);

            player.Attributes?.AddElement(multiplier, Stats.ExperienceRate);
            player.Attributes?.AddElement(multiplier, Stats.MasterExperienceRate);
            var isEnabled = state.State != PeriodicTaskState.NotStarted;
            if (!isEnabled)
            {
                return;
            }

            await this.TrySendStartMessageAsync(player).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogDebug(ex, "Unexpected error handling player state change.");
        }
    }

    /// <inheritdoc/>
    protected override PeriodicTaskGameServerState CreateState(IGameContext gameContext)
    {
        return new PeriodicTaskGameServerState(gameContext);
    }

    /// <inheritdoc/>
    protected override ValueTask OnPrepareEventAsync(PeriodicTaskGameServerState state)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override async ValueTask OnPreparedAsync(PeriodicTaskGameServerState state)
    {
        await state.Context.ForEachPlayerAsync(this.TrySendStartMessageAsync).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override ValueTask OnStartedAsync(PeriodicTaskGameServerState state)
    {
        this.GetMultiplier(state.Context).Value = this.Configuration?.ExperienceMultiplier ?? 1.0f;

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override ValueTask OnFinishedAsync(PeriodicTaskGameServerState state)
    {
        this.GetMultiplier(state.Context).Value = 1.0f;

        return ValueTask.CompletedTask;
    }

    private SimpleElement GetMultiplier(IGameContext gameContext)
    {
        return this._multipliersByContext.GetValue(gameContext, static _ => new SimpleElement(1.0f, AggregateType.Multiplicate));
    }

    /// <summary>
    /// Returns true if the player stays on the map.
    /// </summary>
    /// <param name="player">The player.</param>
    protected bool IsPlayerOnMap(Player player)
    {
        return player.CurrentMap is not null
               && !player.PlayerState.CurrentState.IsDisconnectedOrFinished();
    }

    /// <summary>
    /// Send a golden message to player's client.
    /// </summary>
    /// <param name="player">The player.</param>
    protected async Task TrySendStartMessageAsync(Player player)
    {
        var configuration = this.Configuration;

        if (configuration is null)
        {
            return;
        }

        if (!this.IsPlayerOnMap(player))
        {
            return;
        }

        try
        {
            var message = configuration.StartMessage.GetTranslation(player.Culture) is { Length: > 0 } translation ? translation : player.GetLocalizedMessage(nameof(PlayerMessage.HappyHourEventHasBeenStarted));
            await player.InvokeViewPlugInAsync<IShowMessagePlugIn>(p => p.ShowMessageAsync(message, Interfaces.MessageType.GoldenCenter)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            player.Logger.LogDebug(ex, "Unexpected error sending start message.");
        }
    }
}
