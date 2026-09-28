// <copyright file="PostgreSqlUnicodePathSmokeTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher.Tests;

using System.Net;
using System.Net.Sockets;

/// <summary>
/// Runs PostgreSQL's real Windows tools through the protected ASCII aliases.
/// </summary>
public class PostgreSqlUnicodePathSmokeTests
{
    /// <summary>Initializes, starts, probes, and stops PostgreSQL from a non-ASCII package path.</summary>
    [Test]
    [Explicit("Requires OPENMU_POSTGRES_SMOKE_ROOT pointing to a disposable package with Runtime/PostgreSQL.")]
    public async Task InitializeStartReadyAndStopFromUnicodePackage()
    {
        var packageRoot = Environment.GetEnvironmentVariable("OPENMU_POSTGRES_SMOKE_ROOT");
        Assert.That(packageRoot, Is.Not.Null.And.Not.Empty);
        packageRoot = Path.GetFullPath(packageRoot!);
        Assert.That(packageRoot.Any(character => character > 0x7F), Is.True, "The smoke package must exercise a non-ASCII path.");

        var paths = new LocalPaths(packageRoot);
        Assert.That(File.Exists(paths.InitDbExecutable), Is.True, $"Missing PostgreSQL runtime at {paths.PostgreSqlRuntimeDirectory}.");
        Assert.That(Directory.Exists(Path.Combine(packageRoot, "Data")), Is.False, "The smoke package Data directory must not already exist.");
        paths.EnsureDataDirectories();

        var aliasBaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "OpenMU-Local",
            "SmokeAliases",
            Guid.NewGuid().ToString("N"));
        PostgreSqlExecutionPaths? executionPaths = null;
        PostgreSqlManager? manager = null;
        try
        {
            executionPaths = PostgreSqlExecutionPaths.Create(paths, aliasBaseDirectory);
            var settings = new LocalStackSettings
            {
                DatabasePort = ReserveLoopbackPort(),
                DatabaseStartupTimeoutSeconds = 60,
            };
            manager = new PostgreSqlManager(paths, executionPaths, settings, new ProcessRunner());
            await manager.StartAsync($"Smoke-{Guid.NewGuid():N}!", CancellationToken.None);

            Assert.That(await manager.IsRunningAsync(CancellationToken.None), Is.True);
            Assert.That(File.Exists(Path.Combine(paths.PostgreSqlDataDirectory, "PG_VERSION")), Is.True);
            Assert.That(File.Exists(Path.Combine(executionPaths.DataDirectory, "PG_VERSION")), Is.True);
        }
        finally
        {
            if (manager is not null && await manager.IsRunningAsync(CancellationToken.None))
            {
                await manager.StopAsync(CancellationToken.None);
            }

            if (executionPaths is not null)
            {
                DeleteJunction(executionPaths.RuntimeDirectory);
                DeleteJunction(executionPaths.DataDirectory);
                DeleteJunction(executionPaths.KeysDirectory);
                DeleteJunction(executionPaths.LogsDirectory);
                DeleteJunction(executionPaths.BackupsDirectory);
            }

            if (Directory.Exists(aliasBaseDirectory))
            {
                Directory.Delete(aliasBaseDirectory, recursive: true);
            }

            if (Directory.Exists(Path.Combine(packageRoot, "Data")))
            {
                Directory.Delete(Path.Combine(packageRoot, "Data"), recursive: true);
            }
        }
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static void DeleteJunction(string path)
    {
        if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            Directory.Delete(path);
        }
    }
}
