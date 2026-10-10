// <copyright file="SoloCombatBalanceSeason6UpdatePlugIn.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Updates;

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence.Initialization.CharacterClasses;
using MUnique.OpenMU.PlugIns;

/// <summary>
/// Replays the full-segment R4 combat balance (B1 monster targets + B2 class constants) onto existing
/// Season 6 databases, so an old, never-dropped installation ends up identical to a fresh initialization.
/// Target tables are embedded resources (MonsterTargets.json / ClassTargets.json) produced by the
/// numbers shard; all writes are absolute and therefore idempotent. Relationships are aligned on
/// (target, input) instead of being appended a second time.
/// </summary>
[PlugIn]
[Display(Name = PlugInName, Description = PlugInDescription)]
[Guid("7A3C1D2E-5F60-4B8A-9C1D-2E3F4A5B6C7E")]
public class SoloCombatBalanceSeason6UpdatePlugIn : UpdatePlugInBase
{
    internal const string PlugInName = "Solo Combat Balance (Season 6)";

    internal const string PlugInDescription = "Replays the full-segment R4 monster targets and DK/DW/FE/MG/DL/Summoner/RF base damage constants onto existing Season 6 databases.";

    private static readonly Guid MaximumHealthId = new("A6C39A5C-295F-415E-A314-5E9F9A748D27");
    private static readonly Guid MinimumPhysBaseDmgId = new("3E8D6A02-E973-4AE4-9DF3-CDDC3D3183B3");
    private static readonly Guid MaximumPhysBaseDmgId = new("8A918EA2-893A-48B2-A684-3E71526CA71F");
    private static readonly Guid DefenseBaseId = new("EB098C46-60D4-4CA6-BBD4-5B6270A1407B");
    private static readonly Guid AttackRatePvmId = new("1129442A-E1C7-4240-8866-B781C2838C25");
    private static readonly Guid DefenseRatePvmId = new("C520DD2D-1B06-4392-95EE-3C41F33E68DA");

    // Unlock classes that only receive base constants (their existing relationships are left unchanged per B2).
    private static readonly IReadOnlyDictionary<string, short> UnlockClassNumbers = new Dictionary<string, short>
    {
        ["Magic Gladiator"] = 12,
        ["Dark Lord"] = 16,
        ["Summoner"] = 20,
        ["Rage Fighter"] = 24,
    };

    private static readonly IReadOnlyDictionary<string, AttributeDefinition> StatByName = new Dictionary<string, AttributeDefinition>
    {
        ["MinimumPhysBaseDmg"] = Stats.MinimumPhysBaseDmg,
        ["MaximumPhysBaseDmg"] = Stats.MaximumPhysBaseDmg,
        ["MinimumWizBaseDmg"] = Stats.MinimumWizBaseDmg,
        ["MaximumWizBaseDmg"] = Stats.MaximumWizBaseDmg,
        ["AttackRatePvm"] = Stats.AttackRatePvm,
        ["DefenseRatePvm"] = Stats.DefenseRatePvm,
    };

    /// <inheritdoc />
    public override UpdateVersion Version => UpdateVersion.SoloCombatBalanceSeason6;

    /// <inheritdoc />
    public override string DataInitializationKey => VersionSeasonSix.DataInitialization.Id;

    /// <inheritdoc />
    public override string Name => PlugInName;

    /// <inheritdoc />
    public override string Description => PlugInDescription;

    /// <inheritdoc />
    public override bool IsMandatory => true;

    /// <inheritdoc />
    public override DateTime CreatedAt => new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc />
    protected override ValueTask ApplyAsync(IContext context, GameConfiguration gameConfiguration)
    {
        var soloEnabled = SoloBalance.IsEnabled(gameConfiguration);
        this.ReplayMonsterTargets(gameConfiguration, soloEnabled);
        this.ReplayClassConstants(context, gameConfiguration);
        this.AlignCharacterDamageRelationships(context, gameConfiguration);
        return ValueTask.CompletedTask;
    }

