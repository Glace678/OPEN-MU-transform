// <copyright file="AccountPasswordSecurityTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix.TestAccounts;

/// <summary>
/// Tests for 74-01: GM/test account passwords must be cryptographically random,
/// never equal to the login name, and flagged as must-change-on-first-login.
/// </summary>
[TestFixture]
public class AccountPasswordSecurityTests
{
    /// <summary>
    /// A concrete minimal subclass that exposes the protected method for testing.
    /// </summary>
    private class TestableAccountInitializer : AccountInitializerBase
    {
        public TestableAccountInitializer(IContext context, GameConfiguration gameConfiguration, string accountName)
            : base(context, gameConfiguration, accountName, 1)
        {
        }

        public new void AssignRandomTemporaryPassword(Account account)
        {
            base.AssignRandomTemporaryPassword(account);
        }
    }

    [Test]
    public void AssignRandomTemporaryPassword_DoesNotUseAccountNameAsPassword()
    {
        var accountName = "testgm";
        var account = new Account { LoginName = accountName };
        var initializer = this.CreateInitializer(accountName);

        initializer.AssignRandomTemporaryPassword(account);

        Assert.That(BCrypt.Net.BCrypt.Verify(accountName, account.PasswordHash), Is.False,
            "Password must not equal the login name.");
    }

    [Test]
    public void AssignRandomTemporaryPassword_MarksMustChangePassword()
    {
        var accountName = "testgm";
        var account = new Account { LoginName = accountName };
        var initializer = this.CreateInitializer(accountName);

        initializer.AssignRandomTemporaryPassword(account);

        Assert.That(account.MustChangePassword, Is.True,
            "Account must be flagged as must-change-password.");
    }

    [Test]
    public void AssignRandomTemporaryPassword_ProducesDifferentHashesOnConsecutiveCalls()
    {
        var accountName = "testgm";
        var initializer = this.CreateInitializer(accountName);

        var account1 = new Account { LoginName = accountName };
        var account2 = new Account { LoginName = accountName };

        initializer.AssignRandomTemporaryPassword(account1);
        initializer.AssignRandomTemporaryPassword(account2);

        Assert.That(account1.PasswordHash, Is.Not.EqualTo(account2.PasswordHash),
            "Consecutive password assignments must produce different hashes (crypto random).");
    }

    [Test]
    public void AssignRandomTemporaryPassword_ProducesValidBcryptHash()
    {
        var accountName = "testgm";
        var account = new Account { LoginName = accountName };
        var initializer = this.CreateInitializer(accountName);

        initializer.AssignRandomTemporaryPassword(account);

        Assert.That(account.PasswordHash, Does.StartWith("$2"),
            "Password hash must be a valid BCrypt hash.");
    }

    private TestableAccountInitializer CreateInitializer(string accountName)
    {
        var mockContext = new Mock<IContext>();
        var mockGameConfig = new Mock<GameConfiguration>();
        return new TestableAccountInitializer(mockContext.Object, mockGameConfig.Object, accountName);
    }
}