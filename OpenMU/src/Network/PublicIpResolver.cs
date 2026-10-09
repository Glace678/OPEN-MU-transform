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
                this._publicIPv4 = await this.InternalGetIPv4Async().ConfigureAwait(false);
                this._lastRequest = DateTime.UtcNow;
            }

            return this._publicIPv4;
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
        return new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    private async ValueTask<IPAddress> InternalGetIPv4Async()
    {
        const string url = "https://api.ipify.org/?format=text";
        this._logger.LogDebug("Start Requesting public ip from {url}", url);
        var response = await HttpClient.GetStringAsync(url).ConfigureAwait(false);

        var match = Regex.Match(response, @".*?(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}).*");
        if (match.Success && IPAddress.TryParse(match.Groups[1].Value, out var address))
        {
            var ipString = address.ToString();
            this._logger.LogDebug("Request of public ip answered with: {ipString}", ipString);
            return address;
        }

        this._logger.LogDebug("Request of public ip answered with unknown format: {response}", response);
        throw new FormatException($"Request of public ip answered with unknown format: {response}");
    }
}