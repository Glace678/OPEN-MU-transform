// <copyright file="ShowMerchantStoreItemListPlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.NPC;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.CastleSiege;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// The default implementation of the <see cref="IShowMerchantStoreItemListPlugIn"/> which is forwarding everything to the game client with specific data packets.
/// </summary>
[PlugIn]
[Display(Name = nameof(PlugInResources.ShowMerchantStoreItemListPlugIn_Name), Description = nameof(PlugInResources.ShowMerchantStoreItemListPlugIn_Description), ResourceType = typeof(PlugInResources))]
[Guid("53e00ae0-4d5b-4f63-88e0-7d526f8438af")]
public class ShowMerchantStoreItemListPlugIn : IShowMerchantStoreItemListPlugIn
{
    private readonly RemotePlayer _player;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShowMerchantStoreItemListPlugIn"/> class.
    /// </summary>
    /// <param name="player">The player.</param>
    public ShowMerchantStoreItemListPlugIn(RemotePlayer player) => this._player = player;

    /// <inheritdoc/>
    public async ValueTask ShowMerchantStoreItemListAsync(ICollection<Item> storeItems, StoreKind storeKind)
    {
        var connection = this._player.Connection;
        if (connection is null)
        {
            return;
        }

        byte[]? quotes = null;
        if (BalanceV1.IsEnabled(this._player.GameContext.Configuration)
            && !SoloBalance.IsEnabled(this._player.GameContext.Configuration)
            && this._player.ClientVersion.Season == 106
            && this._player.ClientVersion.Episode >= 3
            && this._player.ItemSerializer is ItemSerializerExtended
            && storeKind == StoreKind.Normal
            && ReferenceEquals(this._player.OpenedNpc?.Definition.MerchantStore?.Items, storeItems))
        {
            var tax = await new CastleSiegeTaxProvider().GetStoreTaxAsync(this._player).ConfigureAwait(false);
            quotes = MerchantPriceQuotePacket.Create(storeItems, this._player.ItemSerializer, this._player.GameContext.Configuration, tax);
        }

        int Write()
        {
            var itemSerializer = this._player.ItemSerializer;
            int sizePerItem = StoredItemRef.GetRequiredSize(itemSerializer.NeededSpace);
            var size = StoreItemListRef.GetRequiredSize(storeItems.Count, sizePerItem);
            var quoteSize = quotes?.Length ?? 0;
            var output = connection.Output.GetSpan(quoteSize + size);
            quotes?.CopyTo(output);
            var span = output.Slice(quoteSize, size);
            var packet = new StoreItemListRef(span)
            {
                ItemCount = (byte)storeItems.Count,
                Type = Convert(storeKind),
            };

            int headerSize = StoreItemListRef.GetRequiredSize(0, 0);
            int actualSize = headerSize;
            int i = 0;
            foreach (var item in storeItems)
            {
                if (item.Definition is null)
                {
                    this._player.Logger.LogWarning("Item {0} has no definition.", item);
                    packet.ItemCount--;
                    continue;
                }

                var storedItem = new StoredItemRef(span[actualSize..]);
                storedItem.ItemSlot = item.ItemSlot;
                var itemSize = itemSerializer.SerializeItem(storedItem.ItemData, item);
                actualSize += StoredItemRef.GetRequiredSize(itemSize);
                i++;
            }

            span.Slice(0, actualSize).SetPacketSize();
            return quoteSize + actualSize;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }

    private static StoreItemList.ItemWindow Convert(StoreKind storeKind)
    {
        return storeKind switch
        {
            StoreKind.Normal => StoreItemList.ItemWindow.Normal,
            StoreKind.ChaosMachine => StoreItemList.ItemWindow.ChaosMachine,
            StoreKind.ResurrectionFailed => StoreItemList.ItemWindow.ResurrectionFailed,
            _ => throw new ArgumentException($"Unknown value {storeKind}", nameof(storeKind)),
        };
    }
}
