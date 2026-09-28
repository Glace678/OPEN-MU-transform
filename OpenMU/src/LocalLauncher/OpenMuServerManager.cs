// <copyright file="OpenMuServerManager.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>
/// Starts and gracefully stops the local OpenMU server process.
/// </summary>
public sealed class OpenMuServerManager
{
    private readonly LocalPaths _paths;
    private readonly LocalStackSettings _settings;
    private readonly ProcessRunner _processRunner;
    private readonly ControlPipeClient _controlClient;
    private Process? _process;
    private int? _attachedProcessId;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenMuServerManager"/> class.
    /// </summary>
    /// <param name="paths">The package paths.</param>
    /// <param name="settings">The local settings.</param>
    /// <param name="processRunner">The process runner.</param>
    public OpenMuServerManager(LocalPaths paths, LocalStackSettings settings, ProcessRunner processRunner)
    {
        this._paths = paths;
        this._settings = settings;
        this._processRunner = processRunner;
        var root = NormalizeControlPath(paths.RootDirectory);
        var data = NormalizeControlPath(paths.DataDirectory);
        var portableData = NormalizeControlPath(Path.Combine(root, "Data"));
        var identity = string.Equals(portableData, data, LocalPlatform.PathComparison)
            ? root : root + "\0" + data;
        this.PipeName = CreatePipeName(identity);
        this._controlClient = new ControlPipeClient(this.PipeName);
    }

    /// <summary>Gets the current server process identifier, if the launcher owns it.</summary>
    public int? ProcessId => this._process is { HasExited: false } process ? process.Id : this._attachedProcessId;

    /// <summary>Gets a value indicating whether the launcher-owned server process is still alive.</summary>
    public bool HasOwnedProcessRunning => this._process is { HasExited: false };

    /// <summary>Gets a value indicating whether a verified owned or attached server process is still alive.</summary>
    public bool HasKnownProcessRunning
    {
        get
        {
            if (this.HasOwnedProcessRunning)
            {
                return true;
            }

            if (this._attachedProcessId is not { } processId)
            {
                return false;
            }

            try
            {
                this.EnsurePackagedServerProcess(processId);
                return true;
            }
            catch (InvalidDataException)
            {
                this._attachedProcessId = null;
                return false;
            }
        }
    }

    /// <summary>Gets the deterministic current-user control pipe name.</summary>
    public string PipeName { get; }

