// <copyright file="TimeoutBasedGameServerRegistry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Dapr.Common;

using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using System.Threading;

/// <summary>
/// A base class for registries which keep track of game servers based on the time of their last registration update.
/// </summary>
public abstract class TimeoutBasedGameServerRegistry : IDisposable
{
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly ILogger _logger;
    private readonly Dictionary<ushort, DateTime> _entries = new();
    private readonly AsyncLock _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutBasedGameServerRegistry"/> class.
    /// </summary>
    /// <param name="timeout">The time after which a server is removed if it didn't update its registration.</param>
    /// <param name="logger">The logger.</param>
    protected TimeoutBasedGameServerRegistry(TimeSpan timeout, ILogger logger)
    {
        this.Timeout = timeout;
        this._logger = logger;

        async Task RunCleanupLoopAsync()
        {
            try
            {
                await this.CleanupLoopAsync(this._disposeCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Error in cleanup loop");
            }
        }

        _ = RunCleanupLoopAsync();
    }

    /// <summary>
    /// Gets the time after which a server is removed if it didn't update its registration.
    /// </summary>
    protected TimeSpan Timeout { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        this._disposeCts.Cancel();
        this._disposeCts.Dispose();
    }

    /// <summary>
    /// Updates the timestamp of the specified game server.
    /// </summary>
    /// <param name="gameServerId">The game server identifier.</param>
    /// <returns><c>true</c> if the game server was newly added to the registry; otherwise, <c>false</c>.</returns>
    protected async Task<bool> UpdateTimestampAsync(ushort gameServerId)
    {
        using var l = await this._lock.LockAsync().ConfigureAwait(false);
        var isNew = !this._entries.ContainsKey(gameServerId);
        this._entries[gameServerId] = DateTime.UtcNow;
        return isNew;
    }

    /// <summary>
    /// Called when a game server timed out and is removed from the registry.
    /// </summary>
    /// <param name="gameServerId">The game server identifier.</param>
    protected abstract void OnGameServerTimedOut(ushort gameServerId);

    private async Task CleanupLoopAsync(CancellationToken cancellationToken)
    {
        while (!this._disposeCts.IsCancellationRequested)
        {
            try
            {
                await this.CleanupOnceAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failure in one cleanup pass (incl. the timeout notification) must not kill the
                // loop permanently; keep running so later heartbeats are still reaped.
                this._logger.LogError(ex, "Error during game server cleanup pass; continuing.");
            }
        }
    }

    private async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(2000, cancellationToken).ConfigureAwait(false);

        // Phase 1: snapshot the currently timed-out servers under the lock.
        List<ushort> candidates;
        using (await this._lock.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            candidates = new List<ushort>();
            foreach (var pair in this._entries)
            {
                if (DateTime.UtcNow - pair.Value > this.Timeout)
                {
                    candidates.Add(pair.Key);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        // Phase 2: the lock was released, so a concurrent heartbeat
        // (UpdateTimestampAsync) may have refreshed a candidate. Re-validate
        // under the lock and only remove servers that are still timed out,
        // otherwise a heartbeat racing this cycle would be followed by a
        // spurious unregister/re-register flap (the server briefly vanishes).
        var removedServers = new List<(ushort GameServerId, DateTime RemovedAt)>();
        using (await this._lock.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            var now = DateTime.UtcNow;
            foreach (var gameServerId in candidates)
            {
                if (this._entries.TryGetValue(gameServerId, out var lastUpdate)
                    && now - lastUpdate > this.Timeout)
                {
                    this._entries.Remove(gameServerId);
                    removedServers.Add((gameServerId, lastUpdate));
                }
            }
        }

        // Notify after releasing the lock so external code is not called
        // while the registry lock is held. If a heartbeat re-registered the
        // server in the small window after removal, don't time it out and
        // unregister the freshly registered instance.
        foreach (var (gameServerId, removedAt) in removedServers)
        {
            bool reRegistered;
            using (await this._lock.LockAsync(cancellationToken).ConfigureAwait(false))
            {
                reRegistered = this._entries.TryGetValue(gameServerId, out var newTimestamp)
                    && newTimestamp > removedAt;
            }

            if (reRegistered)
            {
                this._logger.LogDebug("Game server {0} sent a heartbeat while cleanup was in progress; it stays registered.", gameServerId);
                continue;
            }

            this._logger.LogInformation("Game server {0} timed out and was removed from the registry.", gameServerId);
            this.OnGameServerTimedOut(gameServerId);
        }
    }
}
