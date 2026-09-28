// <copyright file="BalanceV1EarlyEconomyTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MUnique.OpenMU.AttributeSystem;
using System.Buffers.Binary;
using System.IO.Pipelines;
using MUnique.OpenMU.GameServer;
using MUnique.OpenMU.GameServer.RemoteView;
using MUnique.OpenMU.GameServer.RemoteView.NPC;
using MUnique.OpenMU.GameLogic.Views.NPC;
using MUnique.OpenMU.Network;
using MUnique.OpenMU.Network.PlugIns;
using Nito.AsyncEx;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using MUnique.OpenMU.GameLogic.PlugIns;
using MUnique.OpenMU.GameLogic.PlayerActions.Character;
using MUnique.OpenMU.GameLogic.PlayerActions.ItemConsumeActions;
using MUnique.OpenMU.GameLogic.PlayerActions.Items;
using MUnique.OpenMU.GameLogic.PlayerActions.Skills;
using MUnique.OpenMU.GameLogic.Views;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Pathfinding;
using MUnique.OpenMU.PlugIns;
using Probe = BalanceV1CombatProbeTests;
using PersistentIdentity = MUnique.OpenMU.Persistence.IIdentifiable;

/// <summary>Actual creation, commerce and damage boundaries; no database or game server is used.</summary>
[TestFixture]
public class BalanceV1EarlyEconomyTests
{
    private static readonly int[] Stages = [0, 4, 8, 12, 16, 20, 24];
    private static readonly AttributeDefinition[] AllocatedAttributes =
        [Stats.BaseStrength, Stats.BaseAgility, Stats.BaseVitality, Stats.BaseEnergy, Stats.BaseLeadership];

    /// <summary>All seven actual created classes can buy and consume paid supplies, without a welcome-money grant.</summary>
    /// <param name="stage">The creation class.</param>
    [TestCase(0)]
    [TestCase(4)]
    [TestCase(8)]
    [TestCase(12)]
    [TestCase(16)]
    [TestCase(20)]
    [TestCase(24)]
    public async Task AllClassesPayForSeparatedHealthAndManaAsync(int stage)
    {
        var (provider, configuration, context) = await CreateContextAsync().ConfigureAwait(false);
        await using var ownedContext = context;
        var player = await CreatePlayerAsync(context, provider, configuration, stage, 11).ConfigureAwait(false);
        await using var ownedPlayer = player;
        Assert.That(player.Money, Is.Zero, "No test-account money or repeatable new-character currency is assumed.");
        using var merchant = await OpenMerchantAsync(player, 253).ConfigureAwait(false);
        var hpStock = merchant.Definition.MerchantStore!.Items.Single(item => item.Definition!.Number == 1 && item.Level == 0 && item.Durability == 3);
        var mpStock = merchant.Definition.MerchantStore.Items.Single(item => item.Definition!.Number == 4 && item.Level == 0 && item.Durability == 3);
        player.Money = 60; // Explicit transaction-test funding, not game income or character creation.
        await new BuyNpcItemAction().BuyItemAsync(player, hpStock.ItemSlot).ConfigureAwait(false);
        await new BuyNpcItemAction().BuyItemAsync(player, mpStock.ItemSlot).ConfigureAwait(false);
        Assert.That(player.Money, Is.Zero);
        var health = player.Inventory!.Items.Single(item => item.Definition == hpStock.Definition);
        var mana = player.Inventory.Items.Single(item => item.Definition == mpStock.Definition);
        player.Attributes![Stats.CurrentHealth] = 0;
        player.Attributes[Stats.CurrentMana] = 0;
        var healthHandler = new SmallHealthPotionConsumeHandlerPlugIn();
        var manaHandler = new SmallManaPotionConsumeHandler();
        Assert.That(await healthHandler.ConsumeItemAsync(player, health, null, FruitUsage.Undefined).ConfigureAwait(false), Is.True);
        Assert.That(await manaHandler.ConsumeItemAsync(player, mana, null, FruitUsage.Undefined).ConfigureAwait(false), Is.True);
        var after = player.Attributes[Stats.CurrentHealth];
        Assert.Multiple(() =>
        {
            Assert.That(after, Is.EqualTo(Math.Min(160, player.Attributes[Stats.MaximumHealth] * .28)).Within(.001));
            Assert.That(player.Attributes[Stats.CurrentMana], Is.EqualTo(Math.Min(120, player.Attributes[Stats.MaximumMana] * .4)).Within(.001));
            Assert.That(health.Durability, Is.EqualTo(2));
            Assert.That(mana.Durability, Is.EqualTo(2));
        });
        Assert.That(await healthHandler.ConsumeItemAsync(player, health, null, FruitUsage.Undefined).ConfigureAwait(false), Is.False);
        Assert.That(health.Durability, Is.EqualTo(2));
        Assert.That(player.Attributes[Stats.CurrentHealth], Is.EqualTo(after));
        Assert.That(await new SellItemToNpcAction().SellItemAsync(player, health.ItemSlot).ConfigureAwait(false), Is.True);
        Assert.That(await new SellItemToNpcAction().SellItemAsync(player, mana.ItemSlot).ConfigureAwait(false), Is.True);
        Assert.That(player.Money, Is.EqualTo(4), "Paid 60, used two potions, and sold four for one each.");
    }

    /// <summary>Exact supplies preserve positive costs and loss-making resale for every stack and potion level.</summary>
    /// <param name="number">The potion number.</param>
    /// <param name="unit">The exact unit price.</param>
    [TestCase(1, 12)]
    [TestCase(2, 48)]
    [TestCase(3, 176)]
    [TestCase(4, 8)]
    [TestCase(5, 32)]
    [TestCase(6, 112)]
    [TestCase(35, 140)]
    [TestCase(36, 300)]
    [TestCase(37, 600)]
    public async Task PotionPriceHasNoStackOrLevelArbitrageAsync(short number, int unit)
    {
        var (provider, configuration, context) = await CreateContextAsync().ConfigureAwait(false);
        await using var ownedContext = context;
        using var write = provider.CreateNewContext(configuration);
        var calculator = new ItemPriceCalculator();
        var item = write.CreateNew<Item>();
        item.Definition = configuration.Items.Single(definition => definition.Group == 14 && definition.Number == number);
        foreach (var level in new byte[] { 0, 1 })
        {
            item.Level = level;
            for (var count = 1; count <= 255; count++)
            {
                item.Durability = count;
                var buying = calculator.CalculateFinalBuyingPrice(item, configuration);
                var selling = calculator.CalculateSellingPrice(item, item.Durability(), configuration);
                Assert.That(buying, Is.EqualTo(unit * count));
                Assert.That(selling, Is.InRange(count, buying - 1));
                Assert.That(selling, Is.EqualTo(Math.Max(1, unit * 15 / 100) * count), "Splitting stacks cannot improve resale.");
            }
        }
    }

