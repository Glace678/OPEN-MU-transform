// <copyright file="MobileGmAuthenticationDefaults.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Auth;

using System.Net;

/// <summary>
/// Constants and validation helpers for the restricted mobile GM API boundary.
/// </summary>
/// <remarks>
/// F1 (M1-01 / M2-H1, 2026-10-12): the previous static, package-distributed bearer
/// scheme (the <c>X-OpenMU-Mobile-Key</c> header authenticated by a key baked into every
/// APK/HAP) was removed. Decompiling a package exposed that key and let any LAN peer call
/// the grant endpoints. The mobile GM API now reuses the admin-panel cookie session: the
/// packaged GM app signs in at <c>POST /api/mobile-gm/login</c> with an administrator
/// account (password + TOTP when enabled), and the grant endpoints require that cookie
/// session plus the Administrator role.
/// </remarks>
public static class MobileGmAuthenticationDefaults
{
    /// <summary>Gets the environment variable which contains the mobile package key.</summary>
    public const string PackageKeyEnvironmentVariable = "OPENMU_MOBILE_PACKAGE_KEY";

    /// <summary>Gets the environment variable which contains the mobile game account name.</summary>
    public const string AccountNameEnvironmentVariable = "OPENMU_LOCAL_GAME_USERNAME";

    /// <summary>Gets the authorization policy used exclusively by the mobile GM API.</summary>
    internal const string Policy = "OpenMU.MobileGmOnly";

    /// <summary>
    /// Checks whether the direct peer belongs to an explicitly trusted local network range.
    /// </summary>
    /// <param name="address">The direct peer address from the connection.</param>
    /// <returns><c>true</c> for loopback, RFC1918, link-local or IPv6 ULA addresses.</returns>
    /// <remarks>
    /// This is an additional, network-level guard on the login endpoint. It is not the
    /// primary boundary: credential brute force is covered by ASP.NET Core Identity lockout
    /// and the grant endpoints additionally require an authenticated Administrator cookie
    /// session. Behind a correctly configured reverse proxy the ForwardedHeaders middleware
    /// rewrites <see cref="Microsoft.AspNetCore.Http.ConnectionInfo.RemoteIpAddress"/> to the
    /// real client IP before this check runs.
    /// </remarks>
    internal static bool IsAllowedDirectAddress(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        if (bytes.Length == 4)
        {
            return bytes[0] == 10
                   || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                   || (bytes[0] == 192 && bytes[1] == 168)
                   || (bytes[0] == 169 && bytes[1] == 254);
        }

        return address.IsIPv6LinkLocal || (bytes.Length == 16 && (bytes[0] & 0xFE) == 0xFC);
    }
}
