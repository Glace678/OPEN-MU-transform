// <copyright file="SecretStoreDatabaseConnectionSettingsProvider.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Dapr.Common;

using System.Collections.Concurrent;
using System.Threading;
using global::Dapr;
using global::Dapr.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Persistence.EntityFramework;

/// <summary>
/// Implementation of <see cref="IDatabaseConnectionSettingProvider"/> which retrieves the settings from the
/// configured Dapr secret storage.
/// </summary>
public class SecretStoreDatabaseConnectionSettingsProvider : IDatabaseConnectionSettingProvider
{
    private const string SecretStoreName = "secrets";
    private readonly DaprClient _daprClient;
    private readonly ILogger<SecretStoreDatabaseConnectionSettingsProvider> _logger;
    private readonly ConcurrentDictionary<string, ConnectionSetting> _connectionSettings = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly object _initLock = new();
    private volatile bool _isInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretStoreDatabaseConnectionSettingsProvider" /> class.
    /// </summary>
    /// <param name="daprClient">The dapr client.</param>
    /// <param name="logger">The logger.</param>
    public SecretStoreDatabaseConnectionSettingsProvider(DaprClient daprClient, ILogger<SecretStoreDatabaseConnectionSettingsProvider> logger)
    {
        this._daprClient = daprClient;
        this._logger = logger;
    }

    /// <inheritdoc />
    public Task? Initialization { get; private set; }

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (this._isInitialized)
        {
            return Task.CompletedTask;
        }

        async Task InitializeCoreAsync()
        {
            while (!this._isInitialized)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    Console.WriteLine("trying to get secrets ...");
                    var secrets = await this._daprClient.GetBulkSecretAsync(SecretStoreName, cancellationToken: cancellationToken).ConfigureAwait(false);
                    foreach (var secret in secrets.Where(kvp => string.Equals(kvp.Key.Split(':')[0], "connectionStrings", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        if (secret.Value is not { Count: > 0 })
                        {
                            this._logger.LogWarning("Skipping connection string secret '{0}' because it contains no values.", secret.Key);
                            continue;
                        }

                        var contextTypeName = secret.Value.Keys.First().Split(':').Last();
                        var setting = new ConnectionSetting
                        {
                            ContextTypeName = contextTypeName,
                            ConnectionString = secret.Value.Values.First()!,
                            DatabaseEngine = DatabaseEngine.Npgsql,
                        };

                        this._connectionSettings[contextTypeName] = setting;
                    }

                    Console.WriteLine("secrets retrieved :)");

                    this._isInitialized = true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (DaprException ex)
                {
                    this._logger.LogWarning(ex, "Error occurred when retrieving the connection strings from the secrets store. Trying again in 3 seconds...");
                    Console.WriteLine("Error occurred when retrieving the connection strings from the secrets store. Trying again in 3 seconds...");
                    await Task.Delay(3000, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        lock (this._initLock)
        {
            if (this._isInitialized)
            {
                return Task.CompletedTask;
            }

            if (this.Initialization is { IsFaulted: false, IsCanceled: false } existingInit)
            {
                return existingInit;
            }

            this.Initialization = InitializeCoreAsync();
            ConnectionConfigurator.Initialize(this);
            return this.Initialization;
        }
    }

    /// <inheritdoc />
    public ConnectionSetting GetConnectionSetting<TContextType>()
        where TContextType : DbContext
    {
        return this.GetConnectionSetting(typeof(TContextType));
    }

    /// <inheritdoc />
    public ConnectionSetting GetConnectionSetting(Type contextType)
    {
        var fullName = contextType.FullName;
        if (fullName is not null && this._connectionSettings.TryGetValue(fullName, out var result))
        {
            return result;
        }

        if (fullName is not null && fullName.IndexOf('`') is > 0 and var tickIndex
            && this._connectionSettings.TryGetValue(fullName[..tickIndex], out result))
        {
            return result;
        }

        if (this._connectionSettings.TryGetValue(contextType.Name, out result))
        {
            return result;
        }

        var shortUnmangled = contextType.Name.Split('`')[0];
        if (this._connectionSettings.TryGetValue(shortUnmangled, out result))
        {
            return result;
        }

        throw new InvalidOperationException($"No connection string for type '{contextType.FullName}' stored in the secret store.");
    }
}