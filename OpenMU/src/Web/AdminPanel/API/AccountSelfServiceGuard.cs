// <copyright file="AccountSelfServiceGuard.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.API;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Names of the rate limiting policies applied to the anonymous account self-service API.
/// </summary>
public static class AccountSelfServicePolicies
{
    /// <summary>Policy for registration (account creation).</summary>
    public const string Registration = "public-registration-create";

    /// <summary>Policy for every credential-verifying endpoint (BCrypt is expensive).</summary>
    public const string CredentialVerification = "public-registration-credential";
}

/// <summary>
/// Abuse protection for the anonymous account self-service API: a per-account
/// failure lockout, and the maintenance token used by password resets that present
/// no ownership credential.
/// </summary>
/// <remarks>
/// All state is in-memory and per-process, which is intentional: the API is meant for
/// the single local server. A multi-instance deployment must front this with its own
/// limiter (reverse proxy) before the endpoints become publicly reachable.
/// </remarks>
public sealed class AccountSelfServiceGuard
{
    private const int MaximumFailedAttempts = 10;

    private const int MaintenanceTokenEntropyBytes = 32;

    private const string TemporaryTokenFileSuffix = ".tmp";

    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FailureMemory = TimeSpan.FromHours(2);

    private readonly IConfiguration _configuration;
    private readonly object _lock = new();
    private readonly Dictionary<string, FailedAttempts> _failedAttempts = new(StringComparer.OrdinalIgnoreCase);

    // The token file is read once and re-read when it changes on disk (rotation).
    private string? _maintenanceToken;
    private DateTime _maintenanceTokenStamp;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountSelfServiceGuard"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    public AccountSelfServiceGuard(IConfiguration configuration)
    {
        this._configuration = configuration;
    }

    /// <summary>Gets a value indicating whether public (internet-facing) mode is configured.</summary>
    /// <remarks>
    /// Public mode neutralizes responses that would otherwise let an attacker
    /// enumerate existing account names.
    /// </remarks>
    public bool IsPublicMode =>
        bool.TryParse(this._configuration["AccountSelfService:PublicMode"], out var publicMode) && publicMode;

    private string MaintenanceTokenPath
    {
        get
        {
            var configured = this._configuration["AccountSelfService:MaintenanceTokenFile"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return Path.GetFullPath(configured);
            }

            return Path.Combine(Directory.GetCurrentDirectory(), "Data", "Keys", "maintenance-token.txt");
        }
    }

    /// <summary>Gets whether the account is currently locked out after repeated failures.</summary>
    /// <param name="loginName">The account login name.</param>
    /// <returns><c>true</c> when further attempts must be rejected without verification.</returns>
    public bool IsLockedOut(string? loginName)
    {
        if (string.IsNullOrWhiteSpace(loginName))
        {
            return false;
        }

        lock (this._lock)
        {
            return this._failedAttempts.TryGetValue(loginName, out var attempts)
                && attempts.LockedUntil > DateTimeOffset.UtcNow;
        }
    }

    /// <summary>Counts one failed credential verification for the account.</summary>
    /// <param name="loginName">The account login name.</param>
    public void RegisterFailedAttempt(string? loginName)
    {
        if (string.IsNullOrWhiteSpace(loginName))
        {
            return;
        }

        lock (this._lock)
        {
            this.PruneFailuresLocked();
            if (!this._failedAttempts.TryGetValue(loginName, out var attempts))
            {
                attempts = FailedAttempts.None;
            }

            var now = DateTimeOffset.UtcNow;
            var failures = attempts.Failures + 1;
            var lockedUntil = failures >= MaximumFailedAttempts
                ? now.Add(LockoutDuration)
                : attempts.LockedUntil;
            this._failedAttempts[loginName] = new FailedAttempts(failures, lockedUntil, now);
        }
    }

    /// <summary>Clears the failure counter after a successful verification.</summary>
    /// <param name="loginName">The account login name.</param>
    public void RegisterSuccessfulAttempt(string? loginName)
    {
        if (string.IsNullOrWhiteSpace(loginName))
        {
            return;
        }

        lock (this._lock)
        {
            this._failedAttempts.Remove(loginName);
        }
    }

