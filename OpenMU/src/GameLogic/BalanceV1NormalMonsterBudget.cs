// <copyright file="BalanceV1NormalMonsterBudget.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using PersistentIdentity = MUnique.OpenMU.Persistence.IIdentifiable;

/// <summary>
/// Caps excessive original combat attributes on ordinary balance-v1 route instances only.
/// The original definition and item-drop level remain untouched for other maps and profiles.
/// </summary>
public static class BalanceV1NormalMonsterBudget
{
    private const double FirstAdjustedRank = 95;

    private static readonly HashSet<short> BossNumbers = [38, 49, 77, 275, 412, 459];

    /// <summary>Computes the conservative common-gear HP cap for a fixed ordinary content rank.</summary>
    /// <param name="rank">The fixed normal-route content rank.</param>
    public static int GetHealthCap(double rank) => checked((int)Math.Round(900 + (7 * rank), MidpointRounding.AwayFromZero));

    /// <summary>Computes the cap on the source physical-attack maximum, before armor and skills.</summary>
    /// <param name="rank">The fixed normal-route content rank.</param>
    public static double GetPhysicalMaximumCap(double rank) => 35 + (0.24 * rank);

    /// <summary>Applies per-instance attributes before the first spawn; the same attributes persist on respawn.</summary>
    /// <param name="monster">The newly constructed monster instance.</param>
    /// <param name="configuration">The enclosing game configuration, if one was supplied by normal map initialization.</param>
    /// <param name="eventSpawn">Whether an event manager created this instance.</param>
    internal static void Apply(Monster monster, GameConfiguration? configuration, bool eventSpawn)
    {
        if (configuration is null || !BalanceV1.IsEnabled(configuration)
            || eventSpawn || monster.Definition.ObjectKind != NpcObjectKind.Monster
            || monster.SpawnArea.SpawnTrigger != SpawnTrigger.Automatic
            || monster.SpawnArea.MaximumHealthOverride.HasValue
            || BossNumbers.Contains(monster.Definition.Number)
            || monster.CurrentMap.Definition is not PersistentIdentity mapIdentity
            || !BalanceV1ContentRank.TryGetRank(mapIdentity.Id, monster.Definition.Number,
                BalanceV1ContentRank.NormalDifficulty, out var rank)
            || rank < FirstAdjustedRank)
        {
            return;
        }

        var originalHealth = monster.Attributes[Stats.MaximumHealth];
        if (originalHealth > 0 && float.IsFinite(originalHealth))
        {
            var ratio = Math.Min(1, GetHealthCap(rank) / originalHealth);
            if (ratio < 1)
            {
                monster.Attributes.AddElement(new SimpleElement((float)ratio, AggregateType.Multiplicate), Stats.MaximumHealth);
            }
        }

        var originalMaximum = monster.Attributes[Stats.MaximumPhysBaseDmg];
        if (originalMaximum > 0 && float.IsFinite(originalMaximum))
        {
            var ratio = Math.Min(1, GetPhysicalMaximumCap(rank) / originalMaximum);
            if (ratio < 1)
            {
                var element = new SimpleElement((float)ratio, AggregateType.Multiplicate);
                monster.Attributes.AddElement(element, Stats.MinimumPhysBaseDmg);
                monster.Attributes.AddElement(element, Stats.MaximumPhysBaseDmg);
            }
        }
    }
}
