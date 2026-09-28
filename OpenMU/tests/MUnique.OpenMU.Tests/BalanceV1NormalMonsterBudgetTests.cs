// <copyright file="BalanceV1NormalMonsterBudgetTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;
using PersistentIdentity = MUnique.OpenMU.Persistence.IIdentifiable;

/// <summary>Real Season 6 spawn/attribute/combat integration for the opt-in normal budget.</summary>
[TestFixture]
public class BalanceV1NormalMonsterBudgetTests
{
    private static readonly (int Stage, short Skill)[] SevenLineages =
    [
        (0, 12), (6, 43), (8, 52), (12, 55), (16, 61), (20, 214), (24, 262),
    ];

    /// <summary>Checks representative stage caps through the real map spawn and combat pipeline.</summary>
    [Test]
    public async Task FourStageNormalSpawnsKeepTheirOwnCombatAttributesAsync()
    {
        var (provider, configuration) = await CreateSeasonSixAsync().ConfigureAwait(false);
        using (var write = provider.CreateNewContext(configuration))
        {
            new BalanceV1Initializer(write, configuration, BalanceV1.ExperienceProfile.Standard).Initialize();
        }

        var initializer = new MapInitializer(configuration, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var mock = new Mock<IMapInitializer>();
        mock.Setup(value => value.CreateGameMap(It.IsAny<ushort>())).Returns((ushort number) => initializer.CreateGameMap(number));
        mock.Setup(value => value.InitializeStateAsync(It.IsAny<GameMap>())).Returns(ValueTask.CompletedTask);
        await using var context = new GameContext(configuration, provider, mock.Object, NullLoggerFactory.Instance,
            new PlugInManager([], NullLoggerFactory.Instance, null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        initializer.PlugInManager = context.PlugInManager;
        initializer.PathFinderPool = context.PathFinderPool;

        foreach (var (mapNumber, monsterNumber) in new (ushort Map, short Monster)[]
                 { (1, 18), (8, 57), (33, 552), (57, 565) })
        {
            var map = await context.GetMapAsync(mapNumber).ConfigureAwait(false) ?? throw new InvalidOperationException("Missing map.");
            var spawn = map.Definition.MonsterSpawns.First(candidate => candidate.MonsterDefinition!.Number == monsterNumber);
            var sourceHealth = spawn.MonsterDefinition![Stats.MaximumHealth];
            var sourcePhysical = spawn.MonsterDefinition[Stats.MaximumPhysBaseDmg];
            var originalLevel = spawn.MonsterDefinition[Stats.Level];
            var npc = await initializer.InitializeSpawnAsync(0, map, spawn).ConfigureAwait(false);
            Assert.That(npc, Is.InstanceOf<Monster>());
            using var monster = (Monster)npc!;
            var rank = BalanceV1ContentRank.ResolveRank(monster);
            var expectedHealth = Math.Min(sourceHealth, BalanceV1NormalMonsterBudget.GetHealthCap(rank));
            var expectedAttack = Math.Min(sourcePhysical, BalanceV1NormalMonsterBudget.GetPhysicalMaximumCap(rank));
            Assert.Multiple(() =>
            {
                Assert.That(monster.Health, Is.EqualTo((int)expectedHealth).Within(1), $"Initial health on {mapNumber}/{monsterNumber}");
                Assert.That(monster.Attributes[Stats.MaximumHealth], Is.EqualTo(expectedHealth).Within(1));
                Assert.That(monster.Attributes[Stats.MaximumPhysBaseDmg], Is.EqualTo(expectedAttack).Within(0.01));
                Assert.That(monster.Attributes[Stats.Level], Is.EqualTo(originalLevel));
                Assert.That(spawn.MonsterDefinition[Stats.MaximumHealth], Is.EqualTo(sourceHealth));
                Assert.That(spawn.MonsterDefinition[Stats.MaximumPhysBaseDmg], Is.EqualTo(sourcePhysical));
            });

            monster.Health = 1;
            monster.Initialize();
            Assert.That(monster.Health, Is.EqualTo((int)expectedHealth).Within(1), "Respawn must restore the capped instance health.");
            await map.RemoveAsync(monster).ConfigureAwait(false);
        }
    }

    /// <summary>Checks all fixed map identities without mutating any shared Season 6 monster definition.</summary>
    [Test]
    public async Task EveryNormalBindingHasAnInstanceOnlyBudgetAsync()
    {
        var (provider, configuration) = await CreateSeasonSixAsync().ConfigureAwait(false);
        using (var write = provider.CreateNewContext(configuration))
        {
            new BalanceV1Initializer(write, configuration, BalanceV1.ExperienceProfile.Standard).Initialize();
        }

        var initializer = new MapInitializer(configuration, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var mock = new Mock<IMapInitializer>();
        mock.Setup(value => value.CreateGameMap(It.IsAny<ushort>())).Returns((ushort number) => initializer.CreateGameMap(number));
        mock.Setup(value => value.InitializeStateAsync(It.IsAny<GameMap>())).Returns(ValueTask.CompletedTask);
        await using var context = new GameContext(configuration, provider, mock.Object, NullLoggerFactory.Instance,
            new PlugInManager([], NullLoggerFactory.Instance, null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        var adjusted = 0;
        foreach (var mapping in BalanceV1ContentRank.Mappings)
        {
            var mapDefinition = configuration.Maps.Single(map => ((PersistentIdentity)map).Id == mapping.MapDefinitionId);
            var map = await context.GetMapAsync((ushort)mapDefinition.Number).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Missing mapped route.");
            var spawn = mapDefinition.MonsterSpawns.First(candidate => candidate.MonsterDefinition!.Number == mapping.MonsterNumber);
            var source = spawn.MonsterDefinition!;
            var sourceHealth = source[Stats.MaximumHealth];
            var sourceMin = source[Stats.MinimumPhysBaseDmg];
            var sourceMax = source[Stats.MaximumPhysBaseDmg];
            var sourceLevel = source[Stats.Level];
            using var instance = new Monster(spawn, source, map, NullDropGenerator.Instance,
                new Mock<INpcIntelligence>().Object, context.PlugInManager, context.PathFinderPool,
                balanceConfiguration: configuration);
            var enabled = mapping.Rank >= 95 && mapping.MonsterNumber is not (38 or 49 or 77 or 275 or 412 or 459);
            var expectedHealth = enabled ? Math.Min(sourceHealth, BalanceV1NormalMonsterBudget.GetHealthCap(mapping.Rank)) : sourceHealth;
            var attackRatio = enabled ? Math.Min(1, BalanceV1NormalMonsterBudget.GetPhysicalMaximumCap(mapping.Rank) / sourceMax) : 1;
            if (enabled)
            {
                adjusted++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(instance.Attributes[Stats.MaximumHealth], Is.EqualTo(expectedHealth).Within(1), $"HP {mapDefinition.Number}/{source.Number}");
                Assert.That(instance.Attributes[Stats.MaximumPhysBaseDmg], Is.EqualTo(sourceMax * attackRatio).Within(0.01), $"Attack max {mapDefinition.Number}/{source.Number}");
                Assert.That(instance.Attributes[Stats.MinimumPhysBaseDmg], Is.EqualTo(sourceMin * attackRatio).Within(0.01), $"Attack min {mapDefinition.Number}/{source.Number}");
                Assert.That(instance.Attributes[Stats.Level], Is.EqualTo(sourceLevel), $"Loot tier {mapDefinition.Number}/{source.Number}");
                Assert.That(source[Stats.MaximumHealth], Is.EqualTo(sourceHealth), $"Shared HP {mapDefinition.Number}/{source.Number}");
                Assert.That(source[Stats.MaximumPhysBaseDmg], Is.EqualTo(sourceMax), $"Shared attack {mapDefinition.Number}/{source.Number}");
            });
        }

        Assert.Multiple(() =>
        {
            Assert.That(BalanceV1ContentRank.Mappings, Has.Count.EqualTo(135));
            Assert.That(adjusted, Is.EqualTo(90));
        });
    }

    /// <summary>Checks fixed encounter stats stay fixed when a higher-level character revisits the same route.</summary>
    [Test]
    public async Task OverlevelledPlayerDoesNotRescaleNormalEncounterOrZenAsync()
    {
        var (provider, configuration) = await CreateSeasonSixAsync().ConfigureAwait(false);
        using (var write = provider.CreateNewContext(configuration))
        {
            new BalanceV1Initializer(write, configuration, BalanceV1.ExperienceProfile.Standard).Initialize();
        }

        var initializer = new MapInitializer(configuration, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var mock = new Mock<IMapInitializer>();
        mock.Setup(value => value.CreateGameMap(It.IsAny<ushort>())).Returns((ushort number) => initializer.CreateGameMap(number));
        mock.Setup(value => value.InitializeStateAsync(It.IsAny<GameMap>())).Returns(ValueTask.CompletedTask);
        await using var context = new GameContext(configuration, provider, mock.Object, NullLoggerFactory.Instance,
            new PlugInManager([], NullLoggerFactory.Instance, null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        var map = await context.GetMapAsync(8).ConfigureAwait(false) ?? throw new InvalidOperationException("Missing Tarkan.");
        var spawn = map.Definition.MonsterSpawns.First(candidate => candidate.MonsterDefinition!.Number == 57);
        using var instance = new Monster(spawn, spawn.MonsterDefinition!, map, NullDropGenerator.Instance,
            new Mock<INpcIntelligence>().Object, context.PlugInManager, context.PathFinderPool,
            balanceConfiguration: configuration);
        var stage = configuration.CharacterClasses.Single(characterClass => characterClass.Number == 0);
        var near = await BalanceV1CombatProbeTests.CreatePlayerAsync(context, provider, configuration, stage, 200).ConfigureAwait(false);
        var over = await BalanceV1CombatProbeTests.CreatePlayerAsync(context, provider, configuration, stage, 400).ConfigureAwait(false);
        try
        {
            near.CurrentMap = map;
            over.CurrentMap = map;
            var nearExp = await near.CalculateExpAfterKillAsync(instance).ConfigureAwait(false);
            var overExp = await over.CalculateExpAfterKillAsync(instance).ConfigureAwait(false);
            Assert.Multiple(() =>
            {
                Assert.That(overExp, Is.LessThan(nearExp * 0.2), "Existing overlevel XP penalty must still discourage low-tier farming.");
                Assert.That(instance.Attributes[Stats.MaximumHealth], Is.EqualTo(2315).Within(1));
                Assert.That(instance.Attributes[Stats.MaximumPhysBaseDmg], Is.EqualTo(BalanceV1NormalMonsterBudget.GetPhysicalMaximumCap(202.14)).Within(0.01));
                Assert.That(BalanceV1.CalculateZen(202.14, 57), Is.EqualTo(BalanceV1.CalculateZen(BalanceV1ContentRank.ResolveRank(instance), 57)));
                Assert.That(instance.Attributes[Stats.Level], Is.EqualTo(80));
            });
        }
        finally
        {
            await near.DisposeAsync().ConfigureAwait(false);
            await over.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Checks that identity, spawn provenance and profile gating do not scale other content.</summary>
    [Test]
    public async Task ExcludedContentAndOriginalProfileRetainTheOriginalStatsAsync()
    {
        var (provider, configuration) = await CreateSeasonSixAsync().ConfigureAwait(false);
        var map = configuration.Maps.Single(definition => definition.Number == 57);
        var spawn = map.MonsterSpawns.First(candidate => candidate.MonsterDefinition!.Number == 565);
        var originalHealth = spawn.MonsterDefinition![Stats.MaximumHealth];
        var originalAttack = spawn.MonsterDefinition[Stats.MaximumPhysBaseDmg];
        var originalInitializer = new MapInitializer(configuration, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var manager = new PlugInManager([], NullLoggerFactory.Instance, null, null);
        var mock = new Mock<IMapInitializer>();
        mock.Setup(value => value.CreateGameMap(It.IsAny<ushort>())).Returns((ushort number) => originalInitializer.CreateGameMap(number));
        mock.Setup(value => value.InitializeStateAsync(It.IsAny<GameMap>())).Returns(ValueTask.CompletedTask);
        await using var context = new GameContext(configuration, provider, mock.Object, NullLoggerFactory.Instance,
            manager, NullDropGenerator.Instance, new ConfigurationChangeMediator());
        originalInitializer.PlugInManager = manager;
        originalInitializer.PathFinderPool = context.PathFinderPool;
        var liveMap = await context.GetMapAsync(57).ConfigureAwait(false) ?? throw new InvalidOperationException("Missing map.");
        var originalNpc = await originalInitializer.InitializeSpawnAsync(0, liveMap, spawn).ConfigureAwait(false);
        using var original = (Monster)originalNpc!;
        Assert.That(original.Health, Is.EqualTo((int)originalHealth));
        Assert.That(original.Attributes[Stats.MaximumPhysBaseDmg], Is.EqualTo(originalAttack));
        await liveMap.RemoveAsync(original).ConfigureAwait(false);

        using (var write = provider.CreateNewContext(configuration))
        {
            new BalanceV1Initializer(write, configuration, BalanceV1.ExperienceProfile.Standard).Initialize();
        }

        var bossMap = await context.GetMapAsync(4).ConfigureAwait(false) ?? throw new InvalidOperationException("Missing boss map.");
        var bossSpawn = bossMap.Definition.MonsterSpawns.First(candidate => candidate.MonsterDefinition!.Number == 38);
        var bossNpc = await originalInitializer.InitializeSpawnAsync(0, bossMap, bossSpawn).ConfigureAwait(false);
        using var boss = (Monster)bossNpc!;
        Assert.That(boss.Health, Is.EqualTo((int)boss.Definition[Stats.MaximumHealth]));
        await bossMap.RemoveAsync(boss).ConfigureAwait(false);

        var unknownDefinition = new MUnique.OpenMU.Persistence.BasicModel.GameMapDefinition
        {
            Id = Guid.NewGuid(), Number = map.Number, ExpMultiplier = 1,
        };
        var unknownMap = new GameMap(unknownDefinition, TimeSpan.FromSeconds(30), 8);
        using var unknown = new Monster(spawn, spawn.MonsterDefinition, unknownMap, NullDropGenerator.Instance,
            new Mock<INpcIntelligence>().Object, manager, context.PathFinderPool, balanceConfiguration: configuration);
        using var eventMonster = new Monster(spawn, spawn.MonsterDefinition, liveMap, NullDropGenerator.Instance,
            new Mock<INpcIntelligence>().Object, manager, context.PathFinderPool,
            new Mock<IEventStateProvider>().Object, configuration);
        var overrideSpawn = new MUnique.OpenMU.Persistence.BasicModel.MonsterSpawnArea
        {
            MonsterDefinition = spawn.MonsterDefinition, GameMap = map, MaximumHealthOverride = 99999,
            SpawnTrigger = SpawnTrigger.Automatic,
        };
        using var overridden = new Monster(overrideSpawn, spawn.MonsterDefinition, liveMap, NullDropGenerator.Instance,
            new Mock<INpcIntelligence>().Object, manager, context.PathFinderPool, balanceConfiguration: configuration);
        Assert.Multiple(() =>
        {
            Assert.That(unknown.Attributes[Stats.MaximumHealth], Is.EqualTo(originalHealth));
            Assert.That(eventMonster.Attributes[Stats.MaximumPhysBaseDmg], Is.EqualTo(originalAttack));
            Assert.That(overridden.Attributes[Stats.MaximumHealth], Is.EqualTo(originalHealth));
        });
    }

    /// <summary>Samples actual attack and defense paths with legal common gear, plus an explicit +9 sensitivity case.</summary>
    [Test]
    public async Task SevenLineagesReachRealCombatAndEnhancedGearChangesDamageAsync()
    {
        var (provider, configuration) = await CreateSeasonSixAsync().ConfigureAwait(false);
        using (var write = provider.CreateNewContext(configuration))
        {
            new BalanceV1Initializer(write, configuration, BalanceV1.ExperienceProfile.Standard).Initialize();
        }

        var initializer = new MapInitializer(configuration, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var mock = new Mock<IMapInitializer>();
        mock.Setup(value => value.CreateGameMap(It.IsAny<ushort>())).Returns((ushort number) => initializer.CreateGameMap(number));
        mock.Setup(value => value.InitializeStateAsync(It.IsAny<GameMap>())).Returns(ValueTask.CompletedTask);
        await using var context = new GameContext(configuration, provider, mock.Object, NullLoggerFactory.Instance,
            new PlugInManager([], NullLoggerFactory.Instance, null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        initializer.PlugInManager = context.PlugInManager;
        initializer.PathFinderPool = context.PathFinderPool;
        var map = await context.GetMapAsync(57).ConfigureAwait(false) ?? throw new InvalidOperationException("Missing map.");
        var spawn = map.Definition.MonsterSpawns.First(candidate => candidate.MonsterDefinition!.Number == 565);
        var actual = await initializer.InitializeSpawnAsync(0, map, spawn).ConfigureAwait(false);
        Assert.That(actual, Is.InstanceOf<Monster>());
        using var adjusted = (Monster)actual!;
        using var source = new Monster(spawn, spawn.MonsterDefinition!, map, NullDropGenerator.Instance,
            new Mock<INpcIntelligence>().Object, context.PlugInManager, context.PathFinderPool);
        var commonPool = BalanceV1CombatProbeTests.BuildAvailableItems(configuration, 400);
        var rows = new List<object>();
        var gearSensitivity = new List<double>();
        foreach (var (stageNumber, skillNumber) in SevenLineages)
        {
            var stage = configuration.CharacterClasses.Single(characterClass => characterClass.Number == stageNumber);
            var common = await BalanceV1CombatProbeTests.CreatePlayerAsync(context, provider, configuration, stage, 400).ConfigureAwait(false);
            var enhanced = await BalanceV1CombatProbeTests.CreatePlayerAsync(context, provider, configuration, stage, 400).ConfigureAwait(false);
            try
            {
                common.CurrentMap = map;
                enhanced.CurrentMap = map;
                var regularItems = await BalanceV1CombatProbeTests.EquipAsync(common, commonPool, stageNumber is 0 or 20).ConfigureAwait(false);
                var enhancedItems = await BalanceV1CombatProbeTests.EquipAsync(enhanced, commonPool, stageNumber is 0 or 20, 9).ConfigureAwait(false);
                await BalanceV1CombatProbeTests.LearnAsync(common, commonPool).ConfigureAwait(false);
                await BalanceV1CombatProbeTests.LearnAsync(enhanced, commonPool).ConfigureAwait(false);
                var commonSkill = common.SkillList!.Skills.Single(entry => entry.Skill?.Number == skillNumber);
                var enhancedSkill = enhanced.SkillList!.Skills.Single(entry => entry.Skill?.Number == skillNumber);
                var oldIncoming = await SampleMeanAsync(source, common, null).ConfigureAwait(false);
                var adjustedIncoming = await SampleMeanAsync(adjusted, common, null).ConfigureAwait(false);
                var commonPayload = await SampleMeanAsync(common, adjusted, commonSkill).ConfigureAwait(false)
                    * Math.Max(1, (int)commonSkill.Skill!.NumberOfHitsPerAttack);
                var enhancedPayload = await SampleMeanAsync(enhanced, adjusted, enhancedSkill).ConfigureAwait(false)
                    * Math.Max(1, (int)enhancedSkill.Skill!.NumberOfHitsPerAttack);
                var timeToKill = adjusted.Health / commonPayload;
                gearSensitivity.Add((enhancedPayload / commonPayload) - 1);
                rows.Add(new { Stage = stageNumber, Skill = skillNumber, CommonItems = regularItems, EnhancedItems = enhancedItems,
                    SourceHealth = source.Attributes[Stats.MaximumHealth], AdjustedHealth = adjusted.Health,
                    OldIncoming = oldIncoming, AdjustedIncoming = adjustedIncoming,
                    CommonPayload = commonPayload, EnhancedPayload = enhancedPayload, CommonTtkAtOneActionSecond = timeToKill,
                    NoPotionSurvivalAtSourceCadence = common.Attributes![Stats.MaximumHealth] * spawn.MonsterDefinition!.AttackDelay.TotalSeconds / adjustedIncoming });
                Assert.Multiple(() =>
                {
                    Assert.That(adjustedIncoming, Is.LessThan(oldIncoming * 0.15), $"Incoming damage stage {stageNumber}");
                    Assert.That(commonPayload, Is.GreaterThan(0), $"Common gear stage {stageNumber}");
                    Assert.That(enhancedItems.Count, Is.GreaterThanOrEqualTo(3), $"Legal +9 sensitivity gear stage {stageNumber}");
                    Assert.That(enhancedPayload, Is.GreaterThan(0), $"Legal +9 loadout stage {stageNumber}");
                    Assert.That(timeToKill, Is.InRange(4.0, 25.0), $"Virtual one-action-second sensitivity stage {stageNumber}");
                });

                common.Attributes![Stats.Level] = 399;
                Assert.That(adjusted.Attributes[Stats.MaximumHealth], Is.EqualTo(adjusted.Health).Within(1),
                    "An overlevelled player must not rescale fixed normal content.");
            }
            finally
            {
                await common.DisposeAsync().ConfigureAwait(false);
                await enhanced.DisposeAsync().ConfigureAwait(false);
            }
        }

        var output = Environment.GetEnvironmentVariable("OPENMU_MONSTER_GEAR_REPORT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
        }

        await map.RemoveAsync(adjusted).ConfigureAwait(false);
        Assert.That(rows, Has.Count.EqualTo(7));
        Assert.That(gearSensitivity.Any(sensitivity => Math.Abs(sensitivity) > 0.02), Is.True,
            "Legal enhanced gear must expose an actual damage sensitivity, which need not be monotonic across different loadouts.");
    }

    private static async ValueTask<double> SampleMeanAsync(IAttacker attacker, IAttackable defender, SkillEntry? skill)
    {
        const int samples = 256;
        double sum = 0;
        for (var sample = 0; sample < samples; sample++)
        {
            var hit = await attacker.CalculateDamageAsync(defender, skill, false).ConfigureAwait(false);
            sum += hit.HealthDamage + hit.ShieldDamage;
        }

        return sum / samples;
    }

    private static async Task<(InMemoryPersistenceContextProvider Provider, GameConfiguration Configuration)> CreateSeasonSixAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var read = provider.CreateNewConfigurationContext();
        var configuration = (await read.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        configuration.RecoveryInterval = int.MaxValue;
        return (provider, configuration);
    }
}
