// <copyright file="BalanceV1CombatProbeTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;
using MUnique.OpenMU.GameLogic.PlayerActions.Skills;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;
using PersistentIdentity = MUnique.OpenMU.Persistence.IIdentifiable;

/// <summary>An explicit database-free probe, not real-time/client combat acceptance.</summary>
[TestFixture]
public class BalanceV1CombatProbeTests
{
    private static readonly int[] Levels = [1, 11, 50, 51, 100, 200, 300, 400];
    private static readonly short[] Bosses = [38, 49, 77, 275, 412, 459];
    private static readonly short[] Elites = [43, 44, 78, 79, 80, 81, 82, 83];
    private static readonly AttributeDefinition[] AllocatedAttributes =
        [Stats.BaseStrength, Stats.BaseAgility, Stats.BaseVitality, Stats.BaseEnergy, Stats.BaseLeadership];

    /// <summary>Inventories the actual map spawns before choosing an instance-only monster budget.</summary>
    [Test]
    [Explicit("Opt-in full Season 6 spawn inventory; no production values are changed.")]
    public async Task InventoryNormalRouteSpawnsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var read = provider.CreateNewConfigurationContext();
        var configuration = (await read.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var rows = (from mapping in BalanceV1ContentRank.Mappings
                    let map = configuration.Maps.Single(candidate => ((PersistentIdentity)candidate).Id == mapping.MapDefinitionId)
                    from spawn in map.MonsterSpawns.Where(candidate => candidate.MonsterDefinition?.Number == mapping.MonsterNumber)
                    let definition = spawn.MonsterDefinition!
                    select new
                    {
                        Map = map.Number,
                        mapping.MapDefinitionId,
                        mapping.MonsterNumber,
                        mapping.Rank,
                        Boss = Bosses.Contains(mapping.MonsterNumber),
                        Trigger = spawn.SpawnTrigger.ToString(),
                        spawn.MaximumHealthOverride,
                        spawn.Quantity,
                        RawLevel = definition[Stats.Level],
                        Hp = definition[Stats.MaximumHealth],
                        PhysMin = definition[Stats.MinimumPhysBaseDmg],
                        PhysMax = definition[Stats.MaximumPhysBaseDmg],
                        AttackSeconds = definition.AttackDelay.TotalSeconds,
                        AttackSkill = definition.AttackSkill?.Number,
                    }).OrderBy(row => row.Rank).ThenBy(row => row.Map).ThenBy(row => row.MonsterNumber).ToArray();
        Assert.That(rows.Length, Is.GreaterThanOrEqualTo(BalanceV1ContentRank.Mappings.Count));
        var output = Environment.GetEnvironmentVariable("OPENMU_MONSTER_CATALOG_REPORT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
        }

        TestContext.Progress.WriteLine($"Inventoried {rows.Length} spawns for {BalanceV1ContentRank.Mappings.Count} mapped bindings.");
    }