    /// <summary>Original and Solo configurations retain legacy calculator and transaction scale semantics.</summary>
    [Test]
    public async Task OldProfilesAndUnreviewedItemsAreUnchangedAsync()
    {
        var (provider, configuration, context) = await CreateContextAsync().ConfigureAwait(false);
        await using var ownedContext = context;
        using var write = provider.CreateNewContext(configuration);
        var calculator = new ItemPriceCalculator();
        var item = write.CreateNew<Item>();
        item.Definition = configuration.Items.Single(definition => definition.Group == 14 && definition.Number == 1);
        item.Durability = 3;
        var unreviewed = write.CreateNew<Item>();
        unreviewed.Definition = configuration.Items.Single(definition => definition.Group == 0 && definition.Number == 0);
        unreviewed.Durability = unreviewed.GetMaximumDurabilityOfOnePiece() - 1;
        Assert.Multiple(() =>
        {
            Assert.That(calculator.CalculateFinalBuyingPrice(unreviewed, configuration), Is.EqualTo(calculator.CalculateFinalBuyingPrice(unreviewed)));
            Assert.That(calculator.CalculateSellingPrice(unreviewed, unreviewed.Durability(), configuration), Is.EqualTo(calculator.CalculateSellingPrice(unreviewed, unreviewed.Durability())));
            Assert.That(calculator.CalculateFinalOldBuyingPrice(unreviewed, configuration), Is.EqualTo(calculator.CalculateFinalOldBuyingPrice(unreviewed)));
            Assert.That(calculator.CalculateRepairPrice(unreviewed, true, configuration), Is.EqualTo(calculator.CalculateRepairPrice(unreviewed, true)));
        });
        configuration.GlobalBaseAttributeValues.Remove(configuration.GlobalBaseAttributeValues.Single(value => value.Definition.Id == BalanceV1.ProfileAttributeId));
        Assert.That(calculator.CalculateFinalBuyingPrice(item, configuration), Is.EqualTo(240));
        Assert.That(calculator.CalculateSellingPrice(item, item.Durability(), configuration), Is.EqualTo(calculator.CalculateSellingPrice(item, item.Durability())));
        var solo = write.CreateNew<AttributeDefinition>(SoloBalance.ProfileAttributeId, "Solo", "Test only");
        configuration.GlobalBaseAttributeValues.Add(write.CreateNew<ConstValueAttribute>(1, solo));
        Assert.That(SoloBalance.ScalePrice(calculator.CalculateFinalBuyingPrice(item, configuration), configuration), Is.EqualTo(1));
        Assert.That(BalanceV1Economy.TryGetBuyingPrice(item, configuration, out _), Is.False);
    }

    /// <summary>The first glove is actually bought, meets unmodified RF demands and supplies the real four-hit skill.</summary>
    [Test]
    public async Task VitalityRageFighter80Through98CanBuyEquipAndUseGloveAsync()
    {
        var (provider, configuration, context) = await CreateContextAsync().ConfigureAwait(false);
        await using var ownedContext = context;
        for (var level = 80; level <= 98; level++)
        {
            var player = await CreatePlayerAsync(context, provider, configuration, 24, level, true, true).ConfigureAwait(false);
            await using var ownedPlayer = player;
            Assert.That(player.SkillList!.GetSkill(269), Is.Not.Null, "Actual creation supplies Charge, not Dark Side.");
            Assert.That(player.SkillList.GetSkill(263), Is.Null);
            using var merchant = await OpenMerchantAsync(player, 251).ConfigureAwait(false);
            var offer = merchant.Definition.MerchantStore!.Items.Single(item => item.Definition!.Group == 0 && item.Definition.Number == 32 && item.Level == 0);
            player.Money = 599;
            await new BuyNpcItemAction().BuyItemAsync(player, offer.ItemSlot).ConfigureAwait(false);
            Assert.That(player.Money, Is.EqualTo(599));
            Assert.That(player.Inventory!.Items.Any(item => item.Definition == offer.Definition), Is.False);
            player.Money = 600;
            await new BuyNpcItemAction().BuyItemAsync(player, offer.ItemSlot).ConfigureAwait(false);
            var glove = player.Inventory.Items.Single(item => item.Definition == offer.Definition);
            Assert.That(player.Money, Is.Zero);
            Assert.That(player.CompliesRequirements(glove), Is.True, $"Original glove demand at level {level}");
            await player.Inventory.RemoveItemAsync(glove).ConfigureAwait(false);
            Assert.That(await player.Inventory.AddItemAsync(0, glove).ConfigureAwait(false), Is.True);
            var skill = player.SkillList.GetSkill(260)!;
            Probe.FillResources(player);
            var beforeMp = player.Attributes![Stats.CurrentMana];
            var beforeAp = player.Attributes[Stats.CurrentAbility];
            Assert.That(await player.TryConsumeForSkillAsync(skill).ConfigureAwait(false), Is.True);
            Assert.That(player.Attributes[Stats.CurrentMana], Is.EqualTo(beforeMp - 9));
            Assert.That(player.Attributes[Stats.CurrentAbility], Is.EqualTo(beforeAp));
            Assert.That(skill.Skill!.NumberOfHitsPerAttack, Is.EqualTo(4));
            Assert.That(skill.Skill.QualifiedCharacters.Contains(player.SelectedCharacter!.CharacterClass!), Is.True);
            if (level == 80)
            {
                var map = await context.GetMapAsync(1).ConfigureAwait(false) ?? throw new InvalidOperationException();
                var spawn = map.Definition.MonsterSpawns.First(entry => entry.MonsterDefinition!.Number == 5);
                using var monster = new Monster(spawn, spawn.MonsterDefinition!, map, NullDropGenerator.Instance,
                    new Mock<INpcIntelligence>().Object, context.PlugInManager, context.PathFinderPool, balanceConfiguration: configuration);
                monster.Initialize();
                await map.AddAsync(monster).ConfigureAwait(false);
                player.CurrentMap = map;
                player.Position = new Point((byte)(monster.Position.X - 1), monster.Position.Y);
                player.Attributes[Stats.IsInSafezone] = 0;
                var health = monster.Health;
                Probe.FillResources(player);
                beforeMp = player.Attributes[Stats.CurrentMana];
                beforeAp = player.Attributes[Stats.CurrentAbility];
                await new TargetedSkillDefaultPlugin().PerformSkillAsync(player, monster, 260).ConfigureAwait(false);
                Assert.Multiple(() =>
                {
                    Assert.That(monster.Health, Is.LessThan(health), "Actual targeted-skill path damages a real initialized monster.");
                    Assert.That(player.Attributes[Stats.CurrentMana], Is.EqualTo(beforeMp - 9));
                    Assert.That(player.Attributes[Stats.CurrentAbility], Is.EqualTo(beforeAp));
                });
                await map.RemoveAsync(monster).ConfigureAwait(false);
            }
        }
    }

