// <copyright file="ConnectionWrapper.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.Client.Library;

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using MUnique.OpenMU.Network;
using Nito.AsyncEx.Synchronous;

/// <summary>
/// A wrapper for a <see cref="Connection"/>.
/// </summary>
public sealed class ConnectionWrapper : IDisposable
{
    private readonly int _handle;
    private readonly Connection _connection;

    /// <summary>
    /// The unmanaged callback to a packet handler. Parameters:
    ///   - handle
    ///   - packet size
    ///   - pointer to packet.
    /// </summary>
    private readonly unsafe delegate* unmanaged<int, int, byte*, void> _onPacketReceived;

    /// <summary>
    /// The unmanaged callback to a disconnect handler. Parameter: handle.
    /// </summary>
    private readonly unsafe delegate* unmanaged<int, void> _onDisconnected;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionWrapper"/> class.
    /// </summary>
    /// <param name="handle">The handle of the connection.</param>
    /// <param name="connection">The connection.</param>
    /// <param name="onPacketReceived">
    /// The pointer to an unmanaged method which is called when a new packet got received.
    /// Parameters: handle, size, pointer to the data.
    /// </param>
    /// <param name="onDisconnected">
    /// The pointer to an unmanaged method which is called when the connection got disconnected.
    /// Parameter: handle.
    /// </param>
    public unsafe ConnectionWrapper(int handle, Connection connection, delegate* unmanaged<int, int, byte*, void> onPacketReceived, delegate* unmanaged<int, void> onDisconnected)
    {
        this._handle = handle;
        this._connection = connection;
        this._onPacketReceived = onPacketReceived;
        this._onDisconnected = onDisconnected;

        connection.PacketReceived += this.OnPacketReceivedAsync;
        connection.Disconnected += this.OnDisconnectedAsync;
    }

    /// <summary>
    /// Gets the output pipe writer.
    /// </summary>
    internal PipeWriter Output => this._connection.Output;

    /// <summary>
    /// Begins receiving packets from the client.
    /// </summary>
    public void BeginReceive()
    {
        // we never want it on the main thread, so we do a Task.Run.
        // The receive loop must not be fire-and-forget: if it faults (socket reset,
        // corrupt pipe) the exception would otherwise land in an unobserved task and
        // the connection would stay registered as alive while no more bytes flow.
        _ = Task.Run(async () =>
        {
            try
            {
                await this._connection.BeginReceiveAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Receive loop failed on handle {this._handle}: {ex.Message}");
                try
                {
                    this._connection.DisconnectAsync().AsTask().WaitAndUnwrapException();
                }
                catch (Exception innerEx)
                {
                    Debug.WriteLine($"Disconnect after receive failure also failed: {innerEx.Message}");
                }
            }
        });
    }

    private int _disposed;

    /// <inheritdoc />
    public void Dispose()
    {
        // Guard against double-dispose (DisconnectAndDispose + OnDisconnectedAsync both
        // reach here) and unhook the handlers so a disposed wrapper can no longer fire
        // native callbacks or be re-collected while the native side still holds it.
        if (Interlocked.Exchange(ref this._disposed, 1) != 0)
        {
            return;
        }

        this._connection.PacketReceived -= this.OnPacketReceivedAsync;
        this._connection.Disconnected -= this.OnDisconnectedAsync;
        this._connection.Dispose();
    }

    /// <summary>
    /// Disconnects the connection.
    /// </summary>
    public void DisconnectAndDispose()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await this._connection.DisconnectAsync();
                this._connection.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        });
    }

    /// <summary>
    /// Sends the specified bytes.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    public void Send(Span<byte> bytes)
    {
        using var l = this._connection.OutputLock.Lock();
        var targetSpan = this._connection.Output.GetSpan(bytes.Length);

        bytes.CopyTo(targetSpan);
        this._connection.Output.Advance(bytes.Length);
        this._connection.Output.FlushAsync().AsTask().WaitAndUnwrapException();
    }

    /// <summary>
    /// Sends the specified bytes.
    /// </summary>
    /// <param name="packetFactory">The factory which creates the packet and returns the length of it.</param>
    public void CreateAndSend(Func<PipeWriter, int> packetFactory)
    {
        using var l = this._connection.OutputLock.Lock();
        var length = packetFactory(this._connection.Output);
        this._connection.Output.Advance(length);
        this._connection.Output.FlushAsync().AsTask().WaitAndUnwrapException();
    }

    private unsafe ValueTask OnPacketReceivedAsync(ReadOnlySequence<byte> args)
    {
        // PROTO-4: a callback buffer may contain multiple concatenated frames (or,
        // for a corrupted pipe, trailing bytes). Hand the native side exactly the
        // frames declared by their headers -- never a multi-packet blob, and never
        // past a declared boundary. Each frame is copied to a pinned buffer so the
        // native pointer stays valid for the synchronous callback only; it must
        // not be retained by the callee (the pooled owner is disposed right after).
        // Hoisted out of the loop (CA2014): overwritten in full on every iteration.
        Span<byte> header = stackalloc byte[3];
        var remaining = args;
        while (remaining.Length >= 2)
        {
            // C1/C3 headers are only 2 bytes (type + size); C2/C4 are 3 bytes.
            // Peek the type byte first, then pull exactly the number of bytes the
            // header needs -- otherwise a 2-byte short control frame left at the
            // tail of a segment would never be consumed and would be dropped as
            // "trailing bytes", stalling the protocol state machine.
            remaining.Slice(0, 1).CopyTo(header);
            int need = header[0] is 0xC2 or 0xC4 ? 3 : 2;
            if (remaining.Length < need)
            {
                break;
            }

            remaining.Slice(0, need).CopyTo(header);
            var declaredSize = header.GetPacketSize();

            // Minimum valid frame: type + size (+ high size byte for C2/C4).
            var minSize = need;
            if (declaredSize < minSize || declaredSize > remaining.Length)
            {
                Debug.WriteLine(
                    "Handle {0}: malformed frame header (type 0x{1:X2}, declared size {2}, {3} bytes buffered); dropping rest of the buffer.",
                    this._handle, header[0], declaredSize, remaining.Length);
                break;
            }

            var frameSequence = remaining.Slice(0, declaredSize);
            using var memoryOwner = MemoryPool<byte>.Shared.Rent(declaredSize);
            var packet = memoryOwner.Memory.Slice(0, declaredSize);
            frameSequence.CopyTo(packet.Span);

            fixed (byte* packetPtr = packet.Span)
            {
                try
                {
                    this._onPacketReceived(this._handle, packet.Length, packetPtr);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }

            remaining = remaining.Slice(declaredSize);
        }

        if (remaining.Length > 0)
        {
            Debug.WriteLine("Handle {0}: {1} trailing byte(s) after frame splitting discarded.", this._handle, remaining.Length);
        }

        return ValueTask.CompletedTask;
    }

    private unsafe ValueTask OnDisconnectedAsync()
    {
        try
        {
            this._onDisconnected(this._handle);
            this.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

        return ValueTask.CompletedTask;
    }
}