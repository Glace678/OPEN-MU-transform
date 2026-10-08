// <copyright file="AccountCreationTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests;

using System.IO;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.Shared.Components.Modal;
using MUnique.OpenMU.Web.Shared.Services;

/// <summary>Exercises the rendered account creation form without touching player data.</summary>
[TestFixture]
public class AccountCreationTests
{
    /// <summary>Administrative recovery revokes any previously issued recovery credential.</summary>
    [Test]
    public async Task AdministrativeResetRevokesRecoveryCode()
    {
        var playerContext = new Mock<IPlayerContext>();
        playerContext.Setup(p => p.SaveChangesAsync(default)).ReturnsAsync(true);
        var source = new Mock<IDataSource<Account>>();
        source.Setup(s => s.GetContextAsync(default)).ReturnsAsync(playerContext.Object);
        using var service = new AccountService(source.Object, Mock.Of<IModalService>(), NullLogger<AccountService>.Instance);
        var account = new Account { PasswordHash = BCrypt.Net.BCrypt.HashPassword("old-password"), RecoveryCodeHash = "old-code-hash" };

        var error = await service.ResetPasswordAsync(account, "new-password").ConfigureAwait(false);

        Assert.That(error, Is.Null);
        Assert.That(account.RecoveryCodeHash, Is.Null);
        Assert.That(BCrypt.Net.BCrypt.Verify("new-password", account.PasswordHash), Is.True);
        playerContext.Verify(p => p.SaveChangesAsync(default), Times.Once);
    }

    /// <summary>EF-capable administrative recovery clears credentials atomically and never overwrites a newer snapshot.</summary>
    [TestCase(true)]
    [TestCase(false)]
    public async Task AdministrativeResetUsesCredentialCompareAndSwap(bool wins)
    {
        var snapshot = new AccountCredentialSnapshot("solotest", BCrypt.Net.BCrypt.HashPassword("old-password"), null);
        var playerContext = new Mock<IPlayerContext>();
        var credentials = playerContext.As<IAccountCredentialContext>();
        credentials.Setup(c => c.ReadCredentialsAsync("solotest", default)).ReturnsAsync(snapshot);
        credentials.Setup(c => c.TryReplaceCredentialsAsync(snapshot, It.IsAny<string>(), null, default)).ReturnsAsync(wins);
        var source = new Mock<IDataSource<Account>>();
        source.Setup(s => s.GetContextAsync(default)).ReturnsAsync(playerContext.Object);
        using var service = new AccountService(source.Object, Mock.Of<IModalService>(), NullLogger<AccountService>.Instance);
        var account = new Account { LoginName = "solotest", PasswordHash = snapshot.PasswordHash };

        var error = await service.ResetPasswordAsync(account, "new-password").ConfigureAwait(false);

        Assert.That(error is null, Is.EqualTo(wins));
        credentials.Verify(c => c.TryReplaceCredentialsAsync(snapshot, It.IsAny<string>(), null, default), Times.Once);
        playerContext.Verify(p => p.SaveChangesAsync(default), Times.Never);
        Assert.That(account.PasswordHash, Is.EqualTo(snapshot.PasswordHash), "Do not dirty a stale tracked entity after an atomic update.");
    }

