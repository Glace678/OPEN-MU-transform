// <copyright file="SoloBalanceAttributeTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>Exercises the real character attributes used when entering a solo world.</summary>
[TestFixture]
public class SoloBalanceAttributeTests
{
    /// <summary>Every Season 6 class can enter using a fresh or legacy solo configuration.</summary>
    /// <param name="profile">The profile format to verify.</param>
    [TestCase("normal")]
    [TestCase("solo")]
    [TestCase("legacy-solo")]
    public async Task AllClassesCanConstructRuntimeAttributesAsync(string profile)
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new VersionSeasonSix.DataInitialization(provider, NullLoggerFactory.Instance)
            .CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var read = provider.CreateNewConfigurationContext();
        var configuration = (await read.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        using var context = provider.CreateNewContext(configuration);
        if (profile != "normal")
        {
            var initializer = new SoloBalanceInitializer(context, configuration);
            initializer.Initialize();
            initializer.Initialize();
        }

        if (profile == "legacy-solo")
        {
            var bonus = configuration.GlobalBaseAttributeValues.Single(attribute => attribute.Definition.Id == SoloBalance.LevelUpPointBonusAttributeId);
            configuration.GlobalBaseAttributeValues.Remove(bonus);
            configuration.GlobalBaseAttributeValues.Add(context.CreateNew<ConstValueAttribute>(3, Stats.PointsPerLevelUp, AggregateType.AddRaw));
        }

        var account = context.CreateNew<Account>();
        foreach (var characterClass in configuration.CharacterClasses)
        {
            var character = context.CreateNew<Character>();
            character.CharacterClass = characterClass;
            foreach (var stat in characterClass.StatAttributes)
            {
                character.Attributes.Add(context.CreateNew<StatAttribute>(stat.Attribute, stat.BaseValue));
            }

            var points = character.Attributes.Single(attribute => attribute.Definition == Stats.PointsPerLevelUp);
            var originalValue = points.Value;
            using (var attributes = new ItemAwareAttributeSystem(account, character, configuration))
            {
                Assert.That(attributes[Stats.PointsPerLevelUp], Is.EqualTo(originalValue), characterClass.Number.ToString());
                points.Value++;
                Assert.That(attributes[Stats.PointsPerLevelUp], Is.EqualTo(originalValue + 1), "Quest upgrades must remain mutable.");
            }

            using var reloaded = new ItemAwareAttributeSystem(account, character, configuration);
            Assert.That(reloaded[Stats.PointsPerLevelUp], Is.EqualTo(originalValue + 1), "Re-entering must not accumulate a persistent bonus.");
        }

        Assert.That(SoloBalance.GetLevelUpPointBonus(configuration), Is.EqualTo(profile == "normal" ? 0 : 3));
        if (profile == "solo")
        {
            Assert.That(configuration.GlobalBaseAttributeValues.Any(attribute => attribute.Definition == Stats.PointsPerLevelUp), Is.False);
        }
    }
}
