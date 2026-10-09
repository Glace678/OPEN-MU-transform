using System.Text.Json;

namespace OpenMu.Balance;

public sealed class Checks(Rules rules, Simulation simulation)
{
    public int Passed { get; private set; }
    public List<string> Failures { get; } = [];
    private Design D => rules.Design;

    private void Check(bool condition, string message)
    {
        if (condition) Passed++;
        else Failures.Add(message);
    }
    private void Equal(double value, double expected, string message, double tolerance = 1e-7) =>
        Check(double.IsFinite(value) && Math.Abs(value - expected) <= tolerance, message);
    private void Throws(Action action, string message)
    {
        try { action(); Check(false, message); }
        catch (ArgumentException) { Passed++; }
    }

    public void Run()
    {
        ValidateDesign();
        Check(D.Classes.Length == 7 && D.Classes.Sum(c => c.Builds.Length) == 14, "Seven classes and fourteen builds");
        Check(D.Classes.SelectMany(c => c.EngineIds).Distinct().Count() == 18, "Eighteen unique engine class stages");
        var normal = rules.ExperienceTable(false);
        var master = rules.ExperienceTable(true);
        Equal(normal[1], 0, "Normal level one starts at zero XP");
        Equal(master[0], 0, "Master level zero starts at zero XP");
        foreach (var table in new[] { normal, master })
        {
            var first = ReferenceEquals(table, normal) ? 2 : 1;
            for (var i = first; i < table.Length; i++)
            {
                Check(table[i] > table[i - 1], $"XP must strictly increase at {first}:{i}");
                if (i > first)
                {
                    var step = table[i] - table[i - 1];
                    var previous = table[i - 1] - table[i - 2];
                    Check(step >= previous, $"XP increments must not decline at {first}:{i}");
                }
            }
        }
        var jump = Rules.AddExperience(normal, 1, 0, normal[20] + 7);
        Check(jump.Level == 20 && jump.Cumulative == normal[20] + 7, "Multi-level XP overflow must be retained");
        var cap = Rules.AddExperience(master, 0, 0, long.MaxValue);
        Check(cap.Level == D.MasterCap && cap.Cumulative == master[^1], "Master XP caps safely without overflow");
        Throws(() => Rules.AddExperience(normal, 1, 0, -1), "Negative XP rejected");
        Throws(() => rules.Player("DK", "assault", 399, 1), "Master before normal cap rejected");
        Throws(() => rules.Player("DK", "assault", 400, customAllocation: [1, 0, 0, 0, 1]), "Overspending rejected");
        Throws(() => rules.Monster(double.NaN), "NaN monster rank rejected");
        Throws(() => rules.Monster(470, partySize: 6), "Party size bound");
        foreach (var rank in new[] { 1.0, 80, 180, 400, 470 })
        {
            var armor = Enumerable.Range(0, 100).Select(i => rules.Reduction(i * 100, rank)).ToArray();
            Check(armor.Zip(armor.Skip(1)).All(p => p.First <= p.Second), "Armor mitigation monotone");
            Check(armor.All(a => a >= 0 && a <= D.Combat.ArmorCap), "Armor cap enforced");
            Check(rules.Reduction(10000, rank, 0.99) <= D.Combat.TotalReductionCap, "Total mitigation cap enforced");
            Check(rules.HitChance(0, 100000) >= D.Combat.HitMin, "No three-percent accuracy cliff");
            Check(rules.HitChance(100000, 0) <= D.Combat.HitMax, "Accuracy hard cap");
            Equal(rules.ExperienceMultiplier(rank, rank), 1, "On-rank XP exactly one");
            Equal(rules.Monster(rank).Zen, rules.Monster(rank).Zen, "Zen generation is deterministic");
            var old = rules.Monster(rank).Zen;
            foreach (var profile in D.Profiles)
            {
                Equal(rules.Monster(rank).Zen, old, "Profile XP must not affect Zen");
                Equal(profile.NormalXp, profile.MasterXp, "Both progression phases use the same profile multiplier");
            }
        }
        foreach (var cls in D.Classes)
        foreach (var build in cls.Builds)
        foreach (var level in new[] { 1, 19, 20, 39, 40, 79, 80, 179, 180, 300, 400 })
        foreach (var gear in D.Gear)
        {
            var player = rules.Player(cls.Id, build.Id, level, gearId: gear.Id);
            var tag = $"{cls.Id}/{build.Id}/{level}/{gear.Id}";
            Equal(player.Attributes.Sum(), 80 + (level - 1) * D.PointsPerLevel, $"Exact point budget {tag}");
            Check(player.Health > 0 && player.Mana > 0 && player.Power > 0 && player.Armor > 0, $"Positive stats {tag}");
            var target = rules.Monster(player.Rank);
            var skill = rules.SkillFor(player, true);
            var singleHit = skill with { HitWeights = [1] };
            Equal(rules.CastDamage(player, target, skill), rules.CastDamage(player, target, singleHit),
                $"Multi-hit expected budget {tag}");
            var farm = simulation.Farm(player);
            if (gear.Id == "progression")
            {
                Check(farm.KillsPerHour > 0 && double.IsFinite(farm.XpPerHour), $"Progression farm survival {tag}");
                Check(farm.NetZenPerHour > 0, $"Positive Zen after consumables/fees/planned crafting {tag}");
            }
        }
        foreach (var cls in D.Classes)
        foreach (var build in cls.Builds)
        {
            foreach (var axis in Enumerable.Range(0, cls.Id == "DL" ? 5 : 4))
            {
                var extreme = new double[5];
                extreme[axis] = 1;
                var p = rules.Player(cls.Id, build.Id, 400, 200, customAllocation: extreme);
                Check(p.Attributes.Sum() == 2075 && p.Crit <= D.Combat.CritCap, $"Extreme allocation bounded {cls.Id}/{axis}");
                Check(rules.ActionSeconds(p, D.Skill("basic")) >= D.Combat.MinimumActionSeconds, "Animation speed cap");
            }
        }
        TestLoot();
        TestEconomyAndUpgrades();
        TestPvp();
        TestRewards();
        TestBudgets();
        TestAllLevels();
    }

