// <copyright file="MuHelper.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.MuHelper;

using System.Diagnostics;
using System.Threading;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns.ChatCommands;
using MUnique.OpenMU.GameLogic.Views.MuHelper;
using Nito.AsyncEx;

/// <summary>
/// Implements the logic of the 'MU Helper'.
/// </summary>
public class MuHelper : AsyncDisposable
{
    /// <summary>
    /// The <see cref="IElement"/> which is added to the players <see cref="IAttributeSystem"/>
    /// when the MU Helper is active.
    /// </summary>
    private static readonly IElement ActiveElement = new ConstantElement(1);

    /// <summary>
    /// Associated player.
    /// </summary>
    private readonly Player _player;

    /// <summary>
    /// The current configuration.
    /// </summary>
    private readonly MuHelperConfiguration _configuration;

    private readonly AsyncLock _lifecycleLock = new();

    private CancellationTokenSource? _stopCts;
    private Task? _runTask;
    private DateTime _startTimestamp;

    /// <summary>
    /// Initializes a new instance of the <see cref="MuHelper"/> class.
    /// </summary>
    /// <param name="player">current player.</param>
    public MuHelper(Player player)
    {
        this._player = player;
        this._configuration = this._player.GameContext.FeaturePlugIns.GetPlugIn<MuHelperFeaturePlugIn>()?.Configuration ?? new MuHelperConfiguration();
    }

    /// <summary>
    /// Start Mu Helper.
    /// </summary>
    public async ValueTask<bool> TryStartAsync()
    {
        using var l = await this._lifecycleLock.LockAsync().ConfigureAwait(false);
        if (this.IsDisposed || this.IsDisposing)
        {
            return false;
        }

        if (this._runTask is not null)
        {
            await this._player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MuHelperAlreadyRunning)).ConfigureAwait(false);
            return false;
        }

        if (this._player.Level < this._configuration.MinLevel)
        {
            await this._player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MuHelperMinimumLevel), this._configuration.MinLevel).ConfigureAwait(false);
            return false;
        }

        if (this._player.Level > this._configuration.MaxLevel)
        {
            await this._player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MuHelperMaximumLevel), this._configuration.MaxLevel).ConfigureAwait(false);
            return false;
        }

        this._startTimestamp = DateTime.UtcNow;
        var requiredMoney = this.CalculateRequiredMoney();

        if (requiredMoney > 0 && !this._player.TryRemoveMoney(requiredMoney))
        {
            await this._player.ShowLocalizedBlueMessageAsync(nameof(PlayerMessage.MuHelperRequiresMoney), requiredMoney).ConfigureAwait(false);
            return false;
        }

        try
        {
            await this._player.InvokeViewPlugInAsync<IMuHelperStatusUpdatePlugIn>(p => p.StartAsync()).ConfigureAwait(false);
            if (requiredMoney > 0)
            {
                await this._player.InvokeViewPlugInAsync<IMuHelperStatusUpdatePlugIn>(p => p.ConsumeMoneyAsync((uint)requiredMoney)).ConfigureAwait(false);
            }

            this._player.Attributes?.AddElement(ActiveElement, Stats.IsMuHelperActive);
            this._stopCts?.Dispose();
            this._stopCts = new CancellationTokenSource();
            var cts = this._stopCts.Token;
            this._runTask = this.RunLoopAsync(cts);
            return true;
        }
        catch
        {
            if (requiredMoney > 0)
            {
                this._player.TryAddMoney(requiredMoney);
            }

            throw;
        }
    }

    /// <summary>
    /// Stops the MU Helper.
    /// </summary>
    public async ValueTask StopAsync()
    {
        Task? runTask;
        CancellationTokenSource? stopCts;
        using (await this._lifecycleLock.LockAsync().ConfigureAwait(false))
        {
            runTask = this._runTask;
            stopCts = this._stopCts;
            if (runTask is null || stopCts is null)
            {
                return;
            }

            this._runTask = null;
            this._stopCts = null;
            this._player.Attributes?.RemoveElement(ActiveElement, Stats.IsMuHelperActive);
        }

        try
        {
            await stopCts.CancelAsync().ConfigureAwait(false);
            await runTask.ConfigureAwait(false);
            stopCts.Dispose();

            await this._player.InvokeViewPlugInAsync<IMuHelperStatusUpdatePlugIn>(p => p.StopAsync()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._player.Logger.LogWarning(ex, "Exception during stopping the mu helper for {CharacterName}: {Error}", this._player.Name, ex.Message);
        }
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await this.StopAsync().ConfigureAwait(false);
    }

    private int CalculateRequiredMoney() =>
        MuHelperZenCostCalculator.Calculate(this._player, this._configuration, this._startTimestamp);

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        var stoppedDueToInsufficientMoney = false;
        try
        {
            if (this._configuration.PayInterval <= TimeSpan.Zero)
            {
                this._player.Logger.LogDebug("MU Helper PayInterval is {PayInterval}. Stopping for {CharacterName}.", this._configuration.PayInterval, this._player.Name);
                return;
            }

            using var timer = new PeriodicTimer(this._configuration.PayInterval);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
                if (!await this.CollectAsync().ConfigureAwait(false))
                {
                    stoppedDueToInsufficientMoney = true;
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // expected when StopAsync cancels the token.
        }
        catch (Exception ex)
        {
            Debug.Fail(ex.Message, ex.StackTrace);
        }

        if (stoppedDueToInsufficientMoney)
        {
            await this.CleanupFromLoopStopAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs the money collection.
    /// </summary>
    private async ValueTask<bool> CollectAsync()
    {
        var amount = this.CalculateRequiredMoney();
        if (amount <= 0)
        {
            return true;
        }

        if (this._player.TryRemoveMoney(amount))
        {
            await this._player.InvokeViewPlugInAsync<IMuHelperStatusUpdatePlugIn>(p => p.ConsumeMoneyAsync((uint)amount)).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private async ValueTask CleanupFromLoopStopAsync()
    {
        CancellationTokenSource? stopCts;
        using (await this._lifecycleLock.LockAsync().ConfigureAwait(false))
        {
            if (this._runTask is null)
            {
                return;
            }

            stopCts = this._stopCts;
            this._runTask = null;
            this._stopCts = null;
            this._player.Attributes?.RemoveElement(ActiveElement, Stats.IsMuHelperActive);
        }

        try
        {
            stopCts?.Dispose();
            await this._player.InvokeViewPlugInAsync<IMuHelperStatusUpdatePlugIn>(p => p.StopAsync()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._player.Logger.LogWarning(ex, "Exception during stopping the mu helper for {CharacterName}: {Error}", this._player.Name, ex.Message);
        }
    }
}