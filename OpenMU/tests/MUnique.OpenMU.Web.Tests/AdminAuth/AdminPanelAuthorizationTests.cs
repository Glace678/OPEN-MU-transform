// <copyright file="AdminPanelAuthorizationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests.AdminAuth;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MUnique.OpenMU.Persistence.AdminAuth;
using MUnique.OpenMU.Web.AdminPanel.Auth;
using NUnit.Framework;

/// <summary>
/// Drives the real authorization handlers (<see cref="AdminAccessRequirementHandler"/> and
/// <see cref="AdminSetupRequirementHandler"/>) with real principals, covering the fix of the
/// anonymous-accessible admin edit endpoints (79-01), the zero-user setup whitelist (79-02) and the
/// viewer/operator/administrator action grading (79-03).
/// </summary>
[TestFixture]
public class AdminPanelAuthorizationTests
{
    private InMemoryAdminUserRepository _repository = null!;
    private ServiceProvider _provider = null!;
    private AdminUserAvailabilityService _availability = null!;

    /// <summary>
    /// Sets up a fresh empty user repository and the real auth services.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        this._repository = new InMemoryAdminUserRepository();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddDataProtection();
        services.AddSingleton<IAdminUserRepository>(this._repository);
        services.AddSingleton(Options.Create(new AdminPanelAuthOptions()));
        services.AddSingleton<AdminUserSecretProtector>();
        services.AddSingleton<Microsoft.AspNetCore.Identity.IPasswordHasher<AdminUser>, BCryptPasswordHasher>();
        services.AddSingleton<BootstrapAdminUserProvider>();
        this._provider = services.BuildServiceProvider();
        this._availability = new AdminUserAvailabilityService(this._repository, this._provider.GetRequiredService<BootstrapAdminUserProvider>());
    }

    /// <summary>
    /// Disposes the service provider.
    /// </summary>
    [TearDown]
    public void TearDown()
    {
        this._provider.Dispose();
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal Principal(string role)
    {
        // Roles are cumulative in this system (an administrator is also an operator and viewer),
        // so the issued identity carries claims for every effective role, mirroring the real login.
        var identity = new ClaimsIdentity("TestScheme");
        identity.AddClaim(new Claim(ClaimTypes.Name, "tester"));
        foreach (var effectiveRole in AdminRoles.GetEffectiveRoles(role))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, effectiveRole));
        }

        return new ClaimsPrincipal(identity);
    }

    private async Task<bool> SucceedsAsync(ClaimsPrincipal user, IAuthorizationRequirement requirement)
    {
        var requirements = new IAuthorizationRequirement[] { requirement };
        var context = new AuthorizationHandlerContext(requirements, user, null);

        switch (requirement)
        {
            case AdminAccessRequirement access:
                await new AdminAccessRequirementHandler().HandleAsync(context);
                break;
            case AdminSetupRequirement:
                await new AdminSetupRequirementHandler(this._availability).HandleAsync(context);
                break;
        }

        return context.HasSucceeded;
    }

    /// <summary>
    /// In the unprovisioned (zero-user) setup window an anonymous caller must NOT satisfy any
    /// role-based or even the default policy - not the admin/operator/viewer pages, not the API,
    /// not the log files. Baseline (old blanket setup bypass) failed this.
    /// </summary>
    [Test]
    public async Task AnonymousDeniedForEveryProtectedResourceDuringSetupAsync()
    {
        await this._availability.AnyUserExistsAsync();
        Assert.That(await this._availability.IsConfirmedEmptyAsync(), Is.True, "precondition: zero-user setup window should be open");

        Assert.That(await this.SucceedsAsync(Anonymous(), new AdminAccessRequirement(AdminRoles.Administrator)), Is.False, "anonymous must not reach administrator pages");
        Assert.That(await this.SucceedsAsync(Anonymous(), new AdminAccessRequirement(AdminRoles.Operator)), Is.False, "anonymous must not reach operator pages");
        Assert.That(await this.SucceedsAsync(Anonymous(), new AdminAccessRequirement(AdminRoles.Viewer)), Is.False, "anonymous must not reach viewer pages");
        Assert.That(await this.SucceedsAsync(Anonymous(), new AdminAccessRequirement()), Is.False, "anonymous must not satisfy the default policy (closes the /logs leak)");
    }

    /// <summary>
    /// The only thing anonymous may reach during the setup window is the setup wizard itself.
    /// </summary>
    [Test]
    public async Task AnonymousAllowedOnlyOnSetupPolicyDuringSetupAsync()
    {
        await this._availability.AnyUserExistsAsync();
        Assert.That(await this.SucceedsAsync(Anonymous(), new AdminSetupRequirement()), Is.True);
    }

    /// <summary>
    /// Once a user exists, the setup policy must switch to administrator-only and anonymous must be
    /// rejected everywhere.
    /// </summary>
    [Test]
    public async Task SetupPolicyLockedDownAfterFirstUserAsync()
    {
        await this._repository.AddAsync(new AdminUser { LoginName = "admin", Roles = AdminRoles.Administrator });
        await this._availability.AnyUserExistsAsync();
        Assert.That(await this._availability.IsConfirmedEmptyAsync(), Is.False, "precondition: a user exists, setup window closed");

        Assert.That(await this.SucceedsAsync(Anonymous(), new AdminSetupRequirement()), Is.False, "anonymous must not reach setup after provisioning");
        Assert.That(await this.SucceedsAsync(Principal(AdminRoles.Viewer), new AdminSetupRequirement()), Is.False, "viewer must not reach setup");
        Assert.That(await this.SucceedsAsync(Principal(AdminRoles.Administrator), new AdminSetupRequirement()), Is.True, "administrator may reach setup");
    }

    /// <summary>
    /// Role grading in normal (provisioned) mode: viewer reads, operator acts, admin configures.
    /// </summary>
    [Test]
    public async Task RoleGradingInProvisionedModeAsync()
    {
        await this._repository.AddAsync(new AdminUser { LoginName = "admin", Roles = AdminRoles.Administrator });
        await this._availability.AnyUserExistsAsync();

        Assert.That(await this.SucceedsAsync(Anonymous(), new AdminAccessRequirement(AdminRoles.Viewer)), Is.False);

        var viewer = Principal(AdminRoles.Viewer);
        Assert.That(await this.SucceedsAsync(viewer, new AdminAccessRequirement(AdminRoles.Viewer)), Is.True, "viewer may view");
        Assert.That(await this.SucceedsAsync(viewer, new AdminAccessRequirement(AdminRoles.Operator)), Is.False, "viewer must not act (kick/stop/broadcast)");
        Assert.That(await this.SucceedsAsync(viewer, new AdminAccessRequirement(AdminRoles.Administrator)), Is.False, "viewer must not edit config/users");

        var op = Principal(AdminRoles.Operator);
        Assert.That(await this.SucceedsAsync(op, new AdminAccessRequirement(AdminRoles.Viewer)), Is.True);
        Assert.That(await this.SucceedsAsync(op, new AdminAccessRequirement(AdminRoles.Operator)), Is.True, "operator may act");
        Assert.That(await this.SucceedsAsync(op, new AdminAccessRequirement(AdminRoles.Administrator)), Is.False, "operator must not edit config/users");

        var admin = Principal(AdminRoles.Administrator);
        Assert.That(await this.SucceedsAsync(admin, new AdminAccessRequirement(AdminRoles.Administrator)), Is.True, "administrator may configure");
    }

    /// <summary>
    /// The generic reflection editor (79-04) only accepts whitelisted types: configuration types for
    /// the generic editor, player entities for the account editor. Security/account entities must not
    /// be editable through the generic configuration reflection form.
    /// </summary>
    [Test]
    public void GenericEditorTypeWhitelistIsEnforcedAsync()
    {
        bool Allows(System.Type component, System.Type type)
        {
            var instance = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(component);
            var method = component.GetMethod("IsAllowedType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return (bool)method!.Invoke(instance, new object[] { type })!;
        }

        var gameConfig = typeof(MUnique.OpenMU.DataModel.Configuration.GameConfiguration);
        var account = typeof(MUnique.OpenMU.DataModel.Entities.Account);

        Assert.That(Allows(typeof(MUnique.OpenMU.Web.AdminPanel.Pages.EditConfig), gameConfig), Is.True, "generic editor allows configuration");
        Assert.That(Allows(typeof(MUnique.OpenMU.Web.AdminPanel.Pages.EditConfig), account), Is.False, "generic editor must not edit Account");

        Assert.That(Allows(typeof(MUnique.OpenMU.Web.AdminPanel.Pages.EditAccount), account), Is.True, "account editor allows Account");
        Assert.That(Allows(typeof(MUnique.OpenMU.Web.AdminPanel.Pages.EditAccount), gameConfig), Is.False, "account editor must not edit configuration");
    }
    /// <summary>
    /// The pure C# edit components (79-01) carry the administrator authorization attribute, so the
    /// server-side route view rejects anonymous and low-privilege callers.
    /// </summary>
    [Test]
    public void PureCSharpEditComponentsRequireAdministratorAsync()
    {
        bool HasAdminPolicy(Type type) => type.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .OfType<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Any(a => a.Policy == AdminPolicies.Administrator);

        Assert.That(HasAdminPolicy(typeof(MUnique.OpenMU.Web.AdminPanel.Pages.EditBase)), Is.True, "EditBase must require administrator");
        Assert.That(HasAdminPolicy(typeof(MUnique.OpenMU.Web.AdminPanel.Pages.EditConfig)), Is.True, "EditConfig must require administrator");
        Assert.That(HasAdminPolicy(typeof(MUnique.OpenMU.Web.AdminPanel.Pages.EditAccount)), Is.True, "EditAccount must require administrator");
        Assert.That(HasAdminPolicy(typeof(MUnique.OpenMU.Web.AdminPanel.Pages.EditConnectionServer)), Is.True, "EditConnectionServer must require administrator");
        Assert.That(HasAdminPolicy(typeof(MUnique.OpenMU.Web.AdminPanel.Pages.EditMap)), Is.True, "EditMap must require administrator");
    }
}