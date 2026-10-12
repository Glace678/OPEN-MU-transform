// <copyright file="MobileGmAuthenticationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AdminAuth;

using System.IO;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.Web.AdminPanel.Auth;

/// <summary>
/// Tests the mobile GM API boundary after F1 (M1-01 / M2-H1): the static,
/// package-distributed bearer scheme was removed; the API now requires an
/// administrator cookie session.
/// </summary>
[TestFixture]
public class MobileGmAuthenticationTests
{
    /// <summary>Trusted local and private direct peers are accepted by the login address guard.</summary>
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

    /// <summary>
    /// The mobile GM policy no longer references the removed static scheme: it uses the
    /// admin-panel cookie scheme, requires an authenticated user and the Administrator role.
    /// </summary>
    [Test]
    public async Task MobilePolicyUsesAdminCookieAndRequiresAdministratorAsync()
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

            Assert.That(mobilePolicy, Is.Not.Null);

            // The only accepted authentication scheme is the admin-panel cookie session.
            Assert.That(mobilePolicy!.AuthenticationSchemes, Is.EqualTo(new[] { CookieAuthenticationDefaults.AuthenticationScheme }));

            // The policy must demand the Administrator role (not merely an authenticated user).
            // .NET 10 emits RolesAuthorizationRequirement (older versions use ClaimsAuthorizationRequirement);
            // inspect it via reflection so the assertion does not depend on the exact type/namespace.
            var roleRequirement = mobilePolicy.Requirements
                .FirstOrDefault(r => r.GetType().Name.Contains("Role", StringComparison.OrdinalIgnoreCase));
            Assert.That(roleRequirement, Is.Not.Null, "policy is missing a role authorization requirement.");
            var allowedRoles = roleRequirement!.GetType().GetProperties()
                .Where(p => typeof(IEnumerable<string>).IsAssignableFrom(p.PropertyType))
                .SelectMany(p => (IEnumerable<string>?)p.GetValue(roleRequirement) ?? Array.Empty<string>())
                .ToArray();
            Assert.That(allowedRoles, Does.Contain(AdminRoles.Administrator),
                "role requirement does not include the Administrator role.");

            // The retired static scheme name must not be referenced by the policy.
            Assert.That(mobilePolicy.AuthenticationSchemes, Does.Not.Contain("OpenMU.MobileGm"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
