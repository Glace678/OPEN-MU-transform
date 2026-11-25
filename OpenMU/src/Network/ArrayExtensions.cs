// <copyright file="ArrayExtensions.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network;

using System.Buffers.Binary;

/// <summary>
/// Extensions for arrays.
/// </summary>
public static class ArrayExtensions
{
    /// <summary>
    /// Extracts a string of an byte array with the specified encoding.
    /// </summary>
    /// <param name="array">The byte array.</param>
    /// <param name="startIndex">The start index.</param>
    /// <param name="maximumBytes">The maximum length.</param>
    /// <param name="encoding">The encoding.</param>
    /// <returns>The resulting string.</returns>
    public static string ExtractString(this byte[] array, int startIndex, int maximumBytes, Encoding encoding)
    {
        // Guard a negative/out-of-range startIndex (short packet => _data.Length - N
        // can be negative) and a non-positive maximum.
        if (startIndex < 0 || startIndex >= array.Length || maximumBytes <= 0)
        {
            return string.Empty;
        }

        maximumBytes = Math.Min(maximumBytes, array.Length - startIndex);
        int count = array.Skip(startIndex).Take(maximumBytes).TakeWhile(b => b != 0).Count();
        return encoding.GetString(array, startIndex, count);
    }

    /// <summary>
    /// Converts the byte array into a readable HEX string.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The HEX string.</returns>
    public static string AsString(this byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace('-', ' ');
    }

    /// <summary>
    /// Converts bytes of an array to an 32bit unsigned Integer, big endian.
    /// </summary>
    /// <param name="array">Byte array.</param>
    /// <param name="startIndex">Starting index. The array needs 3 more elements.</param>
    /// <returns>An unsigned integer.</returns>
    public static uint MakeDwordBigEndian(this Span<byte> array, int startIndex)
    {
        return BinaryPrimitives.ReadUInt32BigEndian(array[startIndex..]);
    }

    /// <summary>
    /// Converts bytes of an array to an 32bit unsigned Integer, big endian.
    /// </summary>
    /// <param name="array">Byte array.</param>
    /// <param name="startIndex">Starting index. The array needs 3 more elements.</param>
    /// <returns>An unsigned integer.</returns>
    public static uint MakeDwordBigEndian(this byte[] array, int startIndex) =>
        MakeDwordBigEndian(array.AsSpan(), startIndex);

