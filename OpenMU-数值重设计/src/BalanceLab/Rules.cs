namespace OpenMu.Balance;

public sealed record PlayerStats(string ClassId, string BuildId, int Level, int Master, double Rank,
    double[] Attributes, double Health, double Mana, double Power, double Armor, double Accuracy,
    double Evasion, double Haste, double Crit, double Excellent, double Reduction, double LifeSteal,
    double RangedUptime, Build Build, Gear Gear);
public sealed record MonsterStats(double Rank, string Kind, double Health, double Damage, double Armor,
    double Accuracy, double Evasion, double AttackSeconds, bool Telegraph, double Experience, long Zen);
public sealed record LootState(int EligibleKillsWithoutExcellent = 0);
public sealed record LootResult(string Equipment, bool Money, bool Jewel, LootState State);
public sealed record UpgradeState(int Level = 0, int FailuresAtNextLevel = 0);
public sealed record UpgradeResult(bool Succeeded, bool Guaranteed, UpgradeState State, int Jewels, long Zen);
public sealed record ShieldHit(double HealthDamage, double ShieldDamage);

public sealed class Rules(Design design)
{
    public Design Design { get; } = design;

    public static double Curve(double[][] anchors, double x)
    {
        if (x <= anchors[0][0]) return anchors[0][1];
        for (var i = 1; i < anchors.Length; i++)
        {
            if (x > anchors[i][0]) continue;
            var t = (x - anchors[i - 1][0]) / (anchors[i][0] - anchors[i - 1][0]);
            return anchors[i - 1][1] + (anchors[i][1] - anchors[i - 1][1]) * t;
        }
        return anchors[^1][1];
    }

    public PlayerStats Player(string classId, string buildId, int level, int master = 0,
        string gearId = "progression", double[]? customAllocation = null)
    {
        if (level < 1 || level > Design.NormalCap || master < 0 || master > Design.MasterCap
            || (level < Design.NormalCap && master != 0)) throw new ArgumentOutOfRangeException(nameof(level));
        var cls = Design.Classes.Single(c => c.Id == classId);
        var build = cls.Builds.Single(b => b.Id == buildId);
        var allocation = customAllocation ?? build.Allocation;
        if (allocation.Length != 5 || allocation.Any(v => !double.IsFinite(v) || v < 0)
            || Math.Abs(allocation.Sum() - 1) > 1e-9 || (classId != "DL" && allocation[4] != 0))
            throw new ArgumentException("Allocation must contain five legal fractions summing to one.");
        var points = (level - 1) * Design.PointsPerLevel;
        // Largest-remainder allocation spends exactly the same integer budget for every class.
        var allocated = allocation.Select(x => (int)Math.Floor(x * points)).ToArray();
        foreach (var i in Enumerable.Range(0, 5).OrderByDescending(i => allocation[i] * points - allocated[i])
                     .Take(points - allocated.Sum())) allocated[i]++;
        var stats = allocated.Select((x, i) => (double)x + (i == 4 ? 0 : 20)).ToArray();
        var gear = Design.Gear.Single(g => g.Id == gearId);
        var g = Design.Growth;
        var masterFraction = master / (double)Design.MasterCap;
        var rank = level + master * Design.MasterRankPerLevel;
        var powerBudget = g.BasePower + g.PowerPerRank * rank;
        var health = (g.BaseHealth + g.HealthPerLevel * level + g.HealthPerVitality * stats[2])
                     * cls.Health * (1 + g.MasterHealthCap * masterFraction);
        var mana = (g.BaseMana + g.ManaPerLevel * level + g.ManaPerEnergy * stats[3])
                   * cls.Mana * (1 + g.MasterManaCap * masterFraction);
        var power = (20 + stats.Zip(build.PowerWeights).Sum(v => v.First * v.Second)
            + powerBudget * g.GearPowerShare * gear.Factor) * cls.Damage
            * (1 + g.MasterDamageCap * masterFraction)
            * (1 + Math.Min(g.PartyDamageBuffCap, build.Support));
        var armor = (g.BaseArmor + g.ArmorPerAgility * stats[1]
            + powerBudget * g.GearArmorShare * gear.Factor) * cls.Armor
            * (1 + g.MasterArmorCap * masterFraction);
        var haste = 1 + Design.Combat.HasteCap * (1 - Math.Exp(-stats[1] / (80 + rank * 1.8)));
        return new PlayerStats(classId, buildId, level, master, rank, stats, health, mana, power, armor,
            100 + 3 * rank + 0.5 * stats[1], 95 + 2.6 * rank + 0.22 * stats[1], haste,
            Math.Min(Design.Combat.CritCap, Design.Combat.CritBase + gear.Crit),
            Math.Min(Design.Combat.ExcellentCap, gear.Excellent),
            Math.Min(0.25, gear.Reduction + build.BonusReduction),
            Math.Min(g.LifeStealCapPerSecond, build.LifeSteal), cls.RangedUptime, build, gear);
    }