    /// <summary>
    /// Starts OpenMU and waits for both the admin panel and connect server.
    /// </summary>
    /// <param name="secrets">The local secrets.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task StartAsync(LocalSecrets secrets, CancellationToken cancellationToken)
    {
        if (!File.Exists(this._paths.ServerExecutable))
        {
            throw new FileNotFoundException("缺少已发布的 OpenMU 服务器。", this._paths.ServerExecutable);
        }

        var advertisedAddress = ResolveAdvertisedAddress(this._settings);
        var gameplayProfile = GetGameplayProfileArgument(this._settings.GameplayProfile);
        var process = this._processRunner.Start(
            this._paths.ServerExecutable,
            new[] { "-autostart", $"-resolveIP:{advertisedAddress}", "-version:season6", "-gameservers:1", "-testaccounts:false", gameplayProfile },
            this._paths.ServerDirectory,
            this.CreateServerEnvironment(secrets));
        this._process = process;
        try
        {
            var requiredPorts = new[]
            {
                this._settings.AdminPanelPort,
                44405,
                this._settings.ConnectServerPort,
                55901,
                55902,
                55980,
            };
            await Task.WhenAll(requiredPorts.Distinct().Select(port => this.WaitForEndpointAsync(port, cancellationToken))).ConfigureAwait(false);
            var response = await this._controlClient.SendAsync("status", TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            var processId = EnsureControlResponse(response, "running");
            if (processId != process.Id)
            {
                throw new InvalidDataException("OpenMU 控制通道属于未知进程。");
            }
        }
        catch
        {
            await this.TerminateFailedStartAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Requests a graceful shutdown and optionally kills an unresponsive child process.
    /// </summary>
    /// <param name="force">Whether an unresponsive child may be killed.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task StopAsync(bool force, CancellationToken cancellationToken)
    {
        try
        {
            var response = await this._controlClient.SendAsync("shutdown", TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            this._attachedProcessId = EnsureControlResponse(response, "stopping");
            if (this._process is { } ownedProcess && this._attachedProcessId != ownedProcess.Id)
            {
                throw new InvalidDataException("OpenMU 控制通道属于未知进程。");
            }
        }
        catch (Exception ex) when (
            !force
            && (this.HasOwnedProcessRunning || this._attachedProcessId is not null)
            && (ex is IOException or TimeoutException
                || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)))
        {
            throw new TimeoutException("OpenMU 控制通道无法连接。强制停止需要您明确确认。", ex);
        }
        catch (Exception ex) when (force && (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested))
        {
            // The caller explicitly confirmed forced shutdown.
        }

        var process = this._process;
        if (force)
        {
            if (process is null)
            {
                if (this._attachedProcessId is not { } attachedProcessId)
                {
                    throw new InvalidOperationException("OpenMU 控制通道未返回服务器进程标识。");
                }

                await this.ForceTerminateVerifiedProcessAsync(attachedProcessId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await this.ForceTerminateVerifiedProcessAsync(process, cancellationToken).ConfigureAwait(false);
                process.Dispose();
                this._process = null;
            }

            this._attachedProcessId = null;
            return;
        }

        if (process is null)
        {
            await this.WaitForAttachedProcessToExitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (process.HasExited)
        {
            process.Dispose();
            this._process = null;
            this._attachedProcessId = null;
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            this._attachedProcessId = null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && force)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("OpenMU 未在 60 秒内停止。强制停止需要您明确确认。");
        }

        process.Dispose();
        this._process = null;
        this._attachedProcessId = null;
    }

    /// <summary>
    /// Determines whether an OpenMU server responds on the private control channel.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> when the server responds.</returns>
    public async Task<bool> IsRunningAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await this._controlClient.SendAsync("status", TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            var processId = EnsureControlResponse(response, "running");
            this.EnsurePackagedServerProcess(processId);
            this._attachedProcessId = processId;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException)
        {
            if (!this.HasKnownProcessRunning)
            {
                this._attachedProcessId = null;
            }

            return false;
        }
    }

    /// <summary>Forcibly terminates a process only after verifying that it is the packaged server executable.</summary>
    /// <param name="processId">The process identifier to verify and terminate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    internal async Task ForceTerminateVerifiedProcessAsync(int processId, CancellationToken cancellationToken)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return;
        }

        using (process)
        {
            await this.ForceTerminateVerifiedProcessAsync(process, cancellationToken).ConfigureAwait(false);
        }
    }

    internal IReadOnlyDictionary<string, string?> CreateServerEnvironment(LocalSecrets secrets)
    {
        var gameLogin = this._settings.MobileAccessEnabled && this._settings.AutomaticGameLogin
            ? LocalGameLogin.FromMobilePackageKey(this._settings.MobilePackageKey)
            : LocalGameLogin.FromSecrets(secrets);
        var listenerAddress = this._settings.MobileAccessEnabled ? "0.0.0.0" : "127.0.0.1";
        return new Dictionary<string, string?>
        {
            ["OPENMU_LOCAL_GAME_USERNAME"] = this._settings.AutomaticGameLogin ? gameLogin.Username : null,
            ["OPENMU_LOCAL_GAME_PASSWORD"] = this._settings.AutomaticGameLogin ? gameLogin.Password : null,
            ["OPENMU_MOBILE_PACKAGE_KEY"] = this._settings.MobileAccessEnabled ? this._settings.MobilePackageKey : null,
            ["OPENMU_CONNECTION_SETTINGS_FILE"] = this._paths.ConnectionSettingsFile,
            ["OPENMU_BIND_ADDRESS"] = listenerAddress,
            ["OPENMU_CONTROL_PIPE"] = this.PipeName,
            ["OPENMU_ADMIN_USER"] = "localadmin",
            ["OPENMU_ADMIN_PASSWORD"] = secrets.AdminPanelPassword,
            ["OPENMU_ADMIN_TOTP_SECRET"] = null,
            ["DB_HOST"] = null,
            ["DB_ADMIN_USER"] = null,
            ["DB_ADMIN_PW"] = null,
            ["ASPNETCORE_URLS"] = $"http://{listenerAddress}:{this._settings.AdminPanelPort}",
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["DOTNET_ENVIRONMENT"] = "Production",
            ["Database__AssumeExternallyProvisioned"] = "false",
            ["AdminPanel__Auth__DataProtectionKeyPath"] = this._paths.DataProtectionKeysDirectory,
            ["OPENMU_LOG_DIRECTORY"] = this._paths.LogsDirectory,
            ["Serilog__WriteTo__1__Args__path"] = Path.Combine(this._paths.LogsDirectory, "openmu.log"),
            ["Serilog__MinimumLevel__Override__Microsoft.AspNetCore.Components.Server.Circuits"] = "Error",
        };
    }

    internal static string ResolveAdvertisedAddress(LocalStackSettings settings)
    {
        if (!settings.MobileAccessEnabled)
        {
            return IPAddress.Loopback.ToString();
        }

        if (!string.IsNullOrWhiteSpace(settings.MobileAdvertisedAddress))
        {
            return settings.MobileAdvertisedAddress.Trim();
        }

        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up
                && network.NetworkInterfaceType is not NetworkInterfaceType.Loopback
                && network.NetworkInterfaceType is not NetworkInterfaceType.Tunnel)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork
                    && !IPAddress.IsLoopback(address.Address)
                    && !address.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                .Select(address => new
                {
                    Address = address.Address,
                    Score = ScoreNetwork(network, address.Address),
                }))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Address.ToString(), StringComparer.Ordinal)
            .ToArray();
        return candidates.FirstOrDefault()?.Address.ToString()
            ?? throw new InvalidOperationException("已启用手机访问，但没有检测到可用的局域网 IPv4 地址。请先连接 Wi-Fi/网线，或在设置文件中指定 MobileAdvertisedAddress。");
    }

    internal static string GetGameplayProfileArgument(string profile)
        => profile switch
        {
            "solo" => "-solo",
            "balance-v1-standard" => "-balance-v1:standard",
            "balance-v1-relaxed" => "-balance-v1:relaxed",
            "balance-v1-journey" => "-balance-v1:journey",
            _ => throw new InvalidDataException("Unsupported gameplay profile."),
        };

    private static int ScoreNetwork(NetworkInterface network, IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        var isPrivate = bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
        var interfaceScore = network.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => 100,
            NetworkInterfaceType.Ethernet => 80,
            _ => 0,
        };
        var hasGateway = network.GetIPProperties().GatewayAddresses.Any(gateway =>
            gateway.Address.AddressFamily == AddressFamily.InterNetwork
            && !gateway.Address.Equals(IPAddress.Any));
        return interfaceScore + (isPrivate ? 50 : 0) + (hasGateway ? 20 : 0);
    }

    private static string CreatePipeName(string rootDirectory)
    {
        var identity = OperatingSystem.IsWindows() ? rootDirectory.ToUpperInvariant() : rootDirectory;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return $"OpenMU.Local.{hash[..16]}";
    }

    private static string NormalizeControlPath(string path)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        // Preserve the original Windows GUI channel, whose AppContext path ends in a separator.
        return OperatingSystem.IsWindows() && !Path.EndsInDirectorySeparator(normalized)
            ? normalized + Path.DirectorySeparatorChar
            : normalized;
    }

    private static int EnsureControlResponse(string response, string expectedState)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("state", out var state)
                || state.ValueKind != JsonValueKind.String
                || !string.Equals(state.GetString(), expectedState, StringComparison.Ordinal)
                || !root.TryGetProperty("processId", out var processId)
                || processId.ValueKind != JsonValueKind.Number
                || !processId.TryGetInt32(out var id)
                || id <= 0)
            {
                throw new InvalidDataException("OpenMU 控制通道返回了不符合预期的结果。");
            }

            return id;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("OpenMU 控制通道返回了无效的 JSON 数据。", ex);
        }
    }

    private void EnsurePackagedServerProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            this.EnsurePackagedServerProcess(process);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("OpenMU 控制通道所属进程已经退出。", ex);
        }
    }

    private void EnsurePackagedServerProcess(Process process)
    {
        var actualPath = process.MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(actualPath)
            || !string.Equals(LocalPlatform.ResolveExecutablePath(actualPath), LocalPlatform.ResolveExecutablePath(this._paths.ServerExecutable), LocalPlatform.PathComparison))
        {
            throw new InvalidDataException($"OpenMU 控制通道所属进程不是程序包中的 OpenMU 服务器。实际路径：{actualPath}；预期路径：{this._paths.ServerExecutable}");
        }
    }

    private async Task ForceTerminateVerifiedProcessAsync(Process process, CancellationToken cancellationToken)
    {
        if (process.HasExited)
        {
            return;
        }

        _ = process.SafeHandle;
        this.EnsurePackagedServerProcess(process);
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            return;
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForEndpointAsync(int port, CancellationToken cancellationToken)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(this._settings.ServerStartupTimeoutSeconds);
        do
        {
            if (this._process?.HasExited is true)
            {
                throw new InvalidOperationException($"OpenMU 在启动期间退出，退出代码为 {this._process.ExitCode}。请查看 Data/Logs/openmu.log。");
            }

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", port, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }
        }
        while (DateTime.UtcNow < timeoutAt);

        throw new TimeoutException($"OpenMU 未在设定的超时时间内打开本机回环端口 {port}。");
    }

    private async Task WaitForAttachedProcessToExitAsync(CancellationToken cancellationToken)
    {
        if (this._attachedProcessId is not { } processId)
        {
            throw new InvalidOperationException("OpenMU 控制通道未返回服务器进程标识。");
        }

        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            this._attachedProcessId = null;
            return;
        }

        using (process)
        {
            try
            {
                if (process.HasExited)
                {
                    this._attachedProcessId = null;
                    return;
                }

                this.EnsurePackagedServerProcess(process);
            }
            catch (InvalidOperationException)
            {
                this._attachedProcessId = null;
                return;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                this._attachedProcessId = null;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("OpenMU 未在 60 秒内停止。启动器不能强制结束由其他程序启动的进程。");
            }
        }
    }

    private async Task TerminateFailedStartAsync()
    {
        if (this._process is not { HasExited: false } process)
        {
            return;
        }

        try
        {
            var response = await this._controlClient.SendAsync("shutdown", TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
            _ = EnsureControlResponse(response, "stopping");
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or TimeoutException or OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        process.Dispose();
        this._process = null;
        this._attachedProcessId = null;
    }
}
