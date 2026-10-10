// <copyright file="ObservableGameServerAdapterDisposeTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests;

using System.Reflection;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.Web.Map;

/// <summary>
/// Tests for <see cref="ObservableGameServerAdapter.Dispose"/> (issue 80-02): disposing the
/// adapter must symmetrically release the per-map adapters, i.e. unsubscribe from the long-lived
/// GameMap's ObjectAdded/ObjectRemoved events. Previously the outer `using` dispose never disposed
/// the inner map adapters, leaking an event callback (and player dictionary) per request.
/// </summary>
[TestFixture]
public class ObservableGameServerAdapterDisposeTests
{
    [Test]
    public async Task Dispose_UnsubscribesFromGameMapEvents()
    {
        // Arrange: a real GameMap (terrain falls back to the default when TerrainData is null).
        var gameMap = new GameMap(new GameMapDefinition { Number = 1, Name = "Test" }, TimeSpan.FromSeconds(5), 8);

        var context = new Mock<IGameServerContext>();
        context.Setup(c => c.GetMapsAsync())
            .Returns(new ValueTask<IEnumerable<GameMap>>(new[] { gameMap }));
        context.Setup(c => c.ForEachPlayerAsync(It.IsAny<Func<Player, Task>>()))
            .Returns(new ValueTask());

        var adapter = new ObservableGameServerAdapter(context.Object);
        await adapter.InitializeAsync().ConfigureAwait(false);

        // The map adapter subscribed to the GameMap events during InitializeAsync.
        Assert.That(GetObjectAddedHandlerCount(gameMap), Is.GreaterThan(0),
            "expected the per-map adapter to subscribe to GameMap.ObjectAdded");

        // Act
        adapter.Dispose();

        // Assert: disposal removed the subscription (no leak).
        Assert.That(GetObjectAddedHandlerCount(gameMap), Is.EqualTo(0),
            "disposing the adapter must unsubscribe from GameMap.ObjectAdded");
    }

    private static int GetObjectAddedHandlerCount(GameMap map)
    {
        var field = typeof(GameMap).GetField("ObjectAdded", BindingFlags.NonPublic | BindingFlags.Instance);
        var handler = field?.GetValue(map) as Delegate;
        return handler?.GetInvocationList().Length ?? 0;
    }
}