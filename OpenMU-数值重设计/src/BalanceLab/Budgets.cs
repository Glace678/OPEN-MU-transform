namespace OpenMu.Balance;

public sealed record MasterBonuses(double Damage, double Health, double Armor, double Mana, double SpecializationFraction);
public sealed record GearBudget(double WeaponPower, double TotalArmor, Dictionary<string, double> ArmorBySlot);
public sealed record ArenaStats(PlayerStats Player, double Shield);
public sealed record PvpMatch(string AttackerClass, string AttackerBuild, string DefenderClass, string DefenderBuild,
    double ExpectedDps, double NoHealingTimeToKillSeconds);

public sealed class Budgets(Rules rules)
{
    private Design D => rules.Design;

    public MasterBonuses Master(int masterLevel, Dictionary<string, int> allocation)
    {
        if (masterLevel < 0 || masterLevel > D.MasterCap || allocation.Values.Sum() > masterLevel
            || allocation.Any(pair => !D.Progression.MasterAllocation.TryGetValue(pair.Key, out var limit)
                || pair.Value < 0 || pair.Value > limit))
            throw new ArgumentException("Invalid master point allocation.");
        double Fraction(string branch) => allocation.GetValueOrDefault(branch) / (double)D.Progression.MasterAllocation[branch];
        return new MasterBonuses(D.Growth.MasterDamageCap * Fraction("offense"),
            D.Growth.MasterHealthCap * Fraction("defense"), D.Growth.MasterArmorCap * Fraction("defense"),
            D.Growth.MasterManaCap * Fraction("utility"), Fraction("specialization"));
    }

    public GearBudget Equipment(double rank, string classId, double qualityFactor)
    {
        if (rank < 1 || rank > 470 || qualityFactor < 0.5 || qualityFactor > 1.55
            || !double.IsFinite(rank) || !double.IsFinite(qualityFactor) || !D.Classes.Any(c => c.Id == classId))
            throw new ArgumentException("Invalid equipment budget.");
        var budget = D.Growth.BasePower + D.Growth.PowerPerRank * rank;
        var weapon = budget * D.Growth.GearPowerShare * qualityFactor;
        var armor = budget * D.Growth.GearArmorShare * qualityFactor;
        var weights = new Dictionary<string, double>
        {
            ["helm"] = 0.18, ["chest"] = 0.30, ["pants"] = 0.22, ["gloves"] = 0.15, ["boots"] = 0.15,
        };
        if (classId == "MG") weights.Remove("helm");
        if (classId == "RF") weights.Remove("gloves");
        var sum = weights.Values.Sum();
        return new GearBudget(weapon, armor, weights.ToDictionary(p => p.Key, p => armor * p.Value / sum));
    }

    public ArenaStats Arena(string classId, string buildId)
    {
        var gear = D.Gear.Where(g => g.Factor <= D.Pvp.GearFactorCap).OrderByDescending(g => g.Factor).First();
        var player = rules.Player(classId, buildId, D.Pvp.MatchLevel, D.Pvp.MatchMasterLevel, gear.Id);
        var weight = D.Pvp.AllocatedHealthWeight;
        player = player with { Health = D.Pvp.ReferenceHealth * (1 - weight) + player.Health * weight };
        return new ArenaStats(player, player.Health * D.Pvp.ShieldToHealthRatio);
    }

    public PvpMatch Duel(ArenaStats attacker, ArenaStats defender)
    {
        var a = attacker.Player;
        var b = defender.Player;
        var skill = rules.SkillFor(a, false);
        var special = 1 + a.Crit * (D.Combat.CritMultiplier - 1)
            + a.Excellent * (D.Combat.ExcellentMultiplier - 1);
        var dps = a.Power * skill.Coefficient * rules.HitChance(a.Accuracy, b.Evasion)
            * (1 - rules.Reduction(b.Armor, a.Rank, b.Reduction)) * special
            / rules.ActionSeconds(a, skill) * D.Pvp.DamageMultiplier;
        // With a shield ratio of 0.6 and a shield damage share of 0.7, shield is exhausted before HP.
        var ttl = (b.Health + defender.Shield) / dps;
        return new PvpMatch(a.ClassId, a.BuildId, b.ClassId, b.BuildId, dps, ttl);
    }
}
