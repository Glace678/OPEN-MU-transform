// <copyright file="BackupManagerTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher.Tests;

/// <summary>
/// Tests backup retention and restore-version compatibility.
/// </summary>
public class BackupManagerTests
{
    private string _directory = null!;

    /// <summary>Creates an isolated backup directory.</summary>
    [SetUp]
    public void SetUp()
    {
        this._directory = Path.Combine(Path.GetTempPath(), $"openmu-backup-policy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this._directory);
    }

    /// <summary>Removes the isolated backup directory.</summary>
    [TearDown]
    public void TearDown()
    {
        Directory.Delete(this._directory, recursive: true);
    }

    /// <summary>Verifies retention removes only old automatic-stop backups.</summary>
    [Test]
    public void AutomaticRetentionPreservesManualAndProtectiveBackups()
    {
        var oldestAutomatic = this.CreateBackupFile("OpenMU-20260901-000000-000-auto-stop.zip");
        var retainedAutomatic = this.CreateBackupFile("OpenMU-20260902-000000-000-auto-stop.zip");
        var newestAutomatic = this.CreateBackupFile("OpenMU-20260903-000000-000-auto-stop.zip");
        var manual = this.CreateBackupFile("OpenMU-20260801-000000-000-manual.zip");
        var protective = this.CreateBackupFile("OpenMU-20260802-000000-000-before-restore.zip");

        BackupManager.ApplyAutomaticRetention(this._directory, 2);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(oldestAutomatic), Is.False);
            Assert.That(File.Exists(retainedAutomatic), Is.True);
            Assert.That(File.Exists(newestAutomatic), Is.True);
            Assert.That(File.Exists(manual), Is.True);
            Assert.That(File.Exists(protective), Is.True);
        });
    }

    /// <summary>Verifies a newer local package revision is rejected during restore.</summary>
    [Test]
    public void NewerLocalRevisionIsRejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            BackupManager.EnsureRestoreVersionSupported("0.9.10-local.2", "0.9.10-local.1"));
        Assert.Throws<InvalidDataException>(() =>
            BackupManager.EnsureRestoreVersionSupported("0.9.10-local.10", "0.9.10-local.2"));
    }

    /// <summary>Verifies equal and older local package revisions remain restorable.</summary>
    [Test]
    public void CurrentAndOlderLocalRevisionsAreAccepted()
    {
        Assert.DoesNotThrow(() =>
            BackupManager.EnsureRestoreVersionSupported("0.9.10-local.1", "0.9.10-local.1"));
        Assert.DoesNotThrow(() =>
            BackupManager.EnsureRestoreVersionSupported("0.9.10-local.1", "0.9.10-local.2"));
    }

    /// <summary>Verifies a stable release sorts after its prerelease.</summary>
    [Test]
    public void StableReleaseIsNewerThanPrerelease()
    {
        Assert.Throws<InvalidDataException>(() =>
            BackupManager.EnsureRestoreVersionSupported("0.9.10", "0.9.10-local.10"));
        Assert.DoesNotThrow(() =>
            BackupManager.EnsureRestoreVersionSupported("0.9.10-local.10", "0.9.10"));
    }

    /// <summary>Only an explicitly forced stop may continue after a backup failure.</summary>
    [Test]
    public async Task ForceStopContinuesAfterBackupFailure()
    {
        var expectedError = new IOException("backup failed");
        var databaseStopped = false;
        var result = await LocalStackManager.BackupThenStopDatabaseAsync(
            force: true,
            () => Task.FromException(expectedError),
            () =>
            {
                databaseStopped = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.SameAs(expectedError));
            Assert.That(databaseStopped, Is.True);
        });
    }

    /// <summary>A normal stop leaves the database running if its backup fails.</summary>
    [Test]
    public void NormalStopFailsClosedAfterBackupFailure()
    {
        var databaseStopped = false;
        Assert.ThrowsAsync<IOException>(async () =>
            await LocalStackManager.BackupThenStopDatabaseAsync(
                force: false,
                () => Task.FromException(new IOException("backup failed")),
                () =>
                {
                    databaseStopped = true;
                    return Task.CompletedTask;
                },
                CancellationToken.None));
        Assert.That(databaseStopped, Is.False);
    }

    private string CreateBackupFile(string name)
    {
        var path = Path.Combine(this._directory, name);
        File.WriteAllText(path, name);
        return path;
    }
}
