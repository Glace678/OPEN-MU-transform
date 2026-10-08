// <copyright file="CapturedPacketReader.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network;

using System.Buffers;
using System.IO.Pipelines;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handler for a captured data packet.
/// </summary>
/// <param name="packet">The complete data packet.</param>
internal delegate void PacketCapturedHandler(ReadOnlySpan<byte> packet);

/// <summary>
/// Splits the captured outgoing data of a connection into data packets again.
/// </summary>
/// <remarks>
/// One write to a <see cref="PipeWriter"/> is not necessarily one data packet: a bigger message
/// may be written in several chunks, and more than one packet may be written before the writer
/// gets flushed. The written data is therefore copied into an own pipe, which is read by this
/// class like any other packet source of the network layer.
/// </remarks>
internal sealed class CapturedPacketReader : PacketPipeReaderBase
{
    private readonly PacketCapturedHandler _packetCaptured;

    private readonly Action _completed;

    private readonly ILogger _logger;

    private readonly Pipe _pipe;

    private bool _isCompleted;

    /// <summary>
    /// Initializes a new instance of the <see cref="CapturedPacketReader"/> class.
    /// </summary>
    /// <param name="packetCaptured">The handler which is called for each complete data packet.</param>
    /// <param name="completed">Is called when this reader stopped reading, e.g. because the
    /// captured data was malformed. The caller should then stop writing into the <see cref="Writer"/>.</param>
    /// <param name="logger">The logger.</param>
    public CapturedPacketReader(PacketCapturedHandler packetCaptured, Action completed, ILogger logger)
    {
        this._packetCaptured = packetCaptured;
        this._completed = completed;
        this._logger = logger;

        // A threshold of 0 makes the pipe unbounded: attaching a capture on a slow
        // or hostile connection would let the captured buffer grow without limit.
        // Cap the backlog so backpressure applies (or the capture is dropped by the
        // caller) instead of exhausting process memory. Capture is a diagnostic,
        // opt-in feature; it must not be able to consume unbounded memory.
        const long pauseWriterThreshold = 16 * 1024 * 1024;
        const long resumeWriterThreshold = 8 * 1024 * 1024;
        this._pipe = new Pipe(new PipeOptions(
            useSynchronizationContext: false,
            pauseWriterThreshold: pauseWriterThreshold,
            resumeWriterThreshold: resumeWriterThreshold));
        this.Source = this._pipe.Reader;
    }

    /// <summary>
    /// Gets the writer, into which the captured data is written.
    /// </summary>
    public PipeWriter Writer => this._pipe.Writer;

    /// <summary>
    /// Starts reading the captured data.
    /// </summary>
    public void Start()
    {
        _ = this.ReadCapturedDataAsync();
    }

    /// <summary>
    /// Stops the reader by completing the <see cref="Writer"/>.
    /// </summary>
    public void Stop()
    {
        try
        {
            this._pipe.Writer.Complete();
        }
        catch (Exception ex)
        {
            this._logger.LogDebug(ex, "Error when completing the packet capture.");
        }
    }

    /// <inheritdoc />
    protected override ValueTask<bool> ReadPacketAsync(ReadOnlySequence<byte> packet)
    {
        if (packet.IsSingleSegment)
        {
            this._packetCaptured(packet.FirstSpan);
        }
        else
        {
            this._packetCaptured(packet.ToArray());
        }

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc />
    protected override async ValueTask OnCompleteAsync(Exception? exception)
    {
        if (this._isCompleted)
        {
            return;
        }

        this._isCompleted = true;
        if (exception is not null)
        {
            this._logger.LogWarning(exception, "Error while reading the captured data packets. The capturing is stopped.");
        }

        await this._pipe.Reader.CompleteAsync(exception).ConfigureAwait(false);
        this._completed();
    }

    private async Task ReadCapturedDataAsync()
    {
        try
        {
            await this.ReadSourceAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // ReadSourceAsync already reported it to OnCompleteAsync; the capturing of this
            // connection is over, but the connection itself is not affected.
            this._logger.LogDebug(ex, "The packet capture reader stopped.");
        }
    }
}