    public MonsterStats Monster(double rank, string kind = "normal", int partySize = 1)
    {
        if (!double.IsFinite(rank) || rank < 1 || rank > Design.NormalCap + Design.MasterCap * Design.MasterRankPerLevel)
            throw new ArgumentOutOfRangeException(nameof(rank));
        if (partySize < 1 || partySize > Design.Progression.PartyMaxMembers)
            throw new ArgumentOutOfRangeException(nameof(partySize));
        var m = Design.Monsters;
        var spec = Design.Rank(kind);
        return new MonsterStats(rank, kind,
            (m.HpBase + m.HpLinear * rank + m.HpQuadratic * rank * rank) * spec.Hp
                * (1 + Design.Progression.PartyMonsterHpPerExtra * (partySize - 1)),
            (m.DamageBase + m.DamageLinear * rank) * spec.Damage
                * (1 + Design.Progression.PartyMonsterDamagePerExtra * (partySize - 1)),
            (m.ArmorBase + m.ArmorLinear * rank) * spec.Armor, 100 + 3 * rank, 100 + 3 * rank,
            spec.AttackSeconds, spec.Telegraph,
            BaseExperience(rank) * spec.Xp * (1 + Design.Progression.PartyXpPerExtra * (partySize - 1)),
            checked((long)Math.Round(BaseZen(rank) * spec.Zen)));
    }

    public double BaseExperience(double rank) =>
        Design.Monsters.XpBase + Design.Monsters.XpLinear * rank + Design.Monsters.XpQuadratic * rank * rank;
    public double BaseZen(double rank) =>
        Design.Monsters.ZenBase + Design.Monsters.ZenLinear * rank + Design.Monsters.ZenQuadratic * rank * rank;
    public double HitChance(double accuracy, double evasion) =>
        Math.Clamp(Design.Combat.HitBase + Design.Combat.HitSlope * Math.Log2(
            Math.Max(1, accuracy) / Math.Max(1, evasion)), Design.Combat.HitMin, Design.Combat.HitMax);

    public double Reduction(double armor, double attackerRank, double extra = 0, double penetration = 0)
    {
        var c = Design.Combat;
        armor = Math.Max(0, armor) * (1 - Math.Clamp(penetration, 0, c.PenetrationCap));
        var armorReduction = Math.Min(c.ArmorCap, armor / (armor + c.ArmorConstant + c.ArmorPerRank * attackerRank));
        return Math.Min(c.TotalReductionCap, 1 - (1 - armorReduction) * (1 - Math.Clamp(extra, 0, 1)));
    }

    public double CastDamage(PlayerStats player, MonsterStats target, Skill skill, Random? random = null)
    {
        var baseDamage = player.Power * skill.Coefficient * (1 - Reduction(target.Armor, player.Rank));
        var chance = HitChance(player.Accuracy, target.Evasion);
        if (random is null)
        {
            var expectedSpecial = 1 + player.Crit * (Design.Combat.CritMultiplier - 1)
                + player.Excellent * (Design.Combat.ExcellentMultiplier - 1);
            return baseDamage * chance * expectedSpecial;
        }
        // A skill's coefficient is its whole-cast budget, not a per-packet or per-hit multiplier.
        var result = 0.0;
        foreach (var weight in skill.HitWeights)
        {
            if (random.NextDouble() >= chance) continue;
            var special = random.NextDouble();
            var multiplier = special < player.Excellent ? Design.Combat.ExcellentMultiplier
                : special < player.Excellent + player.Crit ? Design.Combat.CritMultiplier : 1;
            result += baseDamage * weight * multiplier * (0.95 + random.NextDouble() * 0.1);
        }
        return result;
    }

    public double ReceiveDamage(PlayerStats player, MonsterStats target, double multiplier = 1,
        Random? random = null, bool telegraph = false)
    {
        var hit = HitChance(target.Accuracy, player.Evasion) * (telegraph ? 1 : player.RangedUptime);
        var damage = target.Damage * multiplier * (1 - Reduction(player.Armor, target.Rank, player.Reduction));
        return random is null ? damage * hit
            : random.NextDouble() < hit ? damage * (0.9 + 0.2 * random.NextDouble()) : 0;
    }

    public double ActionSeconds(PlayerStats player, Skill skill) =>
        Math.Max(Design.Combat.MinimumActionSeconds, skill.ActionSeconds / player.Haste);
    public Skill SkillFor(PlayerStats player, bool area)
    {
        var desired = Design.Skill(area ? player.Build.Area : player.Build.Single);
        return player.Level >= desired.Unlock ? desired : Design.Skill("basic");
    }

    public double ExperienceMultiplier(double playerRank, double targetRank)
    {
        var p = Design.Progression;
        var excess = playerRank - targetRank - p.OverlevelGraceRanks;
        return excess <= 0 ? Math.Min(p.UnderlevelRewardCap, 1 + Math.Max(0, targetRank - playerRank) / 200)
            : Math.Max(p.OverlevelFloor, Math.Exp(-excess / p.OverlevelDecayRanks));
    }

