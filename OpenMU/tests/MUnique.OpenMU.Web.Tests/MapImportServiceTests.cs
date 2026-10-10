// <copyright file="MapImportServiceTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Tests;

using System.Reflection;
using System.Text.Json;
using System.Threading;
using Moq;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Web.Shared.Components.MapEditor;

/// <summary>
/// Tests for <see cref="MapExportImportService.ApplyImportAsync"/> (issue 80-48): the import must
/// fully validate the payload before deleting any existing spawn, so a rejected import leaves the
/// current map untouched and can be retried.
/// </summary>
[TestFixture]
public class MapImportServiceTests
{
    private const int KnownMonsterNumber = 100;

    [Test]
    public async Task ApplyImport_WithUnknownMonsterNumber_LeavesExistingSpawnsUntouched()
    {
        var existingSpawn = new MonsterSpawnArea();
        var map = NewMap(existingSpawn);

        var json = Serialize(new MapSpawnExport
        {
            Spawns = { new SpawnExport { MonsterNumber = 999, X1 = 0, Y1 = 0, X2 = 10, Y2 = 10 } },
        });

        var deleted = new List<MonsterSpawnArea>();
        var context = new Mock<IContext>();
        context.Setup(c => c.GetAsync<MonsterDefinition>(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IEnumerable<MonsterDefinition>>(KnownMonsters()));
        context.Setup(c => c.CreateNew<MonsterSpawnArea>()).Returns(() => new MonsterSpawnArea());
        context.Setup(c => c.DeleteAsync(It.IsAny<MonsterSpawnArea>()))
            .Callback<MonsterSpawnArea>(s => deleted.Add(s))
            .ReturnsAsync(true);

        var result = await new MapExportImportService().ApplyImportAsync(map, json, context.Object).ConfigureAwait(false);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.Not.Null.And.Contains("999"));
        Assert.That(deleted, Is.Empty, "existing spawns must not be deleted when validation fails");
        Assert.That(map.MonsterSpawns, Has.Exactly(1).EqualTo(existingSpawn));
    }

    [Test]
    public async Task ApplyImport_WithInvertedRectangle_LeavesExistingSpawnsUntouched()
    {
        var existingSpawn = new MonsterSpawnArea();
        var map = NewMap(existingSpawn);

        var json = Serialize(new MapSpawnExport
        {
            Spawns = { new SpawnExport { MonsterNumber = KnownMonsterNumber, X1 = 20, Y1 = 0, X2 = 10, Y2 = 10 } },
        });

        var deleted = new List<MonsterSpawnArea>();
        var context = new Mock<IContext>();
        context.Setup(c => c.GetAsync<MonsterDefinition>(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IEnumerable<MonsterDefinition>>(KnownMonsters()));
        context.Setup(c => c.CreateNew<MonsterSpawnArea>()).Returns(() => new MonsterSpawnArea());
        context.Setup(c => c.DeleteAsync(It.IsAny<MonsterSpawnArea>()))
            .Callback<MonsterSpawnArea>(s => deleted.Add(s))
            .ReturnsAsync(true);

        var result = await new MapExportImportService().ApplyImportAsync(map, json, context.Object).ConfigureAwait(false);

        Assert.That(result.Success, Is.False);
        Assert.That(deleted, Is.Empty);
        Assert.That(map.MonsterSpawns, Has.Exactly(1).EqualTo(existingSpawn));
    }

    [Test]
    public async Task ApplyImport_WithInvalidJson_DoesNotTouchExistingMap()
    {
        var existingSpawn = new MonsterSpawnArea();
        var map = NewMap(existingSpawn);

        var deleted = new List<MonsterSpawnArea>();
        var context = new Mock<IContext>();
        context.Setup(c => c.DeleteAsync(It.IsAny<MonsterSpawnArea>()))
            .Callback<MonsterSpawnArea>(s => deleted.Add(s))
            .ReturnsAsync(true);

        var result = await new MapExportImportService().ApplyImportAsync(map, "{ not valid json ", context.Object).ConfigureAwait(false);

        Assert.That(result.Success, Is.False);
        Assert.That(deleted, Is.Empty);
        Assert.That(map.MonsterSpawns, Has.Exactly(1).EqualTo(existingSpawn));
    }

    [Test]
    public async Task ApplyImport_WithValidPayload_ReplacesSpawns()
    {
        var existingSpawn = new MonsterSpawnArea();
        var map = NewMap(existingSpawn);

        var json = Serialize(new MapSpawnExport
        {
            Spawns =
            {
                new SpawnExport { MonsterNumber = KnownMonsterNumber, X1 = 0, Y1 = 0, X2 = 10, Y2 = 10 },
                new SpawnExport { MonsterNumber = KnownMonsterNumber, X1 = 5, Y1 = 5, X2 = 15, Y2 = 15 },
            },
        });

        var context = new Mock<IContext>();
        context.Setup(c => c.GetAsync<MonsterDefinition>(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IEnumerable<MonsterDefinition>>(KnownMonsters()));
        context.Setup(c => c.CreateNew<MonsterSpawnArea>()).Returns(() => new MonsterSpawnArea());
        context.Setup(c => c.DeleteAsync(It.IsAny<MonsterSpawnArea>())).ReturnsAsync(true);

        var result = await new MapExportImportService().ApplyImportAsync(map, json, context.Object).ConfigureAwait(false);

        Assert.That(result.Success, Is.True);
        Assert.That(result.ImportedCount, Is.EqualTo(2));
        Assert.That(map.MonsterSpawns, Has.Exactly(2).Items);
        CollectionAssert.DoesNotContain(map.MonsterSpawns, existingSpawn);
    }

    private static GameMapDefinition NewMap(params MonsterSpawnArea[] existing)
    {
        var map = new GameMapDefinition { Number = 1, Name = "Test" };
        SetCollection(map, nameof(map.MonsterSpawns), new List<MonsterSpawnArea>(existing));
        return map;
    }

    private static List<MonsterDefinition> KnownMonsters() =>
        new() { new MonsterDefinition { Number = KnownMonsterNumber } };

    private static void SetCollection(object entity, string property, object value)
    {
        var prop = entity.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!;
        prop.SetValue(entity, value);
    }

    private static string Serialize(MapSpawnExport dto) =>
        JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
}