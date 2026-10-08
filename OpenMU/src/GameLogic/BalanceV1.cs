// <copyright file="BalanceV1.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using MUnique.OpenMU.DataModel.Configuration;

/// <summary>
/// Runtime rules for configurations which explicitly install the balance-v1 candidate profile.
/// </summary>
public static class BalanceV1
{
    /// <summary>The persistent marker of a completed balance-v1 installation.</summary>
    public static readonly Guid ProfileAttributeId = new("c48ebf5a-04c9-4a5d-89d1-12cc72c84b61");

    /// <summary>The normal-level cap of the candidate design.</summary>
    public const short NormalLevelCap = 400;

    /// <summary>The master-level cap of the candidate design.</summary>
    public const short MasterLevelCap = 200;

    /// <summary>The normal stat points granted by every character class per level.</summary>
    public const int PointsPerLevel = 5;

    /// <summary>The encounter experience added for each extra nearby party member.</summary>
    public const double PartyExperiencePerExtraMember = 0.90;

    // Solo-fun (v2) runtime profile. Keep these constants in lockstep with
    // OpenMU-数值重设计/design/balance.v2.json. The public helpers below are
    // deliberately pure so the live combat path and the balance verifier can
    // exercise exactly the same bounded formulas.
    private const double MoneyChance = 0.75;
    private const double JewelChance = 0.05;
    private const int RareEligibilityRank = 80;
    private const double MasterRankPerLevel = 0.35;
    private const double ReferenceKillCycleSeconds = 5.5;
    private const double OverlevelGraceRanks = 30;
    private const double OverlevelDecayRanks = 100;
    private const double OverlevelExperienceFloor = 0.15;
    private const double UnderlevelExperienceCap = 1.15;

    // Keep combat constants in this runtime class in lockstep with
    // OpenMU-数值重设计/design/balance.v2.json.  The public helpers below
    // are deliberately pure so the live combat path and the balance verifier
    // can exercise exactly the same bounded formulas.
    private const double HitBase = 0.92;
    private const double HitSlope = 0.08;
    private const double HitMinimum = 0.70;
    private const double HitMaximum = 0.98;
    private const double ArmorConstant = 70;
    private const double ArmorPerRank = 2.4;
    private const double ArmorReductionCap = 0.60;
    private const double TotalReductionCap = 0.70;

    private static readonly (double Rank, double Seconds)[] NormalExperienceAnchors =
    [
        (1, 14), (20, 47), (80, 110), (150, 162), (220, 208), (300, 254), (350, 289), (399, 330),
    ];

    private static readonly (double Rank, double Seconds)[] MasterExperienceAnchors =
    [
        (1, 326), (50, 455), (100, 583), (150, 712), (200, 840),
    ];

    // Curated encounter lists keyed by monster number. Membership is checked with HashSet.Contains,
    // so unknown/out-of-range monsters safely fall back to the normal (1x) tier - there is no positional
    // indexing that could go out of range. Not config-driven yet: a boss/elite absent from these lists
    // intentionally receives normal rewards. Promoting this to a configuration field is a gameplay-balance
    // change and is therefore deferred (see balance-v2 design notes).
    private static readonly HashSet<short> BossMonsterNumbers = [38, 49, 77, 275, 412, 459];
    private static readonly HashSet<short> EliteMonsterNumbers = [43, 44, 78, 79, 80, 81, 82, 83];

    private static readonly PotionRule[] Potions =
    [
        new(14, 1, PotionGroup.Health, 1, 0.32, 180, TimeSpan.FromSeconds(8)),
        new(14, 2, PotionGroup.Health, 80, 0.32, 700, TimeSpan.FromSeconds(8)),
        new(14, 3, PotionGroup.Health, 180, 0.32, 3000, TimeSpan.FromSeconds(8)),
        new(14, 4, PotionGroup.Mana, 1, 0.40, 120, TimeSpan.FromSeconds(12)),
        new(14, 5, PotionGroup.Mana, 80, 0.40, 400, TimeSpan.FromSeconds(12)),
        new(14, 6, PotionGroup.Mana, 180, 0.40, 1600, TimeSpan.FromSeconds(12)),
        new(14, 35, PotionGroup.Shield, 150, 0.20, 500, TimeSpan.FromSeconds(15)),
        new(14, 36, PotionGroup.Shield, 250, 0.20, 900, TimeSpan.FromSeconds(15)),
        new(14, 37, PotionGroup.Shield, 350, 0.20, 1600, TimeSpan.FromSeconds(15)),
    ];

