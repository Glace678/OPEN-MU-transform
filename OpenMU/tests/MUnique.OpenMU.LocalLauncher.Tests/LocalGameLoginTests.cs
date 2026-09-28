// <copyright file="LocalGameLoginTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher.Tests;

/// <summary>Verifies private and stable first-run game credentials.</summary>
public class LocalGameLoginTests
{
    private static readonly string MobilePackageKey = new('A', 43);

    /// <summary>Restart and backup restore keep the same account, without reusing admin credentials.</summary>
    [Test]
    public void CredentialsAreStableAndProtocolCompatible()
    {
        var secrets = DpapiSecretStore.Create("administrator-password");
        var login = LocalGameLogin.FromSecrets(secrets);
        var again = LocalGameLogin.FromSecrets(secrets);
        Assert.Multiple(() =>
        {
            Assert.That(login.Username, Is.EqualTo(again.Username));
            Assert.That(login.Password, Is.EqualTo(again.Password));
            Assert.That(login.Username, Does.Match("^solo[A-F0-9]{6}$"));
            Assert.That(login.Password, Does.Match("^[A-Za-z0-9_-]{20}$"));
            Assert.That(login.Password, Is.Not.EqualTo(secrets.AdminPanelPassword));
            Assert.That(login.Password, Is.Not.EqualTo(secrets.AccountPassword));
        });
        secrets.AdminPanelPassword = "changed-administrator-password";
        Assert.That(LocalGameLogin.FromSecrets(secrets).Password, Is.EqualTo(login.Password));
        Assert.That(LocalGameLogin.FromSecrets(DpapiSecretStore.Create("other-administrator-password")).Password,
            Is.Not.EqualTo(login.Password));
    }

    /// <summary>Disabling automatic login removes inherited credentials as well as the flag.</summary>
    [TestCase(true)]
    [TestCase(false)]
    public void OnlyAutomaticLoginReceivesGameCredentials(bool enabled)
    {
        var login = LocalGameLogin.FromSecrets(DpapiSecretStore.Create("administrator-password"));
        var environment = LocalStackManager.CreateGameEnvironment("game-config.ini", enabled, login);
        Assert.That(environment["MU_LOCAL_GAME_USERNAME"], Is.EqualTo(enabled ? login.Username : null));
        Assert.That(environment["MU_LOCAL_GAME_PASSWORD"], Is.EqualTo(enabled ? login.Password : null));
        Assert.That(environment.Keys, Does.Not.Contain("OPENMU_ADMIN_PASSWORD"));
    }

    /// <summary>The package key derives a stable protocol-compatible login with the documented purposes.</summary>
    [Test]
    public void MobilePackageKeyDerivesExpectedCredentials()
    {
        var login = LocalGameLogin.FromMobilePackageKey(MobilePackageKey);
        Assert.Multiple(() =>
        {
            Assert.That(login.Username, Is.EqualTo("mobB92499A"));
            Assert.That(login.Password, Is.EqualTo("bjFzBKJhNZcNXgbgkx5i"));
            Assert.That(login.Username, Does.Match("^mob[A-F0-9]{7}$"));
            Assert.That(login.Password, Does.Match("^[A-Za-z0-9_-]{20}$"));
        });
    }

    /// <summary>Only an exact unpadded 32-byte base64url key is accepted.</summary>
    [Test]
    public void InvalidMobilePackageKeyIsRejected()
    {
        foreach (var packageKey in new[] { string.Empty, new string('A', 42), new string('A', 42) + "+" })
        {
            Assert.Throws<ArgumentException>(() => LocalGameLogin.FromMobilePackageKey(packageKey));
        }
    }

    /// <summary>The server provisions only the same local account offered to the client.</summary>
    [TestCase(true)]
    [TestCase(false)]
    public void ServerProvisioningUsesTheSameAccountAndStaysOnLoopback(bool enabled)
    {
        var secrets = DpapiSecretStore.Create("administrator-password");
        var login = LocalGameLogin.FromSecrets(secrets);
        var server = new OpenMuServerManager(new LocalPaths(Path.GetTempPath()),
            new LocalStackSettings { AutomaticGameLogin = enabled }, new ProcessRunner());
        var environment = server.CreateServerEnvironment(secrets);
        Assert.That(environment["OPENMU_LOCAL_GAME_USERNAME"], Is.EqualTo(enabled ? login.Username : null));
        Assert.That(environment["OPENMU_LOCAL_GAME_PASSWORD"], Is.EqualTo(enabled ? login.Password : null));
        Assert.That(environment["OPENMU_BIND_ADDRESS"], Is.EqualTo("127.0.0.1"));
        Assert.That(environment["OPENMU_CONTROL_PIPE"], Is.EqualTo(server.PipeName));
        Assert.That(environment["OPENMU_ADMIN_PASSWORD"], Is.EqualTo(secrets.AdminPanelPassword));
    }

    /// <summary>Mobile mode opens only server endpoints and advertises the selected LAN address.</summary>
    [Test]
    public void MobileModeUsesWildcardListenersAndKeepsAnExplicitAdvertisedAddress()
    {
        var settings = new LocalStackSettings
        {
            MobileAccessEnabled = true,
            MobileAdvertisedAddress = "192.168.10.25",
            MobilePackageKey = MobilePackageKey,
        };
        var server = new OpenMuServerManager(new LocalPaths(Path.GetTempPath()), settings, new ProcessRunner());
        var environment = server.CreateServerEnvironment(DpapiSecretStore.Create("administrator-password"));
        var login = LocalGameLogin.FromMobilePackageKey(MobilePackageKey);
        Assert.Multiple(() =>
        {
            Assert.That(OpenMuServerManager.ResolveAdvertisedAddress(settings), Is.EqualTo("192.168.10.25"));
            Assert.That(environment["OPENMU_BIND_ADDRESS"], Is.EqualTo("0.0.0.0"));
            Assert.That(environment["ASPNETCORE_URLS"], Is.EqualTo("http://0.0.0.0:5080"));
            Assert.That(environment["OPENMU_MOBILE_PACKAGE_KEY"], Is.EqualTo(MobilePackageKey));
            Assert.That(environment["OPENMU_LOCAL_GAME_USERNAME"], Is.EqualTo(login.Username));
            Assert.That(environment["OPENMU_LOCAL_GAME_PASSWORD"], Is.EqualTo(login.Password));
            Assert.That(environment["DB_HOST"], Is.Null);
        });
    }
}
