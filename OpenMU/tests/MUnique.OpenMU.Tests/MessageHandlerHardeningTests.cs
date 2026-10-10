// <copyright file="MessageHandlerHardeningTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views.Vault;
using MUnique.OpenMU.GameServer.MessageHandler.Messenger;
using MUnique.OpenMU.GameServer.MessageHandler.Vault;
using MUnique.OpenMU.Network.Packets.ClientToServer;

/// <summary>
/// Hardening regression tests for packet handlers that previously crashed or hung the connection on
/// malformed / out-of-range client input (L-2 letter delete, L-4 vault money direction).
/// </summary>
[TestFixture]
public class MessageHandlerHardeningTests
{
    /// <summary>
    /// A truncated letter-delete packet must be dropped before offset 3 is read; previously a short
    /// packet threw an index-out-of-range exception out of the packet loop.
    /// </summary>
    [Test]
    public async Task LetterDeleteIgnoresTruncatedPacketAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var handler = new LetterDeleteHandlerPlugIn();

        // Only a header byte and the code byte; offset 3 (sub-op) and 4..5 (letter index) are missing.
        var truncated = new Memory<byte>(new byte[] { LetterDeleteRequest.HeaderType, LetterDeleteRequest.Code });

        // Must not throw: the length guard returns before indexing the span.
        await handler.HandlePacketAsync(player, truncated).ConfigureAwait(false);
    }

    /// <summary>
    /// An out-of-range vault-money direction byte must be answered with an explicit failure instead
    /// of throwing and leaving the client hanging / dropping the connection.
    /// </summary>
    [Test]
    public async Task VaultMoneyRejectsUnknownDirectionAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        player.Money = 1000;
        SetVaultWindowOpen(player);

        // Build a well-formed 8-byte request (passes the length guard) but with an undefined direction.
        var packet = new byte[VaultMoveMoneyRequest.Length];
        packet[0] = VaultMoveMoneyRequest.HeaderType;
        packet[1] = (byte)VaultMoveMoneyRequest.Length;
        packet[2] = VaultMoveMoneyRequest.Code;
        packet[3] = 2; // undefined direction (only 0 = InventoryToVault, 1 = VaultToInventory exist)
        packet[4] = 100; // amount = 100 (little endian low byte), valid otherwise

        var handler = new VaultMoneyHandlerPlugIn();

        // Must not throw: the default arm now reports failure instead of InvalidEnumArgumentException.
        await handler.HandlePacketAsync(player, new Memory<byte>(packet)).ConfigureAwait(false);

        Mock.Get(player.ViewPlugIns.GetPlugIn<IUpdateVaultMoneyPlugIn>()!)
            .Verify(view => view.UpdateVaultMoneyAsync(false), Times.Once);
    }

    private static void SetVaultWindowOpen(Player player)
    {
        player.OpenedNpc = new NonPlayerCharacter(
            null!,
            new MonsterDefinition { NpcWindow = NpcWindow.VaultStorage },
            null!);
    }
}
