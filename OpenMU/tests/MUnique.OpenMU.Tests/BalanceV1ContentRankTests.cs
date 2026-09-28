// <copyright file="BalanceV1ContentRankTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;
using PersistentIdentity = MUnique.OpenMU.Persistence.IIdentifiable;

/// <summary>
/// Exercises fixed ranks with complete Season 6 data, real monsters/players and actual reward paths.
/// NPC scheduling is disabled in the fixture; combat attributes and content definitions are unchanged.
/// </summary>
[TestFixture]
public class BalanceV1ContentRankTests
{
    private readonly List<GameContext> _contexts = [];
    private readonly List<Player> _players = [];
    private InMemoryPersistenceContextProvider _provider = null!;
    private GameConfiguration _configuration = null!;

    /// <summary>Initializes only in-memory Season 6 entities, without a database or server.</summary>
    [SetUp]
    public async Task InitializeSeasonSixAsync()
    {
        this._provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(this._provider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var read = this._provider.CreateNewConfigurationContext();
        this._configuration = (await read.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        this._configuration.RecoveryInterval = int.MaxValue;
    }

    /// <summary>Disposes fixture timers and players instead of leaving a running game server.</summary>
    [TearDown]
    public async Task DisposeFixtureAsync()
    {
        foreach (var player in this._players)
        {
            await player.DisposeAsync().ConfigureAwait(false);
        }

        foreach (var context in this._contexts)
        {
            await context.DisposeAsync().ConfigureAwait(false);
        }

        this._players.Clear();
        this._contexts.Clear();
    }

    /// <summary>Verifies every installed binding through actual XP and Zen generation, not catalog counts.</summary>
    [Test]
    public async Task AllNormalBindingsReachActualRewardsAsync()
    {
        var context = this.CreateContext();
        var player = await this.CreatePlayerAsync(context).ConfigureAwait(false);
        var generator = CreateDropGenerator(this._configuration, 0.5);
        Assert.That(BalanceV1ContentRank.Mappings, Has.Count.EqualTo(135));

        foreach (var mapping in BalanceV1ContentRank.Mappings)
        {
            var definition = this._configuration.Maps.Single(map => ((PersistentIdentity)map).Id == mapping.MapDefinitionId);
            using var monster = await CreateMonsterAsync(context, definition.Number, mapping.MonsterNumber).ConfigureAwait(false);
            player.CurrentMap = monster.CurrentMap;
            var originalLevel = monster.Definition[Stats.Level];
            var expectedBase = BalanceV1.CalculateKillExperience(mapping.Rank, mapping.MonsterNumber, 100);
            var reward = await player.CalculateExpAfterKillAsync(monster).ConfigureAwait(false);
            var drops = await generator.GenerateItemDropsAsync(monster.Definition, reward, player).ConfigureAwait(false);

            AssertExperienceRange(player, expectedBase, reward);
            Assert.Multiple(() =>
            {
                Assert.That(BalanceV1ContentRank.ResolveRank(monster), Is.EqualTo(mapping.Rank));
                Assert.That(BalanceV1ContentRank.ResolveRank(monster.Definition, definition), Is.EqualTo(mapping.Rank));
                Assert.That(monster.Attributes[Stats.Level], Is.EqualTo(originalLevel), $"Original level {definition.Number}/{mapping.MonsterNumber}");
                Assert.That(drops.Money, Is.EqualTo(BalanceV1.CalculateZen(mapping.Rank, mapping.MonsterNumber)));
                Assert.That(drops.Items, Is.Empty);
            });
        }
    }

    /// <summary>Verifies the real character receives XP based on the fixed route instead of its raw monster level.</summary>
    /// <param name="mapNumber">The original map display number.</param>
    /// <param name="monsterNumber">An actual normal-route spawn.</param>
    [TestCase((short)0, (short)0)]
    [TestCase((short)1, (short)14)]
    [TestCase((short)33, (short)552)]
    [TestCase((short)57, (short)565)]
    public async Task PlayerReceivesMappedExperienceAsync(short mapNumber, short monsterNumber)
    {
        var context = this.CreateContext();
        var player = await this.CreatePlayerAsync(context).ConfigureAwait(false);
        using var monster = await CreateMonsterAsync(context, mapNumber, monsterNumber).ConfigureAwait(false);
        player.CurrentMap = monster.CurrentMap;
        var rank = BalanceV1ContentRank.ResolveRank(monster);
        var before = player.SelectedCharacter!.Experience;

        var granted = await player.AddExpAfterKillAsync(monster).ConfigureAwait(false);

        AssertExperienceRange(player, BalanceV1.CalculateKillExperience(rank, monsterNumber, 100), granted);
        Assert.That(player.SelectedCharacter.Experience - before, Is.EqualTo(granted));
        Assert.That(rank, Is.Not.EqualTo(monster.Attributes[Stats.Level]));
    }

    /// <summary>Verifies a shared monster definition has different fixed rewards on distinct maps.</summary>
    [Test]
    public async Task SharedMonsterUsesMapIdentityAsync()
    {
        var context = this.CreateContext();
        var player = await this.CreatePlayerAsync(context).ConfigureAwait(false);
        var generator = CreateDropGenerator(this._configuration, 0.5);
        using var lorencia = await CreateMonsterAsync(context, 0, 14).ConfigureAwait(false);
        using var dungeon = await CreateMonsterAsync(context, 1, 14).ConfigureAwait(false);
        Assert.That(lorencia.Definition, Is.SameAs(dungeon.Definition));

        player.CurrentMap = lorencia.CurrentMap;
        var firstXp = await player.CalculateExpAfterKillAsync(lorencia).ConfigureAwait(false);
        var firstDrop = await generator.GenerateItemDropsAsync(lorencia.Definition, firstXp, player).ConfigureAwait(false);
        player.CurrentMap = dungeon.CurrentMap;
        var secondXp = await player.CalculateExpAfterKillAsync(dungeon).ConfigureAwait(false);
        var secondDrop = await generator.GenerateItemDropsAsync(dungeon.Definition, secondXp, player).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(BalanceV1ContentRank.ResolveRank(lorencia), Is.EqualTo(30));
            Assert.That(BalanceV1ContentRank.ResolveRank(dungeon), Is.EqualTo(60));
            Assert.That(secondXp, Is.GreaterThan(firstXp));
            Assert.That(firstDrop.Money, Is.EqualTo(BalanceV1.CalculateZen(30, 14)));
            Assert.That(secondDrop.Money, Is.EqualTo(BalanceV1.CalculateZen(60, 14)));
        });
    }

