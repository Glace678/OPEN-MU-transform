namespace OpenMu.Balance;

public sealed record EncounterResult(bool Won, double Seconds, int Kills, double RemainingHealth,
    double RemainingMana, int HealthPotions, int ManaPotions, long PotionZen, int BasicFallbackCasts,
    double RecoverySeconds, double DamageTaken);
public sealed record FarmResult(double KillsPerHour, double XpPerHour, double GrossZenPerHour,
    double PotionZenPerHour, double RepairZenPerHour, double TravelZenPerHour, double CraftBudgetPerHour,
    double NetZenPerHour, double CombatFraction, double RecoveryFraction);
public sealed record ProgressionResult(string ClassId, string BuildId, string GearId, string ProfileId,
    double? NormalHours, double? MasterHours, double MaxNormalLevelMinutes, double MaxMasterLevelMinutes,
    int? StalledNormalLevel, int? StalledMasterLevel);

public sealed class Simulation(Rules rules)
{
    public Rules Rules { get; } = rules;
    private Design D => Rules.Design;

    public EncounterResult Fight(PlayerStats player, MonsterStats target, int count = 1, int? seed = null,
        double? dodgeChance = null, bool potions = true, double maxSeconds = 600)
    {
        if (count < 1 || count > 30 || maxSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        var dodge = dodgeChance ?? D.Combat.TrainedDodgeChance;
        if (dodge < 0 || dodge > 1 || !double.IsFinite(dodge)) throw new ArgumentOutOfRangeException(nameof(dodgeChance));
        var random = seed.HasValue ? new Random(seed.Value) : null;
        var enemyHealth = Enumerable.Repeat(target.Health, count).ToArray();
        var enemyNext = Enumerable.Range(0, count).Select(i => target.AttackSeconds + i * 0.12).ToArray();
        var hp = player.Health;
        var mana = player.Mana;
        var healthPotion = Rules.Potion(player, "health");
        var manaPotion = Rules.Potion(player, "mana");
        double time = 0, nextCast = Rules.ActionSeconds(player, Rules.SkillFor(player, count > 1));
        double nextTelegraph = D.Combat.BossTelegraphSeconds, nextHealthPotion = 0, nextManaPotion = 0;
        double totalTaken = 0;
        int healthPots = 0, manaPots = 0, fallback = 0;
        while (time < maxSeconds && hp > 0 && enemyHealth.Any(x => x > 0))
        {
            var alive = Enumerable.Range(0, count).Where(i => enemyHealth[i] > 0).ToArray();
            var nextEnemy = alive.Min(i => enemyNext[i]);
            var next = Math.Min(nextCast, nextEnemy);
            if (target.Telegraph) next = Math.Min(next, nextTelegraph);
            next = Math.Min(next, maxSeconds);
            var elapsed = next - time;
            hp = Math.Min(player.Health, hp + elapsed * D.Combat.InCombatRegen * player.Health);
            mana = Math.Min(player.Mana, mana + elapsed * D.Combat.ManaRegen * player.Mana);
            time = next;
            if (target.Telegraph && time + 1e-8 >= nextTelegraph)
            {
                var incoming = Rules.ReceiveDamage(player, target, D.Combat.BossTelegraphMultiplier, random, true);
                incoming *= random is null ? 1 - dodge : random.NextDouble() < dodge ? 0 : 1;
                hp -= incoming;
                totalTaken += incoming;
                nextTelegraph += D.Combat.BossTelegraphSeconds;
            }
            foreach (var i in alive.Where(i => time + 1e-8 >= enemyNext[i]))
            {
                var incoming = Rules.ReceiveDamage(player, target, random: random);
                hp -= incoming;
                totalTaken += incoming;
                enemyNext[i] += target.AttackSeconds;
            }
            if (hp <= 0) break;
            if (potions && hp < player.Health * 0.70 && time >= nextHealthPotion)
            {
                hp = Math.Min(player.Health, hp + Math.Min(healthPotion.Cap, player.Health * healthPotion.Fraction));
                nextHealthPotion = time + D.Economy.HealthPotionCooldown;
                healthPots++;
            }
            if (potions && mana < player.Mana * 0.30 && time >= nextManaPotion)
            {
                mana = Math.Min(player.Mana, mana + Math.Min(manaPotion.Cap, player.Mana * manaPotion.Fraction));
                nextManaPotion = time + D.Economy.ManaPotionCooldown;
                manaPots++;
            }
            if (time + 1e-8 < nextCast) continue;
            var skill = Rules.SkillFor(player, alive.Length > 1);
            var cost = skill.ManaFlat + skill.ManaFraction * player.Mana;
            if (mana < cost)
            {
                skill = D.Skill("basic");
                cost = 0;
                fallback++;
            }
            mana -= cost;
            var dealt = 0.0;
            for (var order = 0; order < Math.Min(alive.Length, skill.TargetWeights.Length); order++)
            {
                var damage = Rules.CastDamage(player, target, skill, random) * skill.TargetWeights[order];
                dealt += Math.Min(enemyHealth[alive[order]], damage);
                enemyHealth[alive[order]] -= damage;
            }
            var action = Rules.ActionSeconds(player, skill);
            hp = Math.Min(player.Health, hp + Math.Min(player.Health * player.LifeSteal * action, dealt * 0.03));
            nextCast = time + action;
        }
        var won = hp > 0 && enemyHealth.All(x => x <= 0);
        var recovery = Math.Max((player.Health - Math.Max(0, hp)) / (player.Health * D.Combat.OutOfCombatRegen),
            (player.Mana - mana) / (player.Mana * D.Combat.OutOfCombatRegen));
        // Each repeated encounter starts rested, including potion cooldowns; no free reset between packs.
        recovery = Math.Max(recovery, Math.Max(nextHealthPotion, nextManaPotion) - time);
        return new EncounterResult(won, time, enemyHealth.Count(x => x <= 0), Math.Max(0, hp), mana,
            healthPots, manaPots, checked(healthPots * healthPotion.Price + manaPots * manaPotion.Price), fallback,
            recovery, totalTaken);
    }

    public FarmResult Farm(PlayerStats player, double? contentRank = null, int? count = null)
    {
        var target = Rules.Monster(contentRank ?? player.Rank);
        var pack = count ?? (player.Level < 20 ? 1 : D.Combat.PackSize);
        var result = Fight(player, target, pack);
        if (!result.Won) return new FarmResult(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var cycle = result.Seconds + Math.Max(result.RecoverySeconds, D.Combat.TravelSeconds);
        var cycles = 3600 / cycle;
        var kills = cycles * pack;
        var gross = kills * (target.Zen * D.Loot.MoneyChance
            + D.Loot.Equipment.Single(e => e.Id == "common").Chance * D.Economy.SaleValuePerNormalMonsterZen * Rules.BaseZen(target.Rank));
        var potionCost = cycles * result.PotionZen;
        var repairCost = cycles * result.Seconds / 60 * target.Rank * D.Economy.RepairZenPerCombatMinuteRank;
        var travelCost = target.Rank * D.Economy.TravelZenPerRank * 4;
        var craft = gross * D.Economy.PlannedCraftSpendFraction;
        return new FarmResult(kills, kills * target.Experience * Rules.ExperienceMultiplier(player.Rank, target.Rank),
            gross, potionCost, repairCost, travelCost, craft, gross - potionCost - repairCost - travelCost - craft,
            result.Seconds / cycle, result.RecoverySeconds / cycle);
    }

    public ProgressionResult Progression(string classId, string buildId, string gearId = "progression",
        string profileId = "standard")
    {
        var normal = Rules.ExperienceTable(false);
        var master = Rules.ExperienceTable(true);
        var profile = D.Profile(profileId);
        double normalHours = 0, masterHours = 0, maxNormal = 0, maxMaster = 0;
        int? stalledNormal = null, stalledMaster = null;
        for (var level = 1; level < D.NormalCap; level++)
        {
            var farm = Farm(Rules.Player(classId, buildId, level, gearId: gearId));
            if (farm.XpPerHour <= 0) { stalledNormal = level; break; }
            var hours = (normal[level + 1] - normal[level]) / (farm.XpPerHour * profile.NormalXp);
            normalHours += hours;
            maxNormal = Math.Max(maxNormal, hours * 60);
        }
        for (var ml = 0; ml < D.MasterCap; ml++)
        {
            var farm = Farm(Rules.Player(classId, buildId, D.NormalCap, ml, gearId));
            if (farm.XpPerHour <= 0) { stalledMaster = ml; break; }
            var hours = (master[ml + 1] - master[ml]) / (farm.XpPerHour * profile.MasterXp);
            masterHours += hours;
            maxMaster = Math.Max(maxMaster, hours * 60);
        }
        return new ProgressionResult(classId, buildId, gearId, profileId,
            stalledNormal.HasValue ? null : normalHours, stalledMaster.HasValue ? null : masterHours,
            maxNormal, maxMaster, stalledNormal, stalledMaster);
    }
}
