// <copyright file="PublicIpResolver.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network;

using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;

/// <summary>
/// Resolves the own ip address by calling an external API to get the public <see cref="IPAddress"/>.
/// </summary>
public class PublicIpResolver : IIpAddressResolver
{
    private readonly ILogger<PublicIpResolver> _logger;
    private readonly TimeSpan _maximumCachedAddressLifetime = new(0, 5, 0);
    private IPAddress? _publicIPv4;
    private DateTime _lastRequest = DateTime.MinValue;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="PublicIpResolver"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PublicIpResolver(ILogger<PublicIpResolver> logger)
    {
        this._logger = logger;
    }

    /// <summary>
    /// Gets the public IPv4 address with the help of the following api: https://www.ipify.org/.
    /// </summary>
    /// <returns>The public IPv4 address.</returns>
    public async ValueTask<IPAddress> ResolveIPv4Async()
    {
        if (this._publicIPv4 is not null && this._lastRequest + this._maximumCachedAddressLifetime >= DateTime.UtcNow)
        {
            return this._publicIPv4;
        }

        // Coalesce concurrent refresh requests, so only one call hits the external API. (Code5#4)
        await this._refreshLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (this._publicIPv4 is null || this._lastRequest + this._maximumCachedAddressLifetime < DateTime.UtcNow)
            {
                try
                {
                    this._publicIPv4 = await this.InternalGetIPv4Async().ConfigureAwait(false);
                    this._lastRequest = DateTime.UtcNow;
                }
                catch (Exception ex) when (this._publicIPv4 is not null)
                {
                    this._logger.LogWarning(ex, "Refreshing public IP failed; keeping previously cached address {cachedIp}.", this._publicIPv4);
                }
            }

            return this._publicIPv4!;
        }
        finally
        {
            this._refreshLock.Release();
        }
    }

    // Reuse one HttpClient (creating one per call leaks sockets) and bound the time
    // an unresponsive external service can stall a connection setup.
    private static readonly System.Net.Http.HttpClient HttpClient = CreateHttpClient();

    private static System.Net.Http.HttpClient CreateHttpClient()
    {
        var handler = new System.Net.Http.SocketsHttpHandler
        {
            AllowAutoRedirect = false,
        };
        return new System.Net.Http.HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(10),
            MaxResponseContentBufferSize = 4096,
        };
    }

    private static bool IsValidPublicIPv4(IPAddress address)
    {
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] switch
        {
            0 => false,
            10 => false,
            127 => false,
            169 when bytes[1] == 254 => false,
            172 when bytes[1] is >= 16 and <= 31 => false,
            192 when bytes[1] == 168 => false,
            >= 224 => false,
            _ => true,
        };
    }

    private async ValueTask<IPAddress> InternalGetIPv4Async()
    {
        const string url = "https://api.ipify.org/?format=text";
        this._logger.LogDebug("Start Requesting public ip from {url}", url);
        var response = await HttpClient.GetStringAsync(url).ConfigureAwait(false);
        var trimmed = response.Trim();

        if (Regex.IsMatch(trimmed, @"^\d{1,3}(?:\.\d{1,3}){3}$", RegexOptions.CultureInvariant)
            && IPAddress.TryParse(trimmed, out var address)
            && IsValidPublicIPv4(address))
        {
            var ipString = address.ToString();
            this._logger.LogDebug("Request of public ip answered with: {ipString}", ipString);
            return address;
        }

        var safeResponse = response.Length > 256 ? response[..256] + "..." : response;
        this._logger.LogDebug("Request of public ip answered with unknown or non-public format: {response}", safeResponse);
        throw new FormatException($"Request of public ip answered with unknown format: {safeResponse}");
    }
}