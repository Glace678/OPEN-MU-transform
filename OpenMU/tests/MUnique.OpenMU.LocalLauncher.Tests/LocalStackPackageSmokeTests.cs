// <copyright file="LocalStackPackageSmokeTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher.Tests;

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Buffers.Binary;

/// <summary>
/// Exercises a complete published local stack without opening the launcher or game UI.
/// </summary>
public class LocalStackPackageSmokeTests
{
    private const string SmokeRootVariable = "OPENMU_LOCAL_STACK_SMOKE_ROOT";

    /// <summary>Provisions, starts, backs up, restores, and gracefully stops a disposable package.</summary>
    [Test]
    [Explicit($"Requires {SmokeRootVariable} pointing to a disposable package extracted below the current user's temporary directory.")]
    [Timeout(15 * 60 * 1000)]
    public async Task ProvisionStartBackupRestoreAndStopWithoutGameUi()
    {
        var configuredRoot = Environment.GetEnvironmentVariable(SmokeRootVariable);
        Assert.That(configuredRoot, Is.Not.Null.And.Not.Empty);
        var packageRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configuredRoot!));
        var temporaryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        Assert.That(
            Path.GetRelativePath(temporaryRoot, packageRoot),
            Does.Not.StartWith(".." + Path.DirectorySeparatorChar).And.Not.EqualTo(".."),
            "The integration test only accepts a disposable package below the current user's temporary directory.");

        var paths = new LocalPaths(packageRoot);
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(paths.ManifestFile), Is.True, "Missing package manifest.");
            Assert.That(File.Exists(paths.ServerExecutable), Is.True, "Missing published OpenMU server.");
            Assert.That(File.Exists(paths.GameExecutable), Is.True, "Missing packaged game executable.");
            Assert.That(File.Exists(paths.InitDbExecutable), Is.True, "Missing PostgreSQL runtime.");
        });

        using var manager = new LocalStackManager(packageRoot);
        manager.Settings.StartGameWhenReady = false;
        var fatalLogEntriesBefore = CountFatalLogEntries(paths.LogsDirectory);
        if (manager.RequiresProvisioning)
        {
            manager.Provision($"Local-smoke-{Guid.NewGuid():N}!");
        }

        try
        {
            await manager.StartAsync(startGame: false, CancellationToken.None);
            AssertRunningOnLoopbackOnly(manager);
            await AssertConnectProtocolAsync(manager.Settings.ConnectServerPort);

            var backupPath = await manager.CreateBackupAsync(CancellationToken.None);
            Assert.That(new FileInfo(backupPath).Length, Is.GreaterThan(0), "The manual backup is empty.");

            await manager.RestoreAsync(backupPath, CancellationToken.None);
            AssertRunningOnLoopbackOnly(manager);
            await AssertConnectProtocolAsync(manager.Settings.ConnectServerPort);

            await manager.StopAsync(force: false, CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(manager.Status.State, Is.EqualTo(LocalStackState.Stopped));
                Assert.That(File.Exists(paths.ConnectionSettingsFile), Is.False, "Transient database credentials remained after shutdown.");
                Assert.That(Directory.EnumerateFiles(paths.BackupsDirectory, "*.zip"), Is.Not.Empty);
                Assert.That(CountFatalLogEntries(paths.LogsDirectory), Is.EqualTo(fatalLogEntriesBefore), "The stack added a fatal log entry during a normal lifecycle.");
            });
        }
        catch (Exception exception)
        {
            TestContext.Error.WriteLine(exception);
            throw;
        }
        finally
        {
            if (manager.Status.State != LocalStackState.Stopped)
            {
                try
                {
                    await manager.StopAsync(force: false, CancellationToken.None);
                }
                catch (Exception)
                {
                    await manager.StopAsync(force: true, CancellationToken.None);
                }
            }
        }
    }

    private static async Task AssertConnectProtocolAsync(int port)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
            var stream = client.GetStream();
            var hello = new byte[4];
            await stream.ReadExactlyAsync(hello, timeout.Token);
            Assert.That(hello, Is.EqualTo(new byte[] { 0xC1, 4, 0, 1 }), "The connect server accepted TCP but did not complete the game handshake.");
            await stream.WriteAsync(new byte[] { 0xC1, 4, 0xF4, 6 }, timeout.Token);
            var header = new byte[3];
            await stream.ReadExactlyAsync(header, timeout.Token);
            Assert.That(header[0], Is.EqualTo(0xC2));
            var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(1));
            Assert.That(length, Is.InRange(7, 4096));
            var payload = new byte[length - header.Length];
            await stream.ReadExactlyAsync(payload, timeout.Token);
            Assert.That(payload.Take(2), Is.EqualTo(new byte[] { 0xF4, 6 }));
            Assert.That(BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(2)), Is.GreaterThan(0), "No playable server was advertised.");
        }
    }

    private static int CountFatalLogEntries(string logsDirectory)
        => Directory.Exists(logsDirectory)
            ? Directory.EnumerateFiles(logsDirectory, "openmu*.log")
                .SelectMany(File.ReadLines)
                .Count(line => line.Contains("[Fatal]", StringComparison.Ordinal))
            : 0;

    private static void AssertRunningOnLoopbackOnly(LocalStackManager manager)
    {
        Assert.Multiple(() =>
        {
            Assert.That(manager.Status.State, Is.EqualTo(LocalStackState.Running));
            Assert.That(manager.Status.DatabaseProcessId, Is.Not.Null);
            Assert.That(manager.Status.ServerProcessId, Is.Not.Null);
            Assert.That(manager.Status.ClientProcessId, Is.Null, "The no-UI smoke test unexpectedly started the game client.");
        });

        var expectedPorts = new[]
        {
            manager.Settings.DatabasePort,
            manager.Settings.AdminPanelPort,
            44405,
            manager.Settings.ConnectServerPort,
            55901,
            55902,
            55980,
        };
        var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        foreach (var port in expectedPorts.Distinct())
        {
            var endpoints = listeners.Where(endpoint => endpoint.Port == port).ToArray();
            Assert.That(endpoints, Is.Not.Empty, $"Expected loopback listener 127.0.0.1:{port} was not active.");
            Assert.That(
                endpoints.All(endpoint => endpoint.Address.Equals(IPAddress.Loopback)),
                Is.True,
                $"Port {port} was exposed beyond IPv4 loopback: {string.Join(", ", endpoints.Select(endpoint => endpoint.Address))}");
        }
    }
}
