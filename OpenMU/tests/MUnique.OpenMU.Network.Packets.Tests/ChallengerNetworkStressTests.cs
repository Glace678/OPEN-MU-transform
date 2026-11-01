// <copyright file="ChallengerNetworkStressTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network.Packets.Tests;

using System;
using System.IO;
using System.Text;
using Moq;
using Nito.AsyncEx;
using NUnit.Framework;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ChatServer;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.Packets.ServerToClient;

using PacketEnum = MUnique.OpenMU.Network.Packets.Enum;

/// <summary>
/// Empirical Challenger Stress Test Suite for Network Protocol Remediation (NET-01, NET-03, NET-04).
/// </summary>
[TestFixture]
public class ChallengerNetworkStressTests
{
    private static Mock<IConnection> CreateMockConnection()
    {
        var connection = new Mock<IConnection>();
        connection.SetupGet(c => c.Connected).Returns(true);
        connection.SetupGet(c => c.OutputLock).Returns(new AsyncLock());
        return connection;
    }

    #region NET-03 Dynamic Packet Length Bounds & Narrowing Overflow

    /// <summary>
    /// NET-03: C1 single-byte packets must clamp length to 255 rather than wrapping on integer narrowing cast.
    /// In unhardened code: (byte)data.Length wrapped (e.g. 256 -> 0, 300 -> 44, 512 -> 0).
    /// In hardened code: Math.Min(data.Length, (int)byte.MaxValue) clamps to 255.
    /// </summary>
    [Test]
    public void C1HeaderPacketLength_DoesNotWrapOnPayloadOverflow()
    {
        // 255 bytes: exact byte.MaxValue
        var buffer255 = new byte[255];
        var packet255 = new IllusionTempleResult(buffer255);
        Assert.That(packet255.Header.Length, Is.EqualTo(255), "255 length should equal 255");

        // 256 bytes: without clamp would wrap to 0 (narrowing byte cast)
        var buffer256 = new byte[256];
        var packet256 = new IllusionTempleResult(buffer256);
        Assert.That(packet256.Header.Length, Is.EqualTo(255), "256 length must clamp to 255, NOT wrap to 0");

        // 300 bytes: without clamp would wrap to 44
        var buffer300 = new byte[300];
        var packet300 = new IllusionTempleResult(buffer300);
        Assert.That(packet300.Header.Length, Is.EqualTo(255), "300 length must clamp to 255, NOT wrap to 44");

        // 512 bytes: without clamp would wrap to 0
        var buffer512 = new byte[512];
        var packet512 = new IllusionTempleResult(buffer512);
        Assert.That(packet512.Header.Length, Is.EqualTo(255), "512 length must clamp to 255, NOT wrap to 0");

        // 1024 bytes
        var buffer1024 = new byte[1024];
        var packet1024 = new IllusionTempleResult(buffer1024);
        Assert.That(packet1024.Header.Length, Is.EqualTo(255), "1024 length must clamp to 255");
    }

    /// <summary>
    /// NET-03 Ref Struct: IllusionTempleResultRef must also clamp to 255 without wrapping.
    /// </summary>
    [Test]
    public void C1HeaderRefStructLength_DoesNotWrapOnPayloadOverflow()
    {
        Span<byte> span256 = new byte[256];
        var packet256 = new IllusionTempleResultRef(span256);
        Assert.That(packet256.Header.Length, Is.EqualTo(255), "RefStruct 256 length must clamp to 255");

        Span<byte> span300 = new byte[300];
        var packet300 = new IllusionTempleResultRef(span300);
        Assert.That(packet300.Header.Length, Is.EqualTo(255), "RefStruct 300 length must clamp to 255");
    }

