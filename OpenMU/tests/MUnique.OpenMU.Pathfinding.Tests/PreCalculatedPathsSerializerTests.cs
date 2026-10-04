// <copyright file="PreCalculatedPathsSerializerTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the repository root for full license information.
// </copyright>

namespace MUnique.OpenMU.Pathfinding.Tests;

using System.IO;
using MUnique.OpenMU.Pathfinding.PreCalculation;

/// <summary>
/// Round-trip tests for the compact and normal pre-calculated path serializers.
/// </summary>
[TestFixture]
public class PreCalculatedPathsSerializerTests
{
    private static readonly PathInfo[] SamplePaths =
    [
        new(new PointCombination(new Point(10, 20), new Point(12, 22)), new Point(11, 21)),
        new(new PointCombination(new Point(30, 40), new Point(33, 41)), new Point(31, 40)),
        new(new PointCombination(new Point(200, 200), new Point(202, 203)), new Point(201, 201)),
    ];

    /// <summary>The normal format must round-trip every record, including the last.</summary>
    [Test]
    public void NormalSerializerRoundTripsAllRecords()
    {
        var serializer = new NormalPathsSerializer();
        using var stream = new MemoryStream();
        serializer.Serialize(SamplePaths, stream);
        stream.Position = 0;

        var restored = serializer.Deserialize(stream).ToArray();

        Assert.That(restored, Has.Length.EqualTo(SamplePaths.Length));
        AssertPathEqual(restored[0], SamplePaths[0]);
        AssertPathEqual(restored[^1], SamplePaths[^1]);
    }

    /// <summary>The compact format must round-trip offsets within its bounded range.</summary>
    [Test]
    public void CompactSerializerRoundTripsAllRecords()
    {
        var compactPaths = new[]
        {
            new PathInfo(new PointCombination(new Point(10, 20), new Point(12, 22)), new Point(11, 21)),
            new PathInfo(new PointCombination(new Point(30, 40), new Point(33, 41)), new Point(31, 40)),
        };
        var serializer = new CompactPathsSerializer();
        using var stream = new MemoryStream();
        serializer.Serialize(compactPaths, stream);
        stream.Position = 0;

        var restored = serializer.Deserialize(stream).ToArray();

        Assert.That(restored, Has.Length.EqualTo(compactPaths.Length));
        AssertPathEqual(restored[0], compactPaths[0]);
        AssertPathEqual(restored[^1], compactPaths[^1]);
    }

    private static void AssertPathEqual(PathInfo actual, PathInfo expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(actual.Combination.Start, Is.EqualTo(expected.Combination.Start));
            Assert.That(actual.Combination.End, Is.EqualTo(expected.Combination.End));
            Assert.That(actual.NextStep, Is.EqualTo(expected.NextStep));
        });
    }
}