    /// <summary>The zero-money wizard has its real affordable attack and can pay for supplies from real Zen drops.</summary>
    [Test]
    public async Task NewWizardEarnsFirstSuppliesWithoutFreeMoneyAsync()
    {
        var (provider, configuration, context) = await CreateContextAsync().ConfigureAwait(false);
        await using var ownedContext = context;
        var player = await CreatePlayerAsync(context, provider, configuration, 0, 1).ConfigureAwait(false);
        await using var ownedPlayer = player;
        Assert.That(player.Money, Is.Zero);
        var skill = player.SkillList!.GetSkill(17)!;
        Assert.That(skill, Is.Not.Null);
        var map = await context.GetMapAsync(0).ConfigureAwait(false) ?? throw new InvalidOperationException();
        var spawn = map.Definition.MonsterSpawns.First(entry => entry.MonsterDefinition!.Number == 3);
        using var monster = new Monster(spawn, spawn.MonsterDefinition!, map, NullDropGenerator.Instance,
            new Mock<INpcIntelligence>().Object, context.PlugInManager, context.PathFinderPool, balanceConfiguration: configuration);
        monster.Initialize();
        await map.AddAsync(monster).ConfigureAwait(false);
        player.CurrentMap = map;
        player.Position = new Point((byte)(monster.Position.X - 1), monster.Position.Y);
        player.Attributes![Stats.IsInSafezone] = 0;
        Probe.FillResources(player);
        var mana = player.Attributes[Stats.CurrentMana];
        for (var cast = 0; monster.IsAlive && cast < 15; cast++)
        {
            await new TargetedSkillDefaultPlugin().PerformSkillAsync(player, monster, 17).ConfigureAwait(false);
        }

        Assert.That(monster.IsAlive, Is.False);
        Assert.That(player.Attributes[Stats.CurrentMana], Is.LessThan(mana));
        var random = new Mock<IRandomizer>();
        random.Setup(value => value.NextDouble()).Returns(.5);
        var drops = await new DefaultDropGenerator(configuration, random.Object).GenerateItemDropsAsync(monster.Definition,
            await player.CalculateExpAfterKillAsync(monster).ConfigureAwait(false), player).ConfigureAwait(false);
        Assert.That(drops.Money, Is.EqualTo(9));
        await using var money = new DroppedMoney(drops.Money!.Value, player.Position, map);
        Assert.That(await money.TryPickUpByAsync(player).ConfigureAwait(false), Is.True);
        Assert.That(await money.TryPickUpByAsync(player).ConfigureAwait(false), Is.False, "No duplicate pickup credit.");
        using var merchant = await OpenMerchantAsync(player, 253).ConfigureAwait(false);
        var offer = merchant.Definition.MerchantStore!.Items.Single(item => item.Definition!.Number == 4 && item.Level == 0 && item.Durability == 1);
        await new BuyNpcItemAction().BuyItemAsync(player, offer.ItemSlot).ConfigureAwait(false);
        Assert.That(player.Money, Is.EqualTo(1));
        Assert.That(player.Inventory!.Items.Any(item => item.Definition == offer.Definition && item.Durability == 1), Is.True);
        await map.RemoveAsync(monster).ConfigureAwait(false);
    }

    /// <summary>Stock installation is idempotent for both new and already-marked configurations.</summary>
    [Test]
    public async Task StarterStockAndEnhancedValuationsAreConsistentAsync()
    {
        var (provider, configuration, context) = await CreateContextAsync().ConfigureAwait(false);
        await using var ownedContext = context;
        using var write = provider.CreateNewContext(configuration);
        var counts = configuration.Monsters.Where(monster => monster.Number is 251 or 254).Select(monster => monster.MerchantStore!.Items.Count).ToArray();
        new BalanceV1Initializer(write, configuration, BalanceV1.ExperienceProfile.Standard).Initialize();
        Assert.That(configuration.Monsters.Where(monster => monster.Number is 251 or 254).Select(monster => monster.MerchantStore!.Items.Count), Is.EqualTo(counts));
        var calculator = new ItemPriceCalculator();
        foreach (var family in new[] { (Group: (byte)0, Number: (short)32), (Group: (byte)5, Number: (short)0) })
        {
            var item = write.CreateNew<Item>();
            item.Definition = configuration.Items.Single(definition => definition.Group == family.Group && definition.Number == family.Number);
            item.HasSkill = family.Group == 0;
            item.Durability = item.GetMaximumDurabilityOfOnePiece();
            for (byte level = 0; level <= 15; level++)
            {
                item.Level = level;
                var buy = calculator.CalculateFinalBuyingPrice(item, configuration);
                var sell = calculator.CalculateSellingPrice(item, item.Durability(), configuration);
                Assert.That(buy, Is.GreaterThan(0));
                Assert.That(sell, Is.InRange(1, buy - 1));
                Assert.That(calculator.CalculateFinalOldBuyingPrice(item, configuration), Is.EqualTo(buy), "Crafting contribution cannot use the old high valuation.");
                item.Durability = item.GetMaximumDurabilityOfOnePiece() - 1;
                Assert.That(calculator.CalculateRepairPrice(item, true, configuration), Is.GreaterThan(0));
            }
        }
    }

