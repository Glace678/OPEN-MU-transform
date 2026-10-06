// <copyright file="MerchantPriceQuotePacket.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.NPC;

using System.Buffers.Binary;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Network.PlugIns;

/// <summary>Exact, versioned NPC prices bound to the serialized inventory which follows this packet.</summary>
internal static class MerchantPriceQuotePacket
{
    /// <summary>The packet type code.</summary>
    internal const byte Code = 0xFA;

    /// <summary>The fixed header size in bytes.</summary>
    internal const int HeaderSize = 9;

    /// <summary>The per-entry bytes added on top of the serialized item.</summary>
    internal const int EntryOverhead = 10;

    /// <summary>The maximum number of quoted items (the NPC shop grid).</summary>
    internal const int MaximumItems = 120;

    /// <summary>The maximum serialized size of one item.</summary>
    internal const int MaximumItemBytes = 32;

    /// <summary>
    /// Builds a price-quotation packet for the listed merchant items.
    /// </summary>
    /// <param name="items">The merchant items offered for sale.</param>
    /// <param name="serializer">The item serializer used for the item bytes.</param>
    /// <param name="configuration">The active game configuration.</param>
    /// <param name="taxRate">The purchase tax rate in percent (0..3).</param>
    /// <returns>The serialized packet bytes.</returns>
    internal static byte[] Create(ICollection<Item> items, IItemSerializer serializer, GameConfiguration configuration, int taxRate)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(items.Count, MaximumItems);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(serializer.NeededSpace, MaximumItemBytes);
        if (taxRate is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(taxRate));
        }

        var buffer = new byte[HeaderSize + (items.Count * (EntryOverhead + serializer.NeededSpace))];
        buffer[0] = 0xC2;
        buffer[3] = Code;
        buffer[4] = 0; // NPC purchase quotes, not the independent Solo cash-shop catalog.
        buffer[5] = 1; // Wire version.
        buffer[6] = 1; // Explicit server balance-v1 pricing contract.
        buffer[7] = (byte)taxRate;
        var offset = HeaderSize;
        var slots = new HashSet<byte>();
        var calculator = new ItemPriceCalculator();
        foreach (var item in items.Where(item => item.Definition is not null))
        {
            if (item.ItemSlot >= MaximumItems || !slots.Add(item.ItemSlot))
            {
                throw new ArgumentException("Merchant item slots must be unique and inside the 8x15 grid.", nameof(items));
            }

            buffer[offset] = item.ItemSlot;
            var length = serializer.SerializeItem(buffer.AsSpan(offset + 2, serializer.NeededSpace), item);
            if (length is < 1 or > MaximumItemBytes || length > serializer.NeededSpace)
            {
                throw new ArgumentException("Merchant item serialization must fit the quoted item bytes.", nameof(serializer));
            }

            buffer[offset + 1] = checked((byte)length);
            var price = calculator.CalculateFinalBuyingPrice(item, configuration);

            // Match TryPayStoreCostAsync's int32 wallet bound before applying its percentage tax.
            var payable = price is >= 0 and <= int.MaxValue;
            var total = payable ? price + ((price * taxRate) / 100) : 0;
            payable &= total <= int.MaxValue;
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset + 2 + length), payable ? (uint)price : uint.MaxValue);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset + 6 + length), payable ? (uint)total : uint.MaxValue);
            offset += EntryOverhead + length;
            buffer[8]++;
        }

        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(1), checked((ushort)offset));
        return buffer[..offset];
    }
}