    /// <summary>Verifies display-number collisions cannot apply a normal route to an unknown map GUID.</summary>
    [Test]
    public async Task UnknownMapKeepsOriginalRankAsync()
    {
        var context = this.CreateContext();
        var player = await this.CreatePlayerAsync(context).ConfigureAwait(false);
        var source = this._configuration.Maps.Single(map => map.Number == 0);
        var spawn = source.MonsterSpawns.First(candidate => candidate.MonsterDefinition!.Number == 0);
        var unknown = new Persistence.BasicModel.GameMapDefinition { Id = Guid.NewGuid(), Number = source.Number, ExpMultiplier = 1 };
        var map = new GameMap(unknown, TimeSpan.FromSeconds(30), 8);
        using var monster = CreateMonster(context, map, spawn);
        player.CurrentMap = map;
        var originalLevel = monster.Attributes[Stats.Level];

        var reward = await player.CalculateExpAfterKillAsync(monster).ConfigureAwait(false);
        var drops = await CreateDropGenerator(this._configuration, 0.5)
            .GenerateItemDropsAsync(monster.Definition, reward, player).ConfigureAwait(false);

        AssertExperienceRange(player, BalanceV1.CalculateKillExperience(originalLevel, 0, 100), reward);
        Assert.Multiple(() =>
        {
            Assert.That(BalanceV1ContentRank.ResolveRank(monster), Is.EqualTo(originalLevel));
            Assert.That(drops.Money, Is.EqualTo(BalanceV1.CalculateZen(originalLevel, 0)));
        });
    }

