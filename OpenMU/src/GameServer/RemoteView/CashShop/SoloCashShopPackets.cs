// <copyright file="SoloCashShopPackets.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameServer.RemoteView.CashShop;

using System.Buffers.Binary;
using System.Text;
using MUnique.OpenMU.GameLogic.CashShop;
using MUnique.OpenMU.Network;

/// <summary>
/// Season 6 shop responses plus the local catalog (F0-F2) and gold exchange (F3).
/// Integer and IEEE double fields use the client's packed little-endian layout.
/// </summary>
internal static class SoloCashShopPackets
{
    private const int StoragePageSize = 9;
    private const int CatalogNameBytes = 96;

    internal static async ValueTask SendCatalogAsync(RemotePlayer player)
    {
        var offers = SoloCashShopCatalog.GetOffers(player.GameContext.Configuration);
        var begin = Create(0xF0, 8);
        BinaryPrimitives.WriteUInt16LittleEndian(begin.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(begin.AsSpan(6), checked((ushort)offers.Count));
        await SendAsync(player, begin).ConfigureAwait(false);
        foreach (var offer in offers)
        {
            var packet = Create(0xF1, 18 + CatalogNameBytes);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), offer.Id);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(8), offer.ItemCode);
            packet[10] = offer.Level;
            packet[11] = offer.Category;
            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(12), offer.Price);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(16), 1);
            var name = offer.Level == 0 ? offer.Definition.Name.ToString() ?? $"Item {offer.ItemCode}" : $"{offer.Definition.Name} +{offer.Level}";
            WriteText(packet.AsSpan(18, CatalogNameBytes), name);
            await SendAsync(player, packet).ConfigureAwait(false);
        }

        await SendAsync(player, Create(0xF2, 4)).ConfigureAwait(false);
    }

    internal static ValueTask SendPointsAsync(RemotePlayer player)
    {
        var credit = SoloCashShopState.Read(player.Account!.SoloCashShopData).Credit;
        var packet = Create(0x01, 45);
        BinaryPrimitives.WriteDoubleLittleEndian(packet.AsSpan(5), credit);
        BinaryPrimitives.WriteDoubleLittleEndian(packet.AsSpan(13), credit);
        return SendAsync(player, packet);
    }

    internal static ValueTask SendResultAsync(RemotePlayer player, byte operation, byte result)
    {
        var length = operation switch { 0x03 => 9, 0x04 => 17, _ => 5 };
        var packet = Create(operation, length);
        packet[4] = result;
        if (operation is 0x03 or 0x04)
        {
            BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(5), -1);
        }

        return SendAsync(player, packet);
    }

    internal static async ValueTask SendStorageAsync(RemotePlayer player, uint requestedPage, byte type)
    {
        var entries = SoloCashShopState.Read(player.Account!.SoloCashShopData).Entries
            .Where(e => type == (byte)'S' && e.Recipient.Length == 0
                || type == (byte)'G' && e.Recipient == player.SelectedCharacter!.Name)
            .OrderBy(e => e.Id).ToArray();
        var pages = Math.Max(1, (entries.Length + StoragePageSize - 1) / StoragePageSize);
        var page = (int)Math.Clamp(requestedPage, 1u, (uint)pages);
        var items = entries.Skip((page - 1) * StoragePageSize).Take(StoragePageSize).ToArray();
        var count = Create(0x06, 12);
        BinaryPrimitives.WriteUInt16LittleEndian(count.AsSpan(4), (ushort)entries.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(count.AsSpan(6), (ushort)items.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(count.AsSpan(8), (ushort)page);
        BinaryPrimitives.WriteUInt16LittleEndian(count.AsSpan(10), (ushort)pages);
        await SendAsync(player, count).ConfigureAwait(false);
        foreach (var entry in items)
        {
            var gift = type == (byte)'G';
            var packet = Create(gift ? (byte)0x0E : (byte)0x0D, gift ? 244 : 33);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), entry.Id);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), entry.Id);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), 673);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(16), entry.OfferId);
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(20), entry.OfferId);
            packet[32] = (byte)'P';
            if (gift)
            {
                WriteText(packet.AsSpan(33, 11), entry.Sender);
                WriteText(packet.AsSpan(44, 200), entry.Message);
            }

            await SendAsync(player, packet).ConfigureAwait(false);
        }
    }

    internal static ValueTask SendEmptyEventsAsync(RemotePlayer player) => SendAsync(player, Create(0x13, 6));

    private static byte[] Create(byte operation, int length)
    {
        var packet = new byte[length];
        packet[0] = 0xC1;
        packet[1] = checked((byte)length);
        packet[2] = 0xD2;
        packet[3] = operation;
        return packet;
    }

    private static void WriteText(Span<byte> destination, string value)
    {
        // Encoder truncation preserves complete UTF-8 sequences and leaves the terminator intact.
        Encoding.UTF8.GetEncoder().Convert(value.AsSpan(), destination[..^1], true, out _, out _, out _);
    }

    private static async ValueTask SendAsync(RemotePlayer player, byte[] packet)
    {
        if (player.Connection is not { } connection)
        {
            return;
        }

        int Write()
        {
            packet.CopyTo(connection.Output.GetSpan(packet.Length));
            return packet.Length;
        }

        await connection.SendAsync(Write).ConfigureAwait(false);
    }
}
