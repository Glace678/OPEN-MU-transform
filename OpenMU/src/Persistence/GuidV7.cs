// <copyright file="GuidV7.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence;

using System.Security.Cryptography;

/// <summary>
/// A generator for version 7 GUIDs. Layout:
/// The first 6 bytes is a unix timestamp in milliseconds.
/// The 7th byte contains the version number (7) in the upper 4 bits.
/// The rest is random.
/// </summary>
public static class GuidV7
{
    /// <summary>
    /// Creates a new random guid.
    /// </summary>
    /// <returns>The new guid.</returns>
    public static Guid NewGuid() => NewGuid(DateTimeOffset.UtcNow);

    /// <summary>
    /// Creates a new random guid for the specified date.
    /// </summary>
    /// <param name="dateTimeOffset">The date time offset which is the prefix of the id.</param>
    /// <returns>The new guid.</returns>
    public static Guid NewGuid(DateTimeOffset dateTimeOffset)
    {
        // UUIDv7 (RFC 9562): bytes 0..5 hold the 48-bit unix millisecond timestamp
        // in big-endian order, byte 6 carries version 7 and byte 8 the RFC 4122
        // variant. The previous implementation wrote the timestamp as a little
        // endian long while the Guid was constructed in big-endian mode, which
        // scrambled the prefix and destroyed the time ordering.
        Span<byte> uuidAsBytes = stackalloc byte[16];
        var currentTimestamp = dateTimeOffset.ToUnixTimeMilliseconds();

        uuidAsBytes[0] = (byte)(currentTimestamp >> 40);
        uuidAsBytes[1] = (byte)(currentTimestamp >> 32);
        uuidAsBytes[2] = (byte)(currentTimestamp >> 24);
        uuidAsBytes[3] = (byte)(currentTimestamp >> 16);
        uuidAsBytes[4] = (byte)(currentTimestamp >> 8);
        uuidAsBytes[5] = (byte)currentTimestamp;

        RandomNumberGenerator.Fill(uuidAsBytes[6..]);

        // Version 7.
        uuidAsBytes[6] &= 0x0F;
        uuidAsBytes[6] |= 0x70;

        // RFC 4122 variant (10xx xxxx).
        uuidAsBytes[8] &= 0x3F;
        uuidAsBytes[8] |= 0x80;

        return new Guid(uuidAsBytes, true);
    }
}