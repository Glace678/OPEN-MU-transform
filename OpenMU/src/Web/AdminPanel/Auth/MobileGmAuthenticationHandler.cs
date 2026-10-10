// <copyright file="MobileGmAuthenticationHandler.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Auth;

using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Authenticates the package key of a mobile GM request.
/// </summary>
/// <remarks>
/// <para>
/// Security model (L7 review):
/// </para>
/// <list type="number">
/// <item>
/// <b>Primary boundary</b>: the 256-bit package key, compared in constant time.
/// The key is infeasible to brute-force online, but rate limiting is layered on
/// top to slow down dictionary/probing attacks and to make anomalous traffic
/// visible in the logs.
/// </item>
/// <item>
/// <b>Secondary boundary</b>: the LAN/RFC1918 IP whitelist. This only works as
/// intended when the request arrives directly (or through a trusted reverse proxy
/// that correctly forwards the real client IP via X-Forwarded-For AND is listed
/// in ForwardedHeaders:KnownNetworks). When the app sits behind a proxy whose
/// address is itself RFC1918, the whitelist check sees the proxy IP and always
/// passes — it must not be treated as protection against internet clients.
/// </item>
/// <item>
/// <b>Rate limiting</b>: per-source-IP fixed-window lockout. After
/// <see cref="MobileGmAuthenticationOptions.MaxFailedAttempts"/> consecutive
/// failures, the source IP is locked for
/// <see cref="MobileGmAuthenticationOptions.LockoutSeconds"/>.
/// </item>
/// </list>
/// </remarks>
public sealed class MobileGmAuthenticationHandler : AuthenticationHandler<MobileGmAuthenticationOptions>
{
    /// <summary>
    /// In-memory failure tracker keyed by client IP string. Static because the
    /// handler instance is created per request; the lockout state must persist
    /// across requests for the same source IP.
    /// </summary>
    private static readonly ConcurrentDictionary<string, (int Failures, DateTime LockedUntil)> FailureTracker = new();

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
        // ForwardedHeaders middleware runs before authentication in the pipeline
        // (Startup.cs: app.UseForwardedHeaders() at line 120, app.UseAdminPanelAuth()
        // at line 140). When the direct peer is in KnownNetworks (RFC1918),
        // RemoteIpAddress has already been rewritten to the real client IP from
        // X-Forwarded-For. When the direct peer is NOT trusted (e.g. a misconfigured
        // proxy), RemoteIpAddress stays as the proxy address — see the class remarks.
        var clientIp = this.Context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // 1. Lockout check — fail closed before doing any crypto work.
        if (IsLockedOut(clientIp, this.Options))
        {
            this.Logger.LogWarning(
                "Rejected a mobile GM request from {ClientIp}: IP is locked out due to repeated failures.",
                clientIp);
            return Task.FromResult(AuthenticateResult.Fail("Too many failed attempts; this client address is temporarily locked out."));
        }

        // 2. Header presence + format.
        if (!this.Request.Headers.TryGetValue(MobileGmAuthenticationDefaults.HeaderName, out var header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (header.Count != 1 || header[0] is not { } presentedKey
            || !MobileGmAuthenticationDefaults.IsValidPackageKey(presentedKey))
        {
            RecordFailure(clientIp, this.Options);
            return Task.FromResult(AuthenticateResult.Fail("The mobile package key header is malformed."));
        }

        // 3. IP whitelist (secondary boundary; see class remarks for topology caveats).
        if (!MobileGmAuthenticationDefaults.IsAllowedDirectAddress(this.Context.Connection.RemoteIpAddress))
        {
            RecordFailure(clientIp, this.Options);
            this.Logger.LogWarning(
                "Rejected a mobile GM request from untrusted address {ClientIp}.",
                clientIp);
            return Task.FromResult(AuthenticateResult.Fail("The client address is not trusted."));
        }

        // 4. Constant-time key comparison (primary boundary).
        if (!FixedTimeEquals(this.Options.PackageKey, presentedKey))
        {
            RecordFailure(clientIp, this.Options);
            this.Logger.LogWarning(
                "Rejected a mobile GM request from {ClientIp}: invalid package key.",
                clientIp);
            return Task.FromResult(AuthenticateResult.Fail("The mobile package key is invalid."));
        }

        // 5. Success — reset the failure counter for this IP.
        ResetFailureCount(clientIp);

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

    private static bool IsLockedOut(string clientIp, MobileGmAuthenticationOptions options)
    {
        if (!FailureTracker.TryGetValue(clientIp, out var entry))
        {
            return false;
        }

        // LockedUntil == DateTime.MinValue means failures have accumulated but the
        // threshold has not been reached yet — there is no active lockout to expire.
        // Treating MinValue as an expired lockout (the previous logic) wiped the
        // failure counter on every request, so lockout never triggered.
        if (entry.LockedUntil == DateTime.MinValue)
        {
            return false;
        }

        if (entry.LockedUntil > DateTime.UtcNow)
        {
            return true;
        }

        // Lockout expired — let the request through; failures will re-accumulate.
        FailureTracker.TryRemove(clientIp, out _);
        return false;
    }

    private static void RecordFailure(string clientIp, MobileGmAuthenticationOptions options)
    {
        var entry = FailureTracker.AddOrUpdate(
            clientIp,
            addValue: (1, DateTime.MinValue),
            updateValueFactory: (_, current) =>
            {
                var failures = current.Failures + 1;
                var lockedUntil = failures >= options.MaxFailedAttempts
                    ? DateTime.UtcNow.AddSeconds(options.LockoutSeconds)
                    : current.LockedUntil;
                return (failures, lockedUntil);
            });

        // If we just crossed the threshold on the very first failure (unlikely but possible
        // if MaxFailedAttempts is 1), set the lockout explicitly.
        if (entry.Failures >= options.MaxFailedAttempts && entry.LockedUntil == DateTime.MinValue)
        {
            FailureTracker.TryUpdate(
                clientIp,
                (entry.Failures, DateTime.UtcNow.AddSeconds(options.LockoutSeconds)),
                entry);
        }
    }

    private static void ResetFailureCount(string clientIp)
    {
        FailureTracker.TryRemove(clientIp, out _);
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