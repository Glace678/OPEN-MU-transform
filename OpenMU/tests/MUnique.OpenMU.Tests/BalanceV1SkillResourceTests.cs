// <copyright file="BalanceV1SkillResourceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;
using MUnique.OpenMU.GameLogic.PlayerActions.Skills;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.PlugIns;
using MUnique.OpenMU.Pathfinding;
using Probe = MUnique.OpenMU.Tests.BalanceV1CombatProbeTests;

/// <summary>Full S6 in-memory skill/resource callers, with no server or database.</summary>
[TestFixture]
public class BalanceV1SkillResourceTests
{
    private InMemoryPersistenceContextProvider _provider = null!;
    private GameConfiguration _configuration = null!;
    private GameContext _context = null!;
    private readonly List<Player> _players = [];

    /// <summary>Loads actual class, item, skill and resource relationships.</summary>
    [SetUp]
    public async Task InitializeAsync()
    {
        this._provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(this._provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var read = this._provider.CreateNewConfigurationContext();
        this._configuration = (await read.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        this._configuration.RecoveryInterval = int.MaxValue;
        using var write = this._provider.CreateNewContext(this._configuration);
        new BalanceV1Initializer(write, this._configuration, BalanceV1.ExperienceProfile.Standard).Initialize();
        var factory = new MapInitializer(this._configuration, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var initializer = new Mock<IMapInitializer>();
        initializer.Setup(value => value.CreateGameMap(It.IsAny<ushort>())).Returns((ushort number) => factory.CreateGameMap(number));
        initializer.Setup(value => value.InitializeStateAsync(It.IsAny<GameMap>())).Returns(ValueTask.CompletedTask);
        this._context = new GameContext(this._configuration, this._provider, initializer.Object, NullLoggerFactory.Instance,
            new PlugInManager([], NullLoggerFactory.Instance, null, null), NullDropGenerator.Instance, new ConfigurationChangeMediator());
    }

    /// <summary>Leaves no background game process or timer behind.</summary>
    [TearDown]
    public async Task DisposeAsync()
    {
        foreach (var player in this._players)
        {
            await player.DisposeAsync().ConfigureAwait(false);
        }

        this._players.Clear();
        await this._context.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Tests all qualified stages at legal levels with actual merchant/common-drop learnables.</summary>
    /// <param name="stage">The actual qualified stage.</param>
    /// <param name="level">The legal normal level.</param>
    /// <param name="number">The reviewed base skill number.</param>
    [TestCase(6, 200, 43)]
    [TestCase(6, 400, 43)]
    [TestCase(7, 400, 43)]
    [TestCase(8, 200, 52)]
    [TestCase(8, 400, 52)]
    [TestCase(10, 200, 52)]
    [TestCase(10, 400, 52)]
    [TestCase(11, 400, 52)]
    [TestCase(12, 200, 55)]
    [TestCase(12, 300, 55)]
    [TestCase(12, 400, 55)]
    [TestCase(13, 400, 55)]
    [TestCase(24, 300, 262)]
    [TestCase(24, 400, 262)]
    [TestCase(25, 400, 262)]
    public async Task LegalRoutineCostsPreserveManaAndSixtySecondBudgetAsync(int stage, int level, int number)
    {
        var player = await this.CreatePlayerAsync(stage, level).ConfigureAwait(false);
        var entry = player.SkillList!.GetSkill((ushort)number);
        Assert.That(entry, Is.Not.Null, "Must be learned from a legal merchant/common-drop item, not granted by the test.");
        var skill = entry!.Skill!;
        var ap = skill.ConsumeRequirements.Single(value => value.Attribute == Stats.CurrentAbility);
        var mana = skill.ConsumeRequirements.Single(value => value.Attribute == Stats.CurrentMana);
        var cap = player.Attributes![Stats.MaximumAbility];
        var recovery = Probe.RegenerationRate(player, Stats.AbilityRegeneration);
        var cost = (int)Math.Min(ap.MinimumValue, Math.Max(1, Math.Floor(recovery + cap / 120)));
        Probe.FillResources(player);
        var beforeMana = player.Attributes[Stats.CurrentMana];
        Assert.That(await player.TryConsumeForSkillAsync(entry).ConfigureAwait(false), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(cap - player.Attributes[Stats.CurrentAbility], Is.EqualTo(cost).Within(0.0001));
            Assert.That(beforeMana - player.Attributes[Stats.CurrentMana], Is.EqualTo(mana.MinimumValue).Within(0.0001));
            Assert.That(cost, Is.InRange(1, (int)ap.MinimumValue));
        });

        // This trace integrates the separately verified real regeneration equation, without resource refills.
        Probe.FillResources(player);
        var maximumMana = player.Attributes[Stats.MaximumMana];
        var manaRecovery = Probe.RegenerationRate(player, Stats.ManaRegeneration);
        var potion = this.CreateManaPotion(player);
        var handler = new LargeManaPotionConsumeHandler();
        var lastPotion = -12;
        var successful = 0;
        for (var second = 0; second < 60; second++)
        {
            player.Attributes[Stats.CurrentAbility] = (float)Math.Min(cap, player.Attributes[Stats.CurrentAbility] + recovery);
            player.Attributes[Stats.CurrentMana] = (float)Math.Min(maximumMana, player.Attributes[Stats.CurrentMana] + manaRecovery);
            if (second - lastPotion >= 12 && player.Attributes[Stats.CurrentMana] < maximumMana * 0.7)
            {
                await handler.RecoverAsync(player, potion).ConfigureAwait(false);
                lastPotion = second;
            }

            Assert.That(player.Attributes[Stats.CurrentAbility], Is.GreaterThanOrEqualTo(cost), $"AP exhausted at second {second}.");
            var enoughMana = player.Attributes[Stats.CurrentMana] >= mana.MinimumValue;
            Assert.That(await player.TryConsumeForSkillAsync(entry).ConfigureAwait(false), Is.EqualTo(enoughMana), $"Only unchanged MP may gate a routine cast at second {second}.");
            if (enoughMana)
            {
                successful++;
            }
        }

        Assert.That(player.Attributes[Stats.CurrentAbility], Is.GreaterThanOrEqualTo(cap * 0.5 - cost - 0.01));
        Assert.That(successful, Is.GreaterThan(0));
        TestContext.Progress.WriteLine($"class={stage} level={level} skill={number} AP={cap:F2} regen={recovery:F4}/s original={ap.MinimumValue} new={cost} casts={successful}/60 end={player.Attributes[Stats.CurrentAbility]:F2}");
    }

    /// <summary>Runs the actual public combat action callers and proves resource deltas and target damage.</summary>
    /// <param name="stage">The qualified class.</param>
    /// <param name="number">The real skill number.</param>
    [TestCase(6, 43)]
    [TestCase(8, 52)]
    [TestCase(12, 55)]
    [TestCase(24, 262)]
    public async Task ActualCombatCallerConsumesOnceAndHitsAsync(int stage, int number)
    {
        var player = await this.CreatePlayerAsync(stage, 400).ConfigureAwait(false);
        var map = await this._context.GetMapAsync(57).ConfigureAwait(false) ?? throw new InvalidOperationException();
        var spawn = map.Definition.MonsterSpawns.First(value => value.MonsterDefinition!.Number == 565);
        using var monster = new Monster(spawn, spawn.MonsterDefinition!, map, NullDropGenerator.Instance,
            new Mock<INpcIntelligence>().Object, this._context.PlugInManager, this._context.PathFinderPool);
        monster.Initialize();
        await map.AddAsync(monster).ConfigureAwait(false);
        player.CurrentMap = map;
        player.Position = new Point((byte)(monster.Position.X - 1), monster.Position.Y);
        player.Attributes![Stats.IsInSafezone] = 0;
        Assert.That(player.IsAtSafezone(), Is.False);
        var entry = player.SkillList!.GetSkill((ushort)number)!;
        var skill = entry.Skill!;
        var apCost = player.GetRequiredValue(skill.ConsumeRequirements.Single(value => value.Attribute == Stats.CurrentAbility), entry);
        var mpCost = player.GetRequiredValue(skill.ConsumeRequirements.Single(value => value.Attribute == Stats.CurrentMana), entry);
        var initialHealth = monster.Health;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            Probe.FillResources(player);
            var beforeAp = player.Attributes[Stats.CurrentAbility];
            var beforeMp = player.Attributes[Stats.CurrentMana];
            if (skill.SkillType == SkillType.DirectHit)
            {
                await new TargetedSkillDefaultPlugin().PerformSkillAsync(player, monster, (ushort)number).ConfigureAwait(false);
            }
            else
            {
                await new AreaSkillAttackAction().AttackAsync(player, monster.Id, (ushort)number, monster.Position,
                    (byte)(player.Position.GetAngleDegreeTo(monster.Position) / 360 * 255)).ConfigureAwait(false);
            }

            Assert.Multiple(() =>
            {
                Assert.That(beforeAp - player.Attributes[Stats.CurrentAbility], Is.EqualTo(apCost).Within(0.001));
                Assert.That(beforeMp - player.Attributes[Stats.CurrentMana], Is.EqualTo(mpCost).Within(0.001));
            });
        }

        await Task.Delay(150).ConfigureAwait(false); // Penetration schedules real deferred hits.
        Assert.That(monster.Health, Is.LessThan(initialHealth), "A cost-only call is not proof that the combat action executed.");
        await map.RemoveAsync(monster).ConfigureAwait(false);
    }

    /// <summary>Verifies actual real-time AP recovery, consumption, potion cooldown and stock over sixty seconds.</summary>
    [Test]
    public async Task ActualClockSixtySecondRoutineNeverResetsResourcesAsync()
    {
        var player = await this.CreatePlayerAsync(12, 200).ConfigureAwait(false);
        var entry = player.SkillList!.GetSkill(55)!;
        Probe.FillResources(player);
        player.Attributes![Stats.CurrentAbility] = player.Attributes[Stats.MaximumAbility] / 2;
        player.Attributes[Stats.IsInSafezone] = 0;
        player.Attributes[Stats.IsResting] = 0;
        await player.RegenerateAsync().ConfigureAwait(false);
        var potion = this.CreateManaPotion(player);
        Assert.That(await player.Inventory!.AddItemAsync(12, potion).ConfigureAwait(false), Is.True);
        var initialStock = potion.Durability;
        var handler = new LargeManaPotionConsumeHandler();
        var attempts = 0;
        var potions = 0;
        var observedRegeneration = 0d;
        var watch = Stopwatch.StartNew();
        for (var tick = 0; tick <= 120; tick++)
        {
            var delay = TimeSpan.FromSeconds(tick * 0.5) - watch.Elapsed;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay).ConfigureAwait(false);
            }

            var before = player.Attributes[Stats.CurrentAbility];
            await player.RegenerateAsync().ConfigureAwait(false);
            observedRegeneration += player.Attributes[Stats.CurrentAbility] - before;
            if (player.Attributes[Stats.CurrentMana] < player.Attributes[Stats.MaximumMana] * 0.7
                && await handler.ConsumeItemAsync(player, potion, null, FruitUsage.Undefined).ConfigureAwait(false))
            {
                potions++;
                Assert.That(await handler.ConsumeItemAsync(player, potion, null, FruitUsage.Undefined).ConfigureAwait(false), Is.False);
            }

            if (tick < 120 && tick % 2 == 0)
            {
                Assert.That(await player.TryConsumeForSkillAsync(entry).ConfigureAwait(false), Is.True, $"Failed at {watch.Elapsed.TotalSeconds:F2}s.");
                attempts++;
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(attempts, Is.EqualTo(60));
            Assert.That(watch.Elapsed.TotalSeconds, Is.GreaterThanOrEqualTo(59.9));
            Assert.That(observedRegeneration / watch.Elapsed.TotalSeconds,
                Is.EqualTo(Probe.RegenerationRate(player, Stats.AbilityRegeneration)).Within(0.1));
            Assert.That(player.Attributes[Stats.CurrentAbility], Is.InRange(0, player.Attributes[Stats.MaximumAbility]));
            Assert.That(potions, Is.InRange(1, 5));
            Assert.That(initialStock - potion.Durability, Is.EqualTo(potions));
        });
    }