    /// <summary>
    /// NET-03: C2 two-byte packets must clamp length to 65535 rather than wrapping on ushort narrowing cast.
    /// In unhardened code: (ushort)data.Length wrapped (e.g. 65536 -> 0, 70000 -> 4464).
    /// In hardened code: Math.Min(data.Length, (int)ushort.MaxValue) clamps to 65535.
    /// </summary>
    [Test]
    public void C2HeaderPacketLength_DoesNotWrapOnPayloadOverflow()
    {
        // 65535 bytes: exact ushort.MaxValue
        var buffer65535 = new byte[65535];
        var packet65535 = new ChatRoomClients(buffer65535);
        Assert.That(packet65535.Header.Length, Is.EqualTo(65535), "65535 length should equal 65535");

        // 65536 bytes: without clamp would wrap to 0
        var buffer65536 = new byte[65536];
        var packet65536 = new ChatRoomClients(buffer65536);
        Assert.That(packet65536.Header.Length, Is.EqualTo(65535), "65536 length must clamp to 65535, NOT wrap to 0");

        // 70000 bytes: without clamp would wrap to 4464
        var buffer70000 = new byte[70000];
        var packet70000 = new ChatRoomClients(buffer70000);
        Assert.That(packet70000.Header.Length, Is.EqualTo(65535), "70000 length must clamp to 65535, NOT wrap to 4464");
    }

    /// <summary>
    /// NET-03 Ref Struct: ChatRoomClientsRef must also clamp to 65535 without wrapping.
    /// </summary>
    [Test]
    public void C2HeaderRefStructLength_DoesNotWrapOnPayloadOverflow()
    {
        Span<byte> span65536 = new byte[65536];
        var packet65536 = new ChatRoomClientsRef(span65536);
        Assert.That(packet65536.Header.Length, Is.EqualTo(65535), "RefStruct 65536 length must clamp to 65535");
    }