    private void ValidateDesign()
    {
        void Require(bool value, string message)
        {
            if (!value) throw new InvalidDataException(message);
            Passed++;
        }
        void RequireNotNull(object? value, string name) =>
            Require(value is not null, $"Missing required design section: {name}");
        // B-03: missing/null design sections become structured validation errors
        // instead of a downstream NullReference / IndexOutOfRange on malformed JSON.
        RequireNotNull(D.Combat, "combat");
        RequireNotNull(D.Growth, "growth");
        RequireNotNull(D.Profiles, "profiles");
        RequireNotNull(D.Gear, "gear");
        RequireNotNull(D.Classes, "classes");
        RequireNotNull(D.Skills, "skills");
        Require(D.Classes.Length > 0, "Design must define at least one class");
        Require(D.Skills.Length > 0, "Design must define at least one skill");
        RequireNotNull(D.Monsters, "monsters");
        RequireNotNull(D.Monsters.Ranks, "monsters.ranks");
        RequireNotNull(D.Loot, "loot");
        RequireNotNull(D.Loot.Equipment, "loot.equipment");
        RequireNotNull(D.Economy, "economy");
        RequireNotNull(D.Economy.Potions, "economy.potions");
        RequireNotNull(D.Enhancement, "enhancement");
        RequireNotNull(D.Enhancement.Steps, "enhancement.steps");
        RequireNotNull(D.Progression, "progression");
        RequireNotNull(D.Progression.NormalSeconds, "progression.normalSeconds");
        RequireNotNull(D.Progression.MasterSeconds, "progression.masterSeconds");
        Require(D.NormalCap == 400 && D.MasterCap == 200, "Unsupported S6 level dimensions");
        Require(D.Monsters.Ranks.Select(r => r.Id).Distinct().Count() == 3, "Unique normal/elite/boss definitions");
        foreach (var skill in D.Skills)
        {
            Require(skill.Coefficient > 0 && skill.ActionSeconds > 0 && skill.HitWeights.All(w => w > 0),
                $"Invalid skill: {skill.Id}");
            Require(Math.Abs(skill.HitWeights.Sum() - 1) < 1e-9, $"Whole-cast hit weights: {skill.Id}");
            Require(skill.TargetWeights[0] == 1 && skill.TargetWeights.All(w => w > 0 && w <= 1), "Invalid AoE weights");
        }
        Require(D.Skills.Select(s => s.Id).Distinct().Count() == D.Skills.Length, "Duplicate skill key");
        var engineIds = D.Skills.SelectMany(skill => skill.EngineIds).ToArray();
        Require(engineIds.Distinct().Count() == engineIds.Length, "A runtime skill cannot be merged into two candidate contracts");
        foreach (var skill in D.Skills.Where(skill => skill.RuntimeContract is not null))
        {
            Require(skill.EngineIds.Length == 1, $"Reviewed runtime contract needs one engine ID: {skill.Id}");
            Require(skill.RuntimeContract!.ClassIds.Length > 0 && skill.RuntimeContract.Mana >= 0
                && skill.RuntimeContract.Ability >= 0 && skill.RuntimeContract.PayloadHits > 0,
                $"Invalid original resource contract: {skill.Id}");
        }
        Require(D.Skill("rf_phoenix").EngineIds.SequenceEqual([270])
            && D.Skill("rf_dragon_slasher").EngineIds.SequenceEqual([265]), "Phoenix Shot is not Dragon Slasher");
        Require(D.Skill("su_chain").EngineIds.SequenceEqual([215])
            && D.Skill("su_lightning_shock").EngineIds.SequenceEqual([230]), "Chain Lightning is not Lightning Shock");
        Require(Math.Abs(D.Loot.Equipment.Sum(x => x.Chance) - 1) < 1e-9, "Equipment table must sum to one");
        Require(D.Loot.Equipment.All(e => e.Chance >= 0 && e.Chance <= 1), "Illegal loot probability");
        Require(D.Combat.CritCap + D.Combat.ExcellentCap <= 1, "Mutually exclusive special hit probabilities");
        // Probability and divide-by-zero guards: MoneyChance/JewelChance are compared
        // against a [0,1) roll, SmartClassChance feeds a (1 - c) denominator, and
        // ArmorConstant / OutOfCombatRegen are divisors in the reduction and recovery curves.
        Require(D.Loot.MoneyChance is >= 0 and < 1, "MoneyChance must lie in [0, 1)");
        Require(D.Loot.JewelChance is >= 0 and < 1, "JewelChance must lie in [0, 1)");
        Require(D.Loot.SmartClassChance is > 0 and < 1, "SmartClassChance must lie in (0, 1)");
        Require(D.Combat.ArmorConstant > 0, "ArmorConstant must be positive (reduction denominator)");
        Require(D.Combat.OutOfCombatRegen > 0, "OutOfCombatRegen must be positive (recovery denominator)");
        Require(D.Combat.InCombatRegen >= 0 && D.Combat.ManaRegen >= 0, "Regen rates must be non-negative");
        // Monster ranks must be the three named kinds, not merely three distinct ids.
        Require(D.Monsters.Ranks.Select(r => r.Id).OrderBy(x => x).SequenceEqual(new[] { "boss", "elite", "normal" }),
            "Monster ranks must be normal/elite/boss");
        foreach (var curve in new[] { D.Progression.NormalSeconds, D.Progression.MasterSeconds })
        {
            Require(curve.All(a => a.Length == 2 && a[1] > 0), "Invalid progression anchor");
            Require(curve.Zip(curve.Skip(1)).All(p => p.First[0] < p.Second[0] && p.First[1] <= p.Second[1]),
                "Progression anchors ordered and monotone");
        }
        Require(D.Progression.MasterAllocation.Values.Sum() == D.MasterCap, "Master tree budget must equal two hundred");
        foreach (var step in D.Enhancement.Steps)
            Require(step.Pity > 0 && step.Chance > 0 && step.Chance <= 1 && step.Jewels > 0, "Invalid upgrade step");
        Require(D.Enhancement.Steps.Select(s => s.Level).SequenceEqual(Enumerable.Range(1, D.Enhancement.MaxLevel)),
            "Missing upgrade level");
        Require(D.Economy.Potions.Select(p => (p.Group, p.Number)).Distinct().Count() == 9, "Nine unique potion definitions");
        foreach (var potion in D.Economy.Potions)
            Require(potion.Price > 0 && potion.Fraction > 0 && potion.Fraction < 1 && potion.Cap > 0, "Invalid potion");
    }

