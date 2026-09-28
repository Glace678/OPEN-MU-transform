// <copyright file="MobileGmAuthenticationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AdminAuth;

using System.IO;
using System.Net;
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
        var context = CreateContext("192.168.50.4", PackageKey);

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
        var result = await AuthenticateAsync(CreateContext("192.168.50.4", OtherPackageKey)).ConfigureAwait(false);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure, Is.Not.Null);
    }

    /// <summary>A valid key presented by a public direct peer is rejected.</summary>
    [Test]
    public async Task PublicPeerIsRejectedAsync()
    {
        var result = await AuthenticateAsync(CreateContext("203.0.113.20", PackageKey)).ConfigureAwait(false);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Failure, Is.Not.Null);
    }

    /// <summary>Missing credentials produce no identity and cannot accidentally use this scheme.</summary>
    [Test]
    public async Task MissingHeaderProducesNoResultAsync()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;

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

    private sealed class StaticOptionsMonitor : IOptionsMonitor<MobileGmAuthenticationOptions>
    {
        public StaticOptionsMonitor(string packageKey)
        {
            this.CurrentValue = new MobileGmAuthenticationOptions { PackageKey = packageKey };
        }

        public MobileGmAuthenticationOptions CurrentValue { get; }

        public MobileGmAuthenticationOptions Get(string? name) => this.CurrentValue;

        public IDisposable? OnChange(Action<MobileGmAuthenticationOptions, string?> listener) => null;
    }
}