    /// <summary>Measures the original live attribute boundary; installs no combat numbers.</summary>
    [Test]
    [Explicit("Opt-in combat calibration report; no production values are installed.")]
    public async Task MeasureSeasonSixCombatBudgetsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var read = provider.CreateNewConfigurationContext();
        var configuration = (await read.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        configuration.RecoveryInterval = int.MaxValue;
        using (var write = provider.CreateNewContext(configuration))
        {
            new BalanceV1Initializer(write, configuration, BalanceV1.ExperienceProfile.Standard).Initialize();
        }

        var factory = new MapInitializer(configuration, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var initializer = new Mock<IMapInitializer>();
        initializer.Setup(value => value.CreateGameMap(It.IsAny<ushort>())).Returns((ushort number) => factory.CreateGameMap(number));
        initializer.Setup(value => value.InitializeStateAsync(It.IsAny<GameMap>())).Returns(ValueTask.CompletedTask);
        await using var context = new GameContext(configuration, provider, initializer.Object, NullLoggerFactory.Instance,
            new PlugInManager([], NullLoggerFactory.Instance, null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        var actors = new List<object>();
        var encounterRows = new List<object>();
        var stages = configuration.CharacterClasses.OrderBy(stage => stage.Number).ToArray();
        Assert.That(stages, Has.Length.EqualTo(18));
        foreach (var level in Levels)
        {
            var gearPool = BuildAvailableItems(configuration, level);
            var normal = SelectMapped(configuration, level, false);
            var boss = SelectMapped(configuration, level, true);
            var eliteDefinition = configuration.Monsters.Where(definition => Elites.Contains(definition.Number))
                .OrderBy(definition => Math.Abs(definition[Stats.Level] - level)).First();
            var eliteSpawn = new Persistence.BasicModel.MonsterSpawnArea
            {
                MonsterDefinition = eliteDefinition, GameMap = normal.Map, X1 = normal.Spawn.X1, Y1 = normal.Spawn.Y1,
                X2 = normal.Spawn.X2, Y2 = normal.Spawn.Y2, Quantity = 1,
            };
            var encounters = new[]
            {
                (Kind: "mapped-normal", normal.Map, normal.Spawn),
                (Kind: "mapped-boss-rank-gap", boss.Map, boss.Spawn),
                (Kind: "unbound-elite-definition-synthetic-placement", normal.Map, Spawn: (MonsterSpawnArea)eliteSpawn),
            };
            foreach (var stage in stages)
            {
                var player = await CreatePlayerAsync(context, provider, configuration, stage, level).ConfigureAwait(false);
                try
                {
                    var lineage = GetLineage(stage.Number);
                    var equipped = await EquipAsync(player, gearPool, lineage.Magic).ConfigureAwait(false);
                    var learned = await LearnAsync(player, gearPool).ConfigureAwait(false);
                    var regeneration = await MeasureRegenerationAsync(player).ConfigureAwait(false);
                    var potions = await MeasurePotionsAsync(player, configuration, level).ConfigureAwait(false);
                    FillResources(player);
                    var skills = player.SkillList!.Skills.Where(entry => entry.Skill!.MasterDefinition is null
                        && entry.Skill.SkillType is SkillType.DirectHit or SkillType.AreaSkillAutomaticHits or SkillType.AreaSkillExplicitTarget or SkillType.AreaSkillExplicitHits
                        && entry.Skill.DamageType is DamageType.Physical or DamageType.Wizardry or DamageType.Curse
                        && entry.Skill.Number != 47 // Impale's mount is outside this on-foot loadout.
                        && entry.Skill.Requirements.All(requirement => player.Attributes![requirement.Attribute] >= requirement.MinimumValue)).ToArray();
                    var preferred = lineage.Preferred.Select(number => skills.FirstOrDefault(entry => entry.Skill!.Number == number)).FirstOrDefault(entry => entry is not null)
                        ?? skills.Where(entry => entry.Skill!.SkillType == SkillType.DirectHit).OrderByDescending(entry => entry.Skill!.AttackDamage).FirstOrDefault();
                    var costs = new List<object>();
                    foreach (var entry in skills)
                    {
                        FillResources(player);
                        var before = Resources(player);
                        var success = await player.TryConsumeForSkillAsync(entry).ConfigureAwait(false);
                        var after = Resources(player);
                        costs.Add(new { Number = entry.Skill!.Number, Name = entry.Skill.Name.ToString(), Type = entry.Skill.SkillType.ToString(),
                            entry.Skill.NumberOfHitsPerAttack, success, Mana = before.Mana - after.Mana, Ability = before.Ability - after.Ability,
                            OriginalMana = entry.Skill.ConsumeRequirements.Where(requirement => requirement.Attribute == Stats.CurrentMana).Sum(requirement => requirement.MinimumValue),
                            OriginalAbility = entry.Skill.ConsumeRequirements.Where(requirement => requirement.Attribute == Stats.CurrentAbility).Sum(requirement => requirement.MinimumValue),
                            ResourceCategory = ResourceCategory(entry.Skill.Number),
                            Health = before.Health - after.Health, Shield = before.Shield - after.Shield });
                    }

                    actors.Add(new { Class = stage.Number, Name = stage.Name.ToString(), Level = level, StageLegality = StageLegality(stage, level),
                        Allocation = lineage.Weights, Attributes = ActorAttributes(player), Equipped = equipped, Learned = learned,
                        SelectedSkill = preferred?.Skill?.Number, Costs = costs, Regeneration = regeneration, Potions = potions });
                    var playerEncounters = encounters.ToList();
                    if (level <= 51)
                    {
                        var homeId = ((PersistentIdentity)stage.HomeMap!).Id;
                        var homeMapping = BalanceV1ContentRank.Mappings.Where(mapping => mapping.MapDefinitionId == homeId && !Bosses.Contains(mapping.MonsterNumber))
                            .OrderBy(mapping => Math.Abs(mapping.Rank - level)).First();
                        var homeSpawn = stage.HomeMap!.MonsterSpawns.First(spawn => spawn.MonsterDefinition!.Number == homeMapping.MonsterNumber);
                        playerEncounters.Add(("starting-home-route-nearest", stage.HomeMap, homeSpawn));
                    }

                    foreach (var encounter in playerEncounters)
                    {
                        var map = await context.GetMapAsync((ushort)encounter.Map.Number).ConfigureAwait(false)
                            ?? throw new InvalidOperationException("Missing probe map.");
                        using var monster = new Monster(encounter.Spawn, encounter.Spawn.MonsterDefinition!, map, NullDropGenerator.Instance,
                            new Mock<INpcIntelligence>().Object, context.PlugInManager, context.PathFinderPool, balanceConfiguration: configuration);
                        player.CurrentMap = map;
                        FillResources(player);
                        var basic = await SampleDamageAsync(player, monster, null).ConfigureAwait(false);
                        var incoming = await SampleDamageAsync(monster, player, null).ConfigureAwait(false);
                        var skill = preferred is null ? null : await SampleDamageAsync(player, monster, preferred).ConfigureAwait(false);
                        var verifiedDirect = preferred?.Skill?.SkillType == SkillType.DirectHit;
                        var action = preferred is null ? basic.Mean : skill!.Mean * Math.Max(1, (int)preferred.Skill!.NumberOfHitsPerAttack);
                        var hp = monster.Attributes[Stats.MaximumHealth];
                        var enemyInterval = monster.Definition.AttackDelay.TotalSeconds;
                        var hpPotion = potions.Where(potion => potion.Group == "Health").MaxBy(potion => potion.Recovered);
                        var mpPotion = potions.Where(potion => potion.Group == "Mana").MaxBy(potion => potion.Recovered);
                        var manaCost = preferred is null ? 0 : preferred.Skill!.ConsumeRequirements.Where(requirement => requirement.Attribute!.Id == Stats.CurrentMana.Id)
                            .Sum(requirement => player.GetRequiredValue(requirement, preferred));
                        encounterRows.Add(new { Class = stage.Number, Level = level, StageLegality = StageLegality(stage, level), encounter.Kind,
                            Map = map.Definition.Number, MapId = ((PersistentIdentity)map.Definition).Id, Monster = monster.Definition.Number,
                            Name = monster.Definition.Designation.ToString(), Rank = BalanceV1ContentRank.ResolveRank(monster), RawLevel = monster.Attributes[Stats.Level],
                            MonsterHp = hp, MonsterDefense = monster.Attributes[Stats.DefensePvm], EnemyInterval = enemyInterval,
                            Basic = basic, SelectedSkill = preferred?.Skill?.Number, SkillPerHit = skill, VerifiedDirectAction = verifiedDirect,
                            ActionPolicy = preferred is null || verifiedDirect ? "base/direct payload" : "one successful area-target application only; not full area cast",
                            AreaSettings = preferred?.Skill?.AreaSkillSettings is { } settings ? new { settings.MinimumNumberOfHitsPerTarget,
                                settings.MaximumNumberOfHitsPerTarget, settings.MinimumNumberOfHitsPerAttack, settings.MaximumNumberOfHitsPerAttack,
                                settings.HitChancePerDistanceMultiplier, settings.UseDeferredHits, settings.ProjectileCount } : null,
                            ActionMean = action, TtkAt075Seconds = Divide(hp * 0.75, action), TtkAt100Seconds = Divide(hp, action),
                            TtkAt125Seconds = Divide(hp * 1.25, action), Incoming = incoming,
                            NoPotionSurvivalSeconds = Divide(player.Attributes![Stats.MaximumHealth] * enemyInterval, incoming.Mean),
                            HpPotionPerSecond = hpPotion?.Recovered / 8, ManaPotionPerSecond = mpPotion?.Recovered / 12,
                            HpNetLossPerSecond = incoming.Mean / enemyInterval - (hpPotion?.Recovered / 8 ?? 0) - RegenerationRate(player, Stats.HealthRegeneration),
                            ManaCost = manaCost, ManaNetLossAtOneActionSecond = manaCost - (mpPotion?.Recovered / 12 ?? 0) - RegenerationRate(player, Stats.ManaRegeneration),
                            Sustain = await SustainAsync(player, monster, preferred, configuration, level,
                                new DateTime(2040, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(encounterRows.Count * 3)).ConfigureAwait(false),
                            ResourcesOnlySustain = encounter.Kind == "mapped-normal" ? await SustainAsync(player, monster, preferred, configuration, level,
                                new DateTime(2040, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(encounterRows.Count * 3 + 1), false).ConfigureAwait(false) : null,
                            BurstResourcesOnlySustain = encounter.Kind == "mapped-normal" && player.SkillList.GetSkill(265) is { } burst
                                ? await SustainAsync(player, monster, burst, configuration, level,
                                    new DateTime(2040, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(encounterRows.Count * 3 + 2), false).ConfigureAwait(false) : null });
                    }
                }
                finally
                {
                    await player.DisposeAsync().ConfigureAwait(false);
                }
            }

            TestContext.Progress.WriteLine($"Completed level {level}: {actors.Count} actors, {encounterRows.Count} encounter rows.");
        }

        Assert.That(actors, Has.Count.EqualTo(Levels.Length * 18));
        Assert.That(encounterRows, Has.Count.EqualTo(Levels.Length * 18 * 3 + Levels.Count(level => level <= 51) * 18));
        var report = new { SchemaVersion = 1, SeedPolicy = "Unseeded actual Rand; 512 damage samples per direction; replicate for confidence.",
            Scope = "Full S6 in-memory, original stats; candidate first-build allocation per lineage, real common drop/merchant items.",
            SkillResourcePolicy = "balance-v1 reviewed routine AP only: 43,52,55,262; original MP/capacity/regen/unlocks; 265 remains 100 MP/100 AP burst. RF selection now prioritizes legal 262 instead of misidentifying 265 as Phoenix Shot.",
            CadencePolicy = "Player .75/1/1.25 seconds assumed, not client measured. Enemy original AttackDelay.",
            Limits = "No AI/network/movement/dodging/buff execution/affordability/evolution quest validation. Elite references unbound. Area budgets use one successful target application, not a full cast. Drain Life healing invokes its actual strategy. Other status/area effects omitted.",
            Actors = actors, Encounters = encounterRows };
        var output = Environment.GetEnvironmentVariable("OPENMU_COMBAT_PROBE_REPORT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
        }

        TestContext.Progress.WriteLine($"Measured {actors.Count} actors and {encounterRows.Count} scenarios; report: {output ?? "not requested"}.");
    }

    private static double? Divide(double numerator, double denominator) => denominator > 0 ? numerator / denominator : null;

    private static string ResourceCategory(short skillNumber) => skillNumber switch
    {
        43 or 52 or 55 or 262 => "reviewed-routine-AP",
        265 => "tactical-burst-original-100MP-100AP",
        270 => "weapon-area-original-30MP-zeroAP",
        215 => "chain-burst-original-85MP-zeroAP",
        230 => "area-burst-original-115MP-7AP",
        _ => "original-unmodified-resource-contract",
    };

    private static (GameMapDefinition Map, MonsterSpawnArea Spawn) SelectMapped(GameConfiguration configuration, int level, bool boss)
    {
        var mapping = BalanceV1ContentRank.Mappings.Where(entry => Bosses.Contains(entry.MonsterNumber) == boss)
            .OrderBy(entry => Math.Abs(entry.Rank - level)).ThenBy(entry => entry.MapDefinitionId).ThenBy(entry => entry.MonsterNumber).First();
        var map = configuration.Maps.Single(definition => ((PersistentIdentity)definition).Id == mapping.MapDefinitionId);
        return (map, map.MonsterSpawns.First(spawn => spawn.MonsterDefinition!.Number == mapping.MonsterNumber));
    }

    private static string StageLegality(CharacterClass stage, int level) => stage.IsMasterClass && level < 400
        ? "structural-only: master below 400" : stage.Number is 2 or 6 or 10 or 22 && level < 150
        ? "structural-only: evolved below 150" : stage.Number is 12 or 13 or 16 or 17 or 24 or 25
        ? "advanced-class unlock not verified" : "level-compatible; evolution quest not verified";

    private static (double[] Weights, bool Magic, short[] Preferred) GetLineage(int number) => number switch
    {
        0 or 2 or 3 => ([0.05, 0.15, 0.15, 0.65, 0], true, [12, 3, 4]),
        4 or 6 or 7 => ([0.55, 0.15, 0.25, 0.05, 0], false, [43, 19, 20, 21, 22, 23]),
        8 or 10 or 11 => ([0.10, 0.60, 0.20, 0.10, 0], false, [52, 24]),
        12 or 13 => ([0.50, 0.20, 0.20, 0.10, 0], false, [55, 19, 20, 21, 22, 23]),
        16 or 17 => ([0.25, 0.10, 0.20, 0.10, 0.35], false, [61, 60]),
        20 or 22 or 23 => ([0.05, 0.20, 0.20, 0.55, 0], true, [214, 230, 3, 4]),
        24 or 25 => ([0.45, 0.20, 0.30, 0.05, 0], false, [262, 270, 260, 261]),
        _ => throw new InvalidOperationException($"Unknown class {number}."),
    };

    internal static async Task<Player> CreatePlayerAsync(GameContext context, InMemoryPersistenceContextProvider provider,
        GameConfiguration configuration, CharacterClass stage, int level)
    {
        using var write = provider.CreateNewContext(configuration);
        var character = write.CreateNew<Character>();
        character.Name = "CombatProbe";
        character.CharacterClass = stage;
        character.CurrentMap = stage.HomeMap;
        character.Inventory = write.CreateNew<ItemStorage>();
        var weights = GetLineage(stage.Number).Weights;
        var points = (level - 1) * 5;
        var allocations = weights.Select(weight => (int)(points * weight)).ToArray();
        allocations[Array.IndexOf(weights, weights.Max())] += points - allocations.Sum();
        foreach (var definition in stage.StatAttributes)
        {
            var index = Array.FindIndex(AllocatedAttributes, attribute => attribute.Id == definition.Attribute!.Id);
            var value = definition.Attribute!.Id == Stats.Level.Id ? level : definition.BaseValue + (index >= 0 ? allocations[index] : 0);
            character.Attributes.Add(write.CreateNew<StatAttribute>(definition.Attribute, value));
        }

        character.Experience = context.ExperienceTable[level];
        var player = new ProbePlayer(context) { Account = write.CreateNew<Account>() };
        await player.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection).ConfigureAwait(false);
        await player.SetSelectedCharacterAsync(character).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
        return player;
    }

    internal static List<AvailableItem> BuildAvailableItems(GameConfiguration configuration, int level)
    {
        var result = new List<AvailableItem>();
        var accessible = BalanceV1ContentRank.Mappings.Where(mapping => mapping.Rank <= level).ToArray();
        var maps = accessible.Select(mapping => mapping.MapDefinitionId).ToHashSet();
        foreach (var map in configuration.Maps.Where(map => maps.Contains(((PersistentIdentity)map).Id) || map.Number is 0 or 3 or 51))
        {
            foreach (var merchant in map.MonsterSpawns.Select(spawn => spawn.MonsterDefinition!).Where(monster => monster.MerchantStore is not null).Distinct())
            {
                result.AddRange(merchant.MerchantStore!.Items.Select(item => new AvailableItem(item, $"merchant map={map.Number} npc={merchant.Number}")));
            }
        }

        var tiers = accessible.Select(mapping => (Mapping: mapping, Definition: configuration.Monsters.Single(monster => monster.Number == mapping.MonsterNumber)))
            .GroupBy(entry => entry.Definition[Stats.Level]).Select(group => group.First());
        var index = 0;
        var count = 0;
        var firstCall = true;
        var random = new Mock<IRandomizer>();
        random.Setup(value => value.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns((int min, int max) =>
        {
            if (!firstCall)
            {
                return min;
            }

            firstCall = false;
            count = max;
            return Math.Clamp(index, min, max - 1);
        });
        random.Setup(value => value.NextRandomBool(It.IsAny<int>())).Returns((int chance) => chance == 50);
        random.Setup(value => value.NextRandomBool(It.IsAny<double>())).Returns(false);
        var generator = new ProbeDropGenerator(configuration, random.Object);
        foreach (var tier in tiers)
        {
            index = 0;
            count = 1;
            do
            {
                firstCall = true;
                if (generator.GenerateAtTier((int)tier.Definition[Stats.Level]) is { } item)
                {
                    result.Add(new AvailableItem(item, $"common-drop map={tier.Mapping.MapDefinitionId} monster={tier.Definition.Number} rank={tier.Mapping.Rank} raw={tier.Definition[Stats.Level]}"));
                }

                index++;
            }
            while (index < count);
        }

        return result.Where(entry => entry.Item.Definition!.MaximumSockets == 0 && entry.Item.ItemOptions.Count == 0)
            .DistinctBy(entry => (entry.Item.Definition!.Group, entry.Item.Definition.Number, entry.Item.Level, entry.Item.HasSkill)).ToList();
    }

    internal static async Task<List<object>> EquipAsync(Player player, List<AvailableItem> pool, bool magic, byte targetItemLevel = 0)
    {
        var result = new List<object>();
        for (byte slot = 0; slot <= 6; slot++)
        {
            var candidates = pool.Where(entry => entry.Item.Definition!.ItemSlot?.ItemSlots.Contains(slot) is true
                && player.CompliesRequirements(entry.Item) && !entry.Item.Definition.ConflictsWithEquippedHands(player.Inventory!, slot));
            if (slot == 0)
            {
                candidates = candidates.Where(entry => !entry.Item.Definition!.IsAmmunition && entry.Item.Definition.Group <= 5);
            }
            else if (slot == 1)
            {
                var ranged = player.Inventory!.GetItem(0)?.Definition?.Group == 4;
                candidates = candidates.Where(entry => ranged ? entry.Item.Definition!.IsAmmunition : entry.Item.Definition!.Group == 6);
            }

            AvailableItem? chosen = null;
            Item? item = null;
            foreach (var available in candidates.OrderByDescending(entry => GearScore(entry.Item, slot, magic))
                         .ThenByDescending(entry => entry.Item.HasSkill).ThenBy(entry => entry.Item.Definition!.Number))
            {
                var candidate = CloneItem(player, available.Item);
                if (targetItemLevel > 0)
                {
                    if (candidate.Definition!.MaximumItemLevel < targetItemLevel)
                    {
                        continue;
                    }

                    candidate.Level = targetItemLevel;
                    candidate.Durability = candidate.GetMaximumDurabilityOfOnePiece();
                    if (!player.CompliesRequirements(candidate))
                    {
                        continue;
                    }
                }

                chosen = available;
                item = candidate;
                break;
            }

            if (chosen is null || item is null)
            {
                continue;
            }

            Assert.That(player.CompliesRequirements(item), Is.True);
            Assert.That(await player.Inventory!.AddItemAsync(slot, item).ConfigureAwait(false), Is.True);
            result.Add(new { Slot = slot, Group = item.Definition!.Group, Number = item.Definition.Number, Name = item.Definition.Name.ToString(), item.Level, item.HasSkill, chosen.Source });
        }

        return result;
    }

    private static double GearScore(Item item, byte slot, bool magic)
    {
        var attribute = slot < 2 ? magic ? Stats.StaffRise : Stats.MinimumPhysBaseDmgByWeapon : Stats.DefenseBase;
        return item.Definition!.BasePowerUpAttributes.Where(power => power.TargetAttribute!.Id == attribute.Id).Sum(power => power.BaseValue) + item.Level * 2;
    }

    internal static async Task<List<object>> LearnAsync(Player player, List<AvailableItem> pool)
    {
        var result = new List<object>();
        var handler = new LearnablesConsumeHandlerPlugIn();
        foreach (var available in pool.Where(entry => entry.Item.Definition!.Group == 15 || entry.Item.Definition.Group == 12)
            .Where(entry => entry.Item.Definition!.Skill is { MasterDefinition: null } skill
                && skill.QualifiedCharacters.Contains(player.SelectedCharacter!.CharacterClass!) && player.CompliesRequirements(entry.Item)))
        {
            var item = CloneItem(player, available.Item);
            if (player.SkillList!.ContainsSkill((ushort)item.Definition!.Skill!.Number))
            {
                continue;
            }

            item.Durability = Math.Max(1, item.Durability);
            if (await handler.ConsumeItemAsync(player, item, null, FruitUsage.Undefined).ConfigureAwait(false))
            {
                result.Add(new { Number = item.Definition.Skill.Number, Name = item.Definition.Skill.Name.ToString(), ItemGroup = item.Definition.Group,
                    ItemNumber = item.Definition.Number, available.Source });
            }
        }

        return result;
    }

    private static Item CloneItem(Player player, Item source)
    {
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = source.Definition;
        item.Level = source.Level;
        item.HasSkill = source.HasSkill;
        item.Durability = source.Durability;
        return item;
    }

    internal static void FillResources(Player player)
    {
        foreach (var regeneration in Stats.IntervalRegenerationAttributes)
        {
            player.Attributes![regeneration.CurrentAttribute] = player.Attributes[regeneration.MaximumAttribute];
        }
    }

    private static ResourceValues Resources(Player player) => new(player.Attributes![Stats.CurrentHealth], player.Attributes[Stats.CurrentMana],
        player.Attributes[Stats.CurrentAbility], player.Attributes[Stats.CurrentShield]);

    private static object ActorAttributes(Player player) => new { Health = player.Attributes![Stats.MaximumHealth], Mana = player.Attributes[Stats.MaximumMana],
        Ability = player.Attributes[Stats.MaximumAbility], Shield = player.Attributes[Stats.MaximumShield], Strength = player.Attributes[Stats.TotalStrength],
        Agility = player.Attributes[Stats.TotalAgility], Vitality = player.Attributes[Stats.TotalVitality], Energy = player.Attributes[Stats.TotalEnergy],
        Leadership = player.Attributes[Stats.TotalLeadership], Defense = player.Attributes[Stats.DefensePvm], AttackSpeed = player.Attributes[Stats.AttackSpeed],
        MagicSpeed = player.Attributes[Stats.MagicSpeed], PhysMin = player.Attributes[Stats.MinimumPhysBaseDmg], PhysMax = player.Attributes[Stats.MaximumPhysBaseDmg],
        WizMin = player.Attributes[Stats.MinimumWizBaseDmg], WizMax = player.Attributes[Stats.MaximumWizBaseDmg] };

    internal static double RegenerationRate(Player player, Stats.Regeneration regeneration) =>
        (player.Attributes![regeneration.MaximumAttribute] * player.Attributes[regeneration.RegenerationMultiplier]
            + player.Attributes[regeneration.AbsoluteAttribute]) / regeneration.Interval.TotalSeconds;

    private static async Task<object> MeasureRegenerationAsync(Player player)
    {
        player.Attributes![Stats.IsInSafezone] = 0;
        player.Attributes[Stats.IsResting] = 0;
        await player.RegenerateAsync().ConfigureAwait(false);
        foreach (var regeneration in Stats.IntervalRegenerationAttributes)
        {
            player.Attributes[regeneration.CurrentAttribute] = 0;
        }

        var watch = Stopwatch.StartNew();
        await Task.Delay(30).ConfigureAwait(false);
        await player.RegenerateAsync().ConfigureAwait(false);
        return new { WallClockSeconds = watch.Elapsed.TotalSeconds, ActualDelta = Resources(player), HealthPerSecond = RegenerationRate(player, Stats.HealthRegeneration),
            ManaPerSecond = RegenerationRate(player, Stats.ManaRegeneration), AbilityPerSecond = RegenerationRate(player, Stats.AbilityRegeneration),
            ShieldPolicy = "Original combat enabler/hiatus; no universal shield regeneration assumed." };
    }

    private static async Task<List<PotionMeasurement>> MeasurePotionsAsync(Player player, GameConfiguration configuration, int level)
    {
        var result = new List<PotionMeasurement>();
        foreach (var number in new short[] { 1, 2, 3, 4, 5, 6, 35, 36, 37 })
        {
            if (!BalanceV1.TryGetPotionRule(14, number, out var rule) || rule.UnlockLevel > level)
            {
                continue;
            }

            var item = player.PersistenceContext.CreateNew<Item>();
            item.Definition = configuration.Items.Single(definition => definition.Group == 14 && definition.Number == number);
            item.Durability = 100;
            RecoverConsumeHandlerPlugIn handler = rule.CooldownGroup switch
            {
                BalanceV1.PotionGroup.Health => new SmallHealthPotionConsumeHandlerPlugIn(),
                BalanceV1.PotionGroup.Mana => new SmallManaPotionConsumeHandler(),
                _ => new SmallShieldPotionConsumeHandlerPlugIn(),
            };
            var regeneration = rule.CooldownGroup switch
            {
                BalanceV1.PotionGroup.Health => Stats.HealthRegeneration,
                BalanceV1.PotionGroup.Mana => Stats.ManaRegeneration,
                _ => Stats.ShieldRegeneration,
            };
            player.Attributes![regeneration.CurrentAttribute] = 0;
            await handler.RecoverAsync(player, item).ConfigureAwait(false);
            result.Add(new PotionMeasurement(number, rule.CooldownGroup.ToString(), player.Attributes[regeneration.CurrentAttribute], rule.Cooldown.TotalSeconds));
        }

        return result;
    }

    private static async Task<DamageMeasurement> SampleDamageAsync(IAttacker attacker, IAttackable defender, SkillEntry? skill)
    {
        const int count = 512;
        double sum = 0;
        uint minimum = uint.MaxValue;
        uint maximum = 0;
        var misses = 0;
        for (var i = 0; i < count; i++)
        {
            var hit = await attacker.CalculateDamageAsync(defender, skill, false).ConfigureAwait(false);
            var damage = hit.HealthDamage + hit.ShieldDamage;
            sum += damage;
            minimum = Math.Min(minimum, damage);
            maximum = Math.Max(maximum, damage);
            if (damage == 0)
            {
                misses++;
            }
        }

        return new DamageMeasurement(sum / count, minimum, maximum, misses, count);
    }

    private static async Task<object> SustainAsync(Player player, Monster monster, SkillEntry? entry, GameConfiguration configuration, int level, DateTime start, bool includeIncoming = true)
    {
        FillResources(player);
        var healthNumber = level >= 180 ? 3 : level >= 80 ? 2 : 1;
        var manaNumber = level >= 180 ? 6 : level >= 80 ? 5 : 4;
        var hp = player.PersistenceContext.CreateNew<Item>();
        hp.Definition = configuration.Items.Single(item => item.Group == 14 && item.Number == healthNumber);
        var mp = player.PersistenceContext.CreateNew<Item>();
        mp.Definition = configuration.Items.Single(item => item.Group == 14 && item.Number == manaNumber);
        var healthHandler = new SmallHealthPotionConsumeHandlerPlugIn();
        var manaHandler = new SmallManaPotionConsumeHandler();
        var attempts = 0;
        var successes = 0;
        var potionHp = 0;
        var potionMp = 0;
        double damage = 0;
        double nextEnemy = monster.Definition.AttackDelay.TotalSeconds;
        double elapsed = 0;
        Assert.That(nextEnemy, Is.GreaterThan(0), "The probe needs a finite positive enemy cadence.");
        for (var tick = 0; tick < 600 && player.Attributes![Stats.CurrentHealth] > 0; tick++)
        {
            elapsed = tick * 0.1;
            foreach (var regeneration in new[] { Stats.HealthRegeneration, Stats.ManaRegeneration, Stats.AbilityRegeneration })
            {
                player.Attributes![regeneration.CurrentAttribute] = (float)Math.Min(player.Attributes[regeneration.MaximumAttribute],
                    player.Attributes[regeneration.CurrentAttribute] + RegenerationRate(player, regeneration) * 0.1);
            }

            foreach (var resource in new[] { (Stats.HealthRegeneration, BalanceV1.PotionGroup.Health, hp, (RecoverConsumeHandlerPlugIn)healthHandler, 8),
                (Stats.ManaRegeneration, BalanceV1.PotionGroup.Mana, mp, (RecoverConsumeHandlerPlugIn)manaHandler, 12) })
            {
                if (player.Attributes![resource.Item1.CurrentAttribute] < player.Attributes[resource.Item1.MaximumAttribute] * 0.70
                    && player.BalanceV1PotionCooldowns.TryBegin(resource.Item2, TimeSpan.FromSeconds(resource.Item5), start.AddSeconds(elapsed)))
                {
                    await resource.Item4.RecoverAsync(player, resource.Item3).ConfigureAwait(false);
                    Assert.That(player.BalanceV1PotionCooldowns.TryBegin(resource.Item2, TimeSpan.FromSeconds(resource.Item5), start.AddSeconds(elapsed)), Is.False);
                    if (resource.Item2 == BalanceV1.PotionGroup.Health)
                    {
                        potionHp++;
                    }
                    else
                    {
                        potionMp++;
                    }
                }
            }

            if (tick % 10 == 0)
            {
                attempts++;
                if (entry is null || await player.TryConsumeForSkillAsync(entry).ConfigureAwait(false))
                {
                    successes++;
                    for (var hit = 0; hit < (entry is null ? 1 : Math.Max(1, (int)entry.Skill!.NumberOfHitsPerAttack)); hit++)
                    {
                        var payload = await player.CalculateDamageAsync(monster, entry, false).ConfigureAwait(false);
                        damage += payload.HealthDamage + payload.ShieldDamage;
                        if (entry?.Skill?.Number == 214)
                        {
                            await new DrainLifeSkillPlugIn().AfterTargetGotAttackedAsync(player, monster, entry, monster.Position, payload).ConfigureAwait(false);
                        }
                    }
                }
            }

            if (includeIncoming && elapsed >= nextEnemy)
            {
                var hit = await monster.CalculateDamageAsync(player, null, false).ConfigureAwait(false);
                player.Attributes![Stats.CurrentHealth] = Math.Max(0, player.Attributes[Stats.CurrentHealth] - hit.HealthDamage);
                nextEnemy += monster.Definition.AttackDelay.TotalSeconds;
            }
        }

        return new { Policy = "60s virtual fixed-cadence stationary payload budget: real damage/skill consume/potion recovery/DrainLife healing + original regen-rate integration; no AI/death/kill/complete area-cast effects.",
            IncludeIncoming = includeIncoming,
            Elapsed = elapsed, Attempts = attempts, SuccessfulActions = successes, HealthPotions = potionHp, ManaPotions = potionMp,
            TotalOutgoingDamage = damage, End = Resources(player) };
    }

    internal sealed record AvailableItem(Item Item, string Source);
    private sealed record ResourceValues(double Health, double Mana, double Ability, double Shield);
    private sealed record PotionMeasurement(short Number, string Group, double Recovered, double Cooldown);
    private sealed record DamageMeasurement(double Mean, uint Minimum, uint Maximum, int Misses, int Samples);
    private sealed class ProbeDropGenerator(GameConfiguration configuration, IRandomizer randomizer) : DefaultDropGenerator(configuration, randomizer)
    {
        public Item? GenerateAtTier(int level) => this.GenerateRandomItem(level, false);
    }

    private sealed class ProbePlayer(IGameContext context) : Player(context)
    {
        protected override ICustomPlugInContainer<IViewPlugIn> CreateViewPlugInContainer() => new MockViewPlugInContainer();
    }
}
