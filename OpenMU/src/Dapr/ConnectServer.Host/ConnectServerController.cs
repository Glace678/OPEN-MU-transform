// <copyright file="ConnectServerController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.ConnectServer.Host;

using System.Net;
using global::Dapr;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.ServerClients;

/// <summary>
/// API Controller that receives messages from other services.
/// </summary>
[ApiController]
[Route("")]
public class ConnectServerController : ControllerBase
{
    private readonly GameServerRegistry _registry;
    private readonly ILogger<ConnectServerController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectServerController"/> class.
    /// </summary>
    /// <param name="registry">The registry.</param>
    /// <param name="logger">The logger.</param>
    public ConnectServerController(GameServerRegistry registry, ILogger<ConnectServerController> logger)
    {
        this._registry = registry;
        this._logger = logger;
    }

    /// <summary>
    /// Handles the game server heartbeat.
    /// </summary>
    /// <param name="data">The data.</param>
    [HttpPost("GameServerHeartbeat")]
    [Topic("pubsub", "GameServerHeartbeat")]
    public async Task<IActionResult> GameServerHeartbeatAsync([FromBody] GameServerHeartbeatArguments data)
    {
        if (data?.ServerInfo is null || string.IsNullOrWhiteSpace(data.PublicEndPoint))
        {
            this._logger.LogWarning("Received invalid heartbeat: missing server info or endpoint.");
            return this.BadRequest("ServerInfo and PublicEndPoint are required.");
        }

        if (!IPEndPoint.TryParse(data.PublicEndPoint, out var publicEndPoint) ||
            publicEndPoint.Port is < 1 or > 65535 ||
            publicEndPoint.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            this._logger.LogWarning("Received heartbeat with invalid public endpoint '{0}'.", data.PublicEndPoint);
            return this.BadRequest("PublicEndPoint is not a valid IPv4 endpoint.");
        }

        try
        {
            await this._registry.UpdateRegistrationAsync(data.ServerInfo, publicEndPoint).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error updating the GameServerRegistry");
        }

        return this.Ok();
    }
}