    /// <summary>Keeps the failing unadjusted allocation visible instead of weakening the actual item demand.</summary>
    [Test]
    public async Task UnadjustedVitalityAllocationStillCannotWearFirstGloveAt80Async()
    {
        var (provider, configuration, context) = await CreateContextAsync().ConfigureAwait(false);
        await using var ownedContext = context;
        var player = await CreatePlayerAsync(context, provider, configuration, 24, 80, true).ConfigureAwait(false);
        await using var ownedPlayer = player;
        using var merchant = await OpenMerchantAsync(player, 251).ConfigureAwait(false);
        var glove = merchant.Definition.MerchantStore!.Items.Single(item => item.Definition!.Group == 0 && item.Definition.Number == 32 && item.Level == 0);
        Assert.That(player.Attributes![Stats.TotalStrength], Is.EqualTo(130));
        Assert.That(player.CompliesRequirements(glove), Is.False);
        Assert.That(glove.GetRequirement(glove.Definition!.Requirements.Single(requirement => requirement.Attribute == Stats.TotalStrengthRequirementValue)).Value, Is.EqualTo(152));
        Assert.That(glove.GetRequirement(glove.Definition!.Requirements.Single(requirement => requirement.Attribute == Stats.TotalAgilityRequirementValue)).Value, Is.EqualTo(74));
    }

    /// <summary>Checks original real regeneration for the wizard startup attack and paid RF weapon with no resource resets.</summary>
    [Test]
    public async Task WizardAndGloveHaveSustainableConservativeCadenceFor60SecondsAsync()
    {
        var (provider, configuration, context) = await CreateContextAsync().ConfigureAwait(false);
        await using var ownedContext = context;
        var wizard = await CreatePlayerAsync(context, provider, configuration, 0, 1).ConfigureAwait(false);
        var fighter = await CreatePlayerAsync(context, provider, configuration, 24, 80, true, true).ConfigureAwait(false);
        await using var ownedWizard = wizard;
        await using var ownedFighter = fighter;
        using var merchant = await OpenMerchantAsync(fighter, 251).ConfigureAwait(false);
        var offer = merchant.Definition.MerchantStore!.Items.Single(item => item.Definition!.Group == 0 && item.Definition.Number == 32 && item.Level == 0);
        fighter.Money = 600;
        await new BuyNpcItemAction().BuyItemAsync(fighter, offer.ItemSlot).ConfigureAwait(false);
        var glove = fighter.Inventory!.Items.Single(item => item.Definition == offer.Definition);
        await fighter.Inventory.RemoveItemAsync(glove).ConfigureAwait(false);
        Assert.That(await fighter.Inventory.AddItemAsync(0, glove).ConfigureAwait(false), Is.True);
        foreach (var player in new[] { wizard, fighter })
        {
            player.Attributes![Stats.IsInSafezone] = 0;
            player.Attributes[Stats.IsResting] = 0;
            Probe.FillResources(player); // One explicitly ready start, never reset within the trace.
            await player.RegenerateAsync().ConfigureAwait(false);
        }

        var wizardSkill = wizard.SkillList!.GetSkill(17)!;
        var fighterSkill = fighter.SkillList!.GetSkill(260)!;
        for (var second = 0; second < 60; second++)
        {
            await Task.Delay(1000).ConfigureAwait(false);
            await wizard.RegenerateAsync().ConfigureAwait(false);
            await fighter.RegenerateAsync().ConfigureAwait(false);
            Assert.That(await wizard.TryConsumeForSkillAsync(wizardSkill).ConfigureAwait(false), Is.True, $"Wizard second {second}");
            if (second % 6 == 0)
            {
                var ap = fighter.Attributes![Stats.CurrentAbility];
                Assert.That(await fighter.TryConsumeForSkillAsync(fighterSkill).ConfigureAwait(false), Is.True, $"RF second {second}");
                Assert.That(fighter.Attributes[Stats.CurrentAbility], Is.EqualTo(ap));
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(wizard.Attributes![Stats.CurrentMana], Is.GreaterThan(35));
            Assert.That(fighter.Attributes![Stats.CurrentMana], Is.GreaterThan(80));
            Assert.That(fighter.Money, Is.Zero, "The glove was paid for; no trace money is awarded.");
            Assert.That(wizard.Inventory!.Items.Any(item => item.Definition!.Group == 14), Is.False, "No potion consumption or free stock in this resource-only trace.");
        });
    }

