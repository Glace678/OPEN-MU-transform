// <copyright file="BalanceV1RuntimeTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;

/// <summary>
/// Tests the isolated balance-v1 runtime rules.
/// </summary>
[TestFixture]
public class BalanceV1RuntimeTests
{
    private const byte SourceSlot = 12;

    /// <summary>Verifies the explicit launch option and its supported variants.</summary>
    [Test]
    public void ExplicitProfileOptionIsStrict()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BalanceV1.GetRequestedProfile([]), Is.Null);
            Assert.That(BalanceV1.GetRequestedProfile(["-balance-v1"]), Is.EqualTo(BalanceV1.ExperienceProfile.Standard));
            Assert.That(BalanceV1.GetRequestedProfile(["-BALANCE-V1:RELAXED"]), Is.EqualTo(BalanceV1.ExperienceProfile.Relaxed));
            Assert.That(BalanceV1.GetRequestedProfile(["-balance-v1:journey"]), Is.EqualTo(BalanceV1.ExperienceProfile.Journey));
            Assert.That(
                () => BalanceV1.GetRequestedProfile(["-balance-v1:unknown"]),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => BalanceV1.GetRequestedProfile(["-balance-v1", "-balance-v1:standard"]),
                Throws.TypeOf<ArgumentException>());
        });
    }

    /// <summary>Verifies that the Solo and balance-v1 markers cannot coexist.</summary>
    [Test]
    public void MixedProfileMarkersAreRejected()
    {
        var configuration = CreateConfiguration();
        AddProfileMarker(configuration, SoloBalance.ProfileAttributeId, "Solo");
        AddProfileMarker(configuration, BalanceV1.ProfileAttributeId, "balance-v1");

        Assert.That(
            () => BalanceV1.ValidateProfileMarkers(configuration),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("mutually exclusive"));
    }

    /// <summary>Verifies the generated normal and master tables against the design artifact.</summary>
    [Test]
    public void ExactExperienceTablesMatchDesignEndpoints()
    {
        var normal = BalanceV1.CreateExperienceTable(master: false);
        var master = BalanceV1.CreateExperienceTable(master: true);

        Assert.Multiple(() =>
        {
            Assert.That(normal, Has.Length.EqualTo(BalanceV1.NormalLevelCap + 1));
            Assert.That(normal[1], Is.Zero);
            Assert.That(normal[2], Is.EqualTo(126));
            Assert.That(normal[400], Is.EqualTo(1_049_799_678));
            Assert.That(normal.Zip(normal.Skip(1), (left, right) => right >= left), Is.All.True);

            Assert.That(master, Has.Length.EqualTo(BalanceV1.MasterLevelCap + 1));
            Assert.That(master[1], Is.EqualTo(8_911_201));
            Assert.That(master[200], Is.EqualTo(3_425_281_421));
            Assert.That(master.Zip(master.Skip(1), (left, right) => right > left), Is.All.True);
        });
    }

    /// <summary>Verifies independent channel thresholds and low-rank rare-item protection.</summary>
    [Test]
    public void CandidateLootChannelsAreIndependent()
    {
        var highRank = BalanceV1.RollLoot(100, 0.9996, 0.64, 0.034);
        var lowRank = BalanceV1.RollLoot(79, 0.9999, 0.99, 0.99);

        Assert.Multiple(() =>
        {
            Assert.That(highRank.Equipment, Is.EqualTo(BalanceV1.EquipmentDrop.Ancient));
            Assert.That(highRank.Money, Is.True);
            Assert.That(highRank.Jewel, Is.True);
            Assert.That(lowRank.Equipment, Is.EqualTo(BalanceV1.EquipmentDrop.Common));
            Assert.That(lowRank.Money, Is.False);
            Assert.That(lowRank.Jewel, Is.False);
        });
    }

    /// <summary>Verifies Zen amounts are rank-based and include elite and boss multipliers.</summary>
    [Test]
    public void ZenFormulaUsesContentRankAndMonsterTier()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BalanceV1.CalculateZen(100, 1), Is.EqualTo(158));
            Assert.That(BalanceV1.CalculateZen(100, 43), Is.EqualTo(632));
            Assert.That(BalanceV1.CalculateZen(100, 38), Is.EqualTo(4740));
        });
    }

    /// <summary>Verifies the bounded logarithmic hit formula used by live balance-v1 combat.</summary>
    [Test]
    public void CombatHitChanceIsBoundedAndRatioSensitive()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BalanceV1.CalculateHitChance(100, 100), Is.EqualTo(0.92).Within(0.000001));
            Assert.That(BalanceV1.CalculateHitChance(200, 100), Is.EqualTo(0.98).Within(0.000001));
            Assert.That(BalanceV1.CalculateHitChance(100, 200), Is.EqualTo(0.84).Within(0.000001));
            Assert.That(BalanceV1.CalculateHitChance(0, 1_000_000), Is.EqualTo(0.60).Within(0.000001));
            Assert.That(BalanceV1.CalculateHitChance(double.NaN, double.PositiveInfinity), Is.EqualTo(0.92).Within(0.000001));
        });
    }

    /// <summary>Verifies the capped armor formula used by live balance-v1 combat.</summary>
    [Test]
    public void CombatDamageReductionIsCappedAndPenetrationAware()
    {
        var baseline = BalanceV1.CalculateDamageReduction(100, 100);
        var penetrated = BalanceV1.CalculateDamageReduction(100, 100, penetration: 0.25);
        var capped = BalanceV1.CalculateDamageReduction(1_000_000, 1);

        Assert.Multiple(() =>
        {
            Assert.That(baseline, Is.EqualTo(100d / 420d).Within(0.000001));
            Assert.That(penetrated, Is.LessThan(baseline));
            Assert.That(capped, Is.EqualTo(0.60).Within(0.000001));
            Assert.That(BalanceV1.CalculateDamageReduction(100, 100, extraReduction: 1), Is.EqualTo(0.70).Within(0.000001));
            Assert.That(BalanceV1.GetCombatRank(400, 200), Is.EqualTo(470).Within(0.000001));
        });
    }

    /// <summary>Verifies the live drop generator does not use awarded experience to calculate Zen.</summary>
    [Test]
    public async ValueTask GeneratedZenDoesNotDependOnAwardedExperienceAsync()
    {
        var player = await CreateBalancePlayerAsync().ConfigureAwait(false);
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(value => value.NextDouble()).Returns(0.5);
        var generator = new DefaultDropGenerator(player.GameContext.Configuration, randomizer.Object);
        var monster = new Mock<MonsterDefinition>();
        monster.SetupAllProperties();
        monster.Setup(value => value.Attributes).Returns(
            new List<MonsterAttribute> { new() { AttributeDefinition = Stats.Level, Value = 100 } });
        monster.Setup(value => value.DropItemGroups).Returns(new List<DropItemGroup>());
        monster.Object.Number = 1;
        monster.Object.ObjectKind = NpcObjectKind.Monster;

        var lowExperience = await generator.GenerateItemDropsAsync(monster.Object, 1, player).ConfigureAwait(false);
        var highExperience = await generator.GenerateItemDropsAsync(monster.Object, 1_000_000, player).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(lowExperience.Money, Is.EqualTo(158));
            Assert.That(highExperience.Money, Is.EqualTo(lowExperience.Money));
        });
    }

    /// <summary>Verifies health and mana use separate cooldown groups.</summary>
    [Test]
    public async ValueTask PotionCooldownsAreSeparatedByResourceAsync()
    {
        var player = await CreateBalancePlayerAsync().ConfigureAwait(false);
        player.Attributes![Stats.Level] = 100;
        player.Attributes[Stats.CurrentHealth] = 0;
        player.Attributes[Stats.CurrentMana] = 0;

        var health = CreateConsumable(14, 1, 2);
        var mana = CreateConsumable(14, 4, 1);
        await player.Inventory!.AddItemAsync(SourceSlot, health).ConfigureAwait(false);
        await player.Inventory.AddItemAsync(SourceSlot + 1, mana).ConfigureAwait(false);

        var healthHandler = new SmallHealthPotionConsumeHandlerPlugIn();
        var manaHandler = new SmallManaPotionConsumeHandler();
        var firstHealth = await healthHandler.ConsumeItemAsync(player, health, null, FruitUsage.Undefined).ConfigureAwait(false);
        var secondHealth = await healthHandler.ConsumeItemAsync(player, health, null, FruitUsage.Undefined).ConfigureAwait(false);
        var firstMana = await manaHandler.ConsumeItemAsync(player, mana, null, FruitUsage.Undefined).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(firstHealth, Is.True);
            Assert.That(secondHealth, Is.False);
            Assert.That(firstMana, Is.True);
            Assert.That(player.Attributes[Stats.CurrentHealth], Is.GreaterThan(0));
            Assert.That(player.Attributes[Stats.CurrentMana], Is.GreaterThan(0));
        });
    }

    /// <summary>Verifies a failed level-jewel attempt consumes the jewel but preserves the target.</summary>
    [Test]
    public async ValueTask FailedLevelUpgradePreservesTargetLevelAsync()
    {
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(value => value.NextRandomBool(85)).Returns(false);
        var handler = new SoulJewelConsumeHandlerPlugIn(randomizer.Object);
        var player = await CreateBalancePlayerAsync().ConfigureAwait(false);
        var target = CreateUpgradeableItem(level: 6);
        var jewel = CreateConsumable(14, 14, 1);
        await player.Inventory!.AddItemAsync(SourceSlot, jewel).ConfigureAwait(false);
        await player.Inventory.AddItemAsync(SourceSlot + 1, target).ConfigureAwait(false);

        var consumed = await handler.ConsumeItemAsync(player, jewel, target, FruitUsage.Undefined).ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(consumed, Is.True);
            Assert.That(target.Level, Is.EqualTo(6));
            randomizer.Verify(value => value.NextRandomBool(85), Times.Once);
        });
    }

    private static GameConfiguration CreateConfiguration()
    {
        var configuration = new Mock<GameConfiguration>();
        configuration.Setup(value => value.GlobalBaseAttributeValues).Returns(new List<ConstValueAttribute>());
        return configuration.Object;
    }

    private static void AddProfileMarker(GameConfiguration configuration, Guid id, string name)
    {
        configuration.GlobalBaseAttributeValues.Add(
            new ConstValueAttribute(1, new AttributeDefinition(id, name, string.Empty)));
    }

    private static async ValueTask<Player> CreateBalancePlayerAsync()
    {
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        AddProfileMarker(player.GameContext.Configuration, BalanceV1.ProfileAttributeId, "balance-v1");
        return player;
    }

    private static Item CreateConsumable(byte group, short number, float durability)
    {
        return new Item
        {
            Definition = new ItemDefinition
            {
                Group = group,
                Number = number,
                Width = 1,
                Height = 1,
            },
            Durability = durability,
        };
    }

    private static Item CreateUpgradeableItem(byte level)
    {
        var itemSlot = new Mock<ItemSlotType>();
        itemSlot.Setup(value => value.ItemSlots).Returns(new List<int> { InventoryConstants.LeftHandSlot });
        var definition = new Mock<ItemDefinition>();
        definition.SetupAllProperties();
        definition.Setup(value => value.ItemSlot).Returns(itemSlot.Object);
        definition.Setup(value => value.PossibleItemOptions).Returns(new List<ItemOptionDefinition>());
        definition.Object.MaximumItemLevel = 15;
        definition.Object.Width = 1;
        definition.Object.Height = 2;

        var item = new Mock<Item>();
        item.SetupAllProperties();
        item.Setup(value => value.ItemOptions).Returns(new List<ItemOptionLink>());
        item.Setup(value => value.ItemSetGroups).Returns(new List<ItemOfItemSet>());
        item.Object.Definition = definition.Object;
        item.Object.Durability = 1;
        item.Object.Level = level;
        return item.Object;
    }
}
