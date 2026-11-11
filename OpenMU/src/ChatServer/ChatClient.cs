// <copyright file="ChatClient.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.ChatServer;

using System.Buffers;
using System.Threading;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ChatServer;
using MUnique.OpenMU.Network.Xor;

/// <summary>
/// ChatClient implementation, uses socket connections.
/// </summary>
/// <remarks>
/// Messages are decrypted and encrypted again with the same XOR3 key - in theory we could optimize this (and
/// the conversion to a string) away. However, we'll leave it for easier debugging.
/// </remarks>
internal class ChatClient : IChatClient
{
    private const int TokenOffset = 6;
    private const int MessageOffset = 5;
    private static readonly ISpanDecryptor TokenDecryptor = new Xor3Decryptor(TokenOffset);

    /// <summary>
    /// <see cref="ISpanDecryptor"/> for chat messages. The incoming chat messages are "encrypted" with the commonly known XOR-3 encryption for reasons we don't know ;).
    /// </summary>
    private static readonly ISpanDecryptor MessageDecryptor = new Xor3Decryptor(MessageOffset);

    /// <summary>
    /// <see cref="ISpanEncryptor"/> for chat messages. The outgoing chat messages are "encrypted" with the commonly known XOR-3 encryption for reasons we don't know ;).
    /// </summary>
    private static readonly ISpanEncryptor MessageEncryptor = new Xor3Encryptor(MessageOffset);

    private readonly ChatRoomManager _manager;
    private readonly ILogger<ChatClient> _logger;
    private readonly byte[] _packetBuffer = new byte[0xFF];
    private IConnection? _connection;
    private ChatRoom? _room;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatClient" /> class.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="manager">The manager.</param>
    /// <param name="logger">The logger.</param>
    public ChatClient(IConnection connection, ChatRoomManager manager, ILogger<ChatClient> logger)
    {
        this._manager = manager;
        this._logger = logger;
        this._connection = connection;
        this._connection.PacketReceived += this.ReadPacketAsync;
        this._connection.Disconnected += this.LogOffAsync;

        this.LastActivity = DateTime.UtcNow;
        _ = this._connection.BeginReceiveAsync();
    }

    /// <summary>
    /// Occurs when the client has been disconnected.
    /// </summary>
    public event EventHandler? Disconnected;

    /// <inheritdoc/>
    public byte Index
    {
        get;
        set;
    }

    /// <inheritdoc />
    public string? AuthenticationToken { get; private set; }

    /// <inheritdoc/>
    public string? Nickname { get; set; }

    /// <inheritdoc/>
    public DateTime LastActivity { get; private set; }

    /// <summary>
    /// Gets the connection of this client, so that its traffic can be captured.
    /// </summary>
    internal IConnection? Connection => this._connection;

