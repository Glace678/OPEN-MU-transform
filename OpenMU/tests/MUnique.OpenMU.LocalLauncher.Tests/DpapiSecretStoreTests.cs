// <copyright file="DpapiSecretStoreTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher.Tests;

/// <summary>
/// Tests current-user secret protection.
/// </summary>
public class DpapiSecretStoreTests
{
    /// <summary>Verifies encrypted secrets round-trip and no clear password appears on disk.</summary>
    [Test]
    [Platform("Win")]
    public void SecretsAreProtectedForCurrentUser()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"openmu-secrets-{Guid.NewGuid():N}.dpapi");
        try
        {
            var store = new DpapiSecretStore(filePath);
            var secrets = DpapiSecretStore.Create("correct horse battery");
            store.Save(secrets);
            var encryptedText = Convert.ToBase64String(File.ReadAllBytes(filePath));
            Assert.Multiple(() =>
            {
                Assert.That(store.Load().AdminPanelPassword, Is.EqualTo("correct horse battery"));
                Assert.That(encryptedText, Does.Not.Contain("correct horse battery"));
                Assert.That(secrets.DatabaseAdminPassword, Has.Length.GreaterThanOrEqualTo(40));
            });
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    /// <summary>Verifies the administrator password minimum.</summary>
    [Test]
    public void ShortAdministratorPasswordIsRejected()
    {
        Assert.Throws<ArgumentException>(() => DpapiSecretStore.Create("too-short"));
        Assert.Throws<ArgumentException>(() => DpapiSecretStore.Create("            "));
    }

    /// <summary>Verifies incomplete secret sets are never persisted.</summary>
    [Test]
    public void IncompleteSecretSetIsRejected()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"openmu-secrets-{Guid.NewGuid():N}.dpapi");
        Assert.Throws<InvalidDataException>(() => new DpapiSecretStore(filePath).Save(new LocalSecrets { AdminPanelPassword = "twelve-chars!" }));
        Assert.That(File.Exists(filePath), Is.False);
    }

    /// <summary>Unix secrets are private and survive an atomic replacement.</summary>
    [Test]
    [Platform(Exclude = "Win")]
    public void UnixSecretsRoundTripWithOwnerOnlyPermissions()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"openmu-unix-secrets-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "secrets.json");
        try
        {
            var store = new UnixSecretStore(path);
            var secrets = DpapiSecretStore.Create("local-test-password");
            store.Save(secrets);
            Assert.That(store.Load().AdminPanelPassword, Is.EqualTo(secrets.AdminPanelPassword));
            Assert.That(File.GetUnixFileMode(path), Is.EqualTo(LocalPlatform.PrivateFileMode));
            Assert.That(File.GetUnixFileMode(directory), Is.EqualTo(LocalPlatform.PrivateFileMode | UnixFileMode.UserExecute));
            File.SetUnixFileMode(path, LocalPlatform.PrivateFileMode | UnixFileMode.OtherRead);
            Assert.Throws<InvalidDataException>(() => store.Load());
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
