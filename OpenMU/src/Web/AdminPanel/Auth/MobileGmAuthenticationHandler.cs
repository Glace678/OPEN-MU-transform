// <copyright file="MobileGmAuthenticationHandler.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Auth;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Authenticates the package key of a direct, trusted-network mobile GM request.
/// </summary>
public sealed class MobileGmAuthenticationHandler : AuthenticationHandler<MobileGmAuthenticationOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MobileGmAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">The scheme options.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    public MobileGmAuthenticationHandler(
        IOptionsMonitor<MobileGmAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!this.Request.Headers.TryGetValue(MobileGmAuthenticationDefaults.HeaderName, out var header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (header.Count != 1 || header[0] is not { } presentedKey
            || !MobileGmAuthenticationDefaults.IsValidPackageKey(presentedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("The mobile package key header is malformed."));
        }

        if (!MobileGmAuthenticationDefaults.IsAllowedDirectAddress(this.Context.Connection.RemoteIpAddress))
        {
            this.Logger.LogWarning(
                "Rejected a mobile GM request from untrusted direct address {RemoteIpAddress}.",
                this.Context.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.Fail("The direct client address is not trusted."));
        }

        if (!FixedTimeEquals(this.Options.PackageKey, presentedKey))
        {
            this.Logger.LogWarning(
                "Rejected a mobile GM request from {RemoteIpAddress} because its package key is invalid.",
                this.Context.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.Fail("The mobile package key is invalid."));
        }

        Claim[] claims =
        [
            new(MobileGmAuthenticationDefaults.MarkerClaimType, MobileGmAuthenticationDefaults.MarkerClaimValue),
        ];
        var identity = new ClaimsIdentity(claims, this.Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), this.Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        this.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        this.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    private static bool FixedTimeEquals(string expected, string presented)
    {
        if (!MobileGmAuthenticationDefaults.IsValidPackageKey(expected))
        {
            return false;
        }

        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        var presentedBytes = Encoding.ASCII.GetBytes(presented);
        try
        {
            return CryptographicOperations.FixedTimeEquals(expectedBytes, presentedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(expectedBytes);
            CryptographicOperations.ZeroMemory(presentedBytes);
        }
    }
}
