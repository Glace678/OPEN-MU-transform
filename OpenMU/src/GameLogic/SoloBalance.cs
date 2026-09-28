// <copyright file="SoloBalance.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;

/// <summary>
/// Shared rules for installations explicitly converted to the local solo profile.
/// </summary>
public static class SoloBalance
{
    /// <summary>Prices are expressed in smaller units, without changing item crafting valuations.</summary>
    public const int PriceDivisor = 1000;

    /// <summary>The persistent attribute which marks a completed profile conversion.</summary>
    public static readonly Guid ProfileAttributeId = new("9d0e5c83-794b-4bc1-9260-cc32888650a1");

    /// <summary>The additional normal-level points granted by the solo profile.</summary>
    public static readonly Guid LevelUpPointBonusAttributeId = new("9949c0ce-b6d1-4c57-b519-62a78b28700a");

    /// <summary>Returns whether the configuration has the solo profile installed.</summary>
    /// <param name="configuration">The active configuration.</param>
    /// <returns>Whether the profile is installed.</returns>
    public static bool IsEnabled(GameConfiguration configuration) =>
        configuration.GlobalBaseAttributeValues.Any(a => a.Definition.Id == ProfileAttributeId && a.Value >= 1);

    /// <summary>Reads the solo level bonus, including configurations created before it had its own attribute.</summary>
    /// <param name="configuration">The active configuration.</param>
    /// <returns>The additional points per normal level.</returns>
    public static int GetLevelUpPointBonus(GameConfiguration configuration) =>
        IsEnabled(configuration)
            ? (int)configuration.GlobalBaseAttributeValues
                .Where(attribute => attribute.Definition.Id == LevelUpPointBonusAttributeId
                    || attribute.Definition == Stats.PointsPerLevelUp)
                .Sum(attribute => attribute.Value)
            : 0;

    /// <summary>Excludes the legacy solo bonus which cannot be composed onto the character's mutable stat.</summary>
    /// <param name="configuration">The active configuration.</param>
    /// <returns>The base attributes usable by the character attribute system.</returns>
    internal static IEnumerable<ConstValueAttribute> GetComposableBaseAttributes(GameConfiguration configuration) =>
        IsEnabled(configuration)
            ? configuration.GlobalBaseAttributeValues.Where(attribute => attribute.Definition != Stats.PointsPerLevelUp)
            : configuration.GlobalBaseAttributeValues;

    /// <summary>Scales a transaction once, preserving zero and a minimum positive price of one Zen.</summary>
    /// <param name="price">The original final price.</param>
    /// <param name="configuration">The active configuration.</param>
    /// <returns>The payable price.</returns>
    public static long ScalePrice(long price, GameConfiguration configuration) =>
        IsEnabled(configuration) ? ScalePrice(price) : price;

    /// <summary>Converts a positive legacy price to solo Zen.</summary>
    /// <param name="price">The original final price.</param>
    /// <returns>The converted price.</returns>
    public static long ScalePrice(long price) => price > 0 ? Math.Max(1, price / PriceDivisor) : price;
}
