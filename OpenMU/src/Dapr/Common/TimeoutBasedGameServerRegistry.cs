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
        var removedServers = new List<ushort>();
        while (!this._disposeCts.IsCancellationRequested)
        {
            await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
            using var l = await this._lock.LockAsync(cancellationToken).ConfigureAwait(false);

            foreach (var gameServerId in this._entries.Keys)
            {
                var lastUpdate = this._entries[gameServerId];
                var difference = DateTime.UtcNow - lastUpdate;
                if (difference > this.Timeout)
                {
                    this._logger.LogInformation("Difference of {0} higher than timeout for server {1}", difference, gameServerId);
                    this.OnGameServerTimedOut(gameServerId);
                    removedServers.Add(gameServerId);
                }
            }

            foreach (var gameServerId in removedServers)
            {
                this._entries.Remove(gameServerId, out _);
            }

            removedServers.Clear();
        }
    }
}