    /// <summary>Verifies master instances and event bosses have not been silently converted.</summary>
    [Test]
    public async Task MasterDifficultyAndUnknownEventRemainUnmappedAsync()
    {
        var context = this.CreateContext();
        var player = await this.CreatePlayerAsync(context).ConfigureAwait(false);
        using var boss = await CreateMonsterAsync(context, 58, 459).ConfigureAwait(false);
        player.CurrentMap = boss.CurrentMap;
        var reward = await player.CalculateExpAfterKillAsync(boss).ConfigureAwait(false);
        var drops = await CreateDropGenerator(this._configuration, 0.5)
            .GenerateItemDropsAsync(boss.Definition, reward, player).ConfigureAwait(false);

        foreach (var mapping in BalanceV1ContentRank.Mappings)
        {
            Assert.That(BalanceV1ContentRank.TryGetRank(mapping.MapDefinitionId, mapping.MonsterNumber, "master_2", out _), Is.False);
            Assert.That(BalanceV1ContentRank.TryGetRank(mapping.MapDefinitionId, mapping.MonsterNumber, "master_3", out _), Is.False);
        }

        var originalLevel = boss.Attributes[Stats.Level];
        AssertExperienceRange(player, BalanceV1.CalculateKillExperience(originalLevel, 459, 100), reward);
        Assert.Multiple(() =>
        {
            Assert.That(BalanceV1ContentRank.ResolveRank(boss), Is.EqualTo(originalLevel));
            Assert.That(drops.Money, Is.EqualTo(BalanceV1.CalculateZen(originalLevel, 459)));
        });
    }