    public long[] ExperienceTable(bool master)
    {
        // Normal index is the attained level (1..400); master index is attained ML (0..200).
        var cap = master ? Design.MasterCap : Design.NormalCap;
        var result = new long[cap + 1];
        for (var attained = master ? 1 : 2; attained <= cap; attained++)
        {
            var source = master ? attained : attained - 1;
            var rank = master ? Design.NormalCap + (attained - 1) * Design.MasterRankPerLevel : source;
            var seconds = Curve(master ? Design.Progression.MasterSeconds : Design.Progression.NormalSeconds, source);
            var required = checked((long)Math.Ceiling(BaseExperience(rank) * seconds / Design.Combat.ReferenceKillCycleSeconds));
            result[attained] = checked(result[attained - 1] + Math.Max(1, required));
        }
        return result;
    }

    public static (int Level, long Cumulative) AddExperience(long[] table, int level, long current, long gained)
    {
        if (level < 0 || level >= table.Length || current < table[level] || current > table[^1] || gained < 0)
            throw new ArgumentOutOfRangeException(nameof(current));
        var next = current + Math.Min(gained, table[^1] - current);
        while (level + 1 < table.Length && next >= table[level + 1]) level++;
        return (level, next);
    }

    public Potion Potion(PlayerStats player, string kind)
    {
        var maximum = kind == "health" ? player.Health : player.Mana;
        return Design.Economy.Potions.Where(p => p.Kind == kind && p.Unlock <= player.Level)
            .OrderByDescending(p => Math.Min(p.Cap, p.Fraction * maximum)).ThenBy(p => p.Price).First();
    }

    public LootResult Drop(double contentRank, LootState state, double equipmentRoll, double moneyRoll, double jewelRoll)
    {
        foreach (var roll in new[] { equipmentRoll, moneyRoll, jewelRoll }) RollGuard(roll);
        if (state.EligibleKillsWithoutExcellent < 0 || state.EligibleKillsWithoutExcellent >= Design.Loot.ExcellentPityKills)
            throw new ArgumentOutOfRangeException(nameof(state));
        var eligible = contentRank >= Design.Loot.RareEligibilityRank;
        var threshold = 0.0;
        var equipment = "none";
        foreach (var entry in Design.Loot.Equipment)
        {
            threshold += entry.Chance;
            if (equipmentRoll >= threshold) continue;
            equipment = entry.Id;
            break;
        }
        if (!eligible && equipment is "excellent" or "ancient" or "socket") equipment = "common";
        var kills = state.EligibleKillsWithoutExcellent;
        if (eligible)
        {
            kills++;
            if (equipment is "excellent" or "ancient" or "socket") kills = 0;
            else if (kills >= Design.Loot.ExcellentPityKills) { equipment = "excellent"; kills = 0; }
        }
        return new LootResult(equipment, moneyRoll < Design.Loot.MoneyChance,
            jewelRoll < Design.Loot.JewelChance, new LootState(kills));
    }

    public UpgradeResult Upgrade(UpgradeState state, double itemRank, double roll)
    {
        RollGuard(roll);
        if (state.Level < 0 || state.Level >= Design.Enhancement.MaxLevel)
            throw new ArgumentOutOfRangeException(nameof(state));
        var step = Design.Enhancement.Steps.Single(s => s.Level == state.Level + 1);
        if (state.FailuresAtNextLevel < 0 || state.FailuresAtNextLevel >= step.Pity)
            throw new ArgumentOutOfRangeException(nameof(state));
        var guaranteed = state.FailuresAtNextLevel + 1 >= step.Pity;
        var success = guaranteed || roll < step.Chance;
        var next = success ? new UpgradeState(state.Level + 1) : state with { FailuresAtNextLevel = state.FailuresAtNextLevel + 1 };
        var zen = checked((long)Math.Ceiling(BaseZen(itemRank)
            * (Design.Enhancement.ZenFeeBase + Design.Enhancement.ZenFeeSquared * step.Level * step.Level)));
        return new UpgradeResult(success, guaranteed, next, step.Jewels, zen);
    }

    public static double ExpectedAttempts(UpgradeStep step) =>
        Enumerable.Range(0, step.Pity).Sum(i => Math.Pow(1 - step.Chance, i));
    public long VendorSell(long buyPrice, int count)
    {
        if (buyPrice < 0 || count < 0) throw new ArgumentOutOfRangeException(nameof(buyPrice));
        return checked((long)Math.Floor(buyPrice * Design.Economy.VendorBuybackFraction) * count);
    }
    public ShieldHit SplitPvpDamage(double damage, double availableShield)
    {
        if (damage < 0 || availableShield < 0) throw new ArgumentOutOfRangeException(nameof(damage));
        var shield = Math.Min(availableShield, damage * Design.Pvp.ShieldDamageShare);
        return new ShieldHit(damage - shield, shield);
    }
    public static void RollGuard(double roll)
    {
        if (!double.IsFinite(roll) || roll < 0 || roll >= 1) throw new ArgumentOutOfRangeException(nameof(roll));
    }
}