    /// <summary>
    /// NET-03: ConnectionExtensions Send methods enforce length within single-byte headers.
    /// </summary>
    [Test]
    public void SingleByteHeader_SendExtensionThrowsOnOversizedPayload()
    {
        var conn = CreateMockConnection();
        // 251 payload bytes + 5 header bytes = 256 bytes total > 255
        var oversizedPayload = new byte[251];
        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await conn.Object.SendChatMessageAsync(0, 0, oversizedPayload));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("exceeds the single-byte header maximum (255)"));
    }

    #endregion

    #region NET-01 Field Length Isolation (PlayerResult.Name)

    /// <summary>
    /// NET-01: PlayerResult.Name is bounded to 10 bytes.
    /// Writing varying length strings (empty, short, exact 10) must NEVER overwrite adjacent fields:
    /// MapNumber (offset 10), Team (offset 11), Class (offset 12), AddedExperience (offsets 13..16).
    /// </summary>
    [Test]
    public void PlayerResult_NameSerialization_PreservesAdjacentFields()
    {
        var raw = new byte[IllusionTempleResult.PlayerResult.Length]; // 17 bytes
        var player = new IllusionTempleResult.PlayerResult(raw.AsMemory());

        // Pre-populate adjacent canary fields
        player.MapNumber = 0x55;
        player.Team = 0x66;
        player.Class = 0x77;
        player.AddedExperience = 0x12345678;

        // Verify initial canaries
        Assert.That(player.MapNumber, Is.EqualTo(0x55));
        Assert.That(player.Team, Is.EqualTo(0x66));
        Assert.That(player.Class, Is.EqualTo(0x77));
        Assert.That(player.AddedExperience, Is.EqualTo(0x12345678));

        // Test 1: Empty string
        player.Name = string.Empty;
        Assert.That(player.Name, Is.EqualTo(string.Empty));
        Assert.That(player.MapNumber, Is.EqualTo(0x55), "MapNumber must not be corrupted by empty Name");
        Assert.That(player.Team, Is.EqualTo(0x66), "Team must not be corrupted by empty Name");
        Assert.That(player.Class, Is.EqualTo(0x77), "Class must not be corrupted by empty Name");
        Assert.That(player.AddedExperience, Is.EqualTo(0x12345678), "AddedExperience must not be corrupted by empty Name");

        // Test 2: Short string (5 chars)
        player.Name = "Alice";
        Assert.That(player.Name, Is.EqualTo("Alice"));
        Assert.That(player.MapNumber, Is.EqualTo(0x55), "MapNumber must not be corrupted by short Name");
        Assert.That(player.Team, Is.EqualTo(0x66), "Team must not be corrupted by short Name");
        Assert.That(player.Class, Is.EqualTo(0x77), "Class must not be corrupted by short Name");
        Assert.That(player.AddedExperience, Is.EqualTo(0x12345678), "AddedExperience must not be corrupted by short Name");

        // Test 3: Exact 10 bytes string
        player.Name = "1234567890";
        Assert.That(player.Name, Is.EqualTo("1234567890"));
        Assert.That(player.MapNumber, Is.EqualTo(0x55), "MapNumber must not be corrupted by exact 10 Name");
        Assert.That(player.Team, Is.EqualTo(0x66), "Team must not be corrupted by exact 10 Name");
        Assert.That(player.Class, Is.EqualTo(0x77), "Class must not be corrupted by exact 10 Name");
        Assert.That(player.AddedExperience, Is.EqualTo(0x12345678), "AddedExperience must not be corrupted by exact 10 Name");

        // Test 4: Oversized string (> 10 bytes) - Slice(0, 10).Span must reject without corrupting adjacent bytes
        Assert.Throws<ArgumentException>(() => player.Name = "12345678901");
        Assert.Throws<ArgumentException>(() => player.Name = "VeryLongPlayerNameExceeding10Bytes");

        // Verify that even after failed oversized assignment, adjacent fields remained intact!
        Assert.That(player.MapNumber, Is.EqualTo(0x55), "MapNumber must survive oversized write attempt");
        Assert.That(player.Team, Is.EqualTo(0x66), "Team must survive oversized write attempt");
        Assert.That(player.Class, Is.EqualTo(0x77), "Class must survive oversized write attempt");
        Assert.That(player.AddedExperience, Is.EqualTo(0x12345678), "AddedExperience must survive oversized write attempt");
    }

    /// <summary>
    /// NET-01 Ref Struct: PlayerResultRef must also guarantee strict 10-byte bounds isolation.
    /// </summary>
    [Test]
    public void PlayerResultRef_NameSerialization_PreservesAdjacentFields()
    {
        Span<byte> raw = new byte[IllusionTempleResultRef.PlayerResultRef.Length];
        var player = new IllusionTempleResultRef.PlayerResultRef(raw);

        player.MapNumber = 0x11;
        player.Team = 0x22;
        player.Class = 0x33;
        player.AddedExperience = 0x99887766;

        player.Name = "DarkKnight";
        Assert.That(player.Name, Is.EqualTo("DarkKnight"));
        Assert.That(player.MapNumber, Is.EqualTo(0x11));
        Assert.That(player.Team, Is.EqualTo(0x22));
        Assert.That(player.Class, Is.EqualTo(0x33));
        Assert.That(player.AddedExperience, Is.EqualTo(0x99887766));

        // Oversized rejected (use try-catch since ref struct cannot be captured in lambda)
        bool caughtOversized = false;
        try
        {
            player.Name = "DarkKnightOver10";
        }
        catch (ArgumentException)
        {
            caughtOversized = true;
        }

        Assert.That(caughtOversized, Is.True, "Oversized name must throw ArgumentException");
        Assert.That(player.MapNumber, Is.EqualTo(0x11), "MapNumber must survive oversized write attempt");
        Assert.That(player.Team, Is.EqualTo(0x22), "Team must survive oversized write attempt");
        Assert.That(player.Class, Is.EqualTo(0x33), "Class must survive oversized write attempt");
        Assert.That(player.AddedExperience, Is.EqualTo(0x99887766), "AddedExperience must survive oversized write attempt");
    }

    /// <summary>
    /// NET-01 Array Isolation: Multiple PlayerResult items in IllusionTempleResult packet buffer
    /// must not leak or overwrite neighboring player structures.
    /// </summary>
    [Test]
    public void PlayerResult_ArrayIsolation_MultiPlayerPreserved()
    {
        const int playerCount = 3;
        var buffer = new byte[IllusionTempleResult.GetRequiredSize(playerCount)];
        var packet = (IllusionTempleResult)buffer.AsMemory();
        packet.PlayerCount = playerCount;

        for (int i = 0; i < playerCount; i++)
        {
            var p = packet[i];
            p.Name = $"Player_{i}";
            p.MapNumber = (byte)(10 + i);
            p.Team = (byte)(1 + i);
            p.Class = (byte)(20 + i);
            p.AddedExperience = (uint)(1000 * (i + 1));
        }

        // Verify isolation
        for (int i = 0; i < playerCount; i++)
        {
            var p = packet[i];
            Assert.That(p.Name, Is.EqualTo($"Player_{i}"));
            Assert.That(p.MapNumber, Is.EqualTo((byte)(10 + i)));
            Assert.That(p.Team, Is.EqualTo((byte)(1 + i)));
            Assert.That(p.Class, Is.EqualTo((byte)(20 + i)));
            Assert.That(p.AddedExperience, Is.EqualTo((uint)(1000 * (i + 1))));
        }
    }

    #endregion

    #region NET-04 XML Truncation (PacketDefinitions.Save)

    /// <summary>
    /// NET-04: PacketDefinitions.Save using FileMode.Create must completely truncate previous file contents.
    /// Writing a large PacketDefinitions followed by a shorter one must leave zero trailing bytes.
    /// </summary>
    [Test]
    public void PacketDefinitionsSave_TruncatesTrailingStaleXmlBytes()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"PacketDefs_Test_{Guid.NewGuid():N}.xml");
        try
        {
            // 1. Create a large packet definitions object
            var largeDefs = new PacketDefinitions
            {
                Description = "A very long description that generates substantial XML content with multiple enums and packets to test file truncation.",
                Enums = new[]
                {
                    new PacketEnum { Name = "FirstEnum", Description = "First enum description with lots of padding content to make the file large." },
                    new PacketEnum { Name = "SecondEnum", Description = "Second enum description with lots of padding content to make the file large." },
                    new PacketEnum { Name = "ThirdEnum", Description = "Third enum description with lots of padding content to make the file large." },
                },
                Packets = new[]
                {
                    new PacketDefinition
                    {
                        Name = "LargePacketOne",
                        Caption = "Extensive caption for packet one with many words to inflate the byte length of the serialized XML.",
                        Code = 0xFF,
                        HeaderType = "C1",
                    },
                    new PacketDefinition
                    {
                        Name = "LargePacketTwo",
                        Caption = "Extensive caption for packet two with many words to inflate the byte length of the serialized XML.",
                        Code = 0xFE,
                        HeaderType = "C2",
                    },
                },
            };

            largeDefs.Save(tempFile);
            var initialLength = new FileInfo(tempFile).Length;
            Assert.That(initialLength, Is.GreaterThan(800), "Initial file size should be substantial");

            // Verify initial load
            var loadedLarge = PacketDefinitions.Load(tempFile);
            Assert.That(loadedLarge, Is.Not.Null);
            Assert.That(loadedLarge!.Packets!.Length, Is.EqualTo(2));

            // 2. Overwrite with a much smaller packet definitions object
            var smallDefs = new PacketDefinitions
            {
                Description = "Tiny",
            };

            smallDefs.Save(tempFile);
            var overwrittenLength = new FileInfo(tempFile).Length;

            // In unhardened code (OpenWrite), overwrittenLength would remain initialLength (>= 800) with stale trailing bytes!
            // In hardened code (FileMode.Create), overwrittenLength is strictly the size of the small XML.
            Assert.That(overwrittenLength, Is.LessThan(initialLength / 2), "File must be truncated to shorter length");

            // 3. Verify clean reload - without FileMode.Create this throws XML ParseError due to trailing tags
            var loadedSmall = PacketDefinitions.Load(tempFile);
            Assert.That(loadedSmall, Is.Not.Null);
            Assert.That(loadedSmall!.Description, Is.EqualTo("Tiny"));
            Assert.That(loadedSmall.Packets, Is.Null.Or.Empty);

            // 4. Raw text check - ensure no trailing tags from previous largeDefs
            var rawContent = File.ReadAllText(tempFile, Encoding.UTF8);
            Assert.That(rawContent, Does.Not.Contain("LargePacketOne"));
            Assert.That(rawContent, Does.Not.Contain("LargePacketTwo"));
            Assert.That(rawContent, Does.Not.Contain("FirstEnum"));
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    #endregion
}
