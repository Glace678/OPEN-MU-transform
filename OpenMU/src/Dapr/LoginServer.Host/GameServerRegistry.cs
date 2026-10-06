// <copyright file="GameServerRegistry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LoginServer.Host;
using Microsoft.Extensions.Logging;

using MUnique.OpenMU.Dapr.Common;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The registry for game servers.
/// </summary>
public sealed class GameServerRegistry : TimeoutBasedGameServerRegistry
{
    private readonly TimeSpan _newServerUptimeLimit = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Initializes a new instance of the <see cref="GameServerRegistry"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public GameServerRegistry(ILogger<GameServerRegistry> logger)
        : base(TimeSpan.FromSeconds(20), logger)
    {
    }

    /// <summary>
    /// Occurs when a game server was added to the registry.
    /// </summary>
    public event AsyncEventHandler<ushort>? GameServerAdded;

    /// <summary>
    /// Occurs when a new (=freshly started) game server was added to the registry.
    /// </summary>
    public event AsyncEventHandler<ushort>? NewGameServerAdded;

    /// <summary>
    /// Occurs when a game server was removed from the registry.
    /// </summary>
    public event AsyncEventHandler<ushort>? GameServerRemoved;

    /// <summary>
    /// Updates the registration of the game server.
    /// </summary>
    /// <param name="gameServerId">The game server identifier.</param>
    /// <param name="upTime">The up-time of the server.</param>
    public async Task UpdateRegistrationAsync(ushort gameServerId, TimeSpan upTime)
    {
        if (await this.UpdateTimestampAsync(gameServerId).ConfigureAwait(false))
        {
            if (upTime <= this._newServerUptimeLimit)
            {
                this.NewGameServerAdded?.SafeInvokeAsync(gameServerId);
            }
            else
            {
                this.GameServerAdded?.SafeInvokeAsync(gameServerId);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnGameServerTimedOut(ushort gameServerId)
    {
        _ = this.GameServerRemoved?.SafeInvokeAsync(gameServerId);
    }
}