    /// <summary>Measures exact startup skills and early supply pressure before installing corrections.</summary>
    [Test]
    [Explicit("Opt-in seven-class early economy report, not client cadence or AI acceptance.")]
    public async Task MeasureEarlyEconomyAsync()
    {
        var (provider, configuration, context) = await CreateContextAsync().ConfigureAwait(false);
        await using var ownedContext = context;
        var rows = new List<object>();
        var calculator = new ItemPriceCalculator();
        foreach (var level in new[] { 1, 11, 50, 79, 80, 90, 98, 100 })
        {
            foreach (var stage in Stages)
            {
                foreach (var build in stage == 24 ? new[] { "baseline", "vitality", "gear-first-vitality" } : new[] { "baseline" })
                {
                    var player = await CreatePlayerAsync(context, provider, configuration, stage, level, build != "baseline", build == "gear-first-vitality").ConfigureAwait(false);
                    try
                    {
                        var initialSkills = player.SkillList!.Skills.Select(entry => entry.Skill!.Number).Order().ToArray();
                        var pool = Probe.BuildAvailableItems(configuration, level);
                        // Drop-pool gear is possible, not guaranteed or assumed purchased for free.
                        foreach (var item in player.Inventory!.Items.ToArray())
                        {
                            await player.Inventory.RemoveItemAsync(item).ConfigureAwait(false);
                        }

                        var equipped = await Probe.EquipAsync(player, pool, stage is 0 or 20).ConfigureAwait(false);
                        await Probe.LearnAsync(player, pool).ConfigureAwait(false);
                        Probe.FillResources(player);
                        var skillNumbers = stage switch
                        {
                            0 => new short[] { 3, 4, 17 },
                            4 => [19, 20, 21, 22, 23],
                            8 => [24],
                            12 => [55, 19, 20, 21, 22, 23, 56],
                            16 => [61, 60],
                            20 => [214, 4, 17],
                            _ => [260, 261, 269],
                        };
                        var skills = skillNumbers.Select(number => player.SkillList.GetSkill((ushort)number))
                            .Where(entry => entry?.Skill is { } skill
                                && skill.Requirements.All(requirement => player.Attributes![requirement.Attribute] >= requirement.MinimumValue)).ToArray();
                        var home = player.SelectedCharacter!.CharacterClass!.HomeMap!;
                        var mapping = BalanceV1ContentRank.Mappings.Where(entry => level >= 50 || entry.MapDefinitionId == ((PersistentIdentity)home).Id)
                            .Where(entry => entry.MonsterNumber is not (38 or 49 or 77 or 275 or 412 or 459))
                            .OrderBy(entry => Math.Abs(entry.Rank - level)).First();
                        home = configuration.Maps.Single(map => ((PersistentIdentity)map).Id == mapping.MapDefinitionId);
                        var spawn = home.MonsterSpawns.First(entry => entry.MonsterDefinition!.Number == mapping.MonsterNumber);
                        var map = await context.GetMapAsync((ushort)home.Number).ConfigureAwait(false) ?? throw new InvalidOperationException("Missing home map.");
                        using var monster = new Monster(spawn, spawn.MonsterDefinition!, map, NullDropGenerator.Instance,
                            new Mock<INpcIntelligence>().Object, context.PlugInManager, context.PathFinderPool, balanceConfiguration: configuration);
                        player.CurrentMap = map;
                        player.Attributes![Stats.IsInSafezone] = 0;
                        player.Attributes[Stats.IsResting] = 0;
                        var incoming = await SampleMeanAsync(monster, player, null).ConfigureAwait(false);
                        var actions = new List<object>();
                        foreach (var entry in skills.Append(null))
                        {
                            var payload = await SampleMeanAsync(player, monster, entry).ConfigureAwait(false) * Math.Max(1, (int)(entry?.Skill?.NumberOfHitsPerAttack ?? 1));
                            Probe.FillResources(player);
                            var mp = player.Attributes![Stats.CurrentMana];
                            var ap = player.Attributes[Stats.CurrentAbility];
                            var consumed = entry is null || await player.TryConsumeForSkillAsync(entry).ConfigureAwait(false);
                            var mpCost = mp - player.Attributes[Stats.CurrentMana];
                            actions.Add(new { Skill = entry?.Skill?.Number, Payload = payload, Consumed = consumed,
                                Mp = mpCost, Ap = ap - player.Attributes[Stats.CurrentAbility],
                                TtkAtOneSecond = monster.Attributes[Stats.MaximumHealth] / payload,
                                LegacySupplies = EstimateSupplies(player, monster, incoming, payload, mpCost, false),
                                ReviewedSupplies = EstimateSupplies(player, monster, incoming, payload, mpCost, true) });
                        }

                        var potionRows = configuration.Items.Where(definition => BalanceV1.TryGetPotionRule(definition.Group, definition.Number, out var rule)
                            && rule.UnlockLevel <= level).Select(definition =>
                        {
                            BalanceV1.TryGetPotionRule(definition.Group, definition.Number, out var rule);
                            var max = player.Attributes![rule.CooldownGroup == BalanceV1.PotionGroup.Health ? Stats.MaximumHealth
                                : rule.CooldownGroup == BalanceV1.PotionGroup.Mana ? Stats.MaximumMana : Stats.MaximumShield];
                            var item = player.PersistenceContext.CreateNew<Item>();
                            item.Definition = definition;
                            item.Durability = 1;
                            return new { definition.Number, Group = rule.CooldownGroup.ToString(), Recovery = Math.Min(rule.RecoveryCap, max * rule.RecoveryFraction),
                                UnitLegacyPrice = calculator.CalculateFinalBuyingPrice(item), UnitActualPrice = calculator.CalculateFinalBuyingPrice(item, configuration), rule.UnlockLevel };
                        }).ToArray();
                        rows.Add(new { Stage = stage, Level = level, Build = build, InitialSkills = initialSkills,
                            Wallet = player.Money, Equipped = equipped, Hp = player.Attributes![Stats.MaximumHealth], Mp = player.Attributes[Stats.MaximumMana],
                            Strength = player.Attributes[Stats.TotalStrength], Agility = player.Attributes[Stats.TotalAgility], Vitality = player.Attributes[Stats.TotalVitality],
                            Ap = player.Attributes[Stats.MaximumAbility], HpRegen = Probe.RegenerationRate(player, Stats.HealthRegeneration),
                            MpRegen = Probe.RegenerationRate(player, Stats.ManaRegeneration), ApRegen = Probe.RegenerationRate(player, Stats.AbilityRegeneration),
                            Map = home.Number, Monster = mapping.MonsterNumber, mapping.Rank, MonsterHp = monster.Attributes[Stats.MaximumHealth],
                            Incoming = incoming, EnemySeconds = monster.Definition.AttackDelay.TotalSeconds,
                            ZenAmount = BalanceV1.CalculateZen(mapping.Rank, mapping.MonsterNumber), ExpectedMoneyProbability = 0.65,
                            Actions = actions, Potions = potionRows });
                    }
                    finally
                    {
                        await player.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        var output = Environment.GetEnvironmentVariable("OPENMU_EARLY_ECONOMY_REPORT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
            {
                Policy = "Real CreateCharacterAction with configured creation plugins, legal possible gear, real skill consume/damage. Static one-second attack estimate only; not AI, kills, repair wear or item acquisition acceptance.",
                Rows = rows,
            }, new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
        }

        Assert.That(rows, Has.Count.EqualTo(72));
        TestContext.Progress.WriteLine($"Measured {rows.Count} early economy rows; report {output}.");
    }

    private static async Task<Player> CreatePlayerAsync(GameContext context, InMemoryPersistenceContextProvider provider,
        GameConfiguration configuration, int stageNumber, int level, bool vitality = false, bool gearFirst = false)
    {
        var player = new EconomyPlayer(context) { Account = provider.CreateNewContext(configuration).CreateNew<Account>() };
        await player.PlayerState.TryAdvanceToAsync(PlayerState.LoginScreen).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.Authenticated).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.CharacterSelection).ConfigureAwait(false);
        await new CreateCharacterAction().CreateCharacterAsync(player, "BudgetTest", stageNumber).ConfigureAwait(false);
        var character = player.Account.Characters.Single();
        var weights = stageNumber switch
        {
            0 => new[] { .05, .15, .15, .65, 0 },
            4 => [.55, .15, .25, .05, 0],
            8 => [.1, .6, .2, .1, 0],
            12 => [.5, .2, .2, .1, 0],
            16 => [.25, .1, .2, .1, .35],
            20 => [.05, .2, .2, .55, 0],
            _ when vitality => [.25, .2, .5, .05, 0],
            _ => [.45, .2, .3, .05, 0],
        };
        var points = (level - 1) * BalanceV1.PointsPerLevel;
        var allocations = weights.Select(weight => (int)(points * weight)).ToArray();
        allocations[Array.IndexOf(weights, weights.Max())] += points - allocations.Sum();
        if (gearFirst && stageNumber == 24 && level >= 80)
        {
            var firstGlove = configuration.Monsters.Single(monster => monster.Number == 251).MerchantStore!.Items
                .Single(item => item.Definition!.Group == 0 && item.Definition.Number == 32 && item.Level == 0);
            var baseStrength = character.Attributes.Single(attribute => attribute.Definition!.Id == Stats.BaseStrength.Id).Value;
            var requiredPoints = firstGlove.GetRequirement(firstGlove.Definition!.Requirements.Single(requirement => requirement.Attribute == Stats.TotalStrengthRequirementValue)).Value - (int)baseStrength;
            var redirected = Math.Max(0, requiredPoints - allocations[0]);
            Assert.That(allocations[2], Is.GreaterThanOrEqualTo(redirected));
            allocations[0] += redirected;
            allocations[2] -= redirected;
            Assert.That(allocations.Sum(), Is.EqualTo(points), "Gear-first vitality spends the same earned points, not extra stats.");
        }
        foreach (var attribute in character.Attributes)
        {
            var index = Array.FindIndex(AllocatedAttributes, allocated => allocated.Id == attribute.Definition!.Id);
            if (attribute.Definition!.Id == Stats.Level.Id)
            {
                attribute.Value = level;
            }
            else if (index >= 0)
            {
                attribute.Value += allocations[index];
            }
        }

        character.Experience = context.ExperienceTable[level];
        await player.SetSelectedCharacterAsync(character).ConfigureAwait(false);
        await player.PlayerState.TryAdvanceToAsync(PlayerState.EnteredWorld).ConfigureAwait(false);
        Assert.That(player.PlayerState.CurrentState, Is.EqualTo(PlayerState.EnteredWorld));
        return player;
    }

    /// <summary>The real merchant view emits exact serialized quotes, and the quoted wallet buys through the real action.</summary>
    [TestCase(253, 14, 4)]
    [TestCase(253, 14, 1)]
    [TestCase(254, 5, 0)]
    [TestCase(254, 15, 3)]
    [TestCase(251, 0, 32)]
    public async Task MerchantWireQuotesMatchStockAndActualPaymentAsync(short merchantNumber, int group, int number)
    {
        var (provider, configuration, context) = await CreateContextAsync().ConfigureAwait(false);
        await using var ownedContext = context;
        await using var player = await CreatePlayerAsync(context, provider, configuration, 0, 1).ConfigureAwait(false);
        var merchant = await OpenMerchantAsync(player, merchantNumber).ConfigureAwait(false);
        var stock = merchant.Definition.MerchantStore!.Items;
        var wanted = stock.First(item => item.Definition!.Group == group && item.Definition.Number == number
            && (group == 14 || item.Level == 0) && (group is not (0 or 5) || !item.ItemOptions.Any()));
        var wire = await RenderMerchantWireAsync(player, stock, StoreKind.Normal).ConfigureAwait(false);
        var quoteSize = BinaryPrimitives.ReadUInt16BigEndian(wire.AsSpan(1));
        Assert.That(wire[3], Is.EqualTo(MerchantPriceQuotePacket.Code));
        Assert.That(wire[5], Is.EqualTo(1));
        Assert.That(wire[6], Is.EqualTo(1));
        Assert.That(wire[7], Is.Zero);
        Assert.That(wire[8], Is.EqualTo(stock.Count));
        var offset = MerchantPriceQuotePacket.HeaderSize;
        uint wantedPrice = 0;
        var serializer = new ItemSerializerExtended();
        var calculator = new ItemPriceCalculator();
        var expectedStock = new List<byte>();
        foreach (var item in stock)
        {
            var serialized = new byte[serializer.NeededSpace];
            var length = serializer.SerializeItem(serialized, item);
            Assert.That(wire[offset], Is.EqualTo(item.ItemSlot));
            Assert.That(wire[offset + 1], Is.EqualTo(length));
            Assert.That(wire.AsSpan(offset + 2, length).ToArray(), Is.EqualTo(serialized[..length]));
            var price = BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(offset + 2 + length));
            Assert.That(price, Is.EqualTo(calculator.CalculateFinalBuyingPrice(item, configuration)));
            Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(offset + 6 + length)), Is.EqualTo(price));
            if (ReferenceEquals(item, wanted))
            {
                wantedPrice = price;
            }
            expectedStock.Add(item.ItemSlot);
            expectedStock.AddRange(serialized[..length]);
            offset += MerchantPriceQuotePacket.EntryOverhead + length;
        }
        Assert.That(offset, Is.EqualTo(quoteSize));
        Assert.That(wire[quoteSize], Is.EqualTo(0xC2));
        Assert.That(wire[quoteSize + 3], Is.EqualTo(0x31));
        Assert.That(wire[quoteSize + 5], Is.EqualTo(stock.Count));
        Assert.That(wire.AsSpan(quoteSize + 6).ToArray(), Is.EqualTo(expectedStock));
        Assert.That(wantedPrice, Is.GreaterThan(0));
        player.Money = (int)wantedPrice - 1;
        await new BuyNpcItemAction().BuyItemAsync(player, wanted.ItemSlot).ConfigureAwait(false);
        Assert.That(player.Money, Is.EqualTo(wantedPrice - 1));
        Assert.That(player.Inventory!.Items.Any(item => item.Definition == wanted.Definition), Is.False);
        player.Money = (int)wantedPrice;
        await new BuyNpcItemAction().BuyItemAsync(player, wanted.ItemSlot).ConfigureAwait(false);
        Assert.That(player.Money, Is.Zero);
        Assert.That(player.Inventory.Items.Single(item => item.Definition == wanted.Definition).Durability, Is.EqualTo(wanted.Durability));

