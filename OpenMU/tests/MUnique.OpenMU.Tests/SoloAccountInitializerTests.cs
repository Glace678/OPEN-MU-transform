// <copyright file="SoloAccountInitializerTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Threading;
using Moq;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization;

/// <summary>Automatic setup must not reset or elevate existing accounts.</summary>
public class SoloAccountInitializerTests
{
    private const string Username = "soloABC123";
    private const string Password = "AbCd0123456789_abc-Z";

    /// <summary>A new installation gets a normal account with a hashed password.</summary>
    [Test]
    public async Task CreatesOrdinaryAccount()
    {
        var account = new Account();
        var context = new Mock<IPlayerContext>();
        context.Setup(c => c.CreateNew<Account>(It.IsAny<object[]>())).Returns(account);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await SoloAccountInitializer.EnsureAsync(context.Object, Username, Password).ConfigureAwait(false);
        Assert.That(account.LoginName, Is.EqualTo(Username));
        Assert.That(account.State, Is.EqualTo(AccountState.Normal));
        Assert.That(account.LanguageIsoCode, Is.EqualTo("zh"));
        Assert.That(account.LanguageIsoCode, Has.Length.LessThanOrEqualTo(3));
        Assert.That(BCrypt.Net.BCrypt.Verify(Password, account.PasswordHash), Is.True);
    }

    /// <summary>Restart preserves the account state and all stored progress.</summary>
    [Test]
    public async Task ExistingAccountIsNeverModified()
    {
        var account = new Account { PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password), State = AccountState.Banned };
        var context = new Mock<IPlayerContext>();
        context.Setup(c => c.GetAccountByLoginNameAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        await SoloAccountInitializer.EnsureAsync(context.Object, Username, Password).ConfigureAwait(false);
        Assert.That(account.State, Is.EqualTo(AccountState.Banned));
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        context.Verify(c => c.CreateNew<Account>(It.IsAny<object[]>()), Times.Never);
    }

    /// <summary>A name collision must not reset another account's password.</summary>
    [Test]
    public void NameCollisionFailsClosed()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("other-password");
        var account = new Account { PasswordHash = hash };
        var context = new Mock<IPlayerContext>();
        context.Setup(c => c.GetAccountByLoginNameAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(account);
        Assert.ThrowsAsync<InvalidOperationException>(() => SoloAccountInitializer.EnsureAsync(context.Object, Username, Password));
        Assert.That(account.PasswordHash, Is.EqualTo(hash));
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A database save failure is not reported as successful provisioning.</summary>
    [Test]
    public void SaveFailureIsReported()
    {
        var context = new Mock<IPlayerContext>();
        context.Setup(c => c.CreateNew<Account>(It.IsAny<object[]>())).Returns(new Account());
        Assert.ThrowsAsync<InvalidOperationException>(() => SoloAccountInitializer.EnsureAsync(context.Object, Username, Password));
    }
}
