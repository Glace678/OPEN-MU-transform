// <copyright file="OpenMuServerManagerTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher.Tests;

using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;

/// <summary>
/// Tests the process-identity boundary of forced local-server shutdown.
/// </summary>
public class OpenMuServerManagerTests
{
    private string _directory = null!;
    private LocalPaths _paths = null!;

    /// <summary>Creates an isolated package-shaped directory.</summary>
    [SetUp]
    public void SetUp()
    {
        this._directory = Path.Combine(Path.GetTempPath(), $"OpenMU 强停 tests {Guid.NewGuid():N}");
        this._paths = new LocalPaths(this._directory);
        Directory.CreateDirectory(this._paths.ServerDirectory);
        File.Copy(WaitExecutable, this._paths.ServerExecutable);
    }

    /// <summary>Removes the isolated package directory.</summary>
    [TearDown]
    public void TearDown()
    {
        Directory.Delete(this._directory, recursive: true);
    }

    /// <summary>Verifies force stop terminates a reattached process from the exact packaged path.</summary>
    [Test]
    [Timeout(15_000)]
    public async Task ForceStopTerminatesVerifiedAttachedProcess()
    {
        using var process = StartWaiting(this._paths.ServerExecutable);
        var manager = new OpenMuServerManager(this._paths, new LocalStackSettings(), new ProcessRunner());
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var statusResponse = RespondOnceAsync(manager.PipeName, "status", "running", process.Id, timeout.Token);
            Assert.That(await manager.IsRunningAsync(timeout.Token), Is.True);
            await statusResponse;

            var shutdownResponse = RespondOnceAsync(manager.PipeName, "shutdown", "stopping", process.Id, timeout.Token);
            await manager.StopAsync(force: true, timeout.Token);
            await shutdownResponse;

            Assert.That(process.HasExited, Is.True);
        }
        finally
        {
            KillIfRunning(process);
        }
    }

    /// <summary>Verifies force termination refuses a process which does not run from the packaged server path.</summary>
    [Test]
    [Timeout(15_000)]
    public async Task ForceTerminationRejectsUnverifiedProcess()
    {
        using var process = StartWaiting(WaitExecutable);
        var manager = new OpenMuServerManager(this._paths, new LocalStackSettings(), new ProcessRunner());
        try
        {
            Assert.ThrowsAsync<InvalidDataException>(async () =>
                await manager.ForceTerminateVerifiedProcessAsync(process.Id, CancellationToken.None));
            Assert.That(process.HasExited, Is.False);
        }
        finally
        {
            KillIfRunning(process);
        }
    }

    /// <summary>Unix package aliases still identify the same executable, including macOS temporary paths.</summary>
    [Test]
    [Timeout(15_000)]
    public async Task ForceStopAcceptsLinkedPackageParent()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Ignore("Unix parent-directory symbolic link regression.");
        }

        var alias = this._directory + "-link";
        Directory.CreateSymbolicLink(alias, this._directory);
        using var process = StartWaiting(this._paths.ServerExecutable);
        try
        {
            var manager = new OpenMuServerManager(new LocalPaths(alias), new LocalStackSettings(), new ProcessRunner());
            await manager.ForceTerminateVerifiedProcessAsync(process.Id, CancellationToken.None);
            Assert.That(process.HasExited, Is.True);
        }
        finally
        {
            KillIfRunning(process);
            Directory.Delete(alias);
        }
    }

    private static string WaitExecutable => OperatingSystem.IsWindows()
        ? Path.Combine(Environment.SystemDirectory, "ping.exe") : "/bin/sleep";

    private static Process StartWaiting(string executable)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add("127.0.0.1");
        }
        else
        {
            startInfo.ArgumentList.Add("60");
        }
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start the test process.");
    }

    private static async Task RespondOnceAsync(
        string pipeName,
        string expectedCommand,
        string state,
        int processId,
        CancellationToken cancellationToken)
    {
        await using var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.WaitForConnectionAsync(cancellationToken);
        using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
        await using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
        Assert.That(await reader.ReadLineAsync(cancellationToken), Is.EqualTo(expectedCommand));
        await writer.WriteLineAsync(JsonSerializer.Serialize(new { state, processId }));
    }

    private static void KillIfRunning(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
    }
}