        if (group == 14 && Environment.GetEnvironmentVariable("OPENMU_MERCHANT_QUOTE_FIXTURE") is { Length: > 0 } output)
        {
            await File.WriteAllBytesAsync(output, wire).ConfigureAwait(false);
            TestContext.Progress.WriteLine($"Production merchant quote/stock bytes exported to {output}.");
        }
    }

    /// <summary>Vanilla/Solo and non-merchant inventories never emit a price-profile packet.</summary>
    [TestCase("original")]
    [TestCase("solo")]
    [TestCase("both")]
    [TestCase("vault")]
    [TestCase("mix")]
    [TestCase("closed")]
    [TestCase("legacy-client")]
    [TestCase("pre-extended")]
    public async Task MerchantQuotesAreExcludedFromOtherProfilesAndStoragesAsync(string mode)
    {
        var (provider, configuration, context) = await CreateContextAsync().ConfigureAwait(false);
        await using var ownedContext = context;
        await using var player = await CreatePlayerAsync(context, provider, configuration, 0, 1).ConfigureAwait(false);
        var merchant = await OpenMerchantAsync(player, 254).ConfigureAwait(false);
        using var write = provider.CreateNewContext(configuration);
        if (mode is "original" or "solo")
        {
            configuration.GlobalBaseAttributeValues.Remove(configuration.GlobalBaseAttributeValues.Single(value => value.Definition.Id == BalanceV1.ProfileAttributeId));
        }
        if (mode is "solo" or "both")
        {
            var definition = write.CreateNew<AttributeDefinition>(SoloBalance.ProfileAttributeId, "Solo", "Test only");
            configuration.GlobalBaseAttributeValues.Add(write.CreateNew<ConstValueAttribute>(1, definition));
        }
        if (mode == "closed")
        {
            player.OpenedNpc = null;
        }
        var stock = mode == "vault" ? merchant.Definition.MerchantStore!.Items.ToList() : merchant.Definition.MerchantStore!.Items;
        var clientVersion = mode switch
        {
            "legacy-client" => new ClientVersion(6, 3, ClientLanguage.English),
            "pre-extended" => new ClientVersion(106, 2, ClientLanguage.English),
            _ => (ClientVersion?)null,
        };
        var wire = await RenderMerchantWireAsync(player, stock, mode == "mix" ? StoreKind.ChaosMachine : StoreKind.Normal, clientVersion).ConfigureAwait(false);
        Assert.That(wire[3], Is.EqualTo(0x31));
        Assert.That(BinaryPrimitives.ReadUInt16BigEndian(wire.AsSpan(1)), Is.EqualTo(wire.Length));
    }

    /// <summary>Independent full Season 6 configurations never require a custom quote to buy ordinary stock.</summary>
    [TestCase("original")]
    [TestCase("solo")]
    public async Task UnquotedProfilesKeepTheirActualMerchantPaymentAsync(string profile)
    {
        var (provider, configuration, context) = await CreateContextAsync(profile).ConfigureAwait(false);
        await using var ownedContext = context;
        await using var player = await CreatePlayerAsync(context, provider, configuration, 0, 1).ConfigureAwait(false);
        var merchant = await OpenMerchantAsync(player, 254).ConfigureAwait(false);
        var stock = merchant.Definition.MerchantStore!.Items;
        var offer = stock.First(item => item.Definition!.Group == 5 && item.Definition.Number == 0 && item.Level == 0);
        var wire = await RenderMerchantWireAsync(player, stock, StoreKind.Normal).ConfigureAwait(false);
        Assert.That(wire[3], Is.EqualTo(0x31));
        Assert.That(BinaryPrimitives.ReadUInt16BigEndian(wire.AsSpan(1)), Is.EqualTo(wire.Length));
        var calculator = new ItemPriceCalculator();
        var price = SoloBalance.ScalePrice(calculator.CalculateFinalBuyingPrice(offer, configuration), configuration);
        Assert.That(price, Is.InRange(1, int.MaxValue));
        player.Money = (int)price - 1;
        await new BuyNpcItemAction().BuyItemAsync(player, offer.ItemSlot).ConfigureAwait(false);
        Assert.That(player.Money, Is.EqualTo(price - 1));
        player.Money = (int)price;
        await new BuyNpcItemAction().BuyItemAsync(player, offer.ItemSlot).ConfigureAwait(false);
        Assert.That(player.Money, Is.Zero);
        Assert.That(player.Inventory!.Items.Any(item => item.Definition == offer.Definition), Is.True);
    }

    /// <summary>The tax quote is integer-identical to the actual percentage charge, without local rounding.</summary>
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public async Task MerchantQuotesPreserveExactTaxAndEnhancedStockAsync(int tax)
    {
        var (provider, configuration, context) = await CreateContextAsync().ConfigureAwait(false);
        await using var ownedContext = context;
        using var write = provider.CreateNewContext(configuration);
        var item = write.CreateNew<Item>();
        item.Definition = configuration.Items.Single(definition => definition.Group == 0 && definition.Number == 32);
        item.Level = 15;
        item.Durability = item.GetMaximumDurabilityOfOnePiece();
        var packet = MerchantPriceQuotePacket.Create(new[] { item }, new ItemSerializerExtended(), configuration, tax);
        var offset = MerchantPriceQuotePacket.HeaderSize + 2 + packet[MerchantPriceQuotePacket.HeaderSize + 1];
        var price = BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(offset));
        Assert.That(price, Is.EqualTo(9600));
        Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(offset + 4)), Is.EqualTo(price + ((price * tax) / 100)));
        Assert.That(() => MerchantPriceQuotePacket.Create(new[] { item, item }, new ItemSerializerExtended(), configuration, tax), Throws.ArgumentException);
        Assert.That(() => MerchantPriceQuotePacket.Create(new[] { item }, new ItemSerializerExtended(), configuration, 4), Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    /// <summary>Tax crossing the wallet ceiling cannot turn a high-priced offer into a cheap one.</summary>
    [Test]
    public async Task MerchantQuoteMarksUnpayableWalletBoundaryAsync()
    {
        var (provider, configuration, context) = await CreateContextAsync().ConfigureAwait(false);
        await using var ownedContext = context;
        using var write = provider.CreateNewContext(configuration);
        var definition = configuration.Items.First(item => item.Group == 15 && item.Number == 0);
        definition.Value = int.MaxValue;
        var item = write.CreateNew<Item>();
        item.Definition = definition;
        var calculator = new ItemPriceCalculator();
        var basePrice = calculator.CalculateFinalBuyingPrice(item, configuration);
        Assert.That(basePrice, Is.InRange(2_000_000_000L, int.MaxValue));
        var packet = MerchantPriceQuotePacket.Create(new[] { item }, new ItemSerializerExtended(), configuration, 3);
        var offset = MerchantPriceQuotePacket.HeaderSize + 2 + packet[MerchantPriceQuotePacket.HeaderSize + 1];
        Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(offset)), Is.EqualTo(uint.MaxValue));
        Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(offset + 4)), Is.EqualTo(uint.MaxValue));
        packet = MerchantPriceQuotePacket.Create(new[] { item }, new ItemSerializerExtended(), configuration, 0);
        Assert.That(BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(offset)), Is.EqualTo(basePrice));
    }

    private static async Task<byte[]> RenderMerchantWireAsync(Player source, ICollection<Item> stock, StoreKind kind, ClientVersion? version = null)
    {
        using var output = new MemoryStream();
        var writer = PipeWriter.Create(output, new StreamPipeWriterOptions(leaveOpen: true));
        var connection = new Mock<IConnection>();
        connection.SetupGet(value => value.Connected).Returns(true);
        connection.SetupGet(value => value.Output).Returns(writer);
        connection.SetupGet(value => value.OutputLock).Returns(new AsyncLock());
        var manager = new PlugInManager(null, NullLoggerFactory.Instance, null, null);
        manager.RegisterPlugIn<IViewPlugIn, ItemSerializer095>();
        manager.RegisterPlugIn<IViewPlugIn, ItemSerializerExtended>();
        var server = new Mock<IGameServerContext>();
        server.SetupGet(value => value.Configuration).Returns(source.GameContext.Configuration);
        server.SetupGet(value => value.PersistenceContextProvider).Returns(source.GameContext.PersistenceContextProvider);
        server.SetupGet(value => value.PlugInManager).Returns(manager);
        server.SetupGet(value => value.LoggerFactory).Returns(NullLoggerFactory.Instance);
        var remote = new RemotePlayer(server.Object, connection.Object, version ?? new ClientVersion(106, 3, ClientLanguage.English)) { OpenedNpc = source.OpenedNpc };
        await new ShowMerchantStoreItemListPlugIn(remote).ShowMerchantStoreItemListAsync(stock, kind).ConfigureAwait(false);
        await writer.CompleteAsync().ConfigureAwait(false);
        return output.ToArray();
    }

    private static async ValueTask<double> SampleMeanAsync(IAttacker attacker, IAttackable defender, SkillEntry? entry)
    {
        double sum = 0;
        for (var index = 0; index < 256; index++)
        {
            var damage = await attacker.CalculateDamageAsync(defender, entry, false).ConfigureAwait(false);
            sum += damage.HealthDamage + damage.ShieldDamage;
        }

        return sum / 256;
    }

    private static object EstimateSupplies(Player player, Monster monster, double incoming, double payload, double mpCost, bool reviewed)
    {
        var calculator = new ItemPriceCalculator();
        var hits = Math.Ceiling(monster.Attributes[Stats.MaximumHealth] / payload);
        var healthLoss = Math.Max(0, Math.Floor(hits / monster.Definition.AttackDelay.TotalSeconds) * incoming
            - Probe.RegenerationRate(player, Stats.HealthRegeneration) * hits);
        var manaLoss = Math.Max(0, hits * mpCost - Probe.RegenerationRate(player, Stats.ManaRegeneration) * hits);
        double UnitCost(BalanceV1.PotionGroup group, AttributeDefinition maximum)
        {
            return player.GameContext.Configuration.Items.Where(definition => BalanceV1.TryGetPotionRule(definition.Group, definition.Number, out var rule)
                && rule.CooldownGroup == group && rule.UnlockLevel <= player.Attributes![Stats.Level]).Min(definition =>
            {
                BalanceV1.TryGetPotionRule(definition.Group, definition.Number, out var rule);
                var item = player.PersistenceContext.CreateNew<Item>();
                item.Definition = definition;
                item.Durability = 1;
                var price = reviewed ? calculator.CalculateFinalBuyingPrice(item, player.GameContext.Configuration) : calculator.CalculateFinalBuyingPrice(item);
                return price / Math.Min(rule.RecoveryCap, player.Attributes![maximum] * rule.RecoveryFraction);
            });
        }

        var costs = healthLoss * UnitCost(BalanceV1.PotionGroup.Health, Stats.MaximumHealth)
            + manaLoss * UnitCost(BalanceV1.PotionGroup.Mana, Stats.MaximumMana);
        var gross = .65 * BalanceV1.CalculateZen(BalanceV1ContentRank.ResolveRank(monster), monster.Definition.Number);
        return new { Policy = "Steady-state lower-bound supply accounting, one action/second, no repair/travel/loot-sale income. Fractional potion amortization and post-fight recovery; NOT a win or timing proof.",
            Casts = hits, HealthToReplace = healthLoss, ManaToReplace = manaLoss, ExpectedZen = gross, PotionZen = costs, NetBeforeOtherSinks = gross - costs };
    }

    private static async Task<MerchantNpc> OpenMerchantAsync(Player player, short number)
    {
        var map = await player.GameContext.GetMapAsync(0).ConfigureAwait(false) ?? throw new InvalidOperationException();
        var spawn = map.Definition.MonsterSpawns.First(entry => entry.MonsterDefinition!.Number == number);
        var merchant = new MerchantNpc(spawn, spawn.MonsterDefinition!, map);
        player.OpenedNpc = merchant;
        return merchant;
    }

    private static async Task<(InMemoryPersistenceContextProvider Provider, GameConfiguration Configuration, GameContext Context)> CreateContextAsync(string profile = "balance")
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var read = provider.CreateNewConfigurationContext();
        var configuration = (await read.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        configuration.RecoveryInterval = int.MaxValue;
        using (var write = provider.CreateNewContext(configuration))
        {
            if (profile == "balance")
            {
                new BalanceV1Initializer(write, configuration, BalanceV1.ExperienceProfile.Standard).Initialize();
            }
            else if (profile == "solo")
            {
                new SoloBalanceInitializer(write, configuration).Initialize();
            }
            else if (profile != "original")
            {
                throw new ArgumentOutOfRangeException(nameof(profile));
            }
        }

        var types = typeof(DataInitialization).Assembly.GetTypes().Where(type => typeof(ICharacterCreatedPlugIn).IsAssignableFrom(type) && !type.IsAbstract)
            .Select(type => type.GUID).ToHashSet();
        var manager = new PlugInManager(configuration.PlugInConfigurations.Where(entry => types.Contains(entry.TypeId)).ToList(), NullLoggerFactory.Instance, null, null);
        var factory = new MapInitializer(configuration, NullLogger<MapInitializer>.Instance, NullDropGenerator.Instance, null);
        var initializer = new Mock<IMapInitializer>();
        initializer.Setup(value => value.CreateGameMap(It.IsAny<ushort>())).Returns((ushort number) => factory.CreateGameMap(number));
        initializer.Setup(value => value.InitializeStateAsync(It.IsAny<GameMap>())).Returns(ValueTask.CompletedTask);
        var context = new GameContext(configuration, provider, initializer.Object, NullLoggerFactory.Instance,
            manager, NullDropGenerator.Instance, new ConfigurationChangeMediator());
        return (provider, configuration, context);
    }

    private sealed class EconomyPlayer(IGameContext context) : Player(context)
    {
        protected override ICustomPlugInContainer<IViewPlugIn> CreateViewPlugInContainer() => new MockViewPlugInContainer();
    }
}
