// <copyright file="MobileGmAuthenticationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AdminAuth;

using System.IO;
using System.Net;
using System.Threading;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Tests the isolated mobile GM authentication boundary.
/// </summary>
[TestFixture]
public class MobileGmAuthenticationTests
{
    private static readonly string PackageKey = new('A', 43);
    private static readonly string OtherPackageKey = "B" + new string('A', 42);

    /// <summary>Trusted local and private direct peers are accepted.</summary>
    [TestCase("127.0.0.1")]
    [TestCase("::1")]
    [TestCase("::ffff:127.0.0.1")]
    [TestCase("10.255.0.1")]
    [TestCase("172.16.0.1")]
    [TestCase("172.31.255.254")]
    [TestCase("192.168.1.1")]
    [TestCase("169.254.10.20")]
    [TestCase("fe80::1")]
    [TestCase("fc00::1")]
    [TestCase("fdff::1")]
    public void TrustedDirectAddressIsAllowed(string address)
    {
        Assert.That(MobileGmAuthenticationDefaults.IsAllowedDirectAddress(IPAddress.Parse(address)), Is.True);
    }

    /// <summary>Wildcard, public, multicast and near-miss private addresses are rejected.</summary>
    [TestCase("0.0.0.0")]
    [TestCase("::")]
    [TestCase("8.8.8.8")]
    [TestCase("172.15.255.255")]
    [TestCase("172.32.0.1")]
    [TestCase("192.167.1.1")]
    [TestCase("2001:db8::1")]
    [TestCase("ff02::1")]
    public void UntrustedDirectAddressIsRejected(string address)
    {
        Assert.That(MobileGmAuthenticationDefaults.IsAllowedDirectAddress(IPAddress.Parse(address)), Is.False);
    }

    /// <summary>A missing direct peer address is never treated as trusted.</summary>
    [Test]
    public void MissingDirectAddressIsRejected()
    {
        Assert.That(MobileGmAuthenticationDefaults.IsAllowedDirectAddress(null), Is.False);
    }

