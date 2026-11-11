// <copyright file="PeriodicSaveProgressPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.PlugIns;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Saves the progress of players periodically when their status is 'EnteredWorld'.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.PeriodicSaveProgressPlugIn_Name), Description = nameof(PlugInResources.PeriodicSaveProgressPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("CEBBD5BD-B0DF-4768-816D-AF8DF78888B2")]
public class PeriodicSaveProgressPlugIn : IPeriodicTaskPlugIn, ISupportCustomConfiguration<PeriodicSaveProgressPlugInConfiguration>, ISupportDefaultCustomConfiguration
{
    private readonly ConditionalWeakTable<GameContext, ContextSaveState> _contextStates = new();
    private long _forceStartGeneration;

    /// <inheritdoc />
    public PeriodicSaveProgressPlugInConfiguration? Configuration { get; set; }

    /// <inheritdoc />
    public async ValueTask ExecuteTaskAsync(GameContext gameContext)
    {
        var contextState = this._contextStates.GetOrCreateValue(gameContext);
        var currentForceGen = Interlocked.Read(ref this._forceStartGeneration);
        var configuration = this.Configuration ??= CreateDefaultConfiguration();
        if (!contextState.TryBeginExecution(DateTime.UtcNow, configuration.Interval, currentForceGen))
        {
            return;
        }

        var logger = gameContext.LoggerFactory.CreateLogger(this.GetType().Name);
        using var scope = logger.BeginScope(gameContext);
        logger.LogDebug("Starting periodical save...");
        try
        {
            var players = await gameContext.GetPlayersAsync().ConfigureAwait(false);
            foreach (var player in players)
            {
                using var loggerScope = player.Logger.BeginScope(
                    ("Account", player.Account?.LoginName ?? string.Empty),
                    ("Character", player.SelectedCharacter?.Name ?? string.Empty));
                try
                {
                    if (player.PlayerState.CurrentState == PlayerState.EnteredWorld
                        || (player.IsEconomyPersistencePending && !player.PlayerState.CurrentState.IsDisconnectedOrFinished()))
                    {
                        await player.SaveProgressAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        player.Logger.LogDebug("Skipping saving the progress periodically for player '{player}' because the state is {state}", player, player.PlayerState.CurrentState);
                    }
                }
                catch (Exception ex)
                {
                    player.Logger.LogError(ex, "Error when saving the progress periodically for player '{player}': {ex}", player, ex);
                }
            }

            logger.LogDebug("Finished periodical save without errors.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error when saving player data periodically.");
        }
        finally
        {
            contextState.EndExecution();
        }
    }

    /// <inheritdoc />
    public void ForceStart()
    {
        Interlocked.Increment(ref this._forceStartGeneration);
    }

    /// <inheritdoc />
    public object CreateDefaultConfig()
    {
        return CreateDefaultConfiguration();
    }

    private static PeriodicSaveProgressPlugInConfiguration CreateDefaultConfiguration()
    {
        return new PeriodicSaveProgressPlugInConfiguration();
    }

    private sealed class ContextSaveState
    {
        private readonly Lock _lock = new();
        private DateTime _nextRunUtc = DateTime.UtcNow;
        private long _handledForceGeneration;
        private bool _isRunning;

        public bool TryBeginExecution(DateTime nowUtc, TimeSpan interval, long forceGeneration)
        {
            lock (this._lock)
            {
                if (this._isRunning)
                {
                    return false;
                }

                var isForced = forceGeneration > this._handledForceGeneration;
                if (!isForced && nowUtc < this._nextRunUtc)
                {
                    return false;
                }

                this._handledForceGeneration = forceGeneration;
                this._nextRunUtc = nowUtc + (interval > TimeSpan.Zero ? interval : TimeSpan.Zero);
                this._isRunning = true;
                return true;
            }
        }

        public void EndExecution()
        {
            lock (this._lock)
            {
                this._isRunning = false;
            }
        }
    }
}