    /// <summary>Old profiles, unknown definitions, unlearned entries and tactical burst AP are untouched.</summary>
    [Test]
    public async Task ProfileAndContractExclusionsPreserveOriginalCostsAsync()
    {
        var player = await this.CreatePlayerAsync(24, 400).ConfigureAwait(false);
        foreach (var number in new ushort[] { 265, 264, 268 })
        {
            var entry = player.SkillList!.GetSkill(number)!;
            var requirement = entry.Skill!.ConsumeRequirements.Single(value => value.Attribute == Stats.CurrentAbility);
            Assert.That(player.GetRequiredValue(requirement, entry), Is.EqualTo(requirement.MinimumValue));
        }

        var routine = player.SkillList!.GetSkill(262)!;
        var ap = routine.Skill!.ConsumeRequirements.Single(value => value.Attribute == Stats.CurrentAbility);
        var copy = player.PersistenceContext.CreateNew<SkillEntry>();
        copy.Skill = routine.Skill;
        Assert.That(player.GetRequiredValue(ap, copy), Is.EqualTo(20), "Unlearned entry must not receive a profile discount.");
        var marker = this._configuration.GlobalBaseAttributeValues.Single(value => value.Definition!.Id == BalanceV1.ProfileAttributeId);
        this._configuration.GlobalBaseAttributeValues.Remove(marker);
        Assert.That(player.GetRequiredValue(ap, routine), Is.EqualTo(20));
        var soloDefinition = player.PersistenceContext.CreateNew<AttributeDefinition>(SoloBalance.ProfileAttributeId, "Solo", string.Empty);
        var solo = player.PersistenceContext.CreateNew<ConstValueAttribute>(1f, soloDefinition, AggregateType.AddRaw);
        this._configuration.GlobalBaseAttributeValues.Add(solo);
        Assert.That(player.GetRequiredValue(ap, routine), Is.EqualTo(20));
        this._configuration.GlobalBaseAttributeValues.Add(marker);
        Assert.That(player.GetRequiredValue(ap, routine), Is.EqualTo(20), "Mutually exclusive profile combination never enables cheap skill costs.");
        this._configuration.GlobalBaseAttributeValues.Remove(solo);
        Assert.That(player.GetRequiredValue(ap, routine), Is.LessThan(20));
        routine.Skill.SkillType = SkillType.Buff;
        Assert.That(player.GetRequiredValue(ap, routine), Is.EqualTo(20));
    }

