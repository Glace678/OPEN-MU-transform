// <copyright file="GameMapTerrainTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Tests;

using Moq;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Pathfinding;

/// <summary>
/// Tests for the terrain lookups which are used to get a player off a tile it cannot stand on.
/// </summary>
[TestFixture]
public class GameMapTerrainTests
{
    private const byte Walkable = 0;
    private const byte Safezone = 1;
    private const byte Blocked = 4;

    /// <summary>The documented fallback terrain covers all 256 by 256 coordinates.</summary>
    [Test]
    public void DefaultTerrainIncludesTheEntireMap()
    {
        var terrain = new GameMapTerrain((byte[]?)null);

        Assert.That(terrain.WalkMap.Cast<bool>().All(walkable => walkable), Is.True);
        Assert.That(terrain.AIgrid[255, 255], Is.EqualTo(1));
    }

    /// <summary>Random coordinates include the last row and column at the map edge.</summary>
    [Test]
    public void RandomDropCanReachTheLastMapCoordinate()
    {
        var terrain = new GameMapTerrain(CreateTerrainData(null, (255, 255)));
        var randomizer = new Mock<IRandomizer>();
        randomizer.Setup(random => random.NextInt(It.IsAny<int>(), It.IsAny<int>()))
            .Returns((int min, int max) => max - 1);

        Assert.That(terrain.GetRandomCoordinate(new Point(254, 254), 1, randomizer.Object), Is.EqualTo(new Point(255, 255)));
    }

    /// <summary>A walkable result on the final permitted attempt is still used.</summary>
    [Test]
    public void FinalRandomAttemptIsNotDiscarded()
    {
        var terrain = new GameMapTerrain(CreateTerrainData(null, (1, 1)));
        var randomizer = new Mock<IRandomizer>();
        var calls = 0;
        randomizer.Setup(random => random.NextInt(0, 2)).Returns(() => ++calls <= 40 ? 0 : 1);

        Assert.That(terrain.GetRandomCoordinate(new Point(0, 0), 1, randomizer.Object), Is.EqualTo(new Point(1, 1)));
    }

    /// <summary>
    /// Tests that a map whose walkable tiles are all safezone still yields a coordinate.
    /// <see cref="GameMapTerrain.RandomWalkableCoordinate"/> samples the monster spawn points, which
    /// exclude the safezone by construction, so it returns nothing here - which would leave a player
    /// stranded on a blocked tile of a town.
    /// </summary>
    [Test]
    public void AnyWalkableCoordinateIsFoundOnASafezoneOnlyMap()
    {
        var terrain = new GameMapTerrain(CreateTerrainData(safezoneAt: (10, 10), walkableAt: null));

        Assert.That(terrain.RandomWalkableCoordinate, Is.Null, "precondition: the map has no monster spawn point");
        Assert.That(terrain.AnyWalkableCoordinate, Is.EqualTo(new Pathfinding.Point(10, 10)));
    }

    /// <summary>
    /// Tests that the safezone is preferred over an ordinary walkable tile, so a player who is
    /// recovered from a blocked spawn gate ends up in town rather than in a hunting ground.
    /// </summary>
    [Test]
    public void AnyWalkableCoordinatePrefersTheSafezone()
    {
        // The walkable tile comes first in scan order, so a naive "first walkable tile" would pick it.
        var terrain = new GameMapTerrain(CreateTerrainData(safezoneAt: (20, 20), walkableAt: (5, 5)));

        Assert.That(terrain.AnyWalkableCoordinate, Is.EqualTo(new Pathfinding.Point(20, 20)));
    }

    /// <summary>
    /// Tests that a map without a single walkable tile reports that honestly, so the caller can log
    /// it instead of moving the player somewhere impossible.
    /// </summary>
    [Test]
    public void AnyWalkableCoordinateIsNullWhenNothingIsWalkable()
    {
        var terrain = new GameMapTerrain(CreateTerrainData(safezoneAt: null, walkableAt: null));

        Assert.That(terrain.AnyWalkableCoordinate, Is.Null);
    }

    /// <summary>
    /// Creates fully blocked terrain data with at most one safezone and one ordinary walkable tile.
    /// </summary>
    /// <param name="safezoneAt">The coordinate to mark as safezone, if any.</param>
    /// <param name="walkableAt">The coordinate to mark as walkable but outside the safezone, if any.</param>
    /// <returns>The terrain data, including its three byte header.</returns>
    private static byte[] CreateTerrainData((byte X, byte Y)? safezoneAt, (byte X, byte Y)? walkableAt)
    {
        var data = new byte[(256 * 256) + 3];
        Array.Fill(data, Blocked, 3, 256 * 256);

        if (safezoneAt is { } safezone)
        {
            data[3 + (safezone.Y * 256) + safezone.X] = Safezone;
        }

        if (walkableAt is { } walkable)
        {
            data[3 + (walkable.Y * 256) + walkable.X] = Walkable;
        }

        return data;
    }
}
