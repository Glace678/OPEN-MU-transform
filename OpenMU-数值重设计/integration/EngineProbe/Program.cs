using System.Security.Cryptography;
using System.Text.Json;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.InMemory;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;

// This executable creates a transient configuration only. No database or server is opened.
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: EngineProbe <output-directory>");
    return 2;
}

var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var provider = new InMemoryPersistenceContextProvider();
using var context = provider.CreateNewConfigurationContext();
var config = context.CreateNew<GameConfiguration>();
new GameConfigurationInitializer(context, config).Initialize();

var catalog = new
{
    generatedUtc = DateTimeOffset.UtcNow,
    kind = "real-s6-in-memory-initialization-not-live-database",
    engineAssemblies = new[] { typeof(GameConfigurationInitializer).Assembly, typeof(GameConfiguration).Assembly }
        .Select(a => new { name = a.GetName().Name, version = a.GetName().Version?.ToString(),
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))) }),
    monsters = config.Monsters.OrderBy(m => m.Number).Select(m => new
    {
        id = m.Number, name = m.Designation.ToString(), kind = m.ObjectKind.ToString(),
        level = Read(m, Stats.Level), hp = Read(m, Stats.MaximumHealth),
        minDamage = Read(m, Stats.MinimumPhysBaseDmg), maxDamage = Read(m, Stats.MaximumPhysBaseDmg),
        armor = Read(m, Stats.DefenseBase), accuracy = Read(m, Stats.AttackRatePvm), evasion = Read(m, Stats.DefenseRatePvm),
        attackSeconds = m.AttackDelay.TotalSeconds, respawnSeconds = m.RespawnDelay.TotalSeconds,
        dropSlots = m.NumberOfMaximumItemDrops,
    }),
    maps = config.Maps.OrderBy(m => m.Number).Select(m => new
    {
        id = m.Number, definitionId = m.GetId(), name = m.Name.ToString(),
        spawns = m.MonsterSpawns.Where(s => s.MonsterDefinition is not null).Select(s => new
        {
            monsterId = s.MonsterDefinition!.Number,
            count = s.Quantity,
            trigger = s.SpawnTrigger.ToString(),
        }),
    }),
    skills = config.Skills.OrderBy(s => s.Number).Select(s => new
    {
        id = s.Number, name = s.Name.ToString(), type = s.SkillType.ToString(),
        damage = s.AttackDamage,
        master = s.MasterDefinition is not null,
    }),
    items = config.Items.OrderBy(i => i.Group).ThenBy(i => i.Number).Select(i => new
    {
        group = i.Group, id = i.Number, name = i.Name.ToString(), dropLevel = i.DropLevel,
        value = i.Value, slot = i.ItemSlot?.ToString(), skillId = i.Skill?.Number,
    }),
    classes = config.CharacterClasses.OrderBy(c => c.Number).Select(c => new
    {
        id = c.Number, name = c.Name.ToString(), master = c.IsMasterClass, creatable = c.CanGetCreated,
    }),
};

File.WriteAllText(Path.Combine(output, "engine-catalog.json"),
    JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Transient S6 catalog: {config.CharacterClasses.Count} classes, {config.Monsters.Count} monsters/NPCs, " +
    $"{config.Maps.Count} maps, {config.Skills.Count} skills, {config.Items.Count} items.");
Console.WriteLine("No save, database connection, network listener or game configuration write was performed.");
return 0;

static float Read(MonsterDefinition monster, MUnique.OpenMU.AttributeSystem.AttributeDefinition stat) =>
    monster.Attributes.FirstOrDefault(a => a.AttributeDefinition == stat)?.Value ?? 0;