    /// <summary>Original skill/orb level and class requirements cannot be bypassed by a cheap resource contract.</summary>
    [Test]
    public async Task ActualLearnablesRetainClassAndUnlockRequirementsAsync()
    {
        var pool = Probe.BuildAvailableItems(this._configuration, 400);
        foreach (var (stage, number, level) in new[] { (6, 43, 159), (8, 52, 129), (24, 262, 149), (4, 43, 200), (0, 55, 400) })
        {
            var player = await this.CreatePlayerAsync(stage, level).ConfigureAwait(false);
            var source = pool.First(value => value.Item.Definition!.Group is 12 or 15 && value.Item.Definition.Skill?.Number == number);
            var item = player.PersistenceContext.CreateNew<Item>();
            item.Definition = source.Item.Definition;
            item.Durability = 1;
            var canLearn = player.CompliesRequirements(item) && item.Definition!.QualifiedCharacters.Contains(player.SelectedCharacter!.CharacterClass!)
                && item.Definition.Skill!.QualifiedCharacters.Contains(player.SelectedCharacter.CharacterClass!);
            Assert.That(await new LearnablesConsumeHandlerPlugIn().ConsumeItemAsync(player, item, null, FruitUsage.Undefined).ConfigureAwait(false),
                Is.EqualTo(canLearn), $"Orb class/item boundary {stage}/{level}/{number}");
            if (player.SkillList!.GetSkill((ushort)number) is { } learned)
            {
                Probe.FillResources(player);
                Assert.That(await player.TryConsumeForSkillAsync(learned).ConfigureAwait(false), Is.False, $"Original skill-use unlock {stage}/{level}/{number}");
            }
        }
    }