    /// <summary>Verifies changing the player's level does not change the encounter's rank or Zen.</summary>
    [Test]
    public async Task PlayerLevelDoesNotRescaleEncounterAsync()
    {
        var context = this.CreateContext();
        var player = await this.CreatePlayerAsync(context).ConfigureAwait(false);
        using var monster = await CreateMonsterAsync(context, 57, 565).ConfigureAwait(false);
        player.CurrentMap = monster.CurrentMap;
        var generator = CreateDropGenerator(this._configuration, 0.5);
        var low = await generator.GenerateItemDropsAsync(monster.Definition, 1, player).ConfigureAwait(false);
        player.Attributes![Stats.Level] = 399;
        var high = await generator.GenerateItemDropsAsync(monster.Definition, int.MaxValue, player).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(BalanceV1ContentRank.ResolveRank(monster), Is.EqualTo(400));
            Assert.That(low.Money, Is.EqualTo(high.Money));
            Assert.That(high.Money, Is.EqualTo(BalanceV1.CalculateZen(400, 565)));
            Assert.That(monster.Attributes[Stats.Level], Is.EqualTo(148));
        });
    }

    /// <summary>Verifies the mapped monster rank actually reaches the live armor damage calculation.</summary>
    [Test]
    public async Task MappedRankReachesMonsterCombatAsync()
    {
        var context = this.CreateContext();
        var player = await this.CreatePlayerAsync(context).ConfigureAwait(false);
        using var mapped = await CreateMonsterAsync(context, 57, 565).ConfigureAwait(false);
        player.CurrentMap = mapped.CurrentMap;
        player.Attributes!.AddElement(new SimpleElement(1000, AggregateType.AddRaw), Stats.DefensePvm);
        var defense = (int)((player.Attributes[Stats.DefensePvm] + player.Attributes[Stats.GreaterDefenseBonus])
                            * player.Attributes[Stats.DefenseDecrement]);
        var mappedMultiplier = 1 - BalanceV1.CalculateDamageReduction(defense, 400);
        var originalMultiplier = 1 - BalanceV1.CalculateDamageReduction(defense, mapped.Attributes[Stats.Level]);
        var minimum = (int)(mapped.Attributes[Stats.MinimumPhysBaseDmg] * mappedMultiplier);
        var maximum = (int)(mapped.Attributes[Stats.MaximumPhysBaseDmg] * mappedMultiplier);
        var originalMaximum = (int)(mapped.Attributes[Stats.MaximumPhysBaseDmg] * originalMultiplier);
        Assert.That(minimum, Is.GreaterThan(originalMaximum), "The actual S6 damage ranges must distinguish mapped and original ranks.");

        HitInfo hit = default;
        for (var attempt = 0; attempt < 100 && hit.HealthDamage == 0; attempt++)
        {
            hit = await mapped.CalculateDamageAsync(player, null, false).ConfigureAwait(false);
        }

        Assert.Multiple(() =>
        {
            Assert.That(hit.HealthDamage, Is.InRange((uint)minimum, (uint)maximum));
            Assert.That(hit.ShieldDamage, Is.Zero);
            Assert.That(mapped.Attributes[Stats.Level], Is.EqualTo(148));
        });
    }

    /// <summary>Verifies the adapter does not import any unvalidated HP, damage, AI or level proposal.</summary>
    [Test]
    public void InitializationPreservesEveryMonsterAttribute()
    {
        var original = this._configuration.Monsters.SelectMany(monster => monster.Attributes.Select(
            attribute => (monster.Number, attribute.AttributeDefinition!.Id, attribute.Value))).ToArray();

        _ = this.CreateContext();

        var installed = this._configuration.Monsters.SelectMany(monster => monster.Attributes.Select(
            attribute => (monster.Number, attribute.AttributeDefinition!.Id, attribute.Value))).ToArray();
        Assert.That(installed, Is.EqualTo(original));
        Assert.That(this._configuration.Monsters.Where(monster => monster.ObjectKind == NpcObjectKind.Monster)
            .Max(monster => monster[Stats.Level]), Is.EqualTo(148));
    }

    /// <summary>Verifies high route ranks retain legal common/excellent equipment pools instead of the byte-cache failure.</summary>
    /// <param name="equipmentRoll">A deterministic channel selection.</param>
    [TestCase(0.9)]
    [TestCase(0.997)]
    public async Task EveryHighRankHasLegalEquipmentAsync(double equipmentRoll)
    {
        var context = this.CreateContext();
        var player = await this.CreatePlayerAsync(context).ConfigureAwait(false);
        var generator = CreateDropGenerator(this._configuration, equipmentRoll);
        foreach (var mapping in BalanceV1ContentRank.Mappings.Where(mapping => mapping.Rank >= 300))
        {
            var map = this._configuration.Maps.Single(definition => ((PersistentIdentity)definition).Id == mapping.MapDefinitionId);
            using var monster = await CreateMonsterAsync(context, map.Number, mapping.MonsterNumber).ConfigureAwait(false);
            player.CurrentMap = monster.CurrentMap;
            var drops = await generator.GenerateItemDropsAsync(monster.Definition, 1, player).ConfigureAwait(false);
            var item = drops.Items.Single();
            var excellent = equipmentRoll == 0.997;
            var tier = monster.Definition[Stats.Level] - (excellent ? this._configuration.ExcellentItemDropLevelDelta : 0);
            AssertLegalEquipment(item, tier);
            if (excellent)
            {
                Assert.That(item.ItemOptions.Any(option => object.Equals(option.ItemOption?.OptionType, ItemOptionTypes.Excellent)), Is.True);
            }
        }
    }

    /// <summary>Verifies both endpoint ranks and different rare channels keep real item definitions.</summary>
    /// <param name="mapNumber">The normal-route map.</param>
    /// <param name="monsterNumber">The normal-route monster.</param>
    /// <param name="equipmentRoll">A deterministic channel selection.</param>
    /// <param name="socket">Whether this roll requests socket equipment.</param>
    [TestCase((short)33, (short)552, 0.997, false)]
    [TestCase((short)57, (short)565, 0.997, false)]
    [TestCase((short)57, (short)565, 0.9999, true)]
    public async Task HighRankRareEquipmentUsesOriginalTierAsync(short mapNumber, short monsterNumber, double equipmentRoll, bool socket)
    {
        var context = this.CreateContext();
        var player = await this.CreatePlayerAsync(context).ConfigureAwait(false);
        using var monster = await CreateMonsterAsync(context, mapNumber, monsterNumber).ConfigureAwait(false);
        player.CurrentMap = monster.CurrentMap;
        var drops = await CreateDropGenerator(this._configuration, equipmentRoll)
            .GenerateItemDropsAsync(monster.Definition, 1, player).ConfigureAwait(false);
        var item = drops.Items.Single();
        var itemTier = monster.Definition[Stats.Level] - (socket ? 0 : this._configuration.ExcellentItemDropLevelDelta);

        AssertLegalEquipment(item, itemTier);
        Assert.That(this._configuration.Items, Does.Contain(item.Definition));
        if (socket)
        {
            Assert.That(item.Definition!.MaximumSockets, Is.GreaterThan(0));
        }
        else
        {
            Assert.That(item.ItemOptions.Any(option => object.Equals(option.ItemOption?.OptionType, ItemOptionTypes.Excellent)), Is.True);
        }
    }

    /// <summary>Verifies all supported XP profiles leave mapped Zen unchanged.</summary>
    /// <param name="profile">The explicitly installed rate.</param>
    [TestCase(BalanceV1.ExperienceProfile.Standard)]
    [TestCase(BalanceV1.ExperienceProfile.Relaxed)]
    [TestCase(BalanceV1.ExperienceProfile.Journey)]
    public async Task MappedZenIsIndependentOfExperienceProfileAsync(BalanceV1.ExperienceProfile profile)
    {
        var context = this.CreateContext(profile: profile);
        var player = await this.CreatePlayerAsync(context).ConfigureAwait(false);
        using var monster = await CreateMonsterAsync(context, 57, 565).ConfigureAwait(false);
        player.CurrentMap = monster.CurrentMap;
        var reward = await player.CalculateExpAfterKillAsync(monster).ConfigureAwait(false);
        var generator = CreateDropGenerator(this._configuration, 0.5);
        var first = await generator.GenerateItemDropsAsync(monster.Definition, reward, player).ConfigureAwait(false);
        var second = await generator.GenerateItemDropsAsync(monster.Definition, int.MaxValue, player).ConfigureAwait(false);

        AssertExperienceRange(player, BalanceV1.CalculateKillExperience(400, 565, 100), reward);
        Assert.Multiple(() =>
        {
            Assert.That(first.Money, Is.EqualTo(BalanceV1.CalculateZen(400, 565)));
            Assert.That(second.Money, Is.EqualTo(first.Money));
        });
    }

    /// <summary>Verifies the opt-in adapter cannot alter an original configuration's reward paths.</summary>
    [Test]
    public async Task OriginalProfileKeepsLegacyExperienceAndMoneyAsync()
    {
        var context = this.CreateContext(enabled: false);
        var player = await this.CreatePlayerAsync(context).ConfigureAwait(false);
        using var monster = await CreateMonsterAsync(context, 57, 565).ConfigureAwait(false);
        player.CurrentMap = monster.CurrentMap;
        var legacyBase = monster.CalculateBaseExperience((float)player.Attributes![Stats.TotalLevel]);
        var reward = await player.CalculateExpAfterKillAsync(monster).ConfigureAwait(false);
        AssertExperienceRange(player, legacyBase, reward);
        Assert.That(monster.CalculateBaseExperience(100d, this._configuration), Is.EqualTo(legacyBase));

        // A guaranteed legacy money group isolates the existing gained-XP + 7 contract.
        monster.Definition.DropItemGroups.Clear();
        monster.CurrentMap.Definition.DropItemGroups.Clear();
        player.SelectedCharacter!.DropItemGroups.Clear();
        monster.Definition.NumberOfMaximumItemDrops = 1;
        monster.Definition.DropItemGroups.Add(new Persistence.BasicModel.DropItemGroup { Chance = 1, ItemType = SpecialItemType.Money });
        var generator = CreateDropGenerator(this._configuration, 0.5);
        var low = await generator.GenerateItemDropsAsync(monster.Definition, 10, player).ConfigureAwait(false);
        var high = await generator.GenerateItemDropsAsync(monster.Definition, 1000, player).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(low.Money, Is.EqualTo(17));
            Assert.That(high.Money, Is.EqualTo(1007));
        });
    }

    /// <summary>Verifies summon ownership does not turn a normal binding into farmable XP.</summary>
    [Test]
    public async Task SummonedMappedMonsterCannotAwardExperienceAsync()
    {
        var context = this.CreateContext();
        var player = await this.CreatePlayerAsync(context).ConfigureAwait(false);
        using var monster = await CreateMonsterAsync(context, 0, 0, new SummonedMonsterIntelligence(player)).ConfigureAwait(false);
        var before = player.SelectedCharacter!.Experience;
        var granted = await player.AddExpAfterKillAsync(monster).ConfigureAwait(false);
        Assert.Multiple(() =>
        {
            Assert.That(BalanceV1ContentRank.ResolveRank(monster), Is.EqualTo(monster.Attributes[Stats.Level]));
            Assert.That(granted, Is.Zero);
            Assert.That(player.SelectedCharacter.Experience, Is.EqualTo(before));
        });
    }

    /// <summary>Verifies incomplete bindings fail before any progression edit or profile marker publication.</summary>
    /// <param name="missingMap">Whether to remove the map rather than its required spawn.</param>
    [TestCase(true)]
    [TestCase(false)]
    public void MissingBindingCannotPublishProfile(bool missingMap)
    {
        var map = this._configuration.Maps.Single(definition => definition.Number == 0);
        if (missingMap)
        {
            this._configuration.Maps.Remove(map);
        }
        else
        {
            foreach (var spawn in map.MonsterSpawns.Where(spawn => spawn.MonsterDefinition!.Number == 0).ToArray())
            {
                map.MonsterSpawns.Remove(spawn);
            }
        }

        var originalRate = this._configuration.ExperienceRate;
        using var write = this._provider.CreateNewContext(this._configuration);
        var initializer = new BalanceV1Initializer(write, this._configuration, BalanceV1.ExperienceProfile.Relaxed);
        Assert.That(() => initializer.Initialize(), Throws.TypeOf<InvalidOperationException>().With.Message.Contains("normal content binding"));
        Assert.Multiple(() =>
        {
            Assert.That(BalanceV1.IsEnabled(this._configuration), Is.False);
            Assert.That(this._configuration.ExperienceRate, Is.EqualTo(originalRate));
        });
    }

    /// <summary>Verifies a content rank cannot accidentally replace the required byte-indexed original item tier.</summary>
    [Test]
    public void InvalidOriginalItemTierCannotPublishProfile()
    {
        var monster = this._configuration.Monsters.Single(definition => definition.Number == 565);
        monster.Attributes.Single(attribute => attribute.AttributeDefinition!.Id == Stats.Level.Id).Value = 400;
        using var write = this._provider.CreateNewContext(this._configuration);
        var initializer = new BalanceV1Initializer(write, this._configuration, BalanceV1.ExperienceProfile.Standard);

        Assert.That(() => initializer.Initialize(), Throws.TypeOf<InvalidOperationException>().With.Message.Contains("original item-tier level"));
        Assert.That(BalanceV1.IsEnabled(this._configuration), Is.False);
    }

    /// <summary>Verifies the installed profile keeps checking bindings on subsequent launches.</summary>
    [Test]
    public void InstalledProfileRejectsMissingBinding()
    {
        _ = this.CreateContext();
        this._configuration.Maps.Remove(this._configuration.Maps.Single(map => map.Number == 57));
        Assert.That(() => BalanceV1Initializer.ValidateCoreConfiguration(this._configuration),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("normal content binding"));
    }

    private static void AssertLegalEquipment(Item item, float originalTier)
    {
        Assert.Multiple(() =>
        {
            Assert.That(item.Definition, Is.Not.Null);
            Assert.That(item.Definition!.DropsFromMonsters, Is.True);
            Assert.That(item.Definition.DropLevel, Is.LessThanOrEqualTo(originalTier));
            Assert.That(item.Definition.DropLevel, Is.GreaterThan(originalTier - 12));
            Assert.That(item.Definition.MaximumDropLevel is not { } maximum || originalTier <= maximum, Is.True);
            Assert.That(item.Level, Is.LessThanOrEqualTo(item.Definition.MaximumItemLevel));
            Assert.That(item.Durability, Is.GreaterThanOrEqualTo(0));
        });
    }

    private static void AssertExperienceRange(Player player, double baseExperience, int actual)
    {
        var attributes = player.Attributes!;
        var expected = baseExperience * player.GameContext.ExperienceRate
                       * (attributes[Stats.ExperienceRate] + attributes[Stats.BonusExperienceRate])
                       * (player.CurrentMap?.Definition.ExpMultiplier ?? 1);
        // Keep the original full-S6 random XP multiplier range instead of replacing global attributes.
        var minimum = attributes[Stats.RandomExperienceMinMultiplier];
        var maximum = attributes[Stats.RandomExperienceMaxMultiplier];
        Assert.That(actual, Is.InRange((int)(expected * minimum), (int)(expected * maximum)));
    }

    private static DefaultDropGenerator CreateDropGenerator(GameConfiguration configuration, double roll)
    {
        var random = new Mock<IRandomizer>();
        random.Setup(value => value.NextDouble()).Returns(roll);
        random.Setup(value => value.NextInt(It.IsAny<int>(), It.IsAny<int>())).Returns((int min, int _) => min);
        return new DefaultDropGenerator(configuration, random.Object);
    }

    private static async Task<Monster> CreateMonsterAsync(GameContext context, short mapNumber, short number, INpcIntelligence? intelligence = null)
    {
        var map = await context.GetMapAsync((ushort)mapNumber).ConfigureAwait(false)
                  ?? throw new InvalidOperationException($"Missing map {mapNumber}.");
        var spawn = map.Definition.MonsterSpawns.First(candidate => candidate.MonsterDefinition!.Number == number);
        return CreateMonster(context, map, spawn, intelligence);
    }

    private static Monster CreateMonster(GameContext context, GameMap map, MonsterSpawnArea spawn, INpcIntelligence? intelligence = null) =>
        new(spawn, spawn.MonsterDefinition!, map, NullDropGenerator.Instance, intelligence ?? new Mock<INpcIntelligence>().Object,
            context.PlugInManager, context.PathFinderPool);

    private GameContext CreateContext(bool enabled = true, BalanceV1.ExperienceProfile profile = BalanceV1.ExperienceProfile.Standard)
    {
        if (enabled)
        {
            using var write = this._provider.CreateNewContext(this._configuration);
            new BalanceV1Initializer(write, this._configuration, profile).Initialize();
        }

        var factory = new MapInitializer(this._configuration, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var initializer = new Mock<IMapInitializer>();
        initializer.Setup(value => value.CreateGameMap(It.IsAny<ushort>())).Returns((ushort number) => factory.CreateGameMap(number));
        initializer.Setup(value => value.InitializeStateAsync(It.IsAny<GameMap>())).Returns(ValueTask.CompletedTask);
        var context = new GameContext(this._configuration, this._provider, initializer.Object, NullLoggerFactory.Instance,
            new PlugInManager([], NullLoggerFactory.Instance, null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
        this._contexts.Add(context);
        return context;
    }

    private async Task<Player> CreatePlayerAsync(GameContext context)
    {
        using var write = this._provider.CreateNewContext(this._configuration);
        var character = write.CreateNew<Character>();
        character.Name = "RankTest";
        character.CharacterClass = this._configuration.CharacterClasses.Single(stage => stage.Number == 0);
        character.CurrentMap = character.CharacterClass.HomeMap;
        character.Inventory = write.CreateNew<ItemStorage>();
        foreach (var definition in character.CharacterClass.StatAttributes)
        {
            var value = definition.Attribute!.Id == Stats.Level.Id ? 100 : definition.BaseValue;
            character.Attributes.Add(write.CreateNew<StatAttribute>(definition.Attribute, value));
        }

        character.Experience = context.ExperienceTable[100];
        var player = new TestPlayer(context) { Account = write.CreateNew<Account>() };
        this._players.Add(player);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection).ConfigureAwait(false);
        await player.SetSelectedCharacterAsync(character).ConfigureAwait(false);
        return player;
    }

    private sealed class TestPlayer(IGameContext context) : Player(context)
    {
        protected override ICustomPlugInContainer<IViewPlugIn> CreateViewPlugInContainer() => new MockViewPlugInContainer();
    }
}
