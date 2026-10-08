// <copyright file="InvalidPacketHeaderException.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network;

using System.Buffers;
using System.Globalization;

/// <summary>
/// An exception which occurs when a packet header is invalid.
/// </summary>
/// <remarks>
/// This can be the case when:
///   * The first byte of the packet is not one of <c>0xC1</c>, <c>0xC2</c>, <c>0xC3</c>, <c>0xC4</c>.
///   * The length is defined as 0.
/// </remarks>
/// <seealso cref="System.Exception" />
public class InvalidPacketHeaderException : Exception
{
    /// <summary>
    /// The message, if built yet.
    /// </summary>
    private string? _message;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidPacketHeaderException"/> class.
    /// </summary>
    /// <param name="header">The header which is invalid.</param>
    /// <param name="bufferContent">Content of the buffer when the header was read. With this information you can e.g. find out if a previous packet was malformed.</param>
    /// <param name="position">The position of the <paramref name="header"/> in the <paramref name="bufferContent"/>.</param>
    /// <summary>
    /// Maximum number of leading buffer bytes retained in the exception/log. The
    /// receive pipe can hold a large backlog; copying and logging the whole buffer
    /// on every bad header would amplify both memory use and log volume.
    /// </summary>
    private const int MaximumLoggedBufferBytes = 256;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidPacketHeaderException"/> class.
    /// </summary>
    /// <param name="header">The (invalid) packet header.</param>
    /// <param name="bufferContent">Content of the buffer when the header was read.</param>
    /// <param name="position">The position of the <paramref name="header"/> in the buffer.</param>
    public InvalidPacketHeaderException(byte[] header, ReadOnlySequence<byte> bufferContent, SequencePosition position)
    {
        this.Header = header.ToArray();
        this.BufferWasTruncated = bufferContent.Length > MaximumLoggedBufferBytes;
        var bounded = this.BufferWasTruncated
            ? bufferContent.Slice(0, MaximumLoggedBufferBytes)
            : bufferContent;
        this.BufferContent = bounded.ToArray();
        this.Position = position.GetInteger();
    }

    /// <summary>
    /// Gets the header which is invalid.
    /// </summary>
    public byte[] Header { get; }

    /// <summary>
    /// Gets the content of the buffer when the header was read. With this information you can e.g. find out if a previous packet was malformed.
    /// </summary>
    public byte[] BufferContent { get; }

    /// <summary>
    /// Gets a value indicating whether the original receive buffer was longer than the retained <see cref="BufferContent"/>.
    /// </summary>
    public bool BufferWasTruncated { get; }

    /// <summary>
    /// Gets the position of the <see cref="Header"/> in the <see cref="BufferContent"/>.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// Gets a message that describes the current exception.
    /// </summary>
    public override string Message => this._message ??= this.BuildMessage();

    private string BuildMessage()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append("The packet header is invalid: ").AppendLine(this.Header.AsString())
            .Append("Buffer position: ").AppendLine(this.Position.ToString(CultureInfo.InvariantCulture))
            .Append("Buffer content (first ").Append(this.BufferContent.Length.ToString(CultureInfo.InvariantCulture)).Append(" bytes")
            .Append(this.BufferWasTruncated ? ", truncated" : string.Empty).Append("): ")
            .AppendLine(this.BufferContent.AsString());
        return stringBuilder.ToString();
    }
}