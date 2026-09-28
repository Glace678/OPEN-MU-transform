// <copyright file="LocalStackManager.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

/// <summary>
/// Coordinates the persistent database, OpenMU server, client, backup, and restore lifecycle.
/// </summary>
public sealed class LocalStackManager : IDisposable
{
    private const long MinimumFreeBytes = 2L * 1024 * 1024 * 1024;
    private static readonly (int Port, string Component)[] DefaultServerPorts =
    {
        (44405, "1.04d 连接服务器"),
        (44406, "2.04d 连接服务器"),
        (55901, "1.04d 游戏服务器"),
        (55902, "2.04d 游戏服务器"),
        (55980, "聊天服务器"),
    };
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly LocalPaths _paths;
    private readonly LocalStackSettingsStore _settingsStore;
    private readonly ILocalSecretStore _secretStore;
    private readonly ProcessRunner _processRunner;
    private readonly PackageManifestValidator _manifestValidator;
    private readonly ConnectionSettingsWriter _connectionSettingsWriter;
    private readonly PostgreSqlExecutionPaths _postgreSqlExecutionPaths;
    private readonly PostgreSqlManager _database;
    private readonly OpenMuServerManager _server;
    private readonly BackupManager _backup;
    private Process? _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalStackManager"/> class.
    /// </summary>
    /// <param name="rootDirectory">The portable package root.</param>
    /// <param name="dataDirectory">An optional separate persistent data directory.</param>
    public LocalStackManager(string rootDirectory, string? dataDirectory = null)
    {
        this._paths = new LocalPaths(rootDirectory, dataDirectory);
        this._paths.EnsureDataDirectories();
        this._settingsStore = new LocalStackSettingsStore(this._paths.SettingsFile);
        this.Settings = this._settingsStore.Load();
        this._secretStore = OperatingSystem.IsWindows()
            ? new DpapiSecretStore(this._paths.SecretsFile)
            : new UnixSecretStore(this._paths.SecretsFile);
        this._processRunner = new ProcessRunner();
        this._manifestValidator = new PackageManifestValidator();
        this._connectionSettingsWriter = new ConnectionSettingsWriter();
        this._postgreSqlExecutionPaths = PostgreSqlExecutionPaths.Create(this._paths);
        this._database = new PostgreSqlManager(this._paths, this._postgreSqlExecutionPaths, this.Settings, this._processRunner);
        this._server = new OpenMuServerManager(this._paths, this.Settings, this._processRunner);
        this._backup = new BackupManager(this._paths, this._postgreSqlExecutionPaths, this.Settings, this._processRunner);
        this.Status = new LocalStackStatus(LocalStackState.Stopped, DateTimeOffset.Now);
    }

    /// <summary>Occurs after the stack state changes.</summary>
    public event Action<LocalStackStatus>? StatusChanged;

    /// <summary>Gets the current non-secret settings.</summary>
    public LocalStackSettings Settings { get; }

    /// <summary>Gets the address which the game server advertises to trusted-LAN mobile clients.</summary>
    public string MobileAccessAddress => OpenMuServerManager.ResolveAdvertisedAddress(this.Settings);

    /// <summary>Gets the current stack status.</summary>
    public LocalStackStatus Status { get; private set; }

    /// <summary>Gets a value indicating whether first-run administrator setup is required.</summary>
    public bool RequiresProvisioning => !this._secretStore.Exists;

    /// <summary>
    /// Creates the DPAPI-protected first-run secrets.
    /// </summary>
    /// <param name="administratorPassword">The user-selected administrator password.</param>
    public void Provision(string administratorPassword)
    {
        if (!this.RequiresProvisioning)
        {
            throw new InvalidOperationException("本地服务已完成初始设置。");
        }

        this._secretStore.Save(DpapiSecretStore.Create(administratorPassword));
        this._settingsStore.Save(this.Settings);
    }

