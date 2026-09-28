using System.Text;
using System.Text.Json;

namespace OpenMu.Balance;

public sealed record ContentMonster(string ContentKey, string Route, int MapId, string MapDefinitionId,
    string MapName, int MonsterId, string Name, bool MasterInstance, double ContentRank, string Kind,
    int OriginalSpawnCount, string[] SpawnTriggers, MonsterStats ProposedStats, string Status);
public sealed record CoverageReport(int OriginalMonsters, int OriginalMaps, int OriginalSkills, int OriginalItems,
    int MappedMonsterVariants, int ProtectedOrUnmappedSpawns, int ExplicitSkillOverrides, int SkillsNeedingMapping,
    string[] Errors);

public static class ContentCompiler
{
    public static CoverageReport Compile(Rules rules, string catalogFile, string output, string policyFile)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(catalogFile));
        using var policies = JsonDocument.Parse(File.ReadAllText(policyFile));
        var typeBudgets = policies.RootElement.GetProperty("skillTypeBudgets");
        var utilities = policies.RootElement.GetProperty("utilitySkills").EnumerateArray().ToArray();
        var root = document.RootElement;
        var d = rules.Design;
        var monsters = root.GetProperty("monsters").EnumerateArray().ToDictionary(m => m.GetProperty("id").GetInt32());
        var maps = root.GetProperty("maps").EnumerateArray().ToArray();
        var skills = root.GetProperty("skills").EnumerateArray().ToArray();
        var items = root.GetProperty("items").EnumerateArray().ToArray();
        var errors = new List<string>();
        var result = new List<ContentMonster>();
        var excluded = new List<object>();
        foreach (var map in maps)
        {
            var id = map.GetProperty("id").GetInt32();
            var name = map.GetProperty("name").GetString()!;
            var definitionId = map.TryGetProperty("definitionId", out var guid) ? guid.GetString()! : $"{id}:{name}";
            var spawns = map.GetProperty("spawns").EnumerateArray().ToArray();
            var eligible = spawns.GroupBy(s => s.GetProperty("monsterId").GetInt32())
                .Where(g => monsters.TryGetValue(g.Key, out var monster)
                    && monster.GetProperty("kind").GetString() == "Monster"
                    && monster.GetProperty("hp").GetDouble() > 0 && monster.GetProperty("level").GetDouble() >= 1)
                .ToArray();
            var routes = d.Routes.Where(r => r.MapIds.Contains(id)).ToArray();
            if (routes.Length == 0)
            {
                foreach (var s in spawns)
                    excluded.Add(new { mapId = id, definitionId, mapName = name,
                        monsterId = s.GetProperty("monsterId").GetInt32(),
                        status = "preserve-until-event-quest-or-special-content-reviewed" });
                continue;
            }
            var protectedIds = new[] { 460, 461, 462 }; // Selupan eggs are event mechanics, not farm monsters.
            var farm = eligible.Where(g => !protectedIds.Contains(g.Key)).ToArray();
            if (farm.Length == 0) { errors.Add($"Route map has no eligible monsters: {id}/{name}"); continue; }
            var levels = farm.Select(g => monsters[g.Key].GetProperty("level").GetDouble()).ToArray();
            foreach (var route in routes)
            foreach (var spawn in farm)
            {
                var original = monsters[spawn.Key];
                var originalLevel = original.GetProperty("level").GetDouble();
                var fraction = levels.Max() == levels.Min() ? 1 : (originalLevel - levels.Min()) / (levels.Max() - levels.Min());
                var rank = Math.Round(route.MinRank + fraction * (route.MaxRank - route.MinRank), 2);
                var kind = d.Monsters.BossIds.Contains(spawn.Key) ? "boss" : d.Monsters.EliteIds.Contains(spawn.Key) ? "elite" : "normal";
                result.Add(new ContentMonster($"{route.Id}:{definitionId}:{spawn.Key}", route.Id, id, definitionId,
                    name, spawn.Key, original.GetProperty("name").GetString()!, route.Master, rank, kind,
                    spawn.Sum(s => s.GetProperty("count").GetInt32()),
                    spawn.Select(s => s.GetProperty("trigger").GetString()!).Distinct().ToArray(),
                    rules.Monster(rank, kind), route.Master ? "requires-isolated-master-instance" : "candidate-requires-runtime-adapter"));
            }
            foreach (var s in spawns.Where(s => !farm.Any(g => g.Key == s.GetProperty("monsterId").GetInt32())))
                excluded.Add(new { mapId = id, definitionId, mapName = name,
                    monsterId = s.GetProperty("monsterId").GetInt32(), status = "protected-npc-trap-or-event-mechanic" });
        }
        foreach (var route in d.Routes)
        foreach (var id in route.MapIds)
            if (!maps.Any(m => m.GetProperty("id").GetInt32() == id)) errors.Add($"Missing map ID: {id}");
        foreach (var id in d.Monsters.BossIds.Concat(d.Monsters.EliteIds))
            if (!monsters.ContainsKey(id)) errors.Add($"Unknown explicit rank ID: {id}");
        var explicitSkills = d.Skills.SelectMany(s => s.EngineIds).ToHashSet();
        foreach (var id in explicitSkills)
            if (!skills.Any(s => s.GetProperty("id").GetInt32() == id)) errors.Add($"Unknown skill ID: {id}");
        foreach (var potion in d.Economy.Potions)
            if (!items.Any(i => i.GetProperty("group").GetInt32() == potion.Group && i.GetProperty("id").GetInt32() == potion.Number))
                errors.Add($"Unknown potion: {potion.Group}:{potion.Number}");
        if (result.Select(r => r.ContentKey).Distinct().Count() != result.Count) errors.Add("Duplicate content instance key");
        var skillPolicy = skills.Select(s =>
        {
            var id = s.GetProperty("id").GetInt32();
            var template = d.Skills.SingleOrDefault(t => t.EngineIds.Contains(id));
            var type = s.GetProperty("type").GetString()!;
            var utility = utilities.FirstOrDefault(u => u.GetProperty("engineIds").EnumerateArray().Any(i => i.GetInt32() == id));
            if (!typeBudgets.TryGetProperty(type, out var typeBudget)) errors.Add($"Unbudgeted skill type: {type}/{id}");
            return new
            {
                engineId = id, name = s.GetProperty("name").GetString(),
                status = template is null ? "type-budget-defined-runtime-effect-or-master-node-binding-required"
                    : template.RuntimeContract is null ? "candidate-model-only-runtime-contract-unverified"
                    : "candidate-model-with-reviewed-original-runtime-contract-not-a-damage-installation",
                template = template?.Id,
                coefficient = template?.Coefficient,
                runtimeContract = template?.RuntimeContract,
                candidateManaPolicy = template is null ? null : new { template.ManaFraction, template.ManaFlat },
                master = s.GetProperty("master").GetBoolean(),
                typeBudget = typeBudget.ValueKind == JsonValueKind.Undefined ? (JsonElement?)null : typeBudget,
                utilityBudget = utility.ValueKind == JsonValueKind.Undefined ? (JsonElement?)null : utility,
            };
        }).ToArray();
        var itemPolicy = items.Select(i => new
        {
            group = i.GetProperty("group").GetInt32(), id = i.GetProperty("id").GetInt32(),
            name = i.GetProperty("name").GetString(),
            potion = d.Economy.Potions.SingleOrDefault(p => p.Group == i.GetProperty("group").GetInt32() && p.Number == i.GetProperty("id").GetInt32()),
            familyBudget = i.GetProperty("group").GetInt32() <= 11 ? policies.RootElement.GetProperty("equipment") : (JsonElement?)null,
            status = d.Economy.Potions.Any(p => p.Group == i.GetProperty("group").GetInt32() && p.Number == i.GetProperty("id").GetInt32())
                ? "explicit-price-and-recovery-override"
                : "family-budget-defined-item-requirements-affixes-and-client-mirror-need-review",
        });
        var report = new CoverageReport(monsters.Count, maps.Length, skills.Length, items.Length,
            result.Count, excluded.Count, explicitSkills.Count, skills.Length - explicitSkills.Count, errors.ToArray());
        Write("content-monsters.json", result);
        Write("protected-content.json", excluded);
        Write("skill-coverage.json", skillPolicy);
        Write("item-coverage.json", itemPolicy);
        Write("content-coverage.json", report);
        File.WriteAllLines(Path.Combine(output, "monster-changes.csv"),
            new[] { "route,map_id,map_name,monster_id,name,master,content_rank,kind,hp,damage,armor,xp,zen,spawn_count" }
            .Concat(result.Select(m => $"{m.Route},{m.MapId},{Quote(m.MapName)},{m.MonsterId},{Quote(m.Name)},{m.MasterInstance},{m.ContentRank},{m.Kind},{m.ProposedStats.Health:F2},{m.ProposedStats.Damage:F2},{m.ProposedStats.Armor:F2},{m.ProposedStats.Experience:F2},{m.ProposedStats.Zen},{m.OriginalSpawnCount}")),
            new UTF8Encoding(true));
        return report;

        void Write(string name, object value) =>
            File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, Design.Json), new UTF8Encoding(false));
        static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
