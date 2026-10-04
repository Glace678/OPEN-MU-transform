// <copyright file="PersistentLoginServer.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LoginServer.Host;

using global::Dapr.Client;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Interfaces;

/// <summary>
/// An implementation of a <see cref="ILoginServer"/> which persists the login state in a dapr state store.
/// </summary>
public sealed class PersistentLoginServer : ILoginServer
{
    private const string StoreName = "login-state";

    private const int OfflineServerId = -1;

    private readonly ILogger<PersistentLoginServer> _logger;

    private readonly DaprClient _daprClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersistentLoginServer"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="daprClient">The dapr client.</param>
    public PersistentLoginServer(ILogger<PersistentLoginServer> logger, DaprClient daprClient)
    {
        this._logger = logger;
        this._daprClient = daprClient;
    }

    /// <summary>
    /// Removes the server.
    /// </summary>
    /// <param name="serverId">The server identifier.</param>
    public async Task RemoveServerAsync(byte serverId)
    {
        var indexName = $"serverindex-{serverId}";
        var (serverIndex, eTag) = await this._daprClient.GetStateAndETagAsync<HashSet<string>>(StoreName, indexName, ConsistencyMode.Strong).ConfigureAwait(false);
        if (serverIndex is null || serverIndex.Count == 0)
        {
            return;
        }

        foreach (var accountName in serverIndex)
        {
            await this.SetAccountOfflineAsync(accountName).ConfigureAwait(false);
        }

        serverIndex.Clear();

        if (!await this._daprClient.TrySaveStateAsync(StoreName, indexName, serverIndex, eTag).ConfigureAwait(false))
        {
            // try again, if it failed
            await this.RemoveServerAsync(serverId).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<bool> TryLoginAsync(string accountName, byte serverId)
    {
        try
        {
            var (currentServerId, eTag) = await this._daprClient.GetStateAndETagAsync<int?>(StoreName, accountName, ConsistencyMode.Strong).ConfigureAwait(false);
            if (currentServerId is >= 0)
            {
                return false;
            }

            if (string.IsNullOrEmpty(eTag))
            {
                // Never logged in, so first insert a fresh state and try again.
                // We never want to have the same account logged in twice, because that may lead to game mechanic exploits.
                await this._daprClient.SaveStateAsync<int?>(StoreName, accountName, OfflineServerId, new StateOptions { Concurrency = ConcurrencyMode.FirstWrite, Consistency = ConsistencyMode.Strong }).ConfigureAwait(false);
                return await this.TryLoginAsync(accountName, serverId).ConfigureAwait(false);
            }

            var success = await this._daprClient.TrySaveStateAsync(StoreName, accountName, serverId, eTag).ConfigureAwait(false);
            if (!success)
            {
                return false;
            }

            if (!await this.TryAddToIndexAsync(accountName, serverId).ConfigureAwait(false))
            {
                await this._daprClient.TrySaveStateAsync<int?>(StoreName, accountName, OfflineServerId, eTag).ConfigureAwait(false);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Couldn't get/set logged-in state for account {0}", accountName);
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask LogOffAsync(string accountName, byte serverId)
    {
        try
        {
            var (currentServerId, eTag) = await this._daprClient.GetStateAndETagAsync<int?>(StoreName, accountName, ConsistencyMode.Strong).ConfigureAwait(false);
            if (currentServerId != serverId)
            {
                this._logger.LogWarning(
                    "LogOff for account {0} rejected: current server is {1}, requested by {2}.",
                    accountName,
                    currentServerId,
                    serverId);
                return;
            }

            if (!await this._daprClient.TrySaveStateAsync<int?>(StoreName, accountName, OfflineServerId, eTag).ConfigureAwait(false))
            {
                this._logger.LogWarning("LogOff CAS failed for account {0} on server {1}; state changed concurrently.", accountName, serverId);
                return;
            }

            await this.RemoveFromIndexAsync(accountName, serverId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Unexpected error when removing account {0} from server {1}", accountName, serverId);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Dictionary<string, byte>> GetSnapshotAsync()
    {
        var result = new Dictionary<string, byte>();
        for (int i = 0; i < 20; i++)
        {
            var indexName = $"serverindex-{i}";

            var (serverIndex, eTag) = await this._daprClient.GetStateAndETagAsync<HashSet<string>>(StoreName, indexName, ConsistencyMode.Strong).ConfigureAwait(false);
            if (serverIndex is null)
            {
                continue;
            }

            foreach (var accountName in serverIndex)
            {
                result[accountName] = (byte)i;
            }
        }

        return result;
    }

    private async Task<bool> TryAddToIndexAsync(string accountName, byte serverId)
    {
        const int maxAttempts = 5;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var indexName = $"serverindex-{serverId}";
            var (serverIndex, eTag) = await this._daprClient.GetStateAndETagAsync<HashSet<string>>(StoreName, indexName, ConsistencyMode.Strong).ConfigureAwait(false);
            if (serverIndex is null)
            {
                serverIndex = new HashSet<string> { accountName };
                if (await this._daprClient.TrySaveStateAsync(StoreName, indexName, serverIndex, eTag).ConfigureAwait(false))
                {
                    return true;
                }

                continue;
            }

            if (!serverIndex.Add(accountName))
            {
                return true;
            }

            if (await this._daprClient.TrySaveStateAsync(StoreName, indexName, serverIndex, eTag).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private async Task RemoveFromIndexAsync(string accountName, byte serverId)
    {
        var indexName = $"serverindex-{serverId}";
        var (serverIndex, eTag) = await this._daprClient.GetStateAndETagAsync<HashSet<string>>(StoreName, indexName, ConsistencyMode.Strong).ConfigureAwait(false);
        if (serverIndex is null)
        {
            return;
        }

        if (serverIndex.Remove(accountName))
        {
            if (!await this._daprClient.TrySaveStateAsync(StoreName, indexName, serverIndex, eTag).ConfigureAwait(false))
            {
                // try again, if it failed
                await this.RemoveFromIndexAsync(accountName, serverId).ConfigureAwait(false);
            }
        }
    }

    private async Task SetAccountOfflineAsync(string accountName)
    {
        try
        {
            var (currentServerId, eTag) = await this._daprClient.GetStateAndETagAsync<int?>(StoreName, accountName, ConsistencyMode.Strong).ConfigureAwait(false);
            if (currentServerId == OfflineServerId)
            {
                return;
            }

            await this._daprClient.TrySaveStateAsync<int?>(StoreName, accountName, OfflineServerId, eTag).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Couldn't get/set logged-out state for account {0}", accountName);
        }
    }
}