    private static readonly UpgradeStep[] UpgradeSteps =
    [
        new(1, 1.00, 1, 1), new(2, 1.00, 1, 1), new(3, 1.00, 1, 1),
        new(4, 1.00, 1, 1), new(5, 1.00, 1, 1), new(6, 1.00, 1, 1),
        new(7, 0.90, 2, 1), new(8, 0.80, 3, 1), new(9, 0.70, 3, 2),
        new(10, 0.60, 4, 2), new(11, 0.52, 4, 2), new(12, 0.44, 5, 2),
        new(13, 0.38, 5, 2), new(14, 0.30, 6, 3), new(15, 0.24, 7, 3),
    ];

    /// <summary>The supported experience-rate variants of balance-v1.</summary>
    public enum ExperienceProfile
    {
        /// <summary>Uses the reference progression speed.</summary>
        Standard,

        /// <summary>Uses 1.5 times normal and master experience.</summary>
        Relaxed,

        /// <summary>Uses 0.75 times normal and master experience.</summary>
        Journey,
    }

    /// <summary>The independent equipment channel result.</summary>
    public enum EquipmentDrop
    {
        /// <summary>No equipment.</summary>
        None,

        /// <summary>A common item.</summary>
        Common,

        /// <summary>An excellent item.</summary>
        Excellent,

        /// <summary>An ancient item.</summary>
        Ancient,

        /// <summary>A socket item.</summary>
        Socket,
    }

    /// <summary>The independently cooled-down potion resource.</summary>
    public enum PotionGroup
    {
        /// <summary>Health recovery.</summary>
        Health,

        /// <summary>Mana recovery.</summary>
        Mana,

        /// <summary>Shield recovery.</summary>
        Shield,
    }

    /// <summary>A candidate loot roll split into independent equipment, money, and jewel channels.</summary>
    /// <param name="Equipment">The selected equipment quality.</param>
    /// <param name="Money">Whether Zen drops.</param>
    /// <param name="Jewel">Whether a jewel drops.</param>
    public readonly record struct LootRoll(EquipmentDrop Equipment, bool Money, bool Jewel);

    /// <summary>A potion rule from the candidate design.</summary>
    /// <param name="Group">The item group.</param>
    /// <param name="Number">The item number.</param>
    /// <param name="CooldownGroup">The independently cooled-down resource.</param>
    /// <param name="UnlockLevel">The required normal level.</param>
    /// <param name="RecoveryFraction">The fraction of the maximum resource to restore.</param>
    /// <param name="RecoveryCap">The maximum absolute amount to restore.</param>
    /// <param name="Cooldown">The group cooldown.</param>
    public readonly record struct PotionRule(
        byte Group,
        short Number,
        PotionGroup CooldownGroup,
        int UnlockLevel,
        double RecoveryFraction,
        double RecoveryCap,
        TimeSpan Cooldown);

    /// <summary>A level enhancement step from the candidate design.</summary>
    /// <param name="TargetLevel">The resulting item level.</param>
    /// <param name="Chance">The success chance in the range zero to one.</param>
    /// <param name="PityAttempts">The attempt on which success is guaranteed.</param>
    /// <param name="Jewels">The material-jewel budget.</param>
    /// <remarks>
    /// <see cref="PityAttempts"/> is part of the designed profile but is intentionally NOT wired up
    /// yet: honouring it requires a persisted per-item failed-attempt counter (a wire/save-format change
    /// that must be validated against the client and existing characters). Today only <see cref="Chance"/>
    /// is consumed, so the pity guarantee must not be assumed to take effect.
    /// </remarks>
    public readonly record struct UpgradeStep(int TargetLevel, double Chance, int PityAttempts, int Jewels);