    /// <summary>Excess reduction cannot produce negative, free or increasing routine AP costs.</summary>
    [TestCase(-0.5f)]
    [TestCase(0.5f)]
    [TestCase(1f)]
    [TestCase(2f)]
    public async Task RoutineReductionRemainsBoundedAsync(float reduction)
    {
        var player = await this.CreatePlayerAsync(12, 400).ConfigureAwait(false);
        var entry = player.SkillList!.GetSkill(55)!;
        var requirement = entry.Skill!.ConsumeRequirements.Single(value => value.Attribute == Stats.CurrentAbility);
        var normal = player.GetRequiredValue(requirement, entry);
        player.Attributes![Stats.AbilityUsageReduction] = reduction;
        Assert.That(player.GetRequiredValue(requirement, entry), Is.InRange(1, normal));
    }

    /// <summary>All eighteen legal level-400 class stages preserve all original learned MP/AP costs without the marker.</summary>
    [Test]
    public async Task EveryOriginalClassAndLearnedCostRemainsUnchangedAsync()
    {
        var marker = this._configuration.GlobalBaseAttributeValues.Single(value => value.Definition!.Id == BalanceV1.ProfileAttributeId);
        this._configuration.GlobalBaseAttributeValues.Remove(marker);
        foreach (var stage in this._configuration.CharacterClasses)
        {
            var player = await this.CreatePlayerAsync(stage.Number, 400).ConfigureAwait(false);
            foreach (var entry in player.SkillList!.Skills.Where(value => value.Skill!.MasterDefinition is null))
            {
                foreach (var requirement in entry.Skill!.ConsumeRequirements.Where(value => value.Attribute == Stats.CurrentAbility || value.Attribute == Stats.CurrentMana))
                {
                    Assert.That(player.GetRequiredValue(requirement, entry), Is.EqualTo((int)requirement.MinimumValue), $"Original class={stage.Number} skill={entry.Skill.Number} {requirement.Attribute!.Designation}");
                }
            }
        }
    }

