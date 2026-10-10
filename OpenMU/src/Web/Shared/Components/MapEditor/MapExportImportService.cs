// <copyright file="MapExportImportService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.Shared.Components.MapEditor;

using System.Text.Json;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Handles the export and import of map object data,
/// converting between domain objects and JSON.
/// </summary>
public sealed class MapExportImportService
{
    /// <summary>
    /// Builds a JSON export string from the objects in the given map.
    /// </summary>
    /// <param name="map">The map whose spawns and gates are to be exported.</param>
    /// <returns>A JSON string representing all map objects.</returns>
    public string BuildExport(GameMapDefinition map)
    {
        var dto = new MapSpawnExport();

        foreach (var spawn in map.MonsterSpawns)
        {
            dto.Spawns.Add(new SpawnExport
            {
                Id = spawn.GetId(),
                X1 = spawn.X1,
                Y1 = spawn.Y1,
                X2 = spawn.X2,
                Y2 = spawn.Y2,
                Direction = spawn.Direction,
                Quantity = spawn.Quantity,
                SpawnTrigger = spawn.SpawnTrigger,
                WaveNumber = spawn.WaveNumber,
                MaximumHealthOverride = spawn.MaximumHealthOverride,
                MonsterNumber = spawn.MonsterDefinition?.Number ?? 0,
            });
        }

        foreach (var gate in map.ExitGates)
        {
            dto.ExitGates.Add(new ExitGateExport
            {
                Id = gate.GetId(),
                X1 = gate.X1,
                Y1 = gate.Y1,
                X2 = gate.X2,
                Y2 = gate.Y2,
                Direction = gate.Direction,
                IsSpawnGate = gate.IsSpawnGate,
            });
        }

        foreach (var gate in map.EnterGates)
        {
            dto.EnterGates.Add(new EnterGateExport
            {
                Id = gate.GetId(),
                X1 = gate.X1,
                Y1 = gate.Y1,
                X2 = gate.X2,
                Y2 = gate.Y2,
                LevelRequirement = gate.LevelRequirement,
                Number = gate.Number,
                TargetGateId = gate.TargetGate?.GetId(),
            });
        }

        return JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Applies a JSON import to the given map, replacing all monster spawns with those parsed
    /// from the JSON. The whole payload is validated before any existing spawn is deleted, so a
    /// rejected import leaves the current map untouched and can be retried with a corrected file.
    /// Gates are preserved to avoid breaking warp entries and cross-map references.
    /// </summary>
    /// <param name="map">The map whose spawns will be replaced.</param>
    /// <param name="json">The JSON string containing the replacement spawns.</param>
    /// <param name="context">The persistence context used for create and delete operations.</param>
    /// <returns>The result of the import; on failure the map was not modified.</returns>
    public async Task<MapImportResult> ApplyImportAsync(GameMapDefinition map, string json, IContext context)
    {
        MapSpawnExport? dto;
        try
        {
            dto = JsonSerializer.Deserialize<MapSpawnExport>(json);
        }
        catch (JsonException ex)
        {
            return MapImportResult.Failure($"The import file is not valid JSON: {ex.Message}");
        }

        if (dto is null)
        {
            return MapImportResult.Failure("The import file is empty.");
        }

        if (dto.FormatVersion != "1.0")
        {
            return MapImportResult.Failure($"Unsupported import format version '{dto.FormatVersion}'.");
        }

        if (dto.Spawns is null)
        {
            return MapImportResult.Failure("The import file does not contain a spawn list.");
        }

        var monsters = (await context.GetAsync<MonsterDefinition>().ConfigureAwait(false)).ToList();

        // Validate the whole payload BEFORE touching any existing data. Deleting first and then
        // failing used to silently drop every existing spawn when a monster number was unknown or
        // the rectangle was invalid, with no error surfaced to the caller.
        var unknownMonsterNumbers = dto.Spawns
            .Where(spawnDto => !monsters.Any(m => m.Number == spawnDto.MonsterNumber))
            .Select(spawnDto => spawnDto.MonsterNumber)
            .Distinct()
            .ToList();
        if (unknownMonsterNumbers.Count > 0)
        {
            return MapImportResult.Failure(
                $"The import references unknown monster number(s) {string.Join(", ", unknownMonsterNumbers)}. No existing spawns were modified.");
        }

        var invalidRectangle = dto.Spawns.FirstOrDefault(spawn => spawn.X1 > spawn.X2 || spawn.Y1 > spawn.Y2);
        if (invalidRectangle is not null)
        {
            return MapImportResult.Failure(
                $"A spawn area has inverted coordinates (X1={invalidRectangle.X1}, X2={invalidRectangle.X2}, Y1={invalidRectangle.Y1}, Y2={invalidRectangle.Y2}). No existing spawns were modified.");
        }

        // All checks passed: it is now safe to replace the spawn set.
        foreach (var spawn in map.MonsterSpawns.ToList())
        {
            map.MonsterSpawns.Remove(spawn);
            await context.DeleteAsync(spawn).ConfigureAwait(false);
        }

        foreach (var spawnDto in dto.Spawns)
        {
            var monsterDef = monsters.First(m => m.Number == spawnDto.MonsterNumber);

            var spawn = context.CreateNew<MonsterSpawnArea>();
            spawn.X1 = spawnDto.X1;
            spawn.Y1 = spawnDto.Y1;
            spawn.X2 = spawnDto.X2;
            spawn.Y2 = spawnDto.Y2;
            spawn.Direction = spawnDto.Direction;
            spawn.Quantity = spawnDto.Quantity;
            spawn.SpawnTrigger = spawnDto.SpawnTrigger;
            spawn.WaveNumber = spawnDto.WaveNumber;
            spawn.MaximumHealthOverride = spawnDto.MaximumHealthOverride;
            spawn.MonsterDefinition = monsterDef;
            spawn.GameMap = map;
            map.MonsterSpawns.Add(spawn);
        }

        return MapImportResult.SuccessResult(dto.Spawns.Count);
    }
}