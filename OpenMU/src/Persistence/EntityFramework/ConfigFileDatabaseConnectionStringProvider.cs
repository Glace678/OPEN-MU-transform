// <copyright file="ConfigFileDatabaseConnectionStringProvider.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework;

using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;

/// <summary>
/// Implementation of <see cref="IDatabaseConnectionSettingProvider"/> which takes the connection strings out of
/// a configuration file, usually <c>ConnectionSettings.xml</c>.
/// The settings can be influenced by environment variables:
/// <list type="bullet">
/// <item><c>DB_HOST</c> overrides the database server for all connections.</item>
/// <item><c>OPENMU_DB_ADMIN_PASSWORD</c> / <c>OPENMU_DB_CONFIG_PASSWORD</c> /
/// <c>OPENMU_DB_ACCOUNT_PASSWORD</c> / <c>OPENMU_DB_FRIEND_PASSWORD</c> /
/// <c>OPENMU_DB_GUILD_PASSWORD</c> inject the password for every database role.
/// If a required variable is unset, startup fails fast (no plaintext fallback).</item>
/// <item><c>DB_ADMIN_USER</c> overrides the admin username. <c>DB_ADMIN_PW</c> is a legacy fallback for <c>OPENMU_DB_ADMIN_PASSWORD</c> (admin role only); if both are unset, startup throws.</item>
/// </list>
/// </summary>
public class ConfigFileDatabaseConnectionStringProvider : IDatabaseConnectionSettingProvider
{
    /// <summary>
    /// The environment variable which overrides the connection settings file path.
    /// </summary>
    public const string ConnectionSettingsFileVariableName = "OPENMU_CONNECTION_SETTINGS_FILE";

    private const string DbHostVariableName = "DB_HOST";
    private const string DbAdminUserVariableName = "DB_ADMIN_USER";
    private const string DbAdminPasswordVariableName = "DB_ADMIN_PW";

    private readonly string _fileName;

    private IDictionary<Type, ConnectionSetting>? _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigFileDatabaseConnectionStringProvider"/> class.
    /// </summary>
    public ConfigFileDatabaseConnectionStringProvider()
        : this("ConnectionSettings.xml")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigFileDatabaseConnectionStringProvider"/> class.
    /// </summary>
    /// <param name="fileName">Name of the file.</param>
    public ConfigFileDatabaseConnectionStringProvider(string fileName)
    {
        this._fileName = fileName;
    }

    /// <inheritdoc />
    public Task? Initialization { get; private set; }

    private IDictionary<Type, ConnectionSetting> Settings => this._settings ??= this.LoadSettings();

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        this.Initialization = Task.Run(() => this._settings = this.LoadSettings(), cancellationToken);
        await this.Initialization.ConfigureAwait(false);
        ConnectionConfigurator.Initialize(this);
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
        if (this.Settings.TryGetValue(contextType, out var result))
        {
            return result;
        }

