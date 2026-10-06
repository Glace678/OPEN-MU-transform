// <copyright file="GameServerRegistry.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.ConnectServer.Host;
using Microsoft.Extensions.Logging;

using System.Net;
using MUnique.OpenMU.Dapr.Common;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// A registry which keeps track of available <see cref="IGameServer"/>s.
/// </summary>
public sealed class GameServerRegistry : TimeoutBasedGameServerRegistry
{
    private readonly IConnectServer _connectServer;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameServerRegistry"/> class.
    /// </summary>
    /// <param name="connectServer">The connect server.</param>
    /// <param name="logger">The logger.</param>
    public GameServerRegistry(IConnectServer connectServer, ILogger<GameServerRegistry> logger)
        : base(TimeSpan.FromSeconds(10), logger)
    {
        this._connectServer = connectServer;
    }

    /// <summary>
    /// Updates the registration.
    /// </summary>
    /// <param name="serverInfo">The server information.</param>
    /// <param name="publicEndPoint">The public end point.</param>
    public async Task UpdateRegistrationAsync(ServerInfo serverInfo, IPEndPoint publicEndPoint)
    {
        if (await this.UpdateTimestampAsync(serverInfo.Id).ConfigureAwait(false))
        {
            this._connectServer.RegisterGameServer(serverInfo, publicEndPoint);
        }
        else
        {
            this._connectServer.CurrentConnectionsChanged(serverInfo.Id, serverInfo.CurrentConnections);
        }
    }

    /// <inheritdoc />
    protected override void OnGameServerTimedOut(ushort gameServerId)
    {
        this._connectServer.UnregisterGameServer(gameServerId);
    }
}
