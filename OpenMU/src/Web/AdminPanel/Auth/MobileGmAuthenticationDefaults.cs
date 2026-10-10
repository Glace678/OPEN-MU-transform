// <copyright file="MobileGmAuthenticationDefaults.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Auth;

using System.Net;

/// <summary>
/// Constants and validation helpers of the restricted mobile GM authentication scheme.
/// </summary>
public static class MobileGmAuthenticationDefaults
{
    /// <summary>Gets the request header which carries the mobile package key.</summary>
    public const string HeaderName = "X-OpenMU-Mobile-Key";

    /// <summary>Gets the environment variable which contains the mobile package key.</summary>
    public const string PackageKeyEnvironmentVariable = "OPENMU_MOBILE_PACKAGE_KEY";

    /// <summary>Gets the environment variable which contains the mobile game account name.</summary>
    public const string AccountNameEnvironmentVariable = "OPENMU_LOCAL_GAME_USERNAME";

    /// <summary>Gets the name of the authentication scheme.</summary>
    internal const string AuthenticationScheme = "OpenMU.MobileGm";

    /// <summary>Gets the authorization policy used exclusively by the mobile GM API.</summary>
    internal const string Policy = "OpenMU.MobileGmOnly";

    /// <summary>Gets the private marker claim issued only by this authentication scheme.</summary>
    internal const string MarkerClaimType = "openmu:mobile-gm";

    /// <summary>Gets the required marker claim value.</summary>
    internal const string MarkerClaimValue = "package";

    /// <summary>Checks the syntax of an unpadded, 32-byte base64url package key.</summary>
    /// <param name="packageKey">The package key.</param>
    /// <returns><c>true</c> when the key has the required syntax; otherwise, <c>false</c>.</returns>
    internal static bool IsValidPackageKey(string? packageKey) =>
        packageKey is { Length: 43 }
        && packageKey.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    /// <summary>
    /// Checks whether the direct peer belongs to an explicitly trusted local network range.
    /// </summary>
    /// <param name="address">The direct peer address from the connection.</param>
    /// <returns><c>true</c> for loopback, RFC1918, link-local or IPv6 ULA addresses.</returns>
    /// <remarks>
    /// <para>
    /// Topology note (L7 security review):
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <b>Direct connection</b> (mobile device on the same LAN, no reverse proxy):
    /// <see cref="Microsoft.AspNetCore.Http.ConnectionInfo.RemoteIpAddress"/> is the real
    /// device IP. This whitelist correctly restricts access to LAN peers.
    /// </item>
    /// <item>
    /// <b>Behind a trusted reverse proxy</b> (nginx/docker/traefik on RFC1918):
    /// the ForwardedHeaders middleware (configured in Startup.cs with KnownNetworks
    /// covering RFC1918) rewrites RemoteIpAddress to the real client IP from
    /// X-Forwarded-For <i>before</i> authentication runs. The whitelist then evaluates
    /// the real client IP. If the real client is on the public internet, it is rejected.
    /// </item>
    /// <item>
    /// <b>Behind an untrusted / misconfigured proxy</b>: RemoteIpAddress stays as the
    /// proxy's RFC1918 address and the whitelist always passes. In this topology the IP
    /// check provides <i>no</i> additional security — the actual boundary is the
    /// 256-bit package key plus the per-IP rate limiter in
    /// <see cref="MobileGmAuthenticationHandler"/>. Operators must not rely on the IP
    /// whitelist as a security control when exposing the GM API through a public proxy.
    /// </item>
    /// </list>
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