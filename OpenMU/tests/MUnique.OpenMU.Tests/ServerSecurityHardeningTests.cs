// <copyright file="ServerSecurityHardeningTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration.Items;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.PlugIns.PeriodicTasks;

/// <summary>
/// Tests covering Milestone 2 security and anti-cheat hardening fixes.
/// </summary>
[TestFixture]
public class ServerSecurityHardeningTests
{
    /// <summary>
    /// Tests that SkillHitValidator rejects hits with missing animation.
    /// </summary>
    [Test]
    public void SkillHitValidator_MissingAnimation_RejectsHit()
    {
        var validator = new SkillHitValidator(NullLogger.Instance);
        var (isValid, _) = validator.IsHitValid(9, 1, 1);
        Assert.That(isValid, Is.False, "Hit without prior animation must be rejected.");
    }

    /// <summary>
    /// Tests that SkillHitValidator accepts a registered animation and matching hit.
    /// </summary>
    [Test]
    public void SkillHitValidator_ValidAnimationAndHit_AcceptsHit()
    {
        var validator = new SkillHitValidator(NullLogger.Instance);
        var regOk = validator.TryRegisterAnimation(9, 1);
        Assert.That(regOk, Is.True);

        var (isValid, increaseCounter) = validator.IsHitValid(9, 1, 2);
        Assert.That(isValid, Is.True, "Matching hit should be valid.");
        Assert.That(increaseCounter, Is.True);
    }

    /// <summary>
    /// Tests that SkillHitValidator rejects hits with wrong skill id.
    /// </summary>
    [Test]
    public void SkillHitValidator_WrongSkillId_RejectsHit()
    {
        var validator = new SkillHitValidator(NullLogger.Instance);
        validator.TryRegisterAnimation(9, 1);

        var (isValid, _) = validator.IsHitValid(8, 1, 1);
        Assert.That(isValid, Is.False, "Hit with mismatched skill id must be rejected.");
    }

    /// <summary>
    /// Tests that SkillHitValidator rejects out-of-sync hit counters.
    /// </summary>
    [Test]
    public void SkillHitValidator_CounterOutOfSync_RejectsHit()
    {
        var validator = new SkillHitValidator(NullLogger.Instance);
        validator.TryRegisterAnimation(9, 1);

        var (isValid, _) = validator.IsHitValid(9, 1, 5);
        Assert.That(isValid, Is.False, "Hit with out-of-sync hit counter must be rejected.");
    }

    /// <summary>
    /// Tests that SkillHitValidator rejects counter exceeding maximum value.
    /// </summary>
    [Test]
    public void SkillHitValidator_CounterOutOfRange_RejectsHit()
    {
        var validator = new SkillHitValidator(NullLogger.Instance);
        var (isValid, _) = validator.IsHitValid(9, 255, 255);
        Assert.That(isValid, Is.False, "Out of range counters must be safely rejected.");
    }

    /// <summary>
    /// Tests that ItemPriceCalculator calculates packed jewels without 32-bit integer overflow.
    /// </summary>
    [Test]
    public void ItemPriceCalculator_PackedJewels_CalculatesWithoutOverflow()
    {
        var calculator = new ItemPriceCalculator();

        var itemDefinitionMock = new Mock<ItemDefinition>();
        itemDefinitionMock.SetupAllProperties();
        var itemDefinition = itemDefinitionMock.Object;
        itemDefinition.Group = 12;
        itemDefinition.Number = 136; // PackedLife

        var itemMock = new Mock<Item>();
        itemMock.SetupAllProperties();
        itemMock.Setup(i => i.ItemOptions).Returns(new List<ItemOptionLink>());
        itemMock.Setup(i => i.ItemSetGroups).Returns(new List<ItemOfItemSet>());
        var item = itemMock.Object;
        item.Definition = itemDefinition;
        item.Level = 4; // 5 * 2,950 * 10 = 147,500L

        var price = calculator.CalculateFinalBuyingPrice(item);
        Assert.That(price, Is.EqualTo(147_500L), "Packed life price must match deflated jewel price calculation.");
    }

    /// <summary>
    /// Tests that HappyHourPlugIn does not stack duplicate attribute multipliers on repeat world entries.
    /// </summary>
    [Test]
    public async Task HappyHourPlugIn_DoesNotDuplicateMultiplierAsync()
    {
        var plugin = new HappyHourPlugIn();
        var player = await PlayerTestHelper.CreatePlayerAsync().ConfigureAwait(false);
        var initialRate = player.Attributes![Stats.ExperienceRate];

        // Simulate transition CharacterSelection -> EnteredWorld
        await plugin.PlayerStateChangedAsync(player, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);
        var rate1 = player.Attributes[Stats.ExperienceRate];

        // Simulate returning to CharacterSelection
        await plugin.PlayerStateChangedAsync(player, PlayerState.EnteredWorld, PlayerState.CharacterSelection).ConfigureAwait(false);
        var rateAfterLeave = player.Attributes[Stats.ExperienceRate];
        Assert.That(rateAfterLeave, Is.EqualTo(initialRate), "Multiplier should be removed when leaving EnteredWorld.");

        // Simulate entering world again
        await plugin.PlayerStateChangedAsync(player, PlayerState.CharacterSelection, PlayerState.EnteredWorld).ConfigureAwait(false);
        var rate2 = player.Attributes[Stats.ExperienceRate];

        Assert.That(rate2, Is.EqualTo(rate1), "Multiplier must not stack or duplicate upon re-entering world.");
    }
}
