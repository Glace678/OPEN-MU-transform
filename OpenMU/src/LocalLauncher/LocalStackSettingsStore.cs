// <copyright file="LocalStackSettingsStore.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using System.Net;
using System.Text.Json;

/// <summary>
/// Loads and atomically saves non-secret launcher settings.
/// </summary>
public sealed class LocalStackSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalStackSettingsStore"/> class.
    /// </summary>
    /// <param name="filePath">The settings file path.</param>
    public LocalStackSettingsStore(string filePath)
    {
        this._filePath = filePath;
    }

    /// <summary>Loads settings or returns validated defaults.</summary>
    /// <returns>The settings.</returns>
    public LocalStackSettings Load()
    {
        var settings = File.Exists(this._filePath)
            ? JsonSerializer.Deserialize<LocalStackSettings>(File.ReadAllBytes(this._filePath)) ?? new LocalStackSettings()
            : new LocalStackSettings();
        Validate(settings);
        return settings;
    }

    /// <summary>Saves validated settings atomically.</summary>
    /// <param name="settings">The settings.</param>
    public void Save(LocalStackSettings settings)
    {
        Validate(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(this._filePath)!);
        var temporaryPath = this._filePath + ".tmp";
        File.WriteAllBytes(temporaryPath, JsonSerializer.SerializeToUtf8Bytes(settings, SerializerOptions));
        File.Move(temporaryPath, this._filePath, overwrite: true);
    }

    private static void Validate(LocalStackSettings settings)
    {
        if (settings.GameplayProfile is not ("solo" or "balance-v1-standard" or "balance-v1-relaxed" or "balance-v1-journey"))
        {
            throw new InvalidDataException("Unsupported gameplay profile.");
        }

        if (settings.SoloBalanceVersion is < 0 or > 1 || settings.SoloCashShopVersion is < 0 or > 1)
        {
            throw new InvalidDataException("Unsupported solo profile or shop migration version.");
        }

        var fixedServerPorts = new[] { 44405, 44406, 55901, 55902, 55980 };
        foreach (var port in new[] { settings.DatabasePort, settings.AdminPanelPort, settings.ConnectServerPort })
        {
            if (port is < 1024 or > ushort.MaxValue)
            {
                throw new InvalidDataException($"本地端口 {port} 必须在 1024 到 65535 之间。");
            }
        }

        if (new[] { settings.DatabasePort, settings.AdminPanelPort, settings.ConnectServerPort }.Distinct().Count() != 3)
        {
            throw new InvalidDataException("数据库、管理后台和连接服务器必须使用不同的端口。");
        }

        if (settings.ConnectServerPort != 44406)
        {
            throw new InvalidDataException("随包提供的 2.04d 客户端要求 ConnectServerPort 保持为 44406。");
        }

        if (!string.IsNullOrWhiteSpace(settings.MobileAdvertisedAddress)
            && (!IPAddress.TryParse(settings.MobileAdvertisedAddress, out var mobileAddress)
                || mobileAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork
                || IPAddress.IsLoopback(mobileAddress)
                || mobileAddress.Equals(IPAddress.Any)))
        {
            throw new InvalidDataException("手机访问地址必须是非回环 IPv4 地址，或留空以自动检测。");
        }

        if (settings.MobileAccessEnabled && !LocalGameLogin.IsValidMobilePackageKey(settings.MobilePackageKey))
        {
            throw new InvalidDataException("启用手机访问时，MobilePackageKey 必须是 43 位 base64url 密钥。");
        }

        if (fixedServerPorts.Contains(settings.DatabasePort) || fixedServerPorts.Contains(settings.AdminPanelPort))
        {
            throw new InvalidDataException("数据库和管理后台端口不能与 OpenMU 的固定游戏服务端口重复。");
        }

        if (settings.BackupRetentionCount is < 1 or > 100)
        {
            throw new InvalidDataException("备份保留数量必须在 1 到 100 之间。");
        }

        if (settings.DatabaseStartupTimeoutSeconds is < 5 or > 600)
        {
            throw new InvalidDataException("数据库启动超时必须在 5 到 600 秒之间。");
        }

        if (settings.ServerStartupTimeoutSeconds is < 10 or > 1800)
        {
            throw new InvalidDataException("服务器启动超时必须在 10 到 1800 秒之间。");
        }
    }
}
