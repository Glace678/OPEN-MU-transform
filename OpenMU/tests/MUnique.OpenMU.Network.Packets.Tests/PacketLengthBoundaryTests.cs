// <copyright file="PacketLengthBoundaryTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network.Packets.Tests;

using Moq;
using Nito.AsyncEx;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ChatServer;
using MUnique.OpenMU.Network.Packets.ClientToServer;

/// <summary>
/// Regression tests for the single-byte length and index bounds hardening (Code5#6-#9).
/// </summary>
[TestFixture]
public class PacketLengthBoundaryTests
{
    /// <summary>Code5#6: the ChatRoomClients indexer must reject out-of-range indices explicitly.</summary>
    [Test]
    public void ChatRoomClientsIndexerRejectsOutOfRange()
    {
        var data = new byte[ChatRoomClients.GetRequiredSize(2)];
        var packet = (ChatRoomClients)data.AsMemory();
        packet.ClientCount = 2;

        Assert.DoesNotThrow(() => { _ = packet[0]; _ = packet[1]; });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = packet[2]; });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = packet[-1]; });
    }

    /// <summary>Code5#7/#8: a chat message payload above the single-byte limit is rejected, not wrapped.</summary>
    [Test]
    public void SendChatMessageRejectsOversizedPayloadAsync()
    {
        var connection = CreateConnection();
        var message = new byte[300]; // payload limit is 250 (packet = payload + 5, header is one byte)
        Assert.ThrowsAsync<ArgumentException>(async () => await connection.Object.SendChatMessageAsync(0, 0, message));
    }

    /// <summary>Code5#9: a public chat message whose total exceeds 255 bytes is rejected.</summary>
    [Test]
    public void SendPublicChatMessageRejectsOversizedPayload()
    {
        var connection = CreateConnection();
        var message = new string('a', 300); // UTF-8 300 bytes -> 314 total > 255
        Assert.ThrowsAsync<ArgumentException>(async () => await connection.Object.SendPublicChatMessageAsync("sender", message));
    }

    /// <summary>Code5#9: a whisper message whose total exceeds 255 bytes is rejected.</summary>
    [Test]
    public void SendWhisperMessageRejectsOversizedPayload()
    {
        var connection = CreateConnection();
        var message = new string('a', 300);
        Assert.ThrowsAsync<ArgumentException>(async () => await connection.Object.SendWhisperMessageAsync("receiver", message));
    }

    private static Mock<IConnection> CreateConnection()
    {
        var connection = new Mock<IConnection>();
        connection.SetupGet(c => c.Connected).Returns(true);
        connection.SetupGet(c => c.OutputLock).Returns(new AsyncLock());
        return connection;
    }
}
