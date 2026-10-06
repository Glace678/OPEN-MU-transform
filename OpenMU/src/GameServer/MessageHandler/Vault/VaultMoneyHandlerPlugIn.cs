// <copyright file="VaultMoneyHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler.Vault;

using System.ComponentModel;
using System.Runtime.InteropServices;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Views.Vault;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Handler for warehouse money packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.VaultMoneyHandlerPlugIn_Name), Description = nameof(PlugInResources.VaultMoneyHandlerPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("e365f3f2-55c8-4890-9f6b-26fd39822b71")]
internal class VaultMoneyHandlerPlugIn : IPacketHandlerPlugIn
{
    /// <inheritdoc/>
    public bool IsEncryptionExpected => false;

    /// <inheritdoc/>
    public byte Key => VaultMoveMoneyRequest.Code;

    /// <inheritdoc/>
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        // Validate the fixed-length packet before reading its fields: a truncated packet would
        // read the amount out of bounds.
        if (packet.Length < VaultMoveMoneyRequest.Length)
        {
            return;
        }

        VaultMoveMoneyRequest request = packet;

        // The amount is a uint on the wire; zero or values that don't fit into a positive int
        // would reverse the money comparisons after cast.
        if (request.Amount is 0 or > int.MaxValue)
        {
            await player.InvokeViewPlugInAsync<IUpdateVaultMoneyPlugIn>(p => p.UpdateVaultMoneyAsync(false)).ConfigureAwait(false);
            return;
        }

        // Money movements respect the pin lock and require the vault storage window to be open.
        if (player.IsVaultLocked || player.OpenedNpc?.Definition?.NpcWindow != NpcWindow.VaultStorage)
        {
            await player.InvokeViewPlugInAsync<IUpdateVaultMoneyPlugIn>(p => p.UpdateVaultMoneyAsync(false)).ConfigureAwait(false);
            return;
        }

        int amount = (int)request.Amount;
        bool success;
        switch (request.Direction)
        {
            case VaultMoveMoneyRequest.VaultMoneyMoveDirection.InventoryToVault:
                success = player.TryDepositVaultMoney(amount);
                break;
            case VaultMoveMoneyRequest.VaultMoneyMoveDirection.VaultToInventory:
                success = player.TryTakeVaultMoney(amount);
                break;
            default:
                throw new InvalidEnumArgumentException($"The direction {request.Direction} is not a valid value.");
        }

        await player.InvokeViewPlugInAsync<IUpdateVaultMoneyPlugIn>(p => p.UpdateVaultMoneyAsync(success)).ConfigureAwait(false);
    }
}