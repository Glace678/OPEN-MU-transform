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

    // Above this rank the mid-route linear HP cap gives way to the BalanceLab quadratic model,
    // so end-game (rank 385-400) mobs are not melted by common-gear players. Mid-route (<= kink)
    // stays linear to keep already-pinned encounters (e.g. Tarkan rank 202.14 -> 2315) fixed.
    private const double HighRankKink = 250;
    private const double HighRankQuadratic = 0.099;

    private static readonly HashSet<short> BossNumbers = [38, 49, 77, 275, 412, 459];

    private static readonly Dictionary<short, double> BossRanks = new()
    {
        { 38, 150.0 },
        { 49, 190.0 },
        { 77, 265.0 },
        { 275, 400.0 },
        { 412, 350.0 },
        { 459, 450.0 },
    };

    /// <summary>Computes the health cap for boss monsters to prevent legacy hyper-inflation.</summary>
    public static double GetBossHealthCap(short monsterNumber, double rank)
    {
        return monsterNumber switch
        {
            38 => 3200.0,
            49 => 4200.0,
            77 => 6100.0,
            275 => 6900.0,
            412 => 5200.0,
            459 => 8500.0,
            _ => 8500.0,
        };
    }

    /// <summary>Computes the physical maximum attack cap for boss monsters.</summary>
    public static double GetBossPhysicalMaximumCap(double rank) => 60.0 + (0.50 * rank);

    /// <summary>Computes the conservative common-gear HP cap for a fixed ordinary content rank.</summary>
    /// <param name="rank">The fixed normal-route content rank.</param>
    public static int GetHealthCap(double rank)
    {
        var linear = 900 + (7 * rank);
        if (rank <= HighRankKink)
        {
            return checked((int)Math.Round(linear, MidpointRounding.AwayFromZero));
        }

        var aboveKink = rank - HighRankKink;
        return checked((int)Math.Round(linear + (HighRankQuadratic * aboveKink * aboveKink), MidpointRounding.AwayFromZero));
    }

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
            || monster.SpawnArea.MaximumHealthOverride.HasValue)
        {
            return;
        }

        if (BossNumbers.Contains(monster.Definition.Number))
        {
            var rank = BossRanks.TryGetValue(monster.Definition.Number, out var bRank) ? bRank : monster.Definition[Stats.Level];
            var originalHealth = monster.Attributes[Stats.MaximumHealth];
            if (originalHealth > 0 && float.IsFinite(originalHealth))
            {
                var ratio = Math.Min(1, GetBossHealthCap(monster.Definition.Number, rank) / originalHealth);
                if (ratio < 1)
                {
                    monster.Attributes.AddElement(new SimpleElement((float)ratio, AggregateType.Multiplicate), Stats.MaximumHealth);
                }
            }

            var originalMaximum = monster.Attributes[Stats.MaximumPhysBaseDmg];
            if (originalMaximum > 0 && float.IsFinite(originalMaximum))
            {
                var ratio = Math.Min(1, GetBossPhysicalMaximumCap(rank) / originalMaximum);
                if (ratio < 1)
                {
                    var element = new SimpleElement((float)ratio, AggregateType.Multiplicate);
                    monster.Attributes.AddElement(element, Stats.MinimumPhysBaseDmg);
                    monster.Attributes.AddElement(element, Stats.MaximumPhysBaseDmg);
                }
            }

            return;
        }

        if (monster.CurrentMap.Definition is not PersistentIdentity mapIdentity
            || !BalanceV1ContentRank.TryGetRank(
                mapIdentity.Id,
                monster.Definition.Number,
                BalanceV1ContentRank.NormalDifficulty,
                out var normalRank))
        {
            return;
        }

        // Per-map identity half: scale a shared MonsterDefinition instance up to its fixed content-rank combat
        // budget when the base stats encode a lower-rank home map (e.g. Skeleton Warrior num 14 is shared by
        // Lorencia rank 30 with base HP 411 and Dungeon rank 60; the design wants the Dungeon instance at ~732 HP).
        // Non-shared mobs already sit at their rank target, so the multiplier is ~1 and this is a no-op for them.
        // Lockstep with BalanceLab Rules.Monster(rank): hp = 100 + 10r + 0.009r^2, phys max dmg = 10 + 0.7r, defense = 10 + 0.65r.
        // The high-rank route of a monster that is shared across several routes starts from its low-rank home
        // definition; scale it up to its fixed rank combat budget. Single-route monsters already carry their own
        // authored stats, so they are left untouched.
        if (IsHighestRankSharedRoute(monster.Definition.Number, normalRank))
        {
            ApplySharedInstanceScaleUp(monster, normalRank);
        }

        // The cap-down below only reins in mid/end-route originals that already exceed their rank budget.
        if (normalRank < FirstAdjustedRank)
        {
            return;
        }

        var mobOriginalHealth = monster.Attributes[Stats.MaximumHealth];
        if (mobOriginalHealth > 0 && float.IsFinite(mobOriginalHealth))
        {
            var ratio = Math.Min(1, GetHealthCap(normalRank) / mobOriginalHealth);
            if (ratio < 1)
            {
                monster.Attributes.AddElement(new SimpleElement((float)ratio, AggregateType.Multiplicate), Stats.MaximumHealth);
            }
        }

        var mobOriginalMaximum = monster.Attributes[Stats.MaximumPhysBaseDmg];
        if (mobOriginalMaximum > 0 && float.IsFinite(mobOriginalMaximum))
        {
            var ratio = Math.Min(1, GetPhysicalMaximumCap(normalRank) / mobOriginalMaximum);
            if (ratio < 1)
            {
                var element = new SimpleElement((float)ratio, AggregateType.Multiplicate);
                monster.Attributes.AddElement(element, Stats.MinimumPhysBaseDmg);
                monster.Attributes.AddElement(element, Stats.MaximumPhysBaseDmg);
            }
        }
    }

    /// <summary>
    /// Scales a shared MonsterDefinition instance up to its fixed content-rank combat budget when the base
    /// stats encode a lower-rank home map. Only ever multiplies up (never down); non-shared mobs already sit at
    /// their rank target, so the multiplier is ~1 and nothing changes. Lockstep with BalanceLab Rules.Monster.
    /// </summary>
    /// <summary>The fixed combat budget for the high-rank route of a shared monster, lockstep with BalanceLab Rules.Monster.</summary>
    /// <remarks>health = 100 + 10r + 0.012r^2 (validated against content-monsters.json: rank30=410.8, rank60=743.2, rank66.67=820).</remarks>
    public static double GetSharedRouteHealthTarget(double rank) => 100.0 + (10.0 * rank) + (0.012 * rank * rank);

    /// <summary>The fixed physical maximum damage budget for the high-rank route of a shared monster (10 + 0.78r; rank60=56.8).</summary>
    public static double GetSharedRouteAttackTarget(double rank) => 10.0 + (0.78 * rank);

    /// <summary>
    /// True when this (monster, rank) binding is the highest-rank route among all routes that spawn the same
    /// shared monster definition. Only those instances start from the low-rank home stats and need a scale-up;
    /// single-route monsters and the low-rank home route already carry their authored stats.
    /// </summary>
    public static bool IsHighestRankSharedRoute(short monsterNumber, double rank)
    {
        var routes = BalanceV1ContentRank.Mappings.Where(mapping => mapping.MonsterNumber == monsterNumber).ToList();
        return routes.Count > 1 && rank >= routes.Max(mapping => mapping.Rank);
    }

    /// <summary>
    /// Scales a shared MonsterDefinition instance up to its fixed content-rank combat budget on its high-rank
    /// route. Only ever multiplies up (never down); single-route and low-rank-home instances never reach here.
    /// </summary>
    private static void ApplySharedInstanceScaleUp(Monster monster, double rank)
    {
        if (rank < 1)
        {
            return;
        }

        var targetHealth = GetSharedRouteHealthTarget(rank);
        var originalHealth = monster.Attributes[Stats.MaximumHealth];
        if (originalHealth > 0 && float.IsFinite(originalHealth) && targetHealth > originalHealth)
        {
            monster.Attributes.AddElement(
                new SimpleElement((float)(targetHealth / originalHealth), AggregateType.Multiplicate), Stats.MaximumHealth);
        }

        var targetPhysMax = GetSharedRouteAttackTarget(rank);
        var originalPhysMax = monster.Attributes[Stats.MaximumPhysBaseDmg];
        if (originalPhysMax > 0 && float.IsFinite(originalPhysMax) && targetPhysMax > originalPhysMax)
        {
            var ratio = (float)(targetPhysMax / originalPhysMax);
            monster.Attributes.AddElement(new SimpleElement(ratio, AggregateType.Multiplicate), Stats.MinimumPhysBaseDmg);
            monster.Attributes.AddElement(new SimpleElement(ratio, AggregateType.Multiplicate), Stats.MaximumPhysBaseDmg);
        }

        var targetDefense = 10.0 + (0.65 * rank);
        var originalDefense = monster.Attributes[Stats.DefensePvm];
        if (originalDefense > 0 && float.IsFinite(originalDefense) && targetDefense > originalDefense)
        {
            monster.Attributes.AddElement(
                new SimpleElement((float)(targetDefense / originalDefense), AggregateType.Multiplicate), Stats.DefensePvm);
        }
    }
}