    /// <summary>
    /// Starts the database, server, and optionally the game client.
    /// </summary>
    /// <param name="startGame">Whether to start the game client when ready.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task StartAsync(bool startGame, CancellationToken cancellationToken)
    {
        await this._lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var databaseStartedHere = false;
        var serverStartedHere = false;
        var originalClient = this._client;
        try
        {
            if (await this._server.IsRunningAsync(cancellationToken).ConfigureAwait(false))
            {
                await this.SetStatusAsync(LocalStackState.Running, "已连接到正在运行的 OpenMU 本地服务。", cancellationToken).ConfigureAwait(false);
                if (startGame)
                {
                    await this.StartGameAsync(cancellationToken).ConfigureAwait(false);
                }

                return;
            }

            if (this._server.HasKnownProcessRunning)
            {
                throw new InvalidOperationException("程序包中的 OpenMU 服务器进程仍在运行，但控制通道无法连接。");
            }

            if (this.RequiresProvisioning)
            {
                throw new InvalidOperationException("尚未完成首次运行的管理员设置。");
            }

            await this.SetStatusAsync(LocalStackState.Initializing, null, cancellationToken).ConfigureAwait(false);
            this.EnsureFreeSpace();
            await this._manifestValidator.ValidateAsync(this._paths, cancellationToken).ConfigureAwait(false);
            var databaseWasRunning = await this._database.IsRunningAsync(cancellationToken).ConfigureAwait(false);
            if (!databaseWasRunning)
            {
                EnsurePortAvailable(this.Settings.DatabasePort, "PostgreSQL 数据库");
            }

            EnsurePortAvailable(this.Settings.AdminPanelPort, "管理后台");
            foreach (var endpoint in DefaultServerPorts)
            {
                EnsurePortAvailable(endpoint.Port, endpoint.Component);
            }

            var secrets = this._secretStore.Load();
            await this.SetStatusAsync(LocalStackState.StartingDatabase, null, cancellationToken).ConfigureAwait(false);
            if (!databaseWasRunning)
            {
                databaseStartedHere = true;
                await this._database.StartAsync(secrets.DatabaseAdminPassword, cancellationToken).ConfigureAwait(false);
            }

            this._connectionSettingsWriter.Write(this._paths.ConnectionSettingsFile, this.Settings.DatabasePort, secrets);
            if ((this.Settings.SoloBalanceVersion < 1 || this.Settings.SoloCashShopVersion < 1)
                && await this._database.HasGameDatabaseAsync(secrets.DatabaseAdminPassword, cancellationToken).ConfigureAwait(false))
            {
                var reason = this.Settings.SoloBalanceVersion < 1 ? "before-solo-v1" : "before-solo-shop-v1";
                await this._backup.CreateAsync(secrets, reason, cancellationToken).ConfigureAwait(false);
            }
            await this.SetStatusAsync(LocalStackState.StartingServer, null, cancellationToken).ConfigureAwait(false);
            await this._server.StartAsync(secrets, cancellationToken).ConfigureAwait(false);
            serverStartedHere = true;
            this.Settings.SoloBalanceVersion = 1;
            this.Settings.SoloCashShopVersion = 1;
            this._settingsStore.Save(this.Settings);
            await this.SetStatusAsync(LocalStackState.Running, null, cancellationToken).ConfigureAwait(false);
            if (startGame)
            {
                await this.StartGameAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            var cleanupError = await this.RollBackFailedStartAsync(originalClient, serverStartedHere, databaseStartedHere).ConfigureAwait(false);
            var message = cleanupError is null ? ex.Message : $"{ex.Message} 清理失败：{cleanupError.Message}";
            await this.SetStatusAsync(LocalStackState.Faulted, message, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            this._lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Gracefully stops OpenMU and then PostgreSQL.
    /// </summary>
    /// <param name="force">Whether an owned, unresponsive server may be forcibly terminated.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task StopAsync(bool force, CancellationToken cancellationToken)
    {
        await this._lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await this.StopCoreAsync(force, stopDatabase: true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await this.SetStatusAsync(LocalStackState.Faulted, ex.Message, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            this._lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Creates a manual backup while the stack is running.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The backup archive path.</returns>
    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken)
    {
        await this._lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!await this._database.IsRunningAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("创建备份前请先启动本地服务。");
            }

            return await this._backup.CreateAsync(this._secretStore.Load(), "manual", cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this._lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Checks the running stack without overlapping lifecycle operations.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task CheckHealthAsync(CancellationToken cancellationToken)
    {
        if (this.Status.State != LocalStackState.Running
            || !await this._lifecycleGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            if (!await this._server.IsRunningAsync(cancellationToken).ConfigureAwait(false))
            {
                await this.SetStatusAsync(LocalStackState.Faulted, "OpenMU 服务器异常停止。", CancellationToken.None).ConfigureAwait(false);
            }
            else if (!await this._database.IsRunningAsync(cancellationToken).ConfigureAwait(false))
            {
                await this.SetStatusAsync(LocalStackState.Faulted, "PostgreSQL 数据库异常停止。", CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            await this.SetStatusAsync(LocalStackState.Faulted, ex.Message, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            this._lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Creates a protective backup, restores the selected database, and restarts the server.
    /// </summary>
    /// <param name="archivePath">The selected backup archive.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task RestoreAsync(string archivePath, CancellationToken cancellationToken)
    {
        await this._lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? restoreSourcePath = null;
        try
        {
            var fullArchivePath = Path.GetFullPath(archivePath);
            if (!File.Exists(fullArchivePath))
            {
                throw new FileNotFoundException("选定的备份文件不存在。", fullArchivePath);
            }

            restoreSourcePath = Path.Combine(this._paths.BackupsDirectory, $".restore-source-{Guid.NewGuid():N}.zip");
            File.Copy(fullArchivePath, restoreSourcePath, overwrite: false);
            var secrets = this._secretStore.Load();
            var databaseWasRunning = await this._database.IsRunningAsync(cancellationToken).ConfigureAwait(false);
            if (!databaseWasRunning)
            {
                await this._database.StartAsync(secrets.DatabaseAdminPassword, cancellationToken).ConfigureAwait(false);
            }

            if (await this._server.IsRunningAsync(cancellationToken).ConfigureAwait(false) || this._server.HasKnownProcessRunning)
            {
                await this._server.StopAsync(force: false, cancellationToken).ConfigureAwait(false);
            }

            _ = await this._backup.CreateAsync(secrets, "before-restore", cancellationToken).ConfigureAwait(false);
            await this._backup.RestoreDatabaseAsync(restoreSourcePath, secrets, cancellationToken).ConfigureAwait(false);
            this._connectionSettingsWriter.Write(this._paths.ConnectionSettingsFile, this.Settings.DatabasePort, secrets);
            await this._server.StartAsync(secrets, cancellationToken).ConfigureAwait(false);
            await this.SetStatusAsync(LocalStackState.Running, "备份已还原。", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await this.SetStatusAsync(LocalStackState.Faulted, ex.Message, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            try
            {
                if (restoreSourcePath is not null)
                {
                    File.Delete(restoreSourcePath);
                }
            }
            finally
            {
                this._lifecycleGate.Release();
            }
        }
    }

    /// <summary>Starts the game client against the ready local server.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task StartGameAsync(CancellationToken cancellationToken)
    {
        if (this.Status.State != LocalStackState.Running)
        {
            throw new InvalidOperationException("进入游戏前必须先启动本地服务器。");
        }

        if (!File.Exists(this._paths.GameExecutable))
        {
            throw new FileNotFoundException("缺少游戏客户端。", this._paths.GameExecutable);
        }

        if (this._client is { HasExited: false })
        {
            return;
        }

        var configurationFile = PrepareGameConfiguration(this._paths);
        var gameLogin = this.Settings.AutomaticGameLogin
            ? LocalGameLogin.FromSecrets(this._secretStore.Load()) : null;

        this._client = this._processRunner.Start(
            this._paths.GameExecutable,
            new[]
            {
                "connect",
                "/u127.0.0.1",
                $"/p{this.Settings.ConnectServerPort.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            },
            this._paths.GameDirectory,
            CreateGameEnvironment(
                configurationFile,
                this.Settings.AutomaticGameLogin,
                gameLogin,
                this.Settings.GameplayProfile == "solo"));
        await this.SetStatusAsync(LocalStackState.Running, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Persists non-secret settings.</summary>
    public void SaveSettings() => this._settingsStore.Save(this.Settings);

    /// <summary>Opens the local admin panel in the default browser.</summary>
    public void OpenAdminPanel() => OpenWithShell($"http://127.0.0.1:{this.Settings.AdminPanelPort}");

    /// <summary>Opens the data directory in File Explorer.</summary>
    public void OpenDataDirectory() => OpenWithShell(this._paths.DataDirectory);

    /// <summary>Opens the log directory in File Explorer.</summary>
    public void OpenLogsDirectory() => OpenWithShell(this._paths.LogsDirectory);

    /// <inheritdoc />
    public void Dispose()
    {
        this._client?.Dispose();
        this._lifecycleGate.Dispose();
    }

    /// <summary>Enables automatic login only for the locally launched game process.</summary>
    internal static IReadOnlyDictionary<string, string?> CreateGameEnvironment(
        string configurationFile,
        bool automaticLogin,
        LocalGameLogin? gameLogin = null,
        bool soloBalanceEnabled = true)
        => new Dictionary<string, string?>
        {
            ["MU_SOLO_BALANCE"] = soloBalanceEnabled ? "1" : null,
            ["MU_CONFIG_FILE"] = configurationFile,
            ["MU_LOCAL_AUTO_LOGIN"] = automaticLogin ? "1" : "0",
            ["MU_LOCAL_GAME_USERNAME"] = automaticLogin ? gameLogin?.Username : null,
            ["MU_LOCAL_GAME_PASSWORD"] = automaticLogin ? gameLogin?.Password : null,
        };

    /// <summary>Creates the stop backup and then stops the database, unless a normal stop must fail closed.</summary>
    /// <param name="force">Whether an explicitly confirmed force stop may skip a failed backup.</param>
    /// <param name="createBackupAsync">Creates the automatic backup.</param>
    /// <param name="stopDatabaseAsync">Stops the database.</param>
    /// <param name="cancellationToken">The operation cancellation token.</param>
    /// <returns>The skipped backup error, if a force stop continued after it.</returns>
    internal static async Task<Exception?> BackupThenStopDatabaseAsync(
        bool force,
        Func<Task> createBackupAsync,
        Func<Task> stopDatabaseAsync,
        CancellationToken cancellationToken)
    {
        Exception? backupError = null;
        try
        {
            await createBackupAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (force && (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested))
        {
            backupError = ex;
        }

        await stopDatabaseAsync().ConfigureAwait(false);
        return backupError;
    }

    /// <summary>Rejects existing listeners without stopping or attaching to their processes.</summary>
    internal static void EnsurePortAvailable(int port, string component)
    {
        var occupiedEndpoint = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
            .FirstOrDefault(endpoint => endpoint.Port == port);
        if (occupiedEndpoint is not null)
        {
            throw new InvalidOperationException($"{component} 端口 {occupiedEndpoint} 已被占用。请先正常关闭占用端口的程序；启动器不会自动停止其他服务。");
        }

        var listener = new TcpListener(IPAddress.Loopback, port) { ExclusiveAddressUse = true };
        try
        {
            listener.Start();
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException($"{component} 端口 127.0.0.1:{port} 已被占用。", ex);
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>Keeps mutable player settings outside the verified application payload.</summary>
    internal static string PrepareGameConfiguration(LocalPaths paths)
    {
        var configurationFile = Path.Combine(paths.KeysDirectory, "game-config.ini");
        if (!File.Exists(configurationFile))
        {
            File.Copy(paths.GameConfigurationTemplateFile, configurationFile, overwrite: false);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(configurationFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }

        return configurationFile;
    }

    private static void OpenWithShell(string target)
    {
        _ = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }

    private void EnsureFreeSpace()
    {
        var root = Path.GetPathRoot(this._paths.DataDirectory) ?? throw new InvalidOperationException("无法确定存档所在的磁盘。");
        if (new DriveInfo(root).AvailableFreeSpace < MinimumFreeBytes)
        {
            throw new IOException("OpenMU 本地版启动前至少需要 2 GiB 可用磁盘空间。");
        }
    }

    private async Task StopCoreAsync(bool force, bool stopDatabase, CancellationToken cancellationToken)
    {
        await this.SetStatusAsync(LocalStackState.Stopping, null, cancellationToken).ConfigureAwait(false);
        if (await this._server.IsRunningAsync(cancellationToken).ConfigureAwait(false) || this._server.HasKnownProcessRunning)
        {
            await this._server.StopAsync(force, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // A missing control channel is not proof that the server using this database stopped.
            EnsurePortAvailable(this.Settings.AdminPanelPort, "管理后台");
            foreach (var endpoint in DefaultServerPorts)
            {
                EnsurePortAvailable(endpoint.Port, endpoint.Component);
            }
        }

        Exception? skippedBackupError = null;
        if (stopDatabase && await this._database.IsRunningAsync(cancellationToken).ConfigureAwait(false))
        {
            skippedBackupError = await BackupThenStopDatabaseAsync(
                force,
                async () =>
                {
                    try
                    {
                        _ = await this._backup.CreateAsync(this._secretStore.Load(), "auto-stop", cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new AutomaticBackupFailedException(ex);
                    }
                },
                () => this._database.StopAsync(cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        File.Delete(this._paths.ConnectionSettingsFile);
        var message = skippedBackupError is null ? null : $"强制停止已跳过本次备份：{skippedBackupError.Message}";
        await this.SetStatusAsync(LocalStackState.Stopped, message, cancellationToken).ConfigureAwait(false);
    }

    private async Task SetStatusAsync(LocalStackState state, string? message, CancellationToken cancellationToken)
    {
        this.Status = new LocalStackStatus(state, DateTimeOffset.Now, this._database.ProcessId, this._server.ProcessId, this._client is { HasExited: false } client ? client.Id : null, message);
        var temporaryPath = this._paths.StatusFile + ".tmp";
        await File.WriteAllBytesAsync(temporaryPath, JsonSerializer.SerializeToUtf8Bytes(this.Status), cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, this._paths.StatusFile, overwrite: true);
        this.StatusChanged?.Invoke(this.Status);
    }

    private async Task<Exception?> RollBackFailedStartAsync(Process? originalClient, bool serverStartedHere, bool databaseStartedHere)
    {
        try
        {
            if (this._client is { HasExited: false } client && !ReferenceEquals(client, originalClient))
            {
                client.Kill(entireProcessTree: true);
                await client.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }

            if (serverStartedHere
                && (await this._server.IsRunningAsync(CancellationToken.None).ConfigureAwait(false) || this._server.HasKnownProcessRunning))
            {
                await this._server.StopAsync(force: false, CancellationToken.None).ConfigureAwait(false);
            }

            File.Delete(this._paths.ConnectionSettingsFile);
            if (databaseStartedHere && await this._database.IsRunningAsync(CancellationToken.None).ConfigureAwait(false))
            {
                await this._database.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }

            return null;
        }
        catch (Exception cleanupError)
        {
            return cleanupError;
        }
    }
}
