// <copyright file="SoloCashShopCatalog.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic.CashShop;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// Server-authoritative, permanent solo supplies. No paid services or random boxes.
/// </summary>
public static class SoloCashShopCatalog
{
    /// <summary>The original client identifier for WCoin(C).</summary>
    public const uint CoinIndex = 508;

    /// <summary>Maximum catalog size supported by the solo client.</summary>
    public const int MaximumOffers = 2048;

    /// <summary>
    /// Builds offers from configured NPC supplies and implemented consumable effects.
    /// Stable identifiers include the item level, so event tickets never alias one another.
    /// </summary>
    public static IReadOnlyList<Offer> GetOffers(GameConfiguration configuration)
    {
        var supplies = (configuration.Monsters ?? [])
            .Where(m => m.MerchantStore is not null)
            .SelectMany(m => m.MerchantStore!.Items)
            .Where(i => i.Definition is { IsQuestItem: false, IsBoundToCharacter: false, Width: > 0 and <= 8, Height: > 0 and <= 8 }
                && i.Level <= 15 && i.ItemOptions.Count == 0 && i.ItemSetGroups.Count == 0)
            .Select(i => CreateOffer(i.Definition!, i.Level, i.Durability, i.HasSkill));
        var consumables = configuration.Items
            .Where(d => d.ConsumeEffect is not null && !d.IsQuestItem && !d.IsBoundToCharacter
                && d.Width is > 0 and <= 8 && d.Height is > 0 and <= 8)
            .Select(d => CreateOffer(d, 0, 1));
        var offers = supplies.Concat(consumables).DistinctBy(o => o.Id).OrderBy(o => o.Id).ToArray();
        if (offers.Length > MaximumOffers)
        {
            throw new InvalidOperationException("Solo shop catalog exceeds the client limit.");
        }

        return offers;
    }

    private static Offer CreateOffer(ItemDefinition definition, byte level, double durability, bool hasSkill = false)
    {
        var category = definition.Group switch
        {
            < 12 => (byte)13,
            15 => (byte)15,
            12 when definition.Skill is not null && definition.ItemSlot is null => (byte)15,
            13 => (byte)16,
            14 when definition.Number <= 10 || definition.ConsumeEffect is not null => (byte)17,
            _ => (byte)14,
        };
        var price = category switch { 13 => 10, 14 => 5, 15 => 2, 16 => 20, _ => 1 };
        var code = checked((ushort)((definition.Group * 512) + definition.Number));
        if (!double.IsFinite(durability))
        {
            throw new InvalidOperationException("Solo shop item durability is not finite.");
        }

        return new Offer(((uint)code * 16) + level + 1, code, category, price, level, Math.Clamp(durability, 1, 255), definition, hasSkill);
    }

    /// <summary>A catalog offer, shared by validation, delivery and the client catalog response.</summary>
    public sealed record Offer(uint Id, ushort ItemCode, byte Category, int Price, byte Level, double Durability, ItemDefinition Definition, bool HasSkill = false);
}
