// <copyright file="AncientSetsTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.CashShop;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests the initialized ancient set values.
/// </summary>
[TestFixture]
internal class AncientSetsTests
{
    /// <summary>Verifies the full initialized profile, shop packing and repeat-start protection.</summary>
    [Test]
    public async Task SoloProfileIsCompleteAndIdempotentAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory())
            .CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var read = provider.CreateNewConfigurationContext();
        var configuration = (await read.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var originalStoreItems = configuration.Monsters.Where(m => m.MerchantStore is not null)
            .SelectMany(m => m.MerchantStore!.Items).ToHashSet();
        var originalHealth = configuration.Monsters.Where(m => m.ObjectKind == NpcObjectKind.Monster)
            .ToDictionary(m => m.Number, m => m.Attributes.FirstOrDefault(a => a.AttributeDefinition == Stats.MaximumHealth)?.Value ?? 0);
        using var context = provider.CreateNewContext(configuration);
        var initializer = new SoloBalanceInitializer(context, configuration);
        initializer.Initialize();
        var values = configuration.Monsters.SelectMany(m => m.Attributes).Select(a => a.Value).ToArray();
        var itemCount = configuration.Monsters.Sum(m => m.MerchantStore?.Items.Count ?? 0);
        initializer.Initialize();
        await context.SaveChangesAsync().ConfigureAwait(false);

        Assert.Multiple(() =>
        {
            Assert.That(SoloBalance.IsEnabled(configuration), Is.True);
            Assert.That(configuration.ExperienceRate, Is.EqualTo(8));
            Assert.That(configuration.MasterExperienceRate, Is.EqualTo(5));
            Assert.That(configuration.ShouldDropMoney, Is.False);
            Assert.That(configuration.Monsters.SelectMany(m => m.Attributes).Select(a => a.Value), Is.EqualTo(values));
            Assert.That(configuration.Monsters.Sum(m => m.MerchantStore?.Items.Count ?? 0), Is.EqualTo(itemCount));
            Assert.That(SoloBalance.ScalePrice(9_000_000, configuration), Is.EqualTo(9000));
            Assert.That(SoloBalance.ScalePrice(0, configuration), Is.Zero);
            Assert.That(SoloBalance.ScalePrice(30, configuration), Is.EqualTo(1));
        });
        foreach (var monster in configuration.Monsters.Where(m => m.ObjectKind == NpcObjectKind.Monster))
        {
            var health = monster.Attributes.FirstOrDefault(a => a.AttributeDefinition == Stats.MaximumHealth)?.Value ?? 0;
            Assert.That(health, Is.LessThanOrEqualTo(originalHealth[monster.Number]), monster.ToString());
            Assert.That(health, Is.LessThanOrEqualTo(300000), monster.ToString());
        }

        var offers = SoloCashShopCatalog.GetOffers(configuration);
        Assert.That(offers.Count, Is.InRange(1, SoloCashShopCatalog.MaximumOffers));
        Assert.That(offers.Select(o => o.Id).Distinct().Count(), Is.EqualTo(offers.Count));
        Assert.That(offers.All(o => o.Price is >= 1 and <= 20 && o.Durability is >= 1 and <= 255), Is.True);
        foreach (var game in configuration.MiniGameDefinitions.Where(g => g.TicketItem is not null))
        {
            Assert.That(offers.Any(o => o.Definition == game.TicketItem && o.Level == game.TicketItemLevel), Is.True, game.Name.ToString());
        }
        TestContext.Out.WriteLine($"Solo catalog: {offers.Count} permanent offers, {configuration.Monsters.Count} NPC/monster definitions.");

        foreach (var store in configuration.Monsters.Select(m => m.MerchantStore).Where(s => s is not null))
        {
            var occupied = new HashSet<int>();
            foreach (var item in store!.Items.OrderBy(i => originalStoreItems.Contains(i) ? 0 : 1))
            {
                for (var y = 0; y < item.Definition!.Height; y++)
                {
                    for (var x = 0; x < item.Definition.Width; x++)
                    {
                        var slot = item.ItemSlot + (y * 8) + x;
                        var isNewSlot = occupied.Add(slot);
                        if (!originalStoreItems.Contains(item))
                        {
                            Assert.That(slot, Is.LessThan(120));
                            Assert.That(isNewSlot, Is.True, $"Overlapping new shop item: {item}");
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Verifies that the Kantata set grants ten percent excellent damage chance.
    /// </summary>
    [Test]
    public async Task KantataExcellentDamageChanceIsTenPercentAsync()
    {
        var contextProvider = new InMemoryPersistenceContextProvider();
        var dataInitialization = new VersionSeasonSix.DataInitialization(contextProvider, new NullLoggerFactory());
        await dataInitialization.CreateInitialDataAsync(1, false).ConfigureAwait(false);

        using var context = contextProvider.CreateNewConfigurationContext();
        var gameConfiguration = (await context.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        var kantataSet = gameConfiguration.ItemSetGroups.Single(set => set.Name.Value == "Kantata");
        var excellentDamageChance = kantataSet.Options!.PossibleOptions.Single(option =>
            option.PowerUpDefinition?.TargetAttribute?.Id == Stats.ExcellentDamageChance.Id);

        Assert.Multiple(() =>
        {
            Assert.That(excellentDamageChance.PowerUpDefinition!.Boost!.ConstantValue.Value, Is.EqualTo(0.10f));
            Assert.That(excellentDamageChance.PowerUpDefinition.Boost.ConstantValue.AggregateType, Is.EqualTo(AggregateType.AddRaw));
        });
    }
}