    /// <summary>A valid form persists an account and refreshes its list.</summary>
    [Test]
    public async Task ValidFormCreatesAccount()
    {
        using var context = CreateContext();
        var playerContext = new Mock<IPlayerContext>();
        var account = new Account();
        playerContext.Setup(p => p.CreateNew<Account>(It.IsAny<object?[]>())).Returns(account);
        playerContext.Setup(p => p.SaveChangesAsync(default)).ReturnsAsync(true);
        var source = new Mock<IDataSource<Account>>();
        source.Setup(s => s.GetContextAsync(default)).ReturnsAsync(playerContext.Object);
        var modal = context.Services.GetRequiredService<ModalService>();
        using var service = new AccountService(source.Object, modal, NullLogger<AccountService>.Instance);
        var changed = 0;
        service.DataChanged += (_, _) => changed++;
        var component = context.Render<ModalContainer>();
        var creation = service.CreateNewInModalDialogAsync();

        component.WaitForElement("#LoginName").Change("solotest");
        component.Find("#Password").Change("test-only");
        component.Find("#SecurityCode").Change("123456");
        component.Find("form").Submit();
        await creation.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(account.LoginName, Is.EqualTo("solotest"));
            Assert.That(BCrypt.Net.BCrypt.Verify("test-only", account.PasswordHash), Is.True);
            // Security codes are stored as BCrypt hashes, not in plain text.
            Assert.That(account.SecurityCode, Is.Not.EqualTo("123456"));
            Assert.That(BCrypt.Net.BCrypt.Verify("123456", account.SecurityCode), Is.True);
            Assert.That(changed, Is.EqualTo(1));
        });
    }

    /// <summary>A failed database write must not close the form or fault its event handler.</summary>
    [TestCase(true)]
    [TestCase(false)]
    public async Task SaveFailureKeepsFormOpen(bool throws)
    {
        using var context = CreateContext();
        var playerContext = new Mock<IPlayerContext>();
        playerContext.Setup(p => p.CreateNew<Account>(It.IsAny<object?[]>())).Returns(new Account());
        if (throws)
        {
            playerContext.Setup(p => p.SaveChangesAsync(default)).ThrowsAsync(new IOException("test database unavailable"));
        }
        else
        {
            playerContext.Setup(p => p.SaveChangesAsync(default)).ReturnsAsync(false);
        }
        var source = new Mock<IDataSource<Account>>();
        source.Setup(s => s.GetContextAsync(default)).ReturnsAsync(playerContext.Object);
        var modal = context.Services.GetRequiredService<ModalService>();
        using var service = new AccountService(source.Object, modal, NullLogger<AccountService>.Instance);
        var changed = 0;
        service.DataChanged += (_, _) => changed++;
        var component = context.Render<ModalContainer>();
        var creation = service.CreateNewInModalDialogAsync();
        component.WaitForElement("#LoginName").Change("solotest");
        component.Find("#Password").Change("test-only");
        component.Find("#SecurityCode").Change("123456");
        component.Find("form").Submit();
        await Task.WhenAny(creation, Task.Delay(100)).ConfigureAwait(false);

        Assert.That(creation.IsFaulted, Is.False, "Database errors must not escape the account creation event handler.");
        component.WaitForElement(".alert-danger");
        Assert.That(component.Find("#LoginName").GetAttribute("value"), Is.EqualTo("solotest"));
        Assert.That(changed, Is.Zero);
        playerContext.Verify(p => p.Detach(It.IsAny<Account>()), Times.Once);
        playerContext.Setup(p => p.SaveChangesAsync(default)).ReturnsAsync(true);
        component.Find("form").Submit();
        await creation.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.That(changed, Is.EqualTo(1));
        Assert.That(component.FindAll(".modal"), Is.Empty);
    }

    /// <summary>Duplicate names are rejected before an entity is added; cancel stays available.</summary>
    [Test]
    public async Task DuplicateNameCanBeCorrectedOrCancelled()
    {
        using var context = CreateContext();
        var playerContext = new Mock<IPlayerContext>();
        playerContext.Setup(p => p.GetAccountByLoginNameAsync("solotest", default)).ReturnsAsync(new Account());
        var source = new Mock<IDataSource<Account>>();
        source.Setup(s => s.GetContextAsync(default)).ReturnsAsync(playerContext.Object);
        var modal = context.Services.GetRequiredService<ModalService>();
        using var service = new AccountService(source.Object, modal, NullLogger<AccountService>.Instance);
        var component = context.Render<ModalContainer>();
        var creation = service.CreateNewInModalDialogAsync();
        component.WaitForElement("#LoginName").Change("solotest");
        component.Find("#Password").Change("test-only");
        component.Find("#SecurityCode").Change("123456");
        component.Find("form").Submit();
        Assert.That(component.WaitForElement(".alert-danger").TextContent, Does.Contain("already in use"));
        playerContext.Verify(p => p.CreateNew<Account>(It.IsAny<object?[]>()), Times.Never);
        component.Find("button[type='button']").Click();
        await creation.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        playerContext.Verify(p => p.SaveChangesAsync(default), Times.Never);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddSingleton<ModalService>();
        context.Services.AddSingleton<IChangeNotificationService, ChangeNotificationService>();
        return context;
    }
}