    private void TestLoot()
    {
        var state = new LootState();
        for (var i = 1; i <= D.Loot.ExcellentPityKills; i++)
        {
            var result = rules.Drop(400, state, 0, 0.9, 0.9);
            state = result.State;
            Check(result.Equipment == (i == D.Loot.ExcellentPityKills ? "excellent" : "none"), $"Pity exact boundary {i}");
        }
        Equal(state.EligibleKillsWithoutExcellent, 0, "Pity resets after guarantee");
        var rare = rules.Drop(400, new LootState(100), 0.998, 0, 0);
        Check(rare.Equipment == "excellent" && rare.State.EligibleKillsWithoutExcellent == 0, "Natural rare resets pity");
        var low = rules.Drop(1, new LootState(200), 0, 0, 0);
        Equal(low.State.EligibleKillsWithoutExcellent, 200, "Low-level kills cannot farm pity credit");
        var persisted = JsonSerializer.Deserialize<LootState>(JsonSerializer.Serialize(new LootState(D.Loot.ExcellentPityKills - 1)))!;
        Check(rules.Drop(400, persisted, 0, 0, 0).Equipment == "excellent", "Pity survives JSON roundtrip");
        var eq = rules.Drop(400, new LootState(), 0, D.Loot.MoneyChance, D.Loot.JewelChance);
        Check(!eq.Money && !eq.Jewel, "Probability boundary uses less-than, not less-than-or-equal");
        Throws(() => rules.Drop(400, new LootState(), 1, 0, 0), "Roll one rejected");
        var rng = new Random(20260905);
        int money = 0, jewels = 0, excellent = 0;
        state = new LootState();
        const int trials = 100000;
        for (var i = 0; i < trials; i++)
        {
            var result = rules.Drop(400, state, rng.NextDouble(), rng.NextDouble(), rng.NextDouble());
            state = result.State;
            if (result.Money) money++;
            if (result.Jewel) jewels++;
            if (result.Equipment == "excellent") excellent++;
        }
        Check(Math.Abs(money / (double)trials - D.Loot.MoneyChance) < 0.01, "Money empirical distribution");
        Check(Math.Abs(jewels / (double)trials - D.Loot.JewelChance) < 0.004, "Jewel independent distribution");
        Check(excellent > trials * 0.0035, "Pity increases total excellent rate beyond raw table");
    }