    /// <summary>Returns whether the configuration has a completed balance-v1 marker.</summary>
    /// <param name="configuration">The game configuration.</param>
    /// <returns>Whether balance-v1 is installed.</returns>
    public static bool IsEnabled(GameConfiguration configuration) =>
        configuration.GlobalBaseAttributeValues?.Any(
            attribute => attribute.Definition?.Id == ProfileAttributeId && attribute.Value >= 1) is true;

    /// <summary>Rejects a configuration which contains both mutually exclusive profile markers.</summary>
    /// <param name="configuration">The configuration to check.</param>
    public static void ValidateProfileMarkers(GameConfiguration configuration)
    {
        if (IsEnabled(configuration) && SoloBalance.IsEnabled(configuration))
        {
            throw new InvalidOperationException(
                "The configuration contains both Solo and balance-v1 profile markers. These profiles are mutually exclusive.");
        }
    }

    /// <summary>Reads the explicit balance-v1 command-line option.</summary>
    /// <param name="arguments">The process arguments.</param>
    /// <returns>The requested profile, or <see langword="null"/> when balance-v1 was not requested.</returns>
    public static ExperienceProfile? GetRequestedProfile(IEnumerable<string> arguments)
    {
        var matches = arguments.Where(
                argument => argument.Equals("-balance-v1", StringComparison.OrdinalIgnoreCase)
                            || argument.StartsWith("-balance-v1:", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length == 0)
        {
            return null;
        }

        if (matches.Length > 1)
        {
            throw new ArgumentException("The -balance-v1 option can only be specified once.", nameof(arguments));
        }

        var separator = matches[0].IndexOf(':');
        var profile = separator < 0 ? "standard" : matches[0][(separator + 1)..];
        return profile.ToLowerInvariant() switch
        {
            "standard" => ExperienceProfile.Standard,
            "relaxed" => ExperienceProfile.Relaxed,
            "journey" => ExperienceProfile.Journey,
            _ => throw new ArgumentException(
                $"Unknown balance-v1 experience profile '{profile}'. Expected standard, relaxed, or journey.",
                nameof(arguments)),
        };
    }

    /// <summary>Gets the stable lowercase identifier of a profile.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The profile identifier.</returns>
    public static string GetProfileId(ExperienceProfile profile) => profile switch
    {
        ExperienceProfile.Standard => "standard",
        ExperienceProfile.Relaxed => "relaxed",
        ExperienceProfile.Journey => "journey",
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported balance-v1 profile."),
    };

    /// <summary>Gets the multiplier which applies equally to normal and master experience.</summary>
    /// <param name="profile">The selected experience profile.</param>
    /// <returns>The experience multiplier.</returns>
    public static float GetExperienceMultiplier(ExperienceProfile profile) => profile switch
    {
        ExperienceProfile.Standard => 1.0f,
        ExperienceProfile.Relaxed => 1.5f,
        ExperienceProfile.Journey => 0.75f,
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported balance-v1 profile."),
    };

    /// <summary>
    /// Calculates the bounded chance that an attack connects.
    /// </summary>
    /// <remarks>
    /// Attack and defense rates are used as the engine's accuracy/evasion
    /// proxies. A logarithmic ratio keeps the result useful at both low and
    /// high levels without allowing a guaranteed or impossible hit.
    /// </remarks>
    /// <param name="accuracy">The attacker's accuracy/attack rate.</param>
    /// <param name="evasion">The defender's evasion/defense rate.</param>
    /// <returns>A probability in the inclusive range [0.70, 0.98].</returns>
    public static double CalculateHitChance(double accuracy, double evasion)
    {
        var safeAccuracy = Math.Max(1, double.IsFinite(accuracy) ? accuracy : 1);
        var safeEvasion = Math.Max(1, double.IsFinite(evasion) ? evasion : 1);
        return Math.Clamp(
            HitBase + (HitSlope * Math.Log2(safeAccuracy / safeEvasion)),
            HitMinimum,
            HitMaximum);
    }

    /// <summary>
    /// Calculates the capped armor reduction used by balance-v1.
    /// </summary>
    /// <param name="armor">The defender's effective armor.</param>
    /// <param name="attackerRank">The attacker's content rank.</param>
    /// <param name="extraReduction">Additional multiplicative reduction.</param>
    /// <param name="penetration">The attacker's armor penetration fraction.</param>
    /// <returns>A damage reduction probability in the range [0, 0.70].</returns>
    public static double CalculateDamageReduction(
        double armor,
        double attackerRank,
        double extraReduction = 0,
        double penetration = 0)
    {
        var safeArmor = Math.Max(0, double.IsFinite(armor) ? armor : 0);
        var safeRank = Math.Max(0, double.IsFinite(attackerRank) ? attackerRank : 0);
        var effectivePenetration = Math.Clamp(
            double.IsFinite(penetration) ? penetration : 0,
            0,
            0.25);
        var effectiveArmor = safeArmor * (1 - effectivePenetration);
        var armorReduction = Math.Min(
            ArmorReductionCap,
            effectiveArmor / (effectiveArmor + ArmorConstant + (ArmorPerRank * safeRank)));
        return Math.Min(
            TotalReductionCap,
            1 - ((1 - armorReduction) * (1 - Math.Clamp(extraReduction, 0, 1))));
    }

    /// <summary>Converts normal and master levels to the candidate content rank.</summary>
    /// <param name="level">The normal level.</param>
    /// <param name="masterLevel">The master level.</param>
    /// <returns>The content rank used by balance-v1 combat formulas.</returns>
    public static double GetCombatRank(double level, double masterLevel)
    {
        var safeLevel = Math.Max(1, double.IsFinite(level) ? level : 1);
        var safeMasterLevel = Math.Max(0, double.IsFinite(masterLevel) ? masterLevel : 0);
        return safeLevel + (safeMasterLevel * MasterRankPerLevel);
    }

    /// <summary>Calculates a kill reward using the candidate content rank and monster tier.</summary>
    /// <param name="contentRank">The defeated monster's content rank.</param>
    /// <param name="monsterNumber">The monster definition number.</param>
    /// <param name="playerRank">The player's normal/master content rank.</param>
    /// <returns>The base experience before server, map, and character experience rates.</returns>
    public static double CalculateKillExperience(double contentRank, short monsterNumber, double playerRank)
    {
        if (!double.IsFinite(contentRank) || contentRank < 1)
        {
            return 0;
        }

        var safePlayerRank = Math.Max(1, double.IsFinite(playerRank) ? playerRank : 1);
        var excess = safePlayerRank - contentRank - OverlevelGraceRanks;
        var levelMultiplier = excess <= 0
            ? Math.Min(UnderlevelExperienceCap, 1 + (Math.Max(0, contentRank - safePlayerRank) / 200))
            : Math.Max(OverlevelExperienceFloor, Math.Exp(-excess / OverlevelDecayRanks));
        var tierMultiplier = BossMonsterNumbers.Contains(monsterNumber)
            ? 40
            : EliteMonsterNumbers.Contains(monsterNumber) ? 5 : 1;
        return BaseExperience(contentRank) * tierMultiplier * levelMultiplier;
    }

    /// <summary>Creates the exact cumulative normal or master experience table.</summary>
    /// <param name="master">Whether to create the master table.</param>
    /// <returns>The cumulative experience table indexed by attained level.</returns>
    public static long[] CreateExperienceTable(bool master)
    {
        var cap = master ? MasterLevelCap : NormalLevelCap;
        var table = new long[cap + 1];
        for (var attained = master ? 1 : 2; attained <= cap; attained++)
        {
            var source = master ? attained : attained - 1;
            var rank = master ? NormalLevelCap + ((attained - 1) * MasterRankPerLevel) : source;
            var seconds = Curve(master ? MasterExperienceAnchors : NormalExperienceAnchors, source);
            var required = checked((long)Math.Ceiling(BaseExperience(rank) * seconds / ReferenceKillCycleSeconds));
            table[attained] = checked(table[attained - 1] + Math.Max(1, required));
        }

        return table;
    }

    /// <summary>Rolls the independent candidate loot channels.</summary>
    /// <param name="contentRank">The defeated monster content rank.</param>
    /// <param name="equipmentRoll">The equipment roll in the range [0, 1).</param>
    /// <param name="moneyRoll">The money roll in the range [0, 1).</param>
    /// <param name="jewelRoll">The jewel roll in the range [0, 1).</param>
    /// <returns>The candidate loot result.</returns>
    public static LootRoll RollLoot(double contentRank, double equipmentRoll, double moneyRoll, double jewelRoll)
    {
        ValidateRoll(equipmentRoll, nameof(equipmentRoll));
        ValidateRoll(moneyRoll, nameof(moneyRoll));
        ValidateRoll(jewelRoll, nameof(jewelRoll));

        var equipment = equipmentRoll switch
        {
            < 0.8 => EquipmentDrop.None,
            < 0.996 => EquipmentDrop.Common,
            < 0.9995 => EquipmentDrop.Excellent,
            < 0.99985 => EquipmentDrop.Ancient,
            _ => EquipmentDrop.Socket,
        };

        if (contentRank < RareEligibilityRank
            && equipment is EquipmentDrop.Excellent or EquipmentDrop.Ancient or EquipmentDrop.Socket)
        {
            equipment = EquipmentDrop.Common;
        }

        return new LootRoll(equipment, moneyRoll < MoneyChance, jewelRoll < JewelChance);
    }

    /// <summary>Calculates a Zen amount independently from experience gain and rates.</summary>
    /// <param name="contentRank">The monster content rank.</param>
    /// <param name="monsterNumber">The monster definition number.</param>
    /// <returns>The Zen amount.</returns>
    public static uint CalculateZen(double contentRank, short monsterNumber)
    {
        var rank = Math.Max(1, contentRank);
        var rankMultiplier = BossMonsterNumbers.Contains(monsterNumber)
            ? 50
            : EliteMonsterNumbers.Contains(monsterNumber) ? 6 : 1;
        var amount = Math.Round((8 + (1.6 * rank) + (0.006 * rank * rank)) * rankMultiplier);
        return (uint)Math.Clamp(amount, 1, uint.MaxValue);
    }

    /// <summary>Gets a candidate potion rule by item identifier.</summary>
    /// <param name="group">The item group.</param>
    /// <param name="number">The item number.</param>
    /// <param name="rule">The matched rule.</param>
    /// <returns>Whether a rule was found.</returns>
    public static bool TryGetPotionRule(byte group, short number, out PotionRule rule)
    {
        foreach (var candidate in Potions)
        {
            if (candidate.Group == group && candidate.Number == number)
            {
                rule = candidate;
                return true;
            }
        }

        rule = default;
        return false;
    }

    /// <summary>Gets the enhancement step for a target item level.</summary>
    /// <param name="targetLevel">The target item level.</param>
    /// <returns>The enhancement step.</returns>
    public static UpgradeStep GetUpgradeStep(int targetLevel)
    {
        if (targetLevel < 1 || targetLevel > UpgradeSteps.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(targetLevel), targetLevel, "The target item level must be between 1 and 15.");
        }

        return UpgradeSteps[targetLevel - 1];
    }

    private static double BaseExperience(double rank) => 25 + (12 * rank) + (0.55 * rank * rank);

    private static double Curve(IReadOnlyList<(double Rank, double Seconds)> anchors, double rank)
    {
        if (rank <= anchors[0].Rank)
        {
            return anchors[0].Seconds;
        }

        for (var index = 1; index < anchors.Count; index++)
        {
            if (rank > anchors[index].Rank)
            {
                continue;
            }

            var previous = anchors[index - 1];
            var current = anchors[index];
            var fraction = (rank - previous.Rank) / (current.Rank - previous.Rank);
            return previous.Seconds + ((current.Seconds - previous.Seconds) * fraction);
        }

        return anchors[^1].Seconds;
    }

    private static void ValidateRoll(double roll, string parameterName)
    {
        if (!double.IsFinite(roll) || roll < 0 || roll >= 1)
        {
            throw new ArgumentOutOfRangeException(parameterName, roll, "A loot roll must be finite and in the range [0, 1).");
        }
    }
}
