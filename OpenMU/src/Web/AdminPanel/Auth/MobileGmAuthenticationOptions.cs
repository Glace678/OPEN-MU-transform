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
}
