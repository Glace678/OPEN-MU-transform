// <copyright file="EarlyGameBalanceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the repository root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic;

/// <summary>
/// Tests the profile-independent early-game combat guarantee.
/// </summary>
[TestFixture]
public class EarlyGameBalanceTests
{
    /// <summary>A fresh character keeps the guaranteed hit chance floor.</summary>
    /// <param name="level">The player level.</param>
    [TestCase(1)]
    [TestCase(10)]
    [TestCase(20)]
    public void HitChanceIsFlooredForNewPlayers(int level)
    {
        var collapsedLegacyChance = 0.03f;

        var effective = EarlyGameBalance.AdjustHitChance(level, collapsedLegacyChance);

        Assert.That(effective, Is.EqualTo(EarlyGameBalance.MinimumHitChance));
    }

    /// <summary>The floor does not lower an already good hit chance.</summary>
    [Test]
    public void GoodHitChanceIsPreserved()
    {
        var goodLegacyChance = 0.9f;

        var effective = EarlyGameBalance.AdjustHitChance(1, goodLegacyChance);

        Assert.That(effective, Is.EqualTo(goodLegacyChance));
    }

    /// <summary>Above the protected band the legacy formula is used untouched.</summary>
    [Test]
    public void LegacyHitChanceReturnsAfterProtectedBand()
    {
        var legacyChance = 0.03f;

        var effective = EarlyGameBalance.AdjustHitChance(EarlyGameBalance.ProtectedLevel + 1, legacyChance);

        Assert.That(effective, Is.EqualTo(legacyChance));
    }

    /// <summary>The overrate penalty is waived while the player is in the early band.</summary>
    /// <param name="level">The player level.</param>
    [TestCase(1)]
    [TestCase(40)]
    public void OverratePenaltyIsWaivedEarly(int level)
    {
        Assert.That(EarlyGameBalance.GetOverrateMultiplier(level), Is.EqualTo(1.0f));
    }

    /// <summary>The legacy overrate penalty returns past the waiver level.</summary>
    [Test]
    public void OverratePenaltyReturnsAfterWaiver()
    {
        var multiplier = EarlyGameBalance.GetOverrateMultiplier(EarlyGameBalance.OverrateWaiverLevel + 1);

        Assert.That(multiplier, Is.EqualTo(EarlyGameBalance.LegacyOverrateMultiplier));
    }
}
