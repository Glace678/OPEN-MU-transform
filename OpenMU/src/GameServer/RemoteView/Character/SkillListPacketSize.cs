// <copyright file="SkillListPacketSize.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.Character;

/// <summary>
/// Calculates how many entries of a skill list fit into a single C1 packet.
/// The packet header stores the total length in a single byte, so lists longer than the
/// header limit would be cut off while the body keeps its full length, which desynchronizes
/// the client connection.
/// </summary>
public static class SkillListPacketSize
{
    private const int MaximumPacketLength = byte.MaxValue;

    /// <summary>
    /// Gets the maximum number of entries which fit into a C1 packet with the given header size and entry size.
    /// </summary>
    /// <param name="headerSize">Length of the fixed header fields including the packet header.</param>
    /// <param name="entrySize">Length of a single list entry.</param>
    /// <returns>The maximum number of entries.</returns>
    public static int GetMaximumEntryCount(int headerSize, int entrySize)
        => (MaximumPacketLength - headerSize) / entrySize;
}
