// <copyright file="BalanceV1ContentRank.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Collections.Frozen;
using System.Text.Json;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.GameLogic.NPC;
using PersistentIdentity = MUnique.OpenMU.Persistence.IIdentifiable;

/// <summary>
/// Resolves fixed normal-route ranks without modifying the original monster level or item tier.
/// Unmapped event/master content deliberately retains its original level for compatibility.
/// </summary>
public static class BalanceV1ContentRank
{
    /// <summary>The only difficulty currently installed in the runtime.</summary>
    public const string NormalDifficulty = "normal";

    private static readonly IReadOnlyList<Mapping> NormalMappings = LoadMappings();

    private static readonly FrozenDictionary<(Guid MapId, short MonsterNumber, string Difficulty), double> Ranks =
        NormalMappings.ToFrozenDictionary(
            mapping => (mapping.MapDefinitionId, mapping.MonsterNumber, mapping.Difficulty),
            mapping => mapping.Rank);

    /// <summary>Gets the immutable, versioned normal-route bindings used by initialization.</summary>
    public static IReadOnlyList<Mapping> Mappings => NormalMappings;

    /// <summary>Looks up a fixed rank by map identity, monster number and explicit difficulty.</summary>
    /// <param name="mapDefinitionId">The persistent map definition identity, not its display number.</param>
    /// <param name="monsterNumber">The original monster definition number.</param>
    /// <param name="difficulty">The fixed encounter difficulty; master variants are not installed.</param>
    /// <param name="rank">The mapped content rank, if found.</param>
    /// <returns>Whether an exact binding exists.</returns>
    public static bool TryGetRank(Guid mapDefinitionId, short monsterNumber, string difficulty, out double rank) =>
        Ranks.TryGetValue((mapDefinitionId, monsterNumber, difficulty), out rank);

    /// <summary>Resolves an actual target, retaining legacy levels for non-monsters and unknown bindings.</summary>
    /// <param name="target">The live target.</param>
    /// <returns>The fixed content rank or original level.</returns>
    public static double ResolveRank(IAttackable target)
    {
        if (!target.IsSummonedMonster
            && target is NonPlayerCharacter npc
            && npc.Definition.ObjectKind == NpcObjectKind.Monster
            && TryResolve(npc.Definition, npc.CurrentMap.Definition, out var rank))
        {
            return rank;
        }

        return target.Attributes[Stats.Level];
    }

    /// <summary>Resolves the content rank for the existing definition-based loot interface.</summary>
    /// <param name="monster">The original monster definition.</param>
    /// <param name="map">The encounter's map definition.</param>
    /// <returns>The fixed content rank or original level.</returns>
    public static double ResolveRank(MonsterDefinition monster, GameMapDefinition? map) =>
        monster.ObjectKind == NpcObjectKind.Monster && TryResolve(monster, map, out var rank)
            ? rank
            : monster[Stats.Level];

    /// <summary>Rejects a partial/incompatible configuration before the profile can be published.</summary>
    /// <param name="configuration">The complete Season 6 configuration.</param>
    public static void ValidateConfiguration(GameConfiguration configuration)
    {
        foreach (var mapping in NormalMappings)
        {
            var maps = configuration.Maps.Where(map => map is PersistentIdentity identity
                                                      && identity.Id == mapping.MapDefinitionId).ToList();
            var monsters = maps.Count == 1
                ? maps[0].MonsterSpawns.Select(spawn => spawn.MonsterDefinition)
                    .OfType<MonsterDefinition>().Where(monster => monster.Number == mapping.MonsterNumber
                                                                 && monster.ObjectKind == NpcObjectKind.Monster).Distinct().ToArray()
                : [];
            if (monsters.Length == 0)
            {
                throw new InvalidOperationException(
                    $"balance-v1 normal content binding {mapping.MapDefinitionId}/{mapping.MonsterNumber} "
                    + "requires exactly one map and an existing combat monster spawn.");
            }

            foreach (var monster in monsters)
            {
                var originalLevel = monster.Attributes.SingleOrDefault(attribute => attribute.AttributeDefinition?.Id == Stats.Level.Id)?.Value;
                if (originalLevel is not { } level || !float.IsFinite(level) || level < 1 || level > byte.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"balance-v1 normal content binding {mapping.MapDefinitionId}/{mapping.MonsterNumber} "
                        + "requires an original item-tier level in 1..255; the content rank must not replace Stats.Level.");
                }
            }
        }
    }

    private static bool TryResolve(MonsterDefinition monster, GameMapDefinition? map, out double rank)
    {
        rank = 0;
        return map is PersistentIdentity identity
               && TryGetRank(identity.Id, monster.Number, NormalDifficulty, out rank);
    }

    private static IReadOnlyList<Mapping> LoadMappings()
    {
        using var stream = typeof(BalanceV1ContentRank).Assembly.GetManifestResourceStream(
            "MUnique.OpenMU.GameLogic.Balance.NormalContentRanks.v1.json")
            ?? throw new InvalidOperationException("The balance-v1 normal content resource is missing.");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
        {
            throw new InvalidOperationException("Unsupported balance-v1 normal content schema.");
        }

        var mappings = root.GetProperty("mappings").EnumerateArray().Select(entry => new Mapping(
            entry.GetProperty("mapDefinitionId").GetGuid(),
            entry.GetProperty("monsterNumber").GetInt16(),
            entry.GetProperty("difficulty").GetString() ?? string.Empty,
            entry.GetProperty("rank").GetDouble())).ToArray();
        if (mappings.Length != 135
            || mappings.Any(mapping => mapping.MapDefinitionId == Guid.Empty
                                       || mapping.MonsterNumber < 0
                                       || mapping.Difficulty != NormalDifficulty
                                       || !double.IsFinite(mapping.Rank)
                                       || mapping.Rank < 1
                                       || mapping.Rank > BalanceV1.NormalLevelCap)
            || mappings.Select(mapping => (mapping.MapDefinitionId, mapping.MonsterNumber, mapping.Difficulty))
                .Distinct().Count() != mappings.Length)
        {
            throw new InvalidOperationException("balance-v1 requires exactly 135 unique, valid normal content bindings.");
        }

        return Array.AsReadOnly(mappings);
    }

    /// <summary>A fixed, immutable content binding. It contains no candidate combat attributes.</summary>
    /// <param name="MapDefinitionId">The persistent map definition identity.</param>
    /// <param name="MonsterNumber">The original monster definition number.</param>
    /// <param name="Difficulty">The explicit fixed difficulty.</param>
    /// <param name="Rank">The encounter rank, independent of player level.</param>
    public sealed record Mapping(Guid MapDefinitionId, short MonsterNumber, string Difficulty, double Rank);
}
