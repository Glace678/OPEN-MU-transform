// <copyright file="BalanceV1InitializerTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Configuration.ItemCrafting;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Tests installation and strict validation of the balance-v1 profile.
/// </summary>
[TestFixture]
public class BalanceV1InitializerTests
{
    /// <summary>Verifies the profile installs atomically, validates repeatedly, and rejects mixing.</summary>
    [Test]
    public async Task CompleteProfileIsIdempotentAndMutuallyExclusiveAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new VersionSeasonSix.DataInitialization(provider, new NullLoggerFactory())
            .CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var read = provider.CreateNewConfigurationContext();
        var configuration = (await read.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        using var context = provider.CreateNewContext(configuration);
        var initializer = new BalanceV1Initializer(context, configuration, BalanceV1.ExperienceProfile.Relaxed);

        initializer.Initialize();
        initializer.Initialize();

        Assert.Multiple(() =>
        {
            Assert.That(BalanceV1.IsEnabled(configuration), Is.True);
            Assert.That(configuration.MaximumLevel, Is.EqualTo(BalanceV1.NormalLevelCap));
            Assert.That(configuration.MaximumMasterLevel, Is.EqualTo(BalanceV1.MasterLevelCap));
            Assert.That(configuration.ExperienceRate, Is.EqualTo(1.5f));
            Assert.That(configuration.MasterExperienceRate, Is.EqualTo(1.5f));
            Assert.That(configuration.CharacterClasses, Has.Count.EqualTo(18));
            Assert.That(
                configuration.CharacterClasses.SelectMany(value => value.StatAttributes)
                    .Where(value => value.Attribute?.Id == Stats.PointsPerLevelUp.Id)
                    .Select(value => value.BaseValue),
                Is.All.EqualTo(BalanceV1.PointsPerLevel));
            Assert.That(
                () => BalanceV1Initializer.ValidateCoreConfiguration(configuration, BalanceV1.ExperienceProfile.Standard),
                Throws.TypeOf<InvalidOperationException>().With.Message.Contains("conversion is not automatic"));
        });

        var chaosMachine = configuration.Monsters.Single(value => value.NpcWindow == NpcWindow.ChaosMachine);
        var expectedUpgrades = new[] { (3, 10), (4, 11), (22, 12), (23, 13), (49, 14), (50, 15) };
        foreach (var (craftingNumber, targetLevel) in expectedUpgrades)
        {
            var settings = chaosMachine.ItemCraftings.Single(value => value.Number == craftingNumber).SimpleCraftingSettings!;
            var expectedChance = (byte)Math.Round(BalanceV1.GetUpgradeStep(targetLevel).Chance * 100);
            Assert.That(settings.SuccessPercent, Is.EqualTo(expectedChance), $"+{targetLevel}");
            Assert.That(settings.MaximumSuccessPercent, Is.EqualTo(expectedChance), $"+{targetLevel}");
            Assert.That(
                settings.RequiredItems.Single(value => value.Reference > 0 && value.MinimumItemLevel == targetLevel - 1).FailResult,
                Is.EqualTo(MixResult.StaysAsIs),
                $"+{targetLevel}");
        }

        var soloMarker = context.CreateNew<AttributeDefinition>(SoloBalance.ProfileAttributeId, "Solo", string.Empty);
        configuration.GlobalBaseAttributeValues.Add(context.CreateNew<ConstValueAttribute>(1, soloMarker, AggregateType.AddRaw));
        Assert.That(
            () => initializer.Initialize(),
            Throws.TypeOf<InvalidOperationException>().With.Message.Contains("mutually exclusive"));
    }
}