    /// <summary>MP shortage and AP shortage reject the real consumer without partially debiting another resource.</summary>
    [Test]
    public async Task InsufficientResourcesCannotProduceFreeOrPartialCastAsync()
    {
        var player = await this.CreatePlayerAsync(12, 200).ConfigureAwait(false);
        var entry = player.SkillList!.GetSkill(55)!;
        Probe.FillResources(player);
        player.Attributes![Stats.CurrentMana] = 14;
        var beforeAp = player.Attributes[Stats.CurrentAbility];
        Assert.That(await player.TryConsumeForSkillAsync(entry).ConfigureAwait(false), Is.False);
        Assert.That(player.Attributes[Stats.CurrentAbility], Is.EqualTo(beforeAp));
        Probe.FillResources(player);
        player.Attributes[Stats.CurrentAbility] = 0;
        var beforeMp = player.Attributes[Stats.CurrentMana];
        Assert.That(await player.TryConsumeForSkillAsync(entry).ConfigureAwait(false), Is.False);
        Assert.That(player.Attributes[Stats.CurrentMana], Is.EqualTo(beforeMp));
    }

    /// <summary>Separates reviewed RF and Summoner definitions instead of aliasing unrelated caller contracts.</summary>
    [Test]
    public void OriginalSpecialSkillContractsAreDistinct()
    {
        foreach (var (number, mana, ap, hits, type) in new[]
        {
            (265, 100, 100, 1, SkillType.DirectHit), (270, 30, 0, 4, SkillType.AreaSkillExplicitTarget),
            (215, 85, 0, 1, SkillType.AreaSkillExplicitTarget), (230, 115, 7, 1, SkillType.AreaSkillAutomaticHits),
        })
        {
            var skill = this._configuration.Skills.Single(value => value.Number == number);
            Assert.Multiple(() =>
            {
                Assert.That(skill.ConsumeRequirements.Where(value => value.Attribute == Stats.CurrentMana).Sum(value => value.MinimumValue), Is.EqualTo(mana));
                Assert.That(skill.ConsumeRequirements.Where(value => value.Attribute == Stats.CurrentAbility).Sum(value => value.MinimumValue), Is.EqualTo(ap));
                Assert.That(skill.NumberOfHitsPerAttack, Is.EqualTo(hits));
                Assert.That(skill.SkillType, Is.EqualTo(type));
                Assert.That(skill.QualifiedCharacters.Select(value => (int)value.Number), Is.EquivalentTo(number < 260 ? new[] { 20, 22, 23 } : new[] { 24, 25 }));
            });
        }

        Assert.Multiple(() =>
        {
            Assert.That(new PhoenixShotSkillPlugIn().Key, Is.EqualTo(270));
            Assert.That(new ChainLightningSkillPlugIn().Key, Is.EqualTo(215));
        });
    }

    private async Task<Player> CreatePlayerAsync(int stageNumber, int level)
    {
        var stage = this._configuration.CharacterClasses.Single(value => value.Number == stageNumber);
        var player = await Probe.CreatePlayerAsync(this._context, this._provider, this._configuration, stage, level).ConfigureAwait(false);
        this._players.Add(player);
        var pool = Probe.BuildAvailableItems(this._configuration, level);
        var gear = await Probe.EquipAsync(player, pool, stageNumber is 0 or 2 or 3 or 20 or 22 or 23).ConfigureAwait(false);
        await Probe.LearnAsync(player, pool).ConfigureAwait(false);
        Assert.That(player.Inventory!.EquippedItems.All(value => value.ItemOptions.Count == 0 && value.Definition!.MaximumSockets == 0), Is.True);
        Assert.That(gear, Is.Not.Null);
        return player;
    }

    private Item CreateManaPotion(Player player)
    {
        var item = player.PersistenceContext.CreateNew<Item>();
        item.Definition = this._configuration.Items.Single(value => value.Group == 14 && value.Number == 6);
        item.Durability = 10;
        return item;
    }
}