    private void TestEconomyAndUpgrades()
    {
        foreach (var step in D.Enhancement.Steps)
        {
            var state = new UpgradeState(step.Level - 1);
            var maxAttempts = step.Chance == 1 ? 1 : step.Pity;
            for (var i = 1; i <= maxAttempts; i++)
            {
                var result = rules.Upgrade(state, 400, 0.999999);
                Check(result.Zen > 0 && result.Jewels == step.Jewels, "Upgrade consumes fee and materials on every attempt");
                if (i < maxAttempts)
                {
                    Check(!result.Succeeded && result.State.Level == state.Level, "Failed upgrade never destroys or downgrades");
                    state = JsonSerializer.Deserialize<UpgradeState>(JsonSerializer.Serialize(result.State))!;
                }
                else Check(result.Succeeded && result.State.Level == step.Level, "Upgrade hard pity and persisted attempts");
            }
            Check(Rules.ExpectedAttempts(step) >= 1 && Rules.ExpectedAttempts(step) <= step.Pity, "Expected upgrade attempts bounded");
        }
        foreach (var potion in D.Economy.Potions)
        {
            for (var count = 1; count <= 100; count++)
                Check(rules.VendorSell(potion.Price, count) < potion.Price * count, "No potion buy-sell arbitrage");
        }
        Throws(() => rules.VendorSell(-1, 1), "Negative vendor input rejected");
        Throws(() => rules.Upgrade(new UpgradeState(15), 400, 0), "Upgrade past maximum rejected");
        foreach (var eventSpec in D.Events)
        {
            var xpRatio = eventSpec.RewardKillEquivalents * D.Combat.ReferenceKillCycleSeconds / (eventSpec.DurationMinutes * 60);
            Check(xpRatio >= 0.9 && xpRatio <= 1.2, "Event total XP budget competitive, not additive to uncapped kill XP");
            Check(eventSpec.Tokens <= D.Loot.EventTokenCapPerDay, "Event tokens bounded");
        }
    }