    /// <summary>A matching header on a trusted direct connection receives only the mobile marker claim.</summary>
    [Test]
    public async Task CorrectKeyAuthenticatesWithoutAdminRoleAsync()
    {
        // Use a unique IP to avoid rate-limiter state from other tests.
        var ip = NextUniqueIp();
        var context = CreateContext(ip, PackageKey);

        var result = await AuthenticateAsync(context).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Principal!.HasClaim(
                MobileGmAuthenticationDefaults.MarkerClaimType,
                MobileGmAuthenticationDefaults.MarkerClaimValue), Is.True);
            Assert.That(result.Principal.Claims.Any(claim => claim.Type == ClaimTypes.Role), Is.False);
            Assert.That(result.Principal.Identity!.AuthenticationType, Is.EqualTo(MobileGmAuthenticationDefaults.AuthenticationScheme));
        });
    }

    /// <summary>A wrong package key fails even on a trusted network.</summary>
    [Test]
    public async Task WrongKeyIsRejectedAsync()
    {
        var ip = NextUniqueIp();
        var result = await AuthenticateAsync(CreateContext(ip, OtherPackageKey)).ConfigureAwait(false);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure, Is.Not.Null);
    }

    /// <summary>A valid key presented by a public direct peer is rejected.</summary>
    [Test]
    public async Task PublicPeerIsRejectedAsync()
    {
        var ip = NextUntrustedIp();
        var result = await AuthenticateAsync(CreateContext(ip, PackageKey)).ConfigureAwait(false);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure, Is.Not.Null);
        StringAssert.Contains("not trusted", result.Failure?.Message?.ToLowerInvariant() ?? string.Empty);
    }
    /// <summary>
    /// Rejections caused by an untrusted source IP still increment the per-IP failure
    /// counter. After MaxFailedAttempts such rejections the IP is locked out, which
    /// proves the IP-gate failure feeds the rate limiter (not only wrong-key failures).
    /// </summary>
    [Test]
    public async Task UntrustedAddressRejectionCountsTowardLockoutAsync()
    {
        var ip = NextUntrustedIp();
        var options = new MobileGmAuthenticationOptions
        {
            PackageKey = PackageKey,
            MaxFailedAttempts = 2,
            LockoutSeconds = 60,
        };

        // First two attempts: even with the correct key, the untrusted IP is rejected
        // with "not trusted".
        for (var i = 0; i < options.MaxFailedAttempts; i++)
        {
            var r = await AuthenticateWithOptionsAsync(CreateContext(ip, PackageKey), options).ConfigureAwait(false);
            Assert.That(r.Succeeded, Is.False, $"attempt {i + 1} should fail");
            StringAssert.Contains("not trusted", r.Failure?.Message?.ToLowerInvariant() ?? string.Empty);
        }

        // Third attempt: the counter crossed the threshold, so step 1 (lockout) fires
        // before the IP gate is even evaluated.
        var locked = await AuthenticateWithOptionsAsync(CreateContext(ip, PackageKey), options).ConfigureAwait(false);
        Assert.That(locked.Succeeded, Is.False);
        StringAssert.Contains("locked", locked.Failure?.Message?.ToLowerInvariant() ?? string.Empty);
    }

    /// <summary>Missing credentials produce no identity and cannot accidentally use this scheme.</summary>
    [Test]
    public async Task MissingHeaderProducesNoResultAsync()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(NextUniqueIp());

        var result = await AuthenticateAsync(context).ConfigureAwait(false);

        Assert.That(result.None, Is.True);
    }

    /// <summary>The named policy invokes only the mobile scheme, while the admin default does not invoke it.</summary>
    [Test]
    public async Task MobilePolicyIsIndependentFromAdminAuthenticationAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"openmu-mobile-auth-{Guid.NewGuid():N}");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AdminPanel:Auth:DataProtectionKeyPath"] = directory,
                })
                .Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAdminPanelAuth(configuration);
            await using var provider = services.BuildServiceProvider();
            var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

            var mobilePolicy = await policyProvider.GetPolicyAsync(MobileGmAuthenticationDefaults.Policy).ConfigureAwait(false);
            var defaultPolicy = await policyProvider.GetDefaultPolicyAsync().ConfigureAwait(false);

            Assert.That(mobilePolicy, Is.Not.Null);
            Assert.That(mobilePolicy!.AuthenticationSchemes, Is.EqualTo(new[] { MobileGmAuthenticationDefaults.AuthenticationScheme }));
            Assert.That(defaultPolicy.AuthenticationSchemes, Does.Not.Contain(MobileGmAuthenticationDefaults.AuthenticationScheme));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    // --- L7 rate limiting tests ------------------------------------------------

    /// <summary>
    /// After MaxFailedAttempts consecutive wrong-key requests from one IP, further
    /// requests are rejected with a lockout failure — even if the correct key is
    /// presented. This proves the rate limiter fires regardless of key correctness.
    /// </summary>
    [Test]
    public async Task RepeatedFailuresTriggerLockoutAsync()
    {
        var ip = NextUniqueIp();
        var options = new MobileGmAuthenticationOptions
        {
            PackageKey = PackageKey,
            MaxFailedAttempts = 3,
            LockoutSeconds = 60,
        };

        // First 3 failures: rejected for wrong key.
        for (var i = 0; i < options.MaxFailedAttempts; i++)
        {
            var failResult = await AuthenticateWithOptionsAsync(CreateContext(ip, OtherPackageKey), options).ConfigureAwait(false);
            Assert.That(failResult.Succeeded, Is.False, $"failure attempt {i + 1} should be rejected");
        }

        // 4th request: even with the CORRECT key, the IP is locked out.
        var lockedResult = await AuthenticateWithOptionsAsync(CreateContext(ip, PackageKey), options).ConfigureAwait(false);
        Assert.That(lockedResult.Succeeded, Is.False);
        StringAssert.Contains("locked", lockedResult.Failure?.Message?.ToLowerInvariant() ?? string.Empty);
    }

    /// <summary>
    /// Lockout is per-IP: a different source IP is not affected by another IP's failures.
    /// </summary>
    [Test]
    public async Task LockoutIsPerClientIpAsync()
    {
        var attackerIp = NextUniqueIp();
        var legitIp = NextUniqueIp();
        var options = new MobileGmAuthenticationOptions
        {
            PackageKey = PackageKey,
            MaxFailedAttempts = 2,
            LockoutSeconds = 60,
        };

        // Attacker burns through the threshold.
        await AuthenticateWithOptionsAsync(CreateContext(attackerIp, OtherPackageKey), options).ConfigureAwait(false);
        var secondFail = await AuthenticateWithOptionsAsync(CreateContext(attackerIp, OtherPackageKey), options).ConfigureAwait(false);
        Assert.That(secondFail.Succeeded, Is.False);

        // Legit user from a different IP with the correct key still succeeds.
        var legitResult = await AuthenticateWithOptionsAsync(CreateContext(legitIp, PackageKey), options).ConfigureAwait(false);
        Assert.That(legitResult.Succeeded, Is.True);
    }

    /// <summary>
    /// A successful authentication resets the failure counter for that IP.
    /// </summary>
    [Test]
    public async Task SuccessfulAuthResetsFailureCounterAsync()
    {
        var ip = NextUniqueIp();
        var options = new MobileGmAuthenticationOptions
        {
            PackageKey = PackageKey,
            MaxFailedAttempts = 3,
            LockoutSeconds = 60,
        };

        // Two failures, then a success.
        await AuthenticateWithOptionsAsync(CreateContext(ip, OtherPackageKey), options).ConfigureAwait(false);
        await AuthenticateWithOptionsAsync(CreateContext(ip, OtherPackageKey), options).ConfigureAwait(false);
        var okResult = await AuthenticateWithOptionsAsync(CreateContext(ip, PackageKey), options).ConfigureAwait(false);
        Assert.That(okResult.Succeeded, Is.True);

        // Two more failures should NOT trigger lockout (counter was reset by success).
        await AuthenticateWithOptionsAsync(CreateContext(ip, OtherPackageKey), options).ConfigureAwait(false);
        var fifthResult = await AuthenticateWithOptionsAsync(CreateContext(ip, OtherPackageKey), options).ConfigureAwait(false);
        Assert.That(fifthResult.Succeeded, Is.False);
        // Should NOT be locked out — only 2 failures since reset.
        StringAssert.DoesNotContain("locked", fifthResult.Failure?.Message?.ToLowerInvariant() ?? string.Empty);
    }

    private static int _uniqueIpCounter;

    /// <summary>
    /// Returns a unique trusted RFC1918 (10.x.x.x) address per call. Tests that must
    /// reach the package-key comparison use this; the IP whitelist gate then passes
    /// and the failure counter / lockout logic is actually exercised.
    /// </summary>
    private static string NextUniqueIp()
    {
        var n = Interlocked.Increment(ref _uniqueIpCounter);
        return $"10.{(n / 240) % 240}.{n % 240 + 1}";
    }

    /// <summary>
    /// Returns a unique 192.0.2.x (TEST-NET-1) address per call. These are
    /// explicitly untrusted public-style addresses and are used to exercise the
    /// "client address is not trusted" branch.
    /// </summary>
    private static string NextUntrustedIp()
    {
        var n = Interlocked.Increment(ref _uniqueIpCounter);
        return $"192.0.2.{n % 250 + 1}";
    }

    private static DefaultHttpContext CreateContext(string remoteAddress, string key)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        context.Request.Headers[MobileGmAuthenticationDefaults.HeaderName] = key;
        return context;
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(HttpContext context)
    {
        var handler = new MobileGmAuthenticationHandler(
            new StaticOptionsMonitor(PackageKey),
            NullLoggerFactory.Instance,
            UrlEncoder.Default);
        var scheme = new AuthenticationScheme(
            MobileGmAuthenticationDefaults.AuthenticationScheme,
            null,
            typeof(MobileGmAuthenticationHandler));
        await handler.InitializeAsync(scheme, context).ConfigureAwait(false);
        return await handler.AuthenticateAsync().ConfigureAwait(false);
    }

    private static async Task<AuthenticateResult> AuthenticateWithOptionsAsync(HttpContext context, MobileGmAuthenticationOptions options)
    {
        var handler = new MobileGmAuthenticationHandler(
            new StaticOptionsMonitor(options),
            NullLoggerFactory.Instance,
            UrlEncoder.Default);
        var scheme = new AuthenticationScheme(
            MobileGmAuthenticationDefaults.AuthenticationScheme,
            null,
            typeof(MobileGmAuthenticationHandler));
        await handler.InitializeAsync(scheme, context).ConfigureAwait(false);
        return await handler.AuthenticateAsync().ConfigureAwait(false);
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<MobileGmAuthenticationOptions>
    {
        public StaticOptionsMonitor(string packageKey)
        {
            this.CurrentValue = new MobileGmAuthenticationOptions { PackageKey = packageKey };
        }

        public StaticOptionsMonitor(MobileGmAuthenticationOptions options)
        {
            this.CurrentValue = options;
        }

        public MobileGmAuthenticationOptions CurrentValue { get; }

        public MobileGmAuthenticationOptions Get(string? name) => this.CurrentValue;

        public IDisposable? OnChange(Action<MobileGmAuthenticationOptions, string?> listener) => null;
    }
}