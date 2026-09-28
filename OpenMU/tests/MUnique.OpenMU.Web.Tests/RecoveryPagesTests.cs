// <copyright file="RecoveryPagesTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests;

using System.Globalization;
using System.Resources;
using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using MUnique.OpenMU.Web.AdminPanel.API;
using MUnique.OpenMU.Web.AdminPanel.Pages;
using MUnique.OpenMU.Web.AdminPanel.Properties;

/// <summary>Tests recovery form validation, one-time code display, and localized security messages.</summary>
[TestFixture]
public class RecoveryPagesTests
{
    private const string ModulePath = "./_content/MUnique.OpenMU.Web.AdminPanel/js/account-self-service.js";

    /// <summary>A successful registration shows the response code without allowing a duplicate submit to erase it.</summary>
    [Test]
    public void RegistrationDisplaysNewCodeAndClearsPasswordForm()
    {
        using var context = CreateContext();
        var module = context.JSInterop.SetupModule(ModulePath);
        var code = AccountRecoveryService.GenerateCode();
        module.Setup<AccountRegistrationResponse>("postJson", _ => true)
            .SetResult(new AccountRegistrationResponse(true, "ok", "registered", code));
        var page = context.Render<Register>();
        page.Find("#reg-name").Change("solotest");
        page.Find("#reg-password").Change("old-password");
        page.Find("#reg-confirm").Change("old-password");
        page.Find("#reg-security").Change("123456");
        page.Find("form").Submit();
        var display = page.WaitForElement("#registration-recovery-code");
        Assert.That(display.TextContent, Is.EqualTo(code));
        Assert.That(display.HasAttribute("readonly"), Is.True);
        Assert.That(page.FindAll("form"), Is.Empty);
        Assert.That(page.Markup, Does.Not.Contain("old-password"));
    }

    /// <summary>A missing recovery code does not invoke the password reset request, even when local maintenance is enabled.</summary>
    [TestCase(false)]
    [TestCase(true)]
    public void ResetRequiresCodeBeforeBrowserRequest(bool maintenance)
    {
        using var context = CreateContext(maintenance);
        var module = context.JSInterop.SetupModule(ModulePath);
        var page = context.Render<ResetPassword>();
        page.Find("#reset-name").Change("solotest");
        page.Find("#reset-password").Change("new-password");
        page.Find("#reset-confirm").Change("new-password");
        page.FindAll("form")[0].Submit();
        Assert.That(module.Invocations, Is.Empty);
        Assert.That(page.WaitForElement("form li").TextContent, Is.Not.Empty);
    }

    /// <summary>Recovery sends the supplied credential and displays the replacement without retaining the submitted password.</summary>
    [Test]
    public void RecoveryDisplaysReplacementAndClearsCredentials()
    {
        using var context = CreateContext();
        var module = context.JSInterop.SetupModule(ModulePath);
        var previous = AccountRecoveryService.GenerateCode();
        var replacement = AccountRecoveryService.GenerateCode();
        module.Setup<AccountRegistrationResponse>("postJson", invocation =>
            invocation.Arguments[0]?.ToString() == "api/registration/reset-password")
            .SetResult(new AccountRegistrationResponse(true, "ok", "recovered", replacement));
        var page = context.Render<ResetPassword>();
        page.Find("#reset-name").Change("solotest");
        page.Find("#reset-recovery-code").Change(previous);
        page.Find("#reset-password").Change("new-password");
        page.Find("#reset-confirm").Change("new-password");
        page.FindAll("form")[0].Submit();
        Assert.That(page.WaitForElement("#replacement-recovery-code").TextContent, Is.EqualTo(replacement));
        var payload = module.Invocations["postJson"].Single().Arguments[1]!;
        Assert.That(payload.GetType().GetProperty("RecoveryCode")!.GetValue(payload), Is.EqualTo(previous));
        Assert.That(page.Find("#reset-password").GetAttribute("value"), Is.Empty.Or.Null);
        Assert.That(page.Find("#reset-recovery-code").GetAttribute("value"), Is.Empty.Or.Null);
    }

    /// <summary>All supported languages have their own recovery security messages rather than relying on English fallback.</summary>
    [TestCase("en")]
    [TestCase("zh-CN")]
    [TestCase("zh-TW")]
    [TestCase("ja")]
    [TestCase("ko")]
    [TestCase("de")]
    [TestCase("es")]
    [TestCase("fr")]
    [TestCase("pt")]
    [TestCase("ru")]
    [TestCase("uk")]
    [TestCase("pl")]
    [TestCase("id")]
    [TestCase("vi")]
    [TestCase("tl")]
    public void RecoveryMessagesAreLocalizedWithoutParentFallback(string culture)
    {
        var manager = new ResourceManager("MUnique.OpenMU.Web.AdminPanel.Properties.SelfServiceResources", typeof(SelfServiceResources).Assembly);
        var resources = manager.GetResourceSet(culture == "en" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(culture), true, false);
        Assert.That(resources, Is.Not.Null);
        foreach (var key in new[] { "ResetDisabled", "LocalPasswordResetWarning", "RecoveryCodeLabel", "RecoveryCodeNotice", "RequiredRecoveryCode", "InvalidRecoveryCode", "RecoveryUnavailable", "RecoveryIssueTitle", "IssueRecoveryCodeButton", "RecoveryIssueSuccess", "RecoveryIssueFailed" })
        {
            Assert.That(resources!.GetString(key), Is.Not.Empty.And.Not.Null, culture + ":" + key);
        }
    }

    private static BunitContext CreateContext(bool maintenance = false)
    {
        var context = new BunitContext();
        var localizer = new Mock<IStringLocalizer<SelfServiceResources>>();
        localizer.Setup(text => text[It.IsAny<string>()]).Returns((string key) => new LocalizedString(key, key));
        context.Services.AddSingleton(localizer.Object);
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AccountSelfService:AllowLocalPasswordReset"] = maintenance.ToString(),
        }).Build());
        return context;
    }
}