    /// <summary>
    /// Validates the supplied maintenance token against the server's token file.
    /// </summary>
    /// <param name="suppliedToken">The token from the request body.</param>
    /// <returns><c>true</c> when the token matches.</returns>
    public bool IsMaintenanceTokenValid(string? suppliedToken)
    {
        if (string.IsNullOrWhiteSpace(suppliedToken))
        {
            return false;
        }

        var expected = this.ReadMaintenanceToken();
        if (string.IsNullOrEmpty(expected))
        {
            return false;
        }

        return FixedTimeEquals(suppliedToken.Trim(), expected);
    }

    // The remote peer. We never read a forwarded header ourselves: they can be spoofed by the caller.
    // Behind the trusted reverse proxy, the ForwardedHeaders middleware already validates
    // X-Forwarded-For (KnownProxies/KnownNetworks) and rewrites RemoteIpAddress to the real client,
    // so this is the actual client IP in both direct and portal deployments.
    internal static string PartitionKey(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.ToString();
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private void PruneFailuresLocked()
    {
        if (this._failedAttempts.Count == 0)
        {
            return;
        }

        var cutoff = DateTimeOffset.UtcNow.Subtract(FailureMemory);
        List<string>? stale = null;
        foreach (var pair in this._failedAttempts)
        {
            var attempts = pair.Value;

            // Drop the entry only when no lockout is active (LockedUntil == MinValue
            // means it was never locked out) and the last failure is older than the memory window.
            if (attempts.LockedUntil <= DateTimeOffset.UtcNow && attempts.LastFailure < cutoff)
            {
                (stale ??= new List<string>()).Add(pair.Key);
            }
        }

        if (stale is null)
        {
            return;
        }

        foreach (var loginName in stale)
        {
            this._failedAttempts.Remove(loginName);
        }
    }

    private string? ReadMaintenanceToken()
    {
        var path = this.MaintenanceTokenPath;
        try
        {
            if (!File.Exists(path))
            {
                return this.CreateMaintenanceToken(path);
            }

            var stamp = File.GetLastWriteTimeUtc(path);
            if (this._maintenanceToken is null || stamp != this._maintenanceTokenStamp)
            {
                this._maintenanceToken = File.ReadAllText(path).Trim();
                this._maintenanceTokenStamp = stamp;
            }

            return this._maintenanceToken;
        }
        catch
        {
            // A missing or unreadable token file must fail closed.
            return null;
        }
    }

    private string? CreateMaintenanceToken(string path)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(MaintenanceTokenEntropyBytes));
            var temporaryPath = path + TemporaryTokenFileSuffix;
            File.WriteAllText(temporaryPath, token);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            File.Move(temporaryPath, path, overwrite: true);

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            this._maintenanceToken = token;
            this._maintenanceTokenStamp = File.GetLastWriteTimeUtc(path);
            return token;
        }
        catch
        {
            return null;
        }
    }

    private sealed record FailedAttempts(int Failures, DateTimeOffset LockedUntil, DateTimeOffset LastFailure)
    {
        public static FailedAttempts None { get; } = new(0, DateTimeOffset.MinValue, DateTimeOffset.MinValue);
    }
}

/// <summary>
/// Registers the account self-service abuse protection.
/// </summary>
public static class AccountSelfServiceExtensions
{
    /// <summary>
    /// Registers the rate limiting policies and the guard itself.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, to allow further configuration.</returns>
    public static IServiceCollection AddAccountSelfServiceGuard(this IServiceCollection services)
    {
        services.AddSingleton<AccountSelfServiceGuard>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AccountSelfServicePolicies.Registration, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    AccountSelfServiceGuard.PartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                    }));

            // BCrypt verification is deliberately expensive, so credential endpoints
            // get a tighter budget than registration.
            options.AddPolicy(AccountSelfServicePolicies.CredentialVerification, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    AccountSelfServiceGuard.PartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync(
                    "{\"success\":false,\"error\":\"too_many_requests\",\"message\":\"Too many attempts. Please try again later.\"}",
                    cancellationToken).ConfigureAwait(false);
            };
        });

        return services;
    }
}