    /// <summary>
    /// Converts bytes of an array to an 32bit unsigned Integer, small endian.
    /// </summary>
    /// <param name="array">Byte array.</param>
    /// <param name="startIndex">Starting index. The array needs 3 more elements.</param>
    /// <returns>An unsigned integer.</returns>
    public static uint MakeDwordSmallEndian(this Span<byte> array, int startIndex)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(array[startIndex..]);
    }

    /// <summary>
    /// Converts bytes of an array to an unsigned short, small endian.
    /// </summary>
    /// <param name="array">The array.</param>
    /// <param name="startIndex">The start index.</param>
    /// <returns>An unsigned short.</returns>
    public static ushort MakeWordSmallEndian(this Span<byte> array, int startIndex)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(array[startIndex..]);
    }

    /// <summary>
    /// Converts bytes of an array to an unsigned short, big endian.
    /// </summary>
    /// <param name="array">The array.</param>
    /// <param name="startIndex">The start index.</param>
    /// <returns>An unsigned short.</returns>
    public static ushort MakeWordBigEndian(this Span<byte> array, int startIndex)
    {
        return BinaryPrimitives.ReadUInt16BigEndian(array[startIndex..]);
    }

    /// <summary>
    /// Converts bytes of an array to an unsigned short, big endian.
    /// If the array is not long enough, it returns 0.
    /// </summary>
    /// <param name="array">The array.</param>
    /// <param name="startIndex">The start index.</param>
    /// <returns>An unsigned short.</returns>
    public static ushort TryMakeWordBigEndian(this Span<byte> array, int startIndex)
    {
        return startIndex >= 0 && startIndex <= array.Length - sizeof(ushort) ? array.MakeWordBigEndian(startIndex) : default;
    }

    /// <summary>
    /// Reads a little-endian unsigned short, or returns zero if there are not enough bytes.
    /// </summary>
    /// <param name="array">The source array.</param>
    /// <param name="startIndex">The index of the first byte.</param>
    /// <returns>The unsigned short.</returns>
    public static ushort TryMakeWordLittleEndian(this Span<byte> array, int startIndex)
    {
        return startIndex >= 0 && startIndex <= array.Length - sizeof(ushort) ? array.MakeWordSmallEndian(startIndex) : default;
    }

    /// <summary>
    /// Gets the size of the packet header.
    /// </summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The size of the header.</returns>
    public static int GetPacketHeaderSize(this byte[] packet)
    {
        return packet.Length > 0 ? GetPacketHeaderSize(packet[0]) : 0;
    }

    /// <summary>
    /// Gets the size of the packet header.
    /// </summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The size of the header.</returns>
    public static int GetPacketHeaderSize(this Span<byte> packet)
    {
        return packet.Length > 0 ? GetPacketHeaderSize(packet[0]) : 0;
    }

    /// <summary>
    /// Gets the size of the packet header.
    /// </summary>
    /// <param name="packetPrefix">The packet prefix.</param>
    /// <returns>The size of the header.</returns>
    public static int GetPacketHeaderSize(byte packetPrefix)
    {
        switch (packetPrefix)
        {
            case 0xC1:
            case 0xC3:
                return 2;
            case 0xC2:
            case 0xC4:
                return 3;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Gets the type of the packet. This only works when the packet type is not encrypted.
    /// </summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The type of the packet.</returns>
    public static byte GetPacketType(this Span<byte> packet)
    {
        var headerSize = packet.GetPacketHeaderSize();
        return packet.Length > headerSize ? packet[headerSize] : (byte)0;
    }

    /// <summary>
    /// Gets the sub type of the packet. This only works when the packet type is not encrypted.
    /// </summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The sub type of the packet.</returns>
    public static byte? GetPacketSubType(this Span<byte> packet)
    {
        var subTypeIndex = packet.GetPacketHeaderSize() + 1;
        if (packet.Length > subTypeIndex)
        {
            return packet[subTypeIndex];
        }

        return null;
    }

    /// <summary>
    /// Gets the size of a packet from its header.
    /// C1 and C3 packets have a maximum length of 255, and the length defined in the second byte.
    /// C2 and C4 packets have a maximum length of 65535, and the length defined in the second and third byte.
    /// </summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The size of a packet.</returns>
    public static int GetPacketSize(this Span<byte> packet)
    {
        if (packet.IsEmpty)
        {
            return 0;
        }

        switch (packet[0])
        {
            case 0xC1:
            case 0xC3:
                return packet.Length >= 2 ? packet[1] : 0;
            case 0xC2:
            case 0xC4:
                return packet.Length >= 3 ? (packet[1] << 8) | packet[2] : 0;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Gets the size of a packet from its header.
    /// C1 and C3 packets have a maximum length of 255, and the length defined in the second byte.
    /// C2 and C4 packets have a maximum length of 65535, and the length defined in the second and third byte.
    /// </summary>
    /// <param name="packet">The packet.</param>
    /// <returns>The size of a packet.</returns>
    public static int GetPacketSize(this byte[] packet)
    {
        return packet.AsSpan().GetPacketSize();
    }

    /// <summary>
    /// Sets the size of the byte array as packet length in the corresponding indexes of the byte array.
    /// </summary>
    /// <param name="packet">The packet.</param>
    public static void SetPacketSize(this Span<byte> packet)
    {
        var size = packet.Length;
        if (size < 2)
        {
            throw new ArgumentException("Packet buffer is too small to contain a valid header.", nameof(packet));
        }

        switch (packet[0])
        {
            case 0xC1:
            case 0xC3:
                if (size > byte.MaxValue)
                {
                    throw new ArgumentException("Packet length exceeds the C1/C3 header capacity.", nameof(packet));
                }

                packet[1] = (byte)size;
                break;
            case 0xC2:
            case 0xC4:
                if (size is < 3 or > ushort.MaxValue)
                {
                    throw new ArgumentException("Packet length is outside the C2/C4 header capacity.", nameof(packet));
                }

                packet[1] = (byte)((size & 0xFF00) >> 8);
                packet[2] = (byte)(size & 0x00FF);
                break;
            default:
                throw new ArgumentException($"Unknown packet type {packet[0]:X}", nameof(packet));
        }
    }
}