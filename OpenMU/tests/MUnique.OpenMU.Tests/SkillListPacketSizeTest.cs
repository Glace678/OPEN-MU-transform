// <copyright file="SkillListPacketSizeTest.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameServer.RemoteView.Character;
using MUnique.OpenMU.Network.Packets.ServerToClient;

/// <summary>
/// Tests the packet length calculation of the skill list packets.
/// </summary>
[TestFixture]
public class SkillListPacketSizeTest
{
    /// <summary>
    /// The default skill list packet (C1 0xF3 0x11) must not exceed the one byte packet length.
    /// </summary>
    [Test]
    public void SkillListUpdateFitsIntoOnePacket()
    {
        var maximum = SkillListPacketSize.GetMaximumEntryCount(6, 4);
        Assert.That(SkillListUpdateRef.GetRequiredSize(maximum), Is.LessThanOrEqualTo(byte.MaxValue));
        Assert.That(SkillListUpdateRef.GetRequiredSize(maximum + 1), Is.GreaterThan(byte.MaxValue));
    }

    /// <summary>
    /// The 0.75 skill list packet (C1 0xF3 0x11) must not exceed the one byte packet length.
    /// </summary>
    [Test]
    public void SkillListUpdate075FitsIntoOnePacket()
    {
        var maximum = SkillListPacketSize.GetMaximumEntryCount(5, 3);
        Assert.That(SkillListUpdate075Ref.GetRequiredSize(maximum), Is.LessThanOrEqualTo(byte.MaxValue));
        Assert.That(SkillListUpdate075Ref.GetRequiredSize(maximum + 1), Is.GreaterThan(byte.MaxValue));
    }
}
