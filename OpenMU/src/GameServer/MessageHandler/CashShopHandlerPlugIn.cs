// <copyright file="CashShopHandlerPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.MessageHandler;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.CashShop;
using MUnique.OpenMU.GameLogic.Views.Inventory;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.GameServer.RemoteView.CashShop;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.PlugIns;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Handles the local single-player shop. Every mutation is validated and durably saved.
/// </summary>
[PlugIn]
[Guid("6d667f4c-28a5-49e4-8e09-07a95007f61b")]
[MinimumClient(6, 0, ClientLanguage.Invariant)]
internal sealed class CashShopHandlerPlugIn : IPacketHandlerPlugIn
{
    private readonly SoloCashShopService _service = new();

    /// <inheritdoc />
    public byte Key => 0xD2;

    /// <inheritdoc />
    public bool IsEncryptionExpected => false;

    /// <inheritdoc />
    public async ValueTask HandlePacketAsync(Player player, Memory<byte> packet)
    {
        if (player is not RemotePlayer remote || packet.Length < 4 || packet.Span[0] != 0xC1
            || packet.Span[1] != packet.Length || packet.Span[2] != this.Key)
        {
            return;
        }

        var operation = packet.Span[3];
        var expectedLength = operation switch
        {
            0x01 or 0xF3 => 4,
            0x02 => CashShopOpenState.Length,
            0x03 => CashShopItemBuyRequest.Length,
            0x04 => CashShopItemGiftRequest.Length,
            0x05 => CashShopStorageListRequest.Length,
            0x0A => CashShopDeleteStorageItemRequest.Length,
            0x0B => CashShopStorageItemConsumeRequest.Length,
            0x13 => CashShopEventItemListRequest.Length,
            _ => 0,
        };
        if (packet.Length != expectedLength)
        {
            return;
        }

        if (operation == 0x02 && ((CashShopOpenState)packet).IsClosed)
        {
            await SoloCashShopPackets.SendResultAsync(remote, operation, 0).ConfigureAwait(false);
            return;
        }

        if (!SoloCashShopService.CanUse(player))
        {
            if (operation is 0x02 or 0x03 or 0x04 or 0x0A or 0x0B or 0xF3)
            {
                await SoloCashShopPackets.SendResultAsync(remote, operation, operation == 0x02 ? (byte)0 : (byte)6).ConfigureAwait(false);
            }
            return;
        }

        try
        {
            await this.DispatchAsync(remote, operation, packet).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            player.Logger.LogError(exception, "Cash shop operation {Operation} failed.", operation);
            if (operation is 0x02 or 0x03 or 0x04 or 0x0A or 0x0B or 0xF3)
            {
                await SoloCashShopPackets.SendResultAsync(remote, operation, operation == 0x02 ? (byte)0 : (byte)255).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask DispatchAsync(RemotePlayer player, byte operation, Memory<byte> packet)
    {
        switch (operation)
        {
            case 0x01:
                if (await this._service.InitializeAsync(player).ConfigureAwait(false) == 0)
                {
                    await SoloCashShopPackets.SendPointsAsync(player).ConfigureAwait(false);
                }

                break;
            case 0x02:
                var initialized = await this._service.InitializeAsync(player).ConfigureAwait(false);
                if (initialized == 0)
                {
                    await SoloCashShopPackets.SendCatalogAsync(player).ConfigureAwait(false);
                }

                await SoloCashShopPackets.SendResultAsync(player, operation, initialized == 0 ? (byte)1 : (byte)0).ConfigureAwait(false);
                break;
            case 0x03:
                CashShopItemBuyRequest buy = packet;
                var result = await this._service.BuyAsync(player, buy.PackageMainIndex, buy.Category, buy.ProductMainIndex, buy.ItemIndex, buy.CoinIndex, buy.MileageFlag).ConfigureAwait(false);
                await SoloCashShopPackets.SendResultAsync(player, operation, result).ConfigureAwait(false);
                break;
            case 0x04:
                await this.GiftAsync(player, packet).ConfigureAwait(false);
                break;
            case 0x05:
                CashShopStorageListRequest storage = packet;
                await SoloCashShopPackets.SendStorageAsync(player, storage.PageIndex, storage.InventoryType).ConfigureAwait(false);
                break;
            case 0x0A:
                CashShopDeleteStorageItemRequest delete = packet;
                var deleted = await this._service.DeleteAsync(player, delete.BaseItemCode, delete.MainItemCode, delete.ProductType).ConfigureAwait(false);
                await SoloCashShopPackets.SendResultAsync(player, operation, deleted).ConfigureAwait(false);
                break;
            case 0x0B:
                CashShopStorageItemConsumeRequest claim = packet;
                var claimed = await this._service.ClaimAsync(player, claim.BaseItemCode, claim.MainItemCode, claim.ItemIndex, claim.ProductType).ConfigureAwait(false);
                await SoloCashShopPackets.SendResultAsync(player, operation, claimed).ConfigureAwait(false);
                break;
            case 0x13:
                await SoloCashShopPackets.SendEmptyEventsAsync(player).ConfigureAwait(false);
                break;
            case 0xF3:
                var exchanged = await this._service.ExchangeAsync(player).ConfigureAwait(false);
                await SoloCashShopPackets.SendResultAsync(player, operation, exchanged).ConfigureAwait(false);
                if (exchanged == 0)
                {
                    await SoloCashShopPackets.SendPointsAsync(player).ConfigureAwait(false);
                    await player.InvokeViewPlugInAsync<IUpdateMoneyPlugIn>(p => p.UpdateMoneyAsync()).ConfigureAwait(false);
                }

                break;
        }
    }

    private async ValueTask GiftAsync(RemotePlayer player, Memory<byte> packet)
    {
        CashShopItemGiftRequest gift = packet;
        if (string.IsNullOrWhiteSpace(gift.GiftReceiverName))
        {
            await SoloCashShopPackets.SendResultAsync(player, 0x04, 3).ConfigureAwait(false);
            return;
        }
        var result = await this._service.BuyAsync(
            player, gift.PackageMainIndex, gift.Category, gift.ProductMainIndex,
            gift.ItemIndex, gift.CoinIndex, gift.MileageFlag, gift.GiftReceiverName, gift.GiftText).ConfigureAwait(false);
        result = result switch { 10 => 3, 9 => 10, 4 => 6, 6 => 7, _ => result };
        await SoloCashShopPackets.SendResultAsync(player, 0x04, result).ConfigureAwait(false);
    }
}
