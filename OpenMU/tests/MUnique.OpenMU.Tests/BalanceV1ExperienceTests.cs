// <copyright file="BalanceV1ExperienceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.PlugIns;
using MonsterAttribute = MUnique.OpenMU.Persistence.BasicModel.MonsterAttribute;
using MonsterDefinition = MUnique.OpenMU.Persistence.BasicModel.MonsterDefinition;

/// <summary>Tests balance-v1 rewards through the live player and party experience paths.</summary>
[TestFixture]
public class BalanceV1ExperienceTests
{
    /// <summary>Verifies exact rewards, tier multipliers, and the bounded level-gap curve.</summary>
    [Test]
    public void KillRewardsMatchCandidateDesign()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BalanceV1.CalculateKillExperience(100, 1, 100), Is.EqualTo(6725).Within(0.000001));
            Assert.That(BalanceV1.CalculateKillExperience(100, 43, 100), Is.EqualTo(26900).Within(0.000001));
            Assert.That(BalanceV1.CalculateKillExperience(100, 38, 100), Is.EqualTo(201750).Within(0.000001));
            Assert.That(BalanceV1.CalculateKillExperience(100, 1, 1), Is.EqualTo(6725 * 1.15).Within(0.000001));
            Assert.That(BalanceV1.CalculateKillExperience(100, 1, 120), Is.EqualTo(6725).Within(0.000001));
            Assert.That(BalanceV1.CalculateKillExperience(100, 1, 200), Is.EqualTo(6725 * Math.Exp(-1)).Within(0.000001));
            Assert.That(BalanceV1.CalculateKillExperience(100, 1, 1000), Is.EqualTo(672.5).Within(0.000001));
            Assert.That(BalanceV1.CalculateKillExperience(0, 1, 100), Is.Zero);
            Assert.That(BalanceV1.CalculateKillExperience(double.NaN, 1, 100), Is.Zero);
        });
    }

    /// <summary>Verifies normal/master reward calculation and actual grants, leaving legacy rules unchanged.</summary>
    /// <param name="enabled">Whether balance-v1 is enabled.</param>
    /// <param name="master">Whether the character receives master experience.</param>
    /// <param name="rate">The configured normal/master experience rate.</param>
    [TestCase(false, false, 1.0f)]
    [TestCase(false, true, 1.0f)]
    [TestCase(true, false, 1.0f)]
    [TestCase(true, false, 1.5f)]
    [TestCase(true, true, 1.0f)]
    [TestCase(true, true, 0.75f)]
    public async ValueTask PlayerKillUsesActiveProfileAsync(bool enabled, bool master, float rate)
    {
        var context = CreateContext(enabled, rate);
        var level = master ? (short)400 : (short)100;
        var masterLevel = master ? (short)100 : (short)0;
        var targetRank = master ? 450f : 100f;
        var player = await CreatePlayerAsync(context, level, masterLevel, master).ConfigureAwait(false);
        var target = CreateTarget(targetRank);
        var expected = enabled
            ? (int)(BalanceV1.CalculateKillExperience(targetRank, -1, BalanceV1.GetCombatRank(level, masterLevel)) * rate)
            : (int)(target.CalculateBaseExperience(level + masterLevel) * rate);
        var before = master ? player.SelectedCharacter!.MasterExperience : player.SelectedCharacter!.Experience;

        var calculated = await player.CalculateExpAfterKillAsync(target).ConfigureAwait(false);
        var granted = await player.AddExpAfterKillAsync(target).ConfigureAwait(false);
        var after = master ? player.SelectedCharacter!.MasterExperience : player.SelectedCharacter!.Experience;

        Assert.Multiple(() =>
        {
            Assert.That(calculated, Is.EqualTo(expected));
            Assert.That(granted, Is.EqualTo(expected));
            Assert.That(after - before, Is.EqualTo(expected));
        });
    }

    /// <summary>Verifies a real NPC definition supplies the elite/boss reward tier.</summary>
    /// <param name="monsterNumber">The monster definition number.</param>
    /// <param name="expected">The expected base reward.</param>
    [TestCase(1, 6725)]
    [TestCase(43, 26900)]
    [TestCase(38, 201750)]
    public async ValueTask NpcDefinitionSuppliesRewardTierAsync(short monsterNumber, int expected)
    {
        var context = CreateContext(true);
        var player = await CreatePlayerAsync(context, 100, 0, false).ConfigureAwait(false);
        using var monster = await CreateMonsterAsync(context, monsterNumber).ConfigureAwait(false);

        var reward = await player.CalculateExpAfterKillAsync(monster).ConfigureAwait(false);

        Assert.That(reward, Is.EqualTo(expected));
    }

    /// <summary>Verifies summoned monsters cannot produce candidate experience.</summary>
    [Test]
    public async ValueTask SummonedMonsterDoesNotAwardExperienceAsync()
    {
        var context = CreateContext(true);
        var player = await CreatePlayerAsync(context, 100, 0, false).ConfigureAwait(false);
        using var monster = await CreateMonsterAsync(context, 38, new SummonedMonsterIntelligence(player)).ConfigureAwait(false);
        var before = player.SelectedCharacter!.Experience;

        var reward = await player.AddExpAfterKillAsync(monster).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(reward, Is.Zero);
            Assert.That(player.SelectedCharacter.Experience, Is.EqualTo(before));
        });
    }

    /// <summary>Verifies the shared encounter pool grows by ninety percent per extra member, not per-member copies.</summary>
    /// <param name="members">The nearby party size.</param>
    [TestCase(2)]
    [TestCase(5)]
    public async ValueTask PartyUsesCandidateEncounterBudgetAsync(int members)
    {
        var context = CreateContext(true);
        var players = new List<Player>();
        await using var party = new Party(new PartyManager(5, NullLogger<Party>.Instance), 5, NullLogger<Party>.Instance);
        for (var index = 0; index < members; index++)
        {
            var player = await CreatePlayerAsync(context, 100, 0, false).ConfigureAwait(false);
            players.Add(player);
            await party.AddAsync(player).ConfigureAwait(false);
            if (index > 0)
            {
                await players[0].AddObserverAsync(player).ConfigureAwait(false);
            }
        }

        var shares = await party.DistributeExperienceAfterKillAsync(CreateTarget(100), players[0]).ConfigureAwait(false);
        var expectedBudget = 6725 * (1 + (0.9 * (members - 1)));

        Assert.Multiple(() =>
        {
            Assert.That(shares, Has.Count.EqualTo(members));
            Assert.That(shares.Sum(share => share.Experience), Is.EqualTo(expectedBudget).Within(members + 1));
            Assert.That(shares.Select(share => share.Experience), Is.All.EqualTo(shares[0].Experience));
        });
    }

    /// <summary>Verifies mixed normal/master shares use the same candidate content-rank weighting.</summary>
    [Test]
    public async ValueTask PartyMasterWeightUsesCompressedContentRankAsync()
    {
        var context = CreateContext(true);
        var normal = await CreatePlayerAsync(context, 100, 0, false).ConfigureAwait(false);
        var master = await CreatePlayerAsync(context, 400, 100, true).ConfigureAwait(false);
        await using var party = new Party(new PartyManager(5, NullLogger<Party>.Instance), 5, NullLogger<Party>.Instance);
        await party.AddAsync(normal).ConfigureAwait(false);
        await party.AddAsync(master).ConfigureAwait(false);
        await normal.AddObserverAsync(master).ConfigureAwait(false);

        var shares = await party.DistributeExperienceAfterKillAsync(CreateTarget(300), normal).ConfigureAwait(false);
        var normalShare = shares.Single(share => share.Player == normal).Experience;
        var masterShare = shares.Single(share => share.Player == master).Experience;

        Assert.That(masterShare, Is.EqualTo(normalShare * 4.35).Within(5));
    }

    private static IAttackable CreateTarget(float rank)
    {
        var attributes = new Mock<IAttributeSystem>();
        attributes.Setup(system => system[Stats.Level]).Returns(rank);
        var target = new Mock<IAttackable>();
        target.Setup(value => value.Attributes).Returns(attributes.Object);
        return target.Object;
    }

    private static GameContext CreateContext(bool enabled, float rate = 1)
    {
        var persistence = new InMemoryPersistenceContextProvider();
        using var configurationContext = persistence.CreateNewContext();
        var configuration = configurationContext.CreateNew<GameConfiguration>();
        configuration.RecoveryInterval = int.MaxValue;
        configuration.MaximumLevel = 400;
        configuration.MaximumMasterLevel = 200;
        configuration.MinimumMonsterLevelForMasterExperience = 0;
        configuration.ExperienceRate = rate;
        configuration.MasterExperienceRate = rate;
        if (enabled)
        {
            var marker = configurationContext.CreateNew<AttributeDefinition>(BalanceV1.ProfileAttributeId, "balance-v1", string.Empty);
            configuration.GlobalBaseAttributeValues.Add(configurationContext.CreateNew<ConstValueAttribute>(1, marker, AggregateType.AddRaw));
        }

        var map = configurationContext.CreateNew<GameMapDefinition>();
        map.ExpMultiplier = 1;
        map.TerrainData = new byte[ushort.MaxValue + 3];
        configuration.Maps.Add(map);
        var mapInitializer = new MapInitializer(configuration, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var context = new GameContext(
            configuration,
            persistence,
            mapInitializer,
            NullLoggerFactory.Instance,
            new PlugInManager(new List<PlugInConfiguration>(), NullLoggerFactory.Instance, null, null),
            NullDropGenerator.Instance,
            new ConfigurationChangeMediator());
        mapInitializer.PlugInManager = context.PlugInManager;
        mapInitializer.PathFinderPool = context.PathFinderPool;
        return context;
    }

    private static async ValueTask<Player> CreatePlayerAsync(IGameContext context, short level, short masterLevel, bool master)
    {
        var player = await PlayerTestHelper.CreatePlayerAsync(context).ConfigureAwait(false);
        player.SelectedCharacter!.CharacterClass!.IsMasterClass = master;
        player.Attributes![Stats.Level] = level;
        player.Attributes[Stats.MasterLevel] = masterLevel;
        player.Attributes.AddElement(new SimpleElement(level + masterLevel, AggregateType.AddRaw), Stats.TotalLevel);
        player.Attributes.AddElement(new SimpleElement(1, AggregateType.AddRaw), Stats.ExperienceRate);
        player.Attributes.AddElement(new SimpleElement(1, AggregateType.AddRaw), Stats.MasterExperienceRate);
        player.SelectedCharacter.Experience = context.ExperienceTable[level];
        player.SelectedCharacter.MasterExperience = context.MasterExperienceTable[masterLevel];
        return player;
    }

    private static async ValueTask<Monster> CreateMonsterAsync(GameContext context, short number, INpcIntelligence? intelligence = null)
    {
        var definition = new MonsterDefinition { Number = number, ObjectKind = NpcObjectKind.Monster };
        definition.Attributes.Add(new MonsterAttribute { AttributeDefinition = Stats.Level, Value = 100 });
        var map = await context.GetMapAsync(0).ConfigureAwait(false);
        var spawn = new MonsterSpawnArea { MonsterDefinition = definition, GameMap = map!.Definition };
        return new Monster(
            spawn,
            definition,
            map,
            NullDropGenerator.Instance,
            intelligence ?? new Mock<INpcIntelligence>().Object,
            context.PlugInManager,
            context.PathFinderPool);
    }
}
