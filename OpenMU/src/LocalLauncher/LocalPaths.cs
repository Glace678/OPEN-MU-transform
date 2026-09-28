// <copyright file="LocalPaths.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

/// <summary>
/// Resolves all files in the fixed portable package layout.
/// </summary>
public sealed class LocalPaths
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LocalPaths"/> class.
    /// </summary>
    /// <param name="rootDirectory">The package root directory.</param>
    /// <param name="dataDirectory">Optional writable data directory outside the application payload.</param>
    public LocalPaths(string rootDirectory, string? dataDirectory = null)
    {
        this.RootDirectory = Path.GetFullPath(rootDirectory);
        this.DataDirectory = Path.GetFullPath(dataDirectory ?? Path.Combine(this.RootDirectory, "Data"));
    }

    /// <summary>Gets the package root.</summary>
    public string RootDirectory { get; }

    /// <summary>Gets the writable persistent data directory.</summary>
    public string DataDirectory { get; }

    /// <summary>Gets the game application directory.</summary>
    public string GameDirectory => Path.Combine(this.RootDirectory, "App", "Game");

    /// <summary>Gets the verified default game configuration template.</summary>
    public string GameConfigurationTemplateFile => Path.Combine(this.GameDirectory, "config.ini.template");

    /// <summary>Gets the server application directory.</summary>
    public string ServerDirectory => Path.Combine(this.RootDirectory, "App", "Server");

    /// <summary>Gets the PostgreSQL runtime directory.</summary>
    public string PostgreSqlRuntimeDirectory => Path.Combine(this.RootDirectory, "Runtime", "PostgreSQL");

    /// <summary>Gets the persistent PostgreSQL data directory.</summary>
    public string PostgreSqlDataDirectory => Path.Combine(this.DataDirectory, "PostgreSQL");

    /// <summary>Gets the protected key directory.</summary>
    public string KeysDirectory => Path.Combine(this.DataDirectory, "Keys");

    /// <summary>Gets the logs directory.</summary>
    public string LogsDirectory => Path.Combine(this.DataDirectory, "Logs");

    /// <summary>Gets the backups directory.</summary>
    public string BackupsDirectory => Path.Combine(this.DataDirectory, "Backups");

    /// <summary>Gets the OpenMU data-protection key directory.</summary>
    public string DataProtectionKeysDirectory => Path.Combine(this.KeysDirectory, "OpenMU");

    /// <summary>Gets the launcher settings file.</summary>
    public string SettingsFile => Path.Combine(this.KeysDirectory, "local-settings.json");

    /// <summary>Gets the DPAPI-protected secrets file.</summary>
    public string SecretsFile => Path.Combine(this.KeysDirectory, OperatingSystem.IsWindows() ? "local-secrets.dpapi" : "local-secrets.json");

    /// <summary>Gets the transient database connection file.</summary>
    public string ConnectionSettingsFile => Path.Combine(this.KeysDirectory, "ConnectionSettings.runtime.xml");

    /// <summary>Gets the serializable state file.</summary>
    public string StatusFile => Path.Combine(this.KeysDirectory, "local-stack-status.json");

    /// <summary>Gets the package manifest.</summary>
    public string ManifestFile => Path.Combine(this.RootDirectory, "manifest.json");

    /// <summary>Gets the PostgreSQL control executable.</summary>
    public string PgCtlExecutable => Path.Combine(this.PostgreSqlRuntimeDirectory, "bin", LocalPlatform.ExecutableName("pg_ctl"));

    /// <summary>Gets the PostgreSQL initialization executable.</summary>
    public string InitDbExecutable => Path.Combine(this.PostgreSqlRuntimeDirectory, "bin", LocalPlatform.ExecutableName("initdb"));

    /// <summary>Gets the PostgreSQL readiness executable.</summary>
    public string PgIsReadyExecutable => Path.Combine(this.PostgreSqlRuntimeDirectory, "bin", LocalPlatform.ExecutableName("pg_isready"));

    /// <summary>Gets the PostgreSQL backup executable.</summary>
    public string PgDumpExecutable => Path.Combine(this.PostgreSqlRuntimeDirectory, "bin", LocalPlatform.ExecutableName("pg_dump"));

    /// <summary>Gets the PostgreSQL restore executable.</summary>
    public string PgRestoreExecutable => Path.Combine(this.PostgreSqlRuntimeDirectory, "bin", LocalPlatform.ExecutableName("pg_restore"));

    /// <summary>Gets the OpenMU server executable.</summary>
    public string ServerExecutable => Path.Combine(this.ServerDirectory, LocalPlatform.ExecutableName("MUnique.OpenMU.Startup"));

    /// <summary>Gets the game executable.</summary>
    public string GameExecutable => Path.Combine(this.GameDirectory, LocalPlatform.ExecutableName("Main"));

    /// <summary>Creates the persistent data directories.</summary>
    public void EnsureDataDirectories()
    {
        LocalPlatform.CreatePrivateDirectory(this.DataDirectory);
        foreach (var path in new[] { this.PostgreSqlDataDirectory, this.KeysDirectory, this.LogsDirectory, this.BackupsDirectory, this.DataProtectionKeysDirectory })
        {
            LocalPlatform.CreatePrivateDirectory(path);
        }
    }
}