    private static JsonDocument LoadEmbeddedJson(string fileName)
    {
        var assembly = typeof(SoloCombatBalanceSeason6UpdatePlugIn).Assembly;
        var resourceName = assembly.GetManifestResourceNames().First(n => n.EndsWith(fileName, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        return JsonDocument.Parse(stream);
    }

    private static float Frac(JsonElement monster, string property) =>
        monster.TryGetProperty(property, out var value) ? value.GetSingle() : 0f;

    private static void SetMonsterAttribute(MonsterDefinition monster, Guid attributeId, float value)
    {
        var attribute = monster.Attributes.FirstOrDefault(a => a.AttributeDefinition?.Id == attributeId);
        if (attribute is not null)
        {
            attribute.Value = value;
        }
    }

    private static void SetBaseValue(IContext context, GameConfiguration gameConfiguration, CharacterClass characterClass, AttributeDefinition stat, float value)
    {
        var definition = stat.GetPersistent(gameConfiguration) ?? stat;
        var existing = characterClass.BaseAttributeValues.FirstOrDefault(attr => attr.Definition == definition);
        if (existing is not null)
        {
            characterClass.BaseAttributeValues.Remove(existing);
        }

        characterClass.BaseAttributeValues.Add(context.CreateNew<ConstValueAttribute>(value, definition, AggregateType.AddRaw));
    }

    private static void AlignRelationship(IContext context, GameConfiguration gameConfiguration, CharacterClass characterClass, AttributeDefinition target, AttributeDefinition input, float operand)
    {
        var targetDefinition = target.GetPersistent(gameConfiguration) ?? target;
        var inputDefinition = input.GetPersistent(gameConfiguration) ?? input;
        var existing = characterClass.AttributeCombinations
            .FirstOrDefault(rel => rel.TargetAttribute == targetDefinition && rel.InputAttribute == inputDefinition);
        if (existing is not null)
        {
            existing.InputOperand = operand;
            return;
        }

        characterClass.AttributeCombinations.Add(
            CharacterClassHelper.CreateAttributeRelationship(context, gameConfiguration, target, operand, input));
    }

    private void ReplayMonsterTargets(GameConfiguration gameConfiguration, bool soloEnabled)
    {
        using var doc = LoadEmbeddedJson("MonsterTargets.json");
        var hpCol = soloEnabled ? "PostHp" : "PreHp";
        var minAtkCol = soloEnabled ? "PostMinAtk" : "PreMinAtk";
        var maxAtkCol = soloEnabled ? "PostMaxAtk" : "PreMaxAtk";
        var defCol = soloEnabled ? "PostDefBase" : "PreDefBase";
        var defRateCol = soloEnabled ? "PostDefRatePvm" : "PreDefRatePvm";
        var atkRateCol = soloEnabled ? "PostAtkRatePvm" : "PreAtkRatePvm";

        foreach (var monsterJson in doc.RootElement.EnumerateArray())
        {
            var number = (short)monsterJson.GetProperty("Num").GetInt32();
            var monster = gameConfiguration.Monsters.FirstOrDefault(m => m.Number == number);
            if (monster is null)
            {
                continue;
            }

            SetMonsterAttribute(monster, MaximumHealthId, Frac(monsterJson, hpCol));
            SetMonsterAttribute(monster, MinimumPhysBaseDmgId, Frac(monsterJson, minAtkCol));
            SetMonsterAttribute(monster, MaximumPhysBaseDmgId, Frac(monsterJson, maxAtkCol));
            SetMonsterAttribute(monster, DefenseBaseId, Frac(monsterJson, defCol));
            SetMonsterAttribute(monster, DefenseRatePvmId, Frac(monsterJson, defRateCol));
            SetMonsterAttribute(monster, AttackRatePvmId, Frac(monsterJson, atkRateCol));
        }
    }

    private void ReplayClassConstants(IContext context, GameConfiguration gameConfiguration)
    {
        using var doc = LoadEmbeddedJson("ClassTargets.json");
        var root = doc.RootElement;

        // DK / DW / FE: constBaseValues already carry the exact attribute names.
        foreach (var cls in root.GetProperty("classes").EnumerateArray())
        {
            var characterClass = gameConfiguration.CharacterClasses.FirstOrDefault(c => c.Number == cls.GetProperty("number").GetInt32());
            if (characterClass is null)
            {
                continue;
            }

            foreach (var prop in cls.GetProperty("constBaseValues").EnumerateObject())
            {
                if (StatByName.TryGetValue(prop.Name, out var stat))
                {
                    SetBaseValue(context, gameConfiguration, characterClass, stat, prop.Value.GetSingle());
                }
            }
        }

        // MG / DL / Summoner / RF: only base constants, existing relationships left unchanged.
        var unlock = root.GetProperty("patchForUnlockClasses");
        foreach (var prop in unlock.EnumerateObject())
        {
            if (!UnlockClassNumbers.TryGetValue(prop.Name, out var number))
            {
                continue;
            }

            var characterClass = gameConfiguration.CharacterClasses.FirstOrDefault(c => c.Number == number);
            if (characterClass is null)
            {
                continue;
            }

            var isWiz = string.Equals(prop.Value.GetProperty("dmgStat").GetString(), "wiz", StringComparison.OrdinalIgnoreCase);
            var minAttr = isWiz ? Stats.MinimumWizBaseDmg : Stats.MinimumPhysBaseDmg;
            var maxAttr = isWiz ? Stats.MaximumWizBaseDmg : Stats.MaximumPhysBaseDmg;
            SetBaseValue(context, gameConfiguration, characterClass, minAttr, prop.Value.GetProperty("constMin").GetSingle());
            SetBaseValue(context, gameConfiguration, characterClass, maxAttr, prop.Value.GetProperty("constMax").GetSingle());
            SetBaseValue(context, gameConfiguration, characterClass, Stats.AttackRatePvm, prop.Value.GetProperty("atkRatePvm").GetSingle());
            SetBaseValue(context, gameConfiguration, characterClass, Stats.DefenseRatePvm, prop.Value.GetProperty("defRatePvm").GetSingle());
        }
    }

    private void AlignCharacterDamageRelationships(IContext context, GameConfiguration gameConfiguration)
    {
        foreach (var characterClass in gameConfiguration.CharacterClasses)
        {
            switch (characterClass.Number)
            {
                case 4 or 6 or 7: // Dark Knight lineages
                    AlignRelationship(context, gameConfiguration, characterClass, Stats.MinimumPhysBaseDmg, Stats.TotalStrength, 0.7f);
                    AlignRelationship(context, gameConfiguration, characterClass, Stats.MaximumPhysBaseDmg, Stats.TotalStrength, 1.05f);
                    break;

                case 0 or 2 or 3: // Dark Wizard lineages
                    AlignRelationship(context, gameConfiguration, characterClass, Stats.MinimumWizBaseDmg, Stats.TotalEnergy, 0.6f);
                    AlignRelationship(context, gameConfiguration, characterClass, Stats.MaximumWizBaseDmg, Stats.TotalEnergy, 1.0f);
                    break;

                case 8 or 10 or 11: // Fairy Elf lineages
                    AlignRelationship(context, gameConfiguration, characterClass, Stats.ArcheryMinDmg, Stats.TotalAgility, 0.5f);
                    AlignRelationship(context, gameConfiguration, characterClass, Stats.ArcheryMinDmg, Stats.TotalStrength, 0.25f);
                    AlignRelationship(context, gameConfiguration, characterClass, Stats.ArcheryMaxDmg, Stats.TotalAgility, 0.85f);
                    AlignRelationship(context, gameConfiguration, characterClass, Stats.ArcheryMaxDmg, Stats.TotalStrength, 0.45f);
                    AlignRelationship(context, gameConfiguration, characterClass, Stats.MeleeMinDmg, Stats.TotalStrengthAndAgility, 0.45f);
                    AlignRelationship(context, gameConfiguration, characterClass, Stats.MeleeMaxDmg, Stats.TotalStrengthAndAgility, 0.8f);
                    break;
            }
        }
    }
}