        throw new ArgumentException("DB Configuration not found for type {0}", contextType.FullName);
    }

    private IDictionary<Type, ConnectionSetting> LoadSettings()
    {
        var settings = new XmlReaderSettings
        {
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Ignore,
            CloseInput = true,
            XmlResolver = null,
        };
        var result = new Dictionary<Type, ConnectionSetting>();

        var configurationFilePath = this.GetConfigurationFilePath();
        using var xmlReader = XmlReader.Create(File.OpenRead(configurationFilePath), settings);
        var serializer = new XmlSerializer(typeof(ConnectionSettings));
        if (serializer.CanDeserialize(xmlReader))
        {
            if (serializer.Deserialize(xmlReader) is ConnectionSettings xmlSettings)
            {
                foreach (var setting in xmlSettings.Connections)
                {
                    if (setting.ContextTypeName is null)
                    {
                        throw new InvalidDataException("ContextTypeName is null.");
                    }

                    if (setting.ConnectionString is null)
                    {
                        throw new InvalidDataException("ConnectionString is null.");
                    }

                    if (Type.GetType(setting.ContextTypeName, false, true) is { } contextType)
                    {
                        this.ApplyEnvironmentVariables(setting);
                        result.Add(contextType, setting);
                    }
                    else if (setting.ContextTypeName.EndsWith($".{nameof(TypedContext)}") || setting.ContextTypeName.EndsWith($".{nameof(TypedContext)}^1"))
                    {
                        this.ApplyEnvironmentVariables(setting);
                        result.Add(typeof(TypedContext), setting);
                    }
                    else
                    {
                        throw new InvalidDataException($"Unknown context type: {setting.ContextTypeName}");
                    }
                }
            }
        }

        return result;
    }

    private string GetConfigurationFilePath()
    {
        if (Environment.GetEnvironmentVariable(ConnectionSettingsFileVariableName) is { Length: > 0 } configuredPath)
        {
            return Path.GetFullPath(configuredPath);
        }

        return Path.Combine(Path.GetDirectoryName(new Uri(typeof(ConnectionConfigurator).Assembly.Location!).LocalPath)!, this._fileName);
    }

    /// <summary>
    /// Injects the database host and per-role password from environment variables.
    /// Plaintext passwords in ConnectionSettings.xml are placeholders (<c>__SET_BY_ENV__</c>);
    /// if the required variable is missing, startup throws instead of falling back to a default.
    /// </summary>
    private void ApplyEnvironmentVariables(ConnectionSetting setting)
    {
        var csb = new NpgsqlConnectionStringBuilder(setting.ConnectionString!);

        // DB_HOST overrides the server for every connection (all branches/variants).
        if (Environment.GetEnvironmentVariable(DbHostVariableName) is { Length: > 0 } dbHost
            && !string.IsNullOrEmpty(dbHost))
        {
            csb.Host = dbHost;
        }

        // Map the connection to a database role and inject the corresponding password.
        var role = DetermineRole(csb.Username);
        var passwordVariableName = role switch
        {
            DbRole.Admin => "OPENMU_DB_ADMIN_PASSWORD",
            DbRole.Config => "OPENMU_DB_CONFIG_PASSWORD",
            DbRole.Account => "OPENMU_DB_ACCOUNT_PASSWORD",
            DbRole.Friend => "OPENMU_DB_FRIEND_PASSWORD",
            DbRole.Guild => "OPENMU_DB_GUILD_PASSWORD",
            _ => throw new InvalidDataException($"Unknown database role for username '{csb.Username}'."),
        };

        // Resolve the password: prefer the per-role env var; fall back to the
        // legacy DB_ADMIN_PW alias for the admin role; fail fast if neither is set.
        string? resolvedPassword = Environment.GetEnvironmentVariable(passwordVariableName);
        if (string.IsNullOrEmpty(resolvedPassword)
            && role == DbRole.Admin
            && Environment.GetEnvironmentVariable(DbAdminPasswordVariableName) is { Length: > 0 } legacyAdminPw)
        {
            resolvedPassword = legacyAdminPw;
        }

        if (!string.IsNullOrEmpty(resolvedPassword))
        {
            csb.Password = resolvedPassword;
        }
        else
        {
            var triedVariables = role == DbRole.Admin
                ? $"{passwordVariableName} (or legacy {DbAdminPasswordVariableName})"
                : passwordVariableName;
            throw new InvalidOperationException(
                $"Database password environment variable {triedVariables} is not set. " +
                "ConnectionSettings.xml no longer stores plaintext passwords; set the variable in the environment or secrets store.");
        }

        // Legacy alias: DB_ADMIN_USER overrides the admin username.
        if (role == DbRole.Admin
            && Environment.GetEnvironmentVariable(DbAdminUserVariableName) is { Length: > 0 } legacyAdminUser
            && !string.IsNullOrEmpty(legacyAdminUser))
        {
            csb.Username = legacyAdminUser;
        }

        setting.ConnectionString = csb.ConnectionString;
    }

    private static DbRole DetermineRole(string? username) => (username?.Trim().ToLowerInvariant()) switch
    {
        "postgres" or "admin" => DbRole.Admin,
        "config" => DbRole.Config,
        "account" or "trade" => DbRole.Account,
        "friend" => DbRole.Friend,
        "guild" => DbRole.Guild,
        _ => DbRole.Unknown,
    };

    private enum DbRole
    {
        Unknown,
        Admin,
        Config,
        Account,
        Friend,
        Guild,
    }
}