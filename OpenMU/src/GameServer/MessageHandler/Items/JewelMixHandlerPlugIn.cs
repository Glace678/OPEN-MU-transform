// <copyright file="JewelMixHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Items;

using System.ComponentModel;
using System.Runtime.InteropServices;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Handler for jewel mix packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.JewelMixHandlerPlugIn_Name), Description = nameof(PlugInResources.JewelMixHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("d6067475-a910-488d-8450-9310ae394c47")]
internal class JewelMixHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly ItemStackAction _mixAction = new();

    /// <inheritdoc/>
    public bool IsEncryptionExpected => LahapJewelMixRequest.HeaderType >= 0xC3;

    /// <inheritdoc/>
    public byte Key => LahapJewelMixRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (packet.Length < 6)
        {
            return;
        }

        LahapJewelMixRequest message = packet;

        switch (message.Operation)
        {
            case LahapJewelMixRequest.MixType.Mix:
                if (!TryGetStackSize(message.MixingStackSize, out var stackSize))
                {
                    return;
                }

                await this._mixAction.StackItemsAsync(player, (byte)message.Item, stackSize).ConfigureAwait(false);
                break;
            case LahapJewelMixRequest.MixType.Unmix:
                if (packet.Length < 7)
                {
                    return;
                }

                await this._mixAction.UnstackItemsAsync(player, (byte)message.Item, message.UnmixingSourceSlot).ConfigureAwait(false);
                break;
            default:
                return;
        }
    }

    private static bool TryGetStackSize(LahapJewelMixRequest.StackSize stackSize, out byte count)
    {
        switch (stackSize)
        {
            case LahapJewelMixRequest.StackSize.Ten:
                count = 10;
                return true;
            case LahapJewelMixRequest.StackSize.Twenty:
                count = 20;
                return true;
            case LahapJewelMixRequest.StackSize.Thirty:
                count = 30;
                return true;
            default:
                count = 0;
                return false;
        }
    }
}