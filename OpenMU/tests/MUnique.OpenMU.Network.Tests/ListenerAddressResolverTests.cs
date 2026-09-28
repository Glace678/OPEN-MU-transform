// <copyright file="ListenerAddressResolverTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network.Tests;

using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Tests the explicit TCP listener binding boundary.
/// </summary>
[NonParallelizable]
public class ListenerAddressResolverTests
{
    private string? _originalValue;

    /// <summary>Saves the process environment.</summary>
    [SetUp]
    public void SetUp()
    {
        this._originalValue = Environment.GetEnvironmentVariable(ListenerAddressResolver.EnvironmentVariableName);
    }

    /// <summary>Restores the process environment.</summary>
    [TearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(ListenerAddressResolver.EnvironmentVariableName, this._originalValue);
    }

    /// <summary>Verifies backward-compatible default binding.</summary>
    [Test]
    public void DefaultsToAllIpv4Interfaces()
    {
        Environment.SetEnvironmentVariable(ListenerAddressResolver.EnvironmentVariableName, null);
        Assert.That(ListenerAddressResolver.Resolve(), Is.EqualTo(IPAddress.Any));
    }

    /// <summary>Verifies local packages can force loopback binding.</summary>
    [Test]
    public void ResolvesExplicitLoopbackAddress()
    {
        Environment.SetEnvironmentVariable(ListenerAddressResolver.EnvironmentVariableName, "127.0.0.1");
        Assert.That(ListenerAddressResolver.Resolve(), Is.EqualTo(IPAddress.Loopback));
    }

    /// <summary>An occupied wildcard endpoint cannot be shadowed by a local game server.</summary>
    [TestCase("0.0.0.0")]
    [TestCase("127.0.0.1")]
    public void ListenerRejectsOccupiedPort(string address)
    {
        Environment.SetEnvironmentVariable(ListenerAddressResolver.EnvironmentVariableName, "127.0.0.1");
        var existing = new TcpListener(IPAddress.Parse(address), 0) { ExclusiveAddressUse = false };
        Listener? listener = null;
        try
        {
            existing.Start();
            var port = ((IPEndPoint)existing.LocalEndpoint).Port;
            listener = new Listener(port, null, null, NullLoggerFactory.Instance);
            Assert.Throws<SocketException>(() => listener.Start());
            Assert.That(listener.IsBound, Is.False);
            Assert.That(existing.Server.IsBound, Is.True);
        }
        finally
        {
            listener?.Stop();
            existing.Stop();
        }
    }

    /// <summary>A bound exclusive listener continues accepting after the first connection.</summary>
    [Test]
    public async Task ListenerAcceptsSuccessiveConnections()
    {
        Environment.SetEnvironmentVariable(ListenerAddressResolver.EnvironmentVariableName, "127.0.0.1");
        var portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
        portProbe.Stop();
        var listener = new Listener(port, null, null, NullLoggerFactory.Instance);
        var accepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        listener.ClientAccepting += args =>
        {
            args.Cancel = true;
            if (Interlocked.Increment(ref count) == 3)
            {
                accepted.TrySetResult();
            }

            return ValueTask.CompletedTask;
        };
        try
        {
            listener.Start();
            Assert.That(listener.IsBound, Is.True);
            for (var i = 0; i < 3; i++)
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port).ConfigureAwait(false);
            }

            await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        finally
        {
            listener.Stop();
        }

        Assert.That(listener.IsBound, Is.False);
    }

    /// <summary>Repeated shutdown must release an exclusive socket without recreating it.</summary>
    [Test]
    public async Task RepeatedConcurrentStopReleasesPortAndAllowsRestart()
    {
        Environment.SetEnvironmentVariable(ListenerAddressResolver.EnvironmentVariableName, "127.0.0.1");
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var listener = new Listener(port, null, null, NullLoggerFactory.Instance);
        try
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                listener.Start();
                await Task.WhenAll(Task.Run(listener.Stop), Task.Run(listener.Stop)).ConfigureAwait(false);
                Assert.That(listener.IsBound, Is.False);
            }
        }
        finally
        {
            listener.Stop();
        }
        var replacement = new TcpListener(IPAddress.Loopback, port) { ExclusiveAddressUse = true };
        try { Assert.DoesNotThrow(() => replacement.Start()); }
        finally { replacement.Stop(); }
    }

    /// <summary>Verifies malformed or IPv6 values fail closed.</summary>
    [TestCase("localhost")]
    [TestCase("::1")]
    public void InvalidAddressFailsClosed(string value)
    {
        Environment.SetEnvironmentVariable(ListenerAddressResolver.EnvironmentVariableName, value);
        Assert.Throws<InvalidOperationException>(() => ListenerAddressResolver.Resolve());
    }
}
