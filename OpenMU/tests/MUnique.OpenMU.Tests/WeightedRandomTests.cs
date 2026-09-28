// <copyright file="WeightedRandomTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.GameLogic;

/// <summary>Tests weighted selection used by item upgrades, crafting, and bot movement.</summary>
[TestFixture]
public class WeightedRandomTests
{
    /// <summary>Zero-weight entries never receive a share of a positive distribution.</summary>
    /// <param name="roll">The selected position in the distribution.</param>
    /// <param name="expected">The expected item.</param>
    [TestCase(0, "first")]
    [TestCase(1, "first")]
    [TestCase(2, "last")]
    [TestCase(4, "last")]
    public void WeightedBoundariesSelectTheExpectedItem(int roll, string expected)
    {
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(random => random.NextInt(0, 5)).Returns(roll);

        var result = new[] { "first", "never", "last" }.SelectWeightedRandom([2, 0, 3], randomizer.Object);

        Assert.That(result, Is.EqualTo(expected));
    }

    /// <summary>Lazy item and weight sources need only one enumeration, even for the uniform fallback.</summary>
    /// <param name="weight">The per-item weight.</param>
    [TestCase(0)]
    [TestCase(1)]
    public void SinglePassSourcesAreSupported(int weight)
    {
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(random => random.NextInt(0, 2)).Returns(1);
        var items = EnumerateOnce(new[] { "first", "last" });
        var weights = EnumerateOnce(new[] { weight, weight });

        Assert.That(items.SelectWeightedRandom(weights, randomizer.Object), Is.EqualTo("last"));
    }

    /// <summary>Large valid weights do not overflow the total and crash selection.</summary>
    [Test]
    public void LargeWeightsDoNotOverflow()
    {
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(random => random.NextDouble()).Returns(0.75);

        var result = new[] { "first", "last" }.SelectWeightedRandom([int.MaxValue, int.MaxValue], randomizer.Object);

        Assert.That(result, Is.EqualTo("last"));
    }

    /// <summary>Negative weights are invalid configuration, rather than a biased probability distribution.</summary>
    [Test]
    public void NegativeWeightsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new[] { 1, 2 }.SelectWeightedRandom([2, -1]));
    }

    private static IEnumerable<T> EnumerateOnce<T>(IEnumerable<T> source)
    {
        var enumerated = false;
        return Enumerate();

        IEnumerable<T> Enumerate()
        {
            if (enumerated)
            {
                throw new InvalidOperationException("The source was enumerated more than once.");
            }

            enumerated = true;
            foreach (var item in source)
            {
                yield return item;
            }
        }
    }
}