    /// <inheritdoc/>
    public async ValueTask SendMessageAsync(byte senderId, string message)
    {
        if (this._connection is not { } connection)
        {
            return;
        }

        int WritePacket()
        {
            Span<byte> utf8Buffer = stackalloc byte[byte.MaxValue];
            Encoding.UTF8.GetEncoder().Convert(message.AsSpan(), utf8Buffer, true, out _, out var actualByteCount, out _);
            var messageLength = (byte)actualByteCount;
            var length = ChatMessageRef.GetRequiredSize(messageLength);
            var packet = new ChatMessageRef(connection.Output.GetSpan(length)[..length]);
            packet.SenderIndex = senderId;
            packet.MessageLength = messageLength;
            utf8Buffer[..actualByteCount].CopyTo(packet.Message);
            MessageEncryptor.Encrypt(packet);
            return length;
        }

        await connection.SendAsync(WritePacket).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask SendChatRoomClientListAsync(IReadOnlyCollection<IChatClient> clients)
    {
        if (this._connection is not { } connection)
        {
            return;
        }

        int WritePacket()
        {
            var clientCount = Math.Min(clients.Count, byte.MaxValue);
            var length = ChatRoomClientsRef.GetRequiredSize(clientCount);
            var packet = new ChatRoomClientsRef(connection.Output.GetSpan(length)[..length]);
            packet.ClientCount = (byte)clientCount;
            int i = 0;
            foreach (var client in clients)
            {
                if (i >= clientCount)
                {
                    break;
                }

                var clientBlock = packet[i];
                clientBlock.Index = client.Index;
                clientBlock.Name = client.Nickname ?? string.Empty;
                i++;
            }

            return length;
        }

        await connection.SendAsync(WritePacket).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask SendChatRoomClientUpdateAsync(byte updatedClientId, string updatedClientName, ChatRoomClientUpdateType updateType)
    {
        if (this._connection is not { Connected: true } connection)
        {
            return;
        }

        try
        {
            if (updateType == ChatRoomClientUpdateType.Joined)
            {
                await connection.SendChatRoomClientJoinedAsync(updatedClientId, updatedClientName).ConfigureAwait(false);
            }
            else
            {
                await connection.SendChatRoomClientLeftAsync(updatedClientId, updatedClientName).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Error sending room update");
        }
    }

    /// <inheritdoc/>
    public async ValueTask LogOffAsync()
    {
        var connection = Interlocked.Exchange(ref this._connection, null);
        if (connection is null)
        {
            this._logger.LogDebug("Client {Nickname} is already disconnected.", this.Nickname);
            return;
        }

        connection.PacketReceived -= this.ReadPacketAsync;
        connection.Disconnected -= this.LogOffAsync;
        this._logger.LogDebug("Client {Connection} is going to be disconnected.", connection);
        var room = Interlocked.Exchange(ref this._room, null);
        if (room != null)
        {
            await room.LeaveAsync(this).ConfigureAwait(false);
        }

        await connection.DisconnectAsync().ConfigureAwait(false);

        this.Disconnected?.Invoke(this, EventArgs.Empty);
        this.Disconnected = null;
    }

    /// <summary>
    /// Returns a <see cref="string" /> that represents this instance.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        return $"Connection:{this._connection}, Client name:{this.Nickname}, Room-ID:{this._room?.RoomId}, Index: {this.Index}";
    }

    private async ValueTask ReadPacketAsync(ReadOnlySequence<byte> sequence)
    {
        if (sequence.Length < 3)
        {
            return;
        }

        if (sequence.Length > this._packetBuffer.Length)
        {
            this._logger.LogWarning("Chat packet length {Length} exceeds maximum buffer size.", sequence.Length);
            await this.LogOffAsync().ConfigureAwait(false);
            return;
        }

        sequence.CopyTo(this._packetBuffer);
        var packet = this._packetBuffer.AsMemory(0, (int)sequence.Length);
        if (this._packetBuffer[0] != Authenticate.HeaderType)
        {
            return;
        }

        this.LastActivity = DateTime.UtcNow;
        switch (this._packetBuffer[2])
        {
            case 0:
                await this.AuthenticateAsync(packet).ConfigureAwait(false);
                break;
            case 1:
            case 2:
            case 3:
                // We did never capture such packets, but they don't seem to be wrong (next is 4), so do nothing.
                break;
            case 4:
                if (this._room is { } room && this.CheckMessage(packet))
                {
                    MessageDecryptor.Decrypt(packet.Span);
                    var message = packet.Span.ExtractString(5, packet.Span[4], Encoding.UTF8);
                    if (this._logger.IsEnabled(LogLevel.Debug))
                    {
                        this._logger.LogDebug("Message received from {Index}: \"{message}\"", this.Index, message);
                    }

                    await room.SendMessageAsync(this.Index, message).ConfigureAwait(false);
                }

                break;

            case 5:
                // This is something like a keep-connection-alive packet.
                // Last activity is always set, so we have to do nothing here.
                this._logger.LogDebug("Keep-alive received");
                break;

            case var value:
                this._logger.LogError("Received unknown packet of type {PacketType}: {PacketSpan}", value, packet.Span.AsString());
                await this.LogOffAsync().ConfigureAwait(false);
                break;
        }
    }

    private bool CheckMessage(Memory<byte> packet)
    {
        return packet.Length > 4 && (packet.Span[4] + 5) <= packet.Length;
    }

    private async ValueTask AuthenticateAsync(Memory<byte> packet)
    {
        if (packet.Length < Authenticate.Length)
        {
            this._logger.LogWarning("Authentication packet is too short ({Length} bytes).", packet.Length);
            await this.LogOffAsync().ConfigureAwait(false);
            return;
        }

        var roomId = NumberConversionExtensions.MakeWord(packet.Span[4], packet.Span[5]);
        var requestedRoom = this._manager.GetChatRoom(roomId);
        if (requestedRoom is null)
        {
            this._logger.LogError("Requested room {RoomId} has not been registered before.", roomId);
            await this.LogOffAsync().ConfigureAwait(false);
            return;
        }

        TokenDecryptor.Decrypt(packet.Span);
        var tokenAsString = packet.Span.ExtractString(TokenOffset, 10, Encoding.UTF8);
        if (!uint.TryParse(tokenAsString, out uint _))
        {
            this._logger.LogError("The supplied authentication token is not a parseable integer.");
            await this.LogOffAsync().ConfigureAwait(false);
            return;
        }

        this.AuthenticationToken = tokenAsString;
        if (await requestedRoom.TryJoinAsync(this).ConfigureAwait(false))
        {
            this._room = requestedRoom;
            if (Volatile.Read(ref this._connection) is null)
            {
                var orphanedRoom = Interlocked.Exchange(ref this._room, null);
                if (orphanedRoom is not null)
                {
                    await orphanedRoom.LeaveAsync(this).ConfigureAwait(false);
                }
            }
        }
        else
        {
            await this.LogOffAsync().ConfigureAwait(false);
        }
    }
}