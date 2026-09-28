// <copyright file="BalanceV1Economy.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>Opt-in prices for cooled-down supplies and two reviewed starter weapon families.</summary>
public static class BalanceV1Economy
{
    /// <summary>Gets a reviewed purchase valuation, including the actual potion stack size.</summary>
    /// <param name="item">The item being valued.</param>
    /// <param name="configuration">The active profile.</param>
    /// <param name="price">The positive unit valuation times the remaining stack size.</param>
    /// <returns>Whether this item has an installed balance-v1 price.</returns>
    public static bool TryGetBuyingPrice(Item item, GameConfiguration configuration, out long price)
    {
        price = 0;
        if (!BalanceV1.IsEnabled(configuration) || SoloBalance.IsEnabled(configuration) || item.Definition is not { } definition)
        {
            return false;
        }

        var unit = (definition.Group, definition.Number) switch
        {
            (14, 1) => 12, (14, 2) => 48, (14, 3) => 176,
            (14, 4) => 8, (14, 5) => 32, (14, 6) => 112,
            (14, 35) => 140, (14, 36) => 300, (14, 37) => 600,
            (15, 3) => 36,
            _ => 0,
        };
        if (unit > 0)
        {
            // The live recovery contract ignores +1, so its price must not double either.
            price = unit * (definition.Group == 14 ? item.Durability() : 1L);
            return true;
        }

        var weaponBase = (definition.Group, definition.Number) switch
        {
            (0, 32) => 600, // Sacred Glove: real Killing Blow, original equip requirements.
            (5, 0) => 120, // Skull Staff: no free skill, no demand or power change.
            _ => 0,
        };
        if (weaponBase == 0)
        {
            return false;
        }

        // Valuations of enhanced/optioned descendants use the same bounded family scale,
        // including crafting contributions and repairs, not the old network-game valuation.
        var enhancement = 1 + (0.2 * Math.Min(15, (int)item.Level));
        var options = item.ItemOptions.Sum(option => option.ItemOption?.OptionType == ItemOptionTypes.Luck ? .25
            : option.ItemOption?.OptionType == ItemOptionTypes.Option ? .25 * Math.Clamp((int)option.Level, 0, 4)
            : option.ItemOption?.OptionType == ItemOptionTypes.Excellent ? 1 : 0);
        price = Math.Max(1, (long)Math.Ceiling(weaponBase * enhancement * enhancement * (1 + options)));
        return true;
    }

    /// <summary>Gets a consistent low resale price without stack-splitting or enhancement arbitrage.</summary>
    /// <param name="item">The item.</param>
    /// <param name="configuration">The active profile.</param>
    /// <param name="price">The resale amount.</param>
    /// <returns>Whether a reviewed resale price exists.</returns>
    public static bool TryGetSellingPrice(Item item, GameConfiguration configuration, out long price)
    {
        if (!TryGetBuyingPrice(item, configuration, out var buying))
        {
            price = 0;
            return false;
        }

        var count = item.Definition!.Group == 14 ? item.Durability() : 1;
        price = count > 0 ? Math.Max(1, (buying / count) * 15 / 100) * count : 0;
        return true;
    }
}
