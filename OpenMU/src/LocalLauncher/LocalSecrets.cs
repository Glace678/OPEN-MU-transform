// <copyright file="LocalSecrets.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

/// <summary>
/// Contains secrets which are persisted with Windows DPAPI for the current user.
/// </summary>
public sealed class LocalSecrets
{
    /// <summary>Gets or sets the PostgreSQL administrative password.</summary>
    public string DatabaseAdminPassword { get; set; } = string.Empty;

    /// <summary>Gets or sets the database configuration-role password.</summary>
    public string ConfigurationPassword { get; set; } = string.Empty;

    /// <summary>Gets or sets the database account-role password.</summary>
    public string AccountPassword { get; set; } = string.Empty;

    /// <summary>Gets or sets the database friend-role password.</summary>
    public string FriendPassword { get; set; } = string.Empty;

    /// <summary>Gets or sets the database guild-role password.</summary>
    public string GuildPassword { get; set; } = string.Empty;

    /// <summary>Gets or sets the bootstrap administrator password.</summary>
    public string AdminPanelPassword { get; set; } = string.Empty;
}
