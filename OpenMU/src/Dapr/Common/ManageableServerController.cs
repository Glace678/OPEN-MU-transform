// <copyright file="ManageableServerController.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Dapr.Common;

using Microsoft.AspNetCore.Mvc;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// An api controller to control a <see cref="IManageableServer"/>.
/// </summary>
[ApiController]
[Route("")]
public class ManageableServerController
{
    private readonly IManageableServer _manageableServer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManageableServerController"/> class.
    /// </summary>
    /// <param name="manageableServer">The manageable server.</param>
    public ManageableServerController(IManageableServer manageableServer)
    {
        this._manageableServer = manageableServer;
    }

    /// <summary>
    /// Shuts the manageable server down.
    /// </summary>
    [HttpPost(nameof(IManageableServer.ShutdownAsync))]
    public async Task ShutdownAsync()
    {
        await this._manageableServer.ShutdownAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Starts the manageable server.
    /// </summary>
    [HttpPost(nameof(IManageableServer.StartAsync))]
    public async Task StartAsync()
    {
        await this._manageableServer.StartAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the configuration identifier of the manageable server.
    /// </summary>
    /// <returns>The configuration identifier.</returns>
    [HttpGet($"get{nameof(IManageableServer.ConfigurationId)}")]
    [HttpPost($"get{nameof(IManageableServer.ConfigurationId)}")]
    public Guid GetConfigurationId() => this._manageableServer.ConfigurationId;

    /// <summary>
    /// Gets the current state of the manageable server.
    /// </summary>
    /// <returns>The server state.</returns>
    [HttpGet(nameof(IManageableServer.ServerState))]
    [HttpPost(nameof(IManageableServer.ServerState))]
    public ServerState GetServerState() => this._manageableServer.ServerState;
}