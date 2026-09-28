// <copyright file="ConnectServerClientTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Buffers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.ConnectServer;
using MUnique.OpenMU.ConnectServer.PacketHandler;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.PlugIns;

/// <summary>Tests connection-server packet bounds without opening a network port.</summary>
[TestFixture]
public class ConnectServerClientTests
{
    /// <summary>Oversized packets stop at disconnect; valid packets still reach their handler.</summary>
    [TestCase(4, true)]
    [TestCase(12, false)]
    public async Task ReceiveRespectsBufferSize(int packetSize, bool accepted)
    {
        AsyncEventHandler<ReadOnlySequence<byte>>? receive = null;
        var connection = new Mock<IConnection>();
        connection.SetupAdd(c => c.PacketReceived += It.IsAny<AsyncEventHandler<ReadOnlySequence<byte>>>())
            .Callback<AsyncEventHandler<ReadOnlySequence<byte>>>(handler => receive = handler);
        var handler = new CountingPacketHandler();
        using var client = new Client(connection.Object, TimeSpan.FromMinutes(1), handler, 10, NullLogger<Client>.Instance);
        var packet = new byte[packetSize];
        packet[0] = 0xC1;
        packet[1] = (byte)packetSize;
        packet[2] = 0xF4;
        packet[3] = 0x06;

        Assert.That(receive, Is.Not.Null);
        await receive!(new ReadOnlySequence<byte>(packet)).ConfigureAwait(false);

        Assert.That(handler.Count, Is.EqualTo(accepted ? 1 : 0));
        connection.Verify(c => c.DisconnectAsync(), accepted ? Times.Never() : Times.Once());
    }

    private sealed class CountingPacketHandler : IPacketHandler<Client>
    {
        public int Count { get; private set; }

        public ValueTask HandlePacketAsync(Client obj, Memory<byte> packet)
        {
            this.Count++;
            return ValueTask.CompletedTask;
        }
    }
}