    private void TestPvp()
    {
        var full = rules.SplitPvpDamage(100, 200);
        Equal(full.HealthDamage, 30, "PvP shield split health");
        Equal(full.ShieldDamage, 70, "PvP shield split shield");
        var depleted = rules.SplitPvpDamage(100, 20);
        Equal(depleted.HealthDamage, 80, "PvP shield spillover");
        Equal(depleted.ShieldDamage, 20, "Cannot consume unavailable shield");
        Equal(depleted.HealthDamage + depleted.ShieldDamage, 100, "PvP damage conserved");
    }

    private void TestRewards()
    {
        var rewards = new Rewards(rules);
        foreach (var cls in D.Classes)
        {
            Check(rewards.EquipmentClass(cls.Id, 0) == cls.Id, "Smart loot own-class branch");
            Check(rewards.EquipmentClass(cls.Id, D.Loot.SmartClassChance) != cls.Id, "Smart loot other-class branch excludes own class");
        }
        var time = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
        var state = new TokenState(DateOnly.FromDateTime(time.UtcDateTime), 0, 0, [], []);
        var first = rewards.Claim(state, "kill-1", "selupan", 2, time, true);
        Check(first.Granted && first.State.Balance == 2, "Boss tokens awarded");
        var duplicate = rewards.Claim(first.State, "kill-1", "selupan", 2, time.AddHours(1), true);
        Check(!duplicate.Granted && duplicate.State.Balance == 2, "Duplicate death/reward cannot mint tokens");
        var cooldown = rewards.Claim(first.State, "kill-2", "selupan", 2, time.AddMinutes(1), true);
        Check(!cooldown.Granted && cooldown.Reason == "boss-cooldown", "Boss token cooldown");
        var newDay = rewards.Claim(first.State, "event-new", "blood_castle", 12, time.AddDays(1), false);
        Check(newDay.Tokens == 12 && newDay.State.Balance == 14, "UTC daily cap resets earned amount, not balance");
        var capped = rewards.Claim(newDay.State, "event-cap", "blood_castle", 8, time.AddDays(1).AddHours(1), false);
        // Earned was 12; a daily cap of 18 leaves six, so an eight-token request
        // is trimmed to exactly the remaining budget and reaches the cap.
        Check(capped.Tokens == D.Loot.EventTokenCapPerDay - 12 && capped.State.EarnedToday == D.Loot.EventTokenCapPerDay,
            "Daily token cap");
        var overCap = rewards.Claim(capped.State, "event-cap-2", "blood_castle", 2, time.AddDays(1).AddHours(2), false);
        Check(overCap.Tokens == 0 && overCap.State.Balance == capped.State.Balance, "No tokens minted past the daily cap");
        var replay = rewards.Claim(capped.State, "event-cap", "blood_castle", 2, time.AddDays(2), false);
        Check(!replay.Granted, "Capped reward IDs cannot be replayed on a later day");
        Check(rewards.Purchase(100, 30, 3).Balance == 10, "Purchase exact debit");
        Check(!rewards.Purchase(100, long.MaxValue, int.MaxValue).Success, "Overflow-sized purchase safely rejected");
        Check(!rewards.Credit(D.Economy.WalletCap - 1, long.MaxValue).Success, "Full-wallet reward deferred without wraparound");
        var persisted = JsonSerializer.Deserialize<TokenState>(JsonSerializer.Serialize(first.State))!;
        Check(!rewards.Claim(persisted, "kill-1", "selupan", 2, time.AddHours(1), true).Granted,
            "Reward deduplication survives persistence");
        foreach (var entry in D.Events)
        {
            var budget = rewards.EventBudget(entry.Id, 400);
            Equal(budget.TotalExperienceBudget, rules.BaseExperience(400) * entry.RewardKillEquivalents, "Fixed whole-event XP budget");
        }
    }

