// <copyright file="RandTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using MUnique.OpenMU.GameLogic;

/// <summary>
/// Tests the random chance boundary calculations.
/// </summary>
[TestFixture]
public class RandTests
{
    /// <summary>
    /// Verifies that exactly <paramref name="chance"/> values in a zero-based range are successful.
    /// </summary>
    /// <param name="chance">The chance to verify.</param>
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(50)]
    [TestCase(99)]
    [TestCase(100)]
    public void ChanceUsesExclusiveUpperBound(int chance)
    {
        var successfulValues = Enumerable.Range(0, 100)
            .Count(randomValue => Rand.IsRandomValueWithinChance(randomValue, chance));

        Assert.That(successfulValues, Is.EqualTo(chance));
    }

    /// <summary>
    /// Verifies that a zero chance with a custom basis can never succeed.
    /// </summary>
    [Test]
    public void ZeroChanceWithCustomBasisNeverSucceeds()
    {
        Assert.That(Rand.NextRandomBool(0, 1), Is.False);
    }
}
