// <copyright file="MobileGmAuthenticationOptions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Auth;

using Microsoft.AspNetCore.Authentication;

/// <summary>
/// Options of the restricted mobile GM authentication scheme.
/// </summary>
public sealed class MobileGmAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>Gets or sets the expected package key.</summary>
    internal string PackageKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of consecutive failed auth attempts from one
    /// client IP before that IP is locked out. Defaults to 5.
    /// </summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets how long (in seconds) a client IP stays locked out after
    /// exceeding <see cref="MaxFailedAttempts"/>. Defaults to 300 (5 minutes).
    /// </summary>
    public int LockoutSeconds { get; set; } = 300;
}