    private void TestAllLevels()
    {
        foreach (var cls in D.Classes)
        foreach (var build in cls.Builds)
        {
            for (var index = 1; index <= D.NormalCap + D.MasterCap; index++)
            {
                var level = Math.Min(index, D.NormalCap);
                var ml = Math.Max(0, index - D.NormalCap);
                var player = rules.Player(cls.Id, build.Id, level, ml);
                var farm = simulation.Farm(player);
                Check(farm.KillsPerHour > 0, $"Full progression survival {cls.Id}/{build.Id}/L{level}/ML{ml}");
                Check(farm.NetZenPerHour > 0, $"Full progression economy {cls.Id}/{build.Id}/L{level}/ML{ml}");
                var normalRoute = D.Routes.Any(r => r.Master == (ml > 0) && r.MinRank <= player.Rank && r.MaxRank >= player.Rank);
                Check(normalRoute, $"Continuous route band coverage {level}/{ml}");
            }
        }
    }

    private void TestBudgets()
    {
        var budgets = new Budgets(rules);
        var all = budgets.Master(200, D.Progression.MasterAllocation);
        Equal(all.Damage, D.Growth.MasterDamageCap, "Master offense cap");
        Equal(all.Health, D.Growth.MasterHealthCap, "Master health cap");
        Throws(() => budgets.Master(1, new Dictionary<string, int> { ["offense"] = 2 }), "Master overspend rejected");
        Throws(() => budgets.Master(200, new Dictionary<string, int> { ["offense"] = 61 }), "Master branch limit");
        foreach (var cls in D.Classes)
        {
            var equipment = budgets.Equipment(400, cls.Id, 1);
            Equal(equipment.ArmorBySlot.Values.Sum(), equipment.TotalArmor, "Class armor slots share one conserved budget");
            if (cls.Id == "MG") Check(!equipment.ArmorBySlot.ContainsKey("helm"), "MG missing helm compensated in remaining slots");
            if (cls.Id == "RF") Check(!equipment.ArmorBySlot.ContainsKey("gloves"), "RF missing gloves compensated in remaining slots");
            foreach (var build in cls.Builds)
            {
                var a = budgets.Arena(cls.Id, build.Id);
                Check(a.Player.Master == 0 && a.Player.Gear.Factor <= D.Pvp.GearFactorCap, "Arena progression normalization");
                var duel = budgets.Duel(a, a);
                Check(duel.NoHealingTimeToKillSeconds >= 8 && duel.NoHealingTimeToKillSeconds <= 30,
                    $"Arena mirror-match no-heal TTK {cls.Id}/{build.Id}");
            }
        }
    }
}
