// <copyright file="LocalStackSettings.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

/// <summary>
/// Defines runtime settings of the local OpenMU stack.
/// </summary>
public sealed class LocalStackSettings
{
    /// <summary>Gets or sets the gameplay rules selected for the server process.</summary>
    public string GameplayProfile { get; set; } = "solo";

    /// <summary>Gets or sets the last solo profile successfully started after a pre-conversion backup.</summary>
    public int SoloBalanceVersion { get; set; }

    /// <summary>Gets or sets the shop schema version started after a protective backup.</summary>
    public int SoloCashShopVersion { get; set; }

    /// <summary>Gets or sets the PostgreSQL loopback port.</summary>
    public int DatabasePort { get; set; } = 55432;

    /// <summary>Gets or sets the OpenMU admin panel loopback port.</summary>
    public int AdminPanelPort { get; set; } = 5080;

    /// <summary>Gets or sets the game connect server loopback port used for readiness checks.</summary>
    public int ConnectServerPort { get; set; } = 44406;

    /// <summary>Gets or sets a value indicating whether game and admin endpoints accept trusted-LAN clients.</summary>
    public bool MobileAccessEnabled { get; set; }

    /// <summary>Gets or sets an optional IPv4 address advertised to mobile clients; empty selects the best LAN address.</summary>
    public string MobileAdvertisedAddress { get; set; } = string.Empty;

    /// <summary>Gets or sets the package-specific credential shared with the mobile applications.</summary>
    public string MobilePackageKey { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the launcher should start the game after the server is ready.</summary>
    public bool StartGameWhenReady { get; set; } = true;

    /// <summary>Gets or sets whether the local client uses previously saved credentials without a login form.</summary>
    public bool AutomaticGameLogin { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether closing the window keeps the launcher in the notification area.</summary>
    public bool CloseToTray { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the launcher starts with the current Windows user.</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>Gets or sets the number of automatic backups to retain.</summary>
    public int BackupRetentionCount { get; set; } = 10;

    /// <summary>Gets or sets the database startup timeout.</summary>
    public int DatabaseStartupTimeoutSeconds { get; set; } = 60;

    /// <summary>Gets or sets the OpenMU startup timeout.</summary>
    public int ServerStartupTimeoutSeconds { get; set; } = 180;
}
