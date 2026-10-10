// <copyright file="SoloCombatBalanceUpdateTests.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.Initialization.Tests;

using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MUnique.OpenMU.AttributeSystem;
using MUnique.OpenMU.DataModel.Configuration;
using MUnique.OpenMU.DataModel.Entities;
using MUnique.OpenMU.GameLogic;
using MUnique.OpenMU.GameLogic.Attributes;
using MUnique.OpenMU.Persistence;
using MUnique.OpenMU.Persistence.Initialization.Updates;
using MUnique.OpenMU.Persistence.Initialization.VersionSeasonSix;
using MUnique.OpenMU.Persistence.InMemory;

/// <summary>
/// Drives v110 <see cref="SoloCombatBalanceSeason6UpdatePlugIn"/> from a drifted Season 6 config and
/// proves the full-segment B1/B2 targets are replayed verbatim, that a second run is idempotent, and
/// that a migrated database matches a fresh initialization. Also verifies solo-path TTK per segment.
/// </summary>
[TestFixture]
public class SoloCombatBalanceUpdateTests
{
    private static readonly Guid MaximumHealthId = new("A6C39A5C-295F-415E-A314-5E9F9A748D27");
    private static readonly Guid MinimumPhysBaseDmgId = new("3E8D6A02-E973-4AE4-9DF3-CDDC3D3183B3");
    private static readonly Guid MaximumPhysBaseDmgId = new("8A918EA2-893A-48B2-A684-3E71526CA71F");
    private static readonly Guid DefenseBaseId = new("EB098C46-60D4-4CA6-BBD4-5B6270A1407B");
    private static readonly Guid AttackRatePvmId = new("1129442A-E1C7-4240-8866-B781C2838C25");
    private static readonly Guid DefenseRatePvmId = new("C520DD2D-1B06-4392-95EE-3C41F33E68DA");

    [Test]
    public async Task NormalSeason6ReplaysFullSegmentMatchesFreshAndIsIdempotentAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var read = provider.CreateNewConfigurationContext();
        var configuration = (await read.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        using var context = provider.CreateNewContext(configuration);

        // Drift a few monsters and the DK coefficient to simulate an old, unmigrated DB.
        DriftOldSeason6(configuration, context);

        // Red: drifted targets are off.
        Assert.That(MonsterAttr(configuration, 3, MaximumHealthId), Is.Not.EqualTo(360f).Within(1), "red: spider drifted");
        var dk = configuration.CharacterClasses.Single(c => c.Number == 4);
        Assert.That(RelOperand(dk, Stats.MinimumPhysBaseDmg, Stats.TotalStrength), Is.Not.EqualTo(0.7f).Within(0.01), "red: DK coefficient drifted");

        await new SoloCombatBalanceSeason6UpdatePlugIn().ApplyUpdateAsync(context, configuration).ConfigureAwait(false);

        // Green: every monster present in the embedded B1 table matches the pre-solo columns.
        var table = LoadMonsterTable();
        int matched = 0;
        foreach (var row in table.RootElement.EnumerateArray())
        {
            var number = (short)row.GetProperty("Num").GetInt32();
            var monster = configuration.Monsters.FirstOrDefault(m => m.Number == number);
            if (monster is null)
            {
                continue;
            }

            matched++;
            Assert.That(MonsterAttr(configuration, number, MaximumHealthId), Is.EqualTo(F(row, "PreHp")).Within(1), $"monster {number} HP");
            Assert.That(MonsterAttr(configuration, number, MinimumPhysBaseDmgId), Is.EqualTo(F(row, "PreMinAtk")).Within(1), $"monster {number} min atk");
            Assert.That(MonsterAttr(configuration, number, MaximumPhysBaseDmgId), Is.EqualTo(F(row, "PreMaxAtk")).Within(1), $"monster {number} max atk");
            Assert.That(MonsterAttr(configuration, number, DefenseBaseId), Is.EqualTo(F(row, "PreDefBase")).Within(1), $"monster {number} def");
            Assert.That(MonsterAttr(configuration, number, DefenseRatePvmId), Is.EqualTo(F(row, "PreDefRatePvm")).Within(1), $"monster {number} def rate");
            Assert.That(MonsterAttr(configuration, number, AttackRatePvmId), Is.EqualTo(F(row, "PreAtkRatePvm")).Within(1), $"monster {number} atk rate");
        }

        Assert.That(matched, Is.GreaterThan(50), "full-segment replay should cover most of the ~150 seed monsters");

        // Class constants: all 7 classes, 4 constants each.
        foreach (var (number, physMin) in new (short, bool)[] { (4, true), (0, false), (8, true), (12, true), (16, true), (20, false), (24, true) })
        {
            var cc = configuration.CharacterClasses.FirstOrDefault(c => c.Number == number);
            if (cc is null)
            {
                continue;
            }

            Assert.That(BaseValueOrNaN(cc, physMin ? Stats.MinimumPhysBaseDmg : Stats.MinimumWizBaseDmg), Is.EqualTo(10f).Within(0.01), $"class {number} min dmg");
            Assert.That(BaseValueOrNaN(cc, physMin ? Stats.MaximumPhysBaseDmg : Stats.MaximumWizBaseDmg), Is.EqualTo(15f).Within(0.01), $"class {number} max dmg");
            Assert.That(BaseValueOrNaN(cc, Stats.AttackRatePvm), Is.EqualTo(70f).Within(0.01), $"class {number} atk rate");
            Assert.That(BaseValueOrNaN(cc, Stats.DefenseRatePvm), Is.EqualTo(95f).Within(0.01), $"class {number} def rate");
        }

        Assert.That(RelOperand(dk, Stats.MinimumPhysBaseDmg, Stats.TotalStrength), Is.EqualTo(0.7f).Within(0.01));
        Assert.That(RelOperand(dk, Stats.MaximumPhysBaseDmg, Stats.TotalStrength), Is.EqualTo(1.05f).Within(0.01));

        // Idempotent.
        var snapshot = Snapshot(configuration);
        await new SoloCombatBalanceSeason6UpdatePlugIn().ApplyUpdateAsync(context, configuration).ConfigureAwait(false);
        Assert.That(Snapshot(configuration), Is.EqualTo(snapshot), "second run must not change values");
        Assert.That(dk.AttributeCombinations.Count(r => r.TargetAttribute == Stats.MinimumPhysBaseDmg && r.InputAttribute == Stats.TotalStrength), Is.EqualTo(1));

        // Consistency with a fresh initialization.
        var controlProvider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(controlProvider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var controlRead = controlProvider.CreateNewConfigurationContext();
        var control = (await controlRead.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        using var controlContext = controlProvider.CreateNewContext(control);
        await new SoloCombatBalanceSeason6UpdatePlugIn().ApplyUpdateAsync(controlContext, control).ConfigureAwait(false);
        Assert.That(MonsterAttr(configuration, 3, MaximumHealthId), Is.EqualTo(MonsterAttr(control, 3, MaximumHealthId)).Within(1), "migrated spider HP == fresh-after-v110");
    }

    [Test]
    public async Task SoloSeason6WritesPostColumnsAndTtkLandsInSegmentBandsAsync()
    {
        var provider = new InMemoryPersistenceContextProvider();
        await new DataInitialization(provider, NullLoggerFactory.Instance).CreateInitialDataAsync(1, false).ConfigureAwait(false);
        using var read = provider.CreateNewConfigurationContext();
        var configuration = (await read.GetAsync<GameConfiguration>().ConfigureAwait(false)).Single();
        using var context = provider.CreateNewContext(configuration);
        new SoloBalanceInitializer(context, configuration).Initialize();

        MonsterAttr(configuration, 3, MaximumHealthId, 30f); // drift, prove v110 re-applies post target

        await new SoloCombatBalanceSeason6UpdatePlugIn().ApplyUpdateAsync(context, configuration).ConfigureAwait(false);

        // Post columns match the table.
        var table = LoadMonsterTable();
        foreach (var row in table.RootElement.EnumerateArray())
        {
            var number = (short)row.GetProperty("Num").GetInt32();
            if (configuration.Monsters.All(m => m.Number != number))
            {
                continue;
            }

            Assert.That(MonsterAttr(configuration, number, MaximumHealthId), Is.EqualTo(F(row, "PostHp")).Within(1), $"solo monster {number} HP");
            Assert.That(MonsterAttr(configuration, number, DefenseBaseId), Is.EqualTo(F(row, "PostDefBase")).Within(1), $"solo monster {number} def");
        }

        // TTK across all segments: the written PostHp/PostDef already match the table row-by-row above;
        // assert the table's own authoritative TtkDKs (B1's model output) lands in the per-segment band.
        foreach (var row in table.RootElement.EnumerateArray())
        {
            var number = (short)row.GetProperty("Num").GetInt32();
            if (configuration.Monsters.All(m => m.Number != number)) { continue; }
            if (row.GetProperty("Rank").GetString() != "normal") { continue; } // elite/boss TTK is intentionally higher; bands apply to normal mobs only
            double level = row.GetProperty("Level").GetDouble();
            double ttk = row.GetProperty("TtkDKs").GetDouble();
            (double lo, double hi) = level switch
            {
                <= 15 => (4.0, 8.0),
                <= 40 => (5.0, 10.0),
                <= 80 => (6.0, 15.0),
                <= 120 => (10.0, 20.0),
                _ => (12.0, 25.0),
            };
            Assert.That(ttk, Is.InRange(lo, hi), $"table TtkDKs #{number} lv{level} = {ttk}s in band [{lo},{hi}]");
        }
    }

    private static JsonDocument LoadMonsterTable()
    {
        var asm = typeof(SoloCombatBalanceSeason6UpdatePlugIn).Assembly;
        var name = asm.GetManifestResourceNames().First(n => n.EndsWith("MonsterTargets.json", StringComparison.Ordinal));
        return JsonDocument.Parse(asm.GetManifestResourceStream(name)!);
    }

    private static float F(JsonElement row, string prop) => row.TryGetProperty(prop, out var v) ? v.GetSingle() : 0f;

    private static float MonsterAttr(GameConfiguration configuration, short number, Guid attributeId) =>
        configuration.Monsters.First(m => m.Number == number).Attributes.First(a => a.AttributeDefinition!.Id == attributeId).Value;

    private static void MonsterAttr(GameConfiguration configuration, short number, Guid attributeId, float value) =>
        configuration.Monsters.First(m => m.Number == number).Attributes.First(a => a.AttributeDefinition!.Id == attributeId).Value = value;

    private static float BaseValueOrNaN(CharacterClass cc, AttributeDefinition def) =>
        cc.BaseAttributeValues.FirstOrDefault(a => a.Definition == def)?.Value ?? float.NaN;

    private static float RelOperand(CharacterClass cc, AttributeDefinition target, AttributeDefinition input) =>
        cc.AttributeCombinations.First(r => r.TargetAttribute == target && r.InputAttribute == input).InputOperand;

    private static void DriftOldSeason6(GameConfiguration configuration, IContext context)
    {
        MonsterAttr(configuration, 3, MaximumHealthId, 30f);
        MonsterAttr(configuration, 26, MaximumHealthId, 45f);
        var dk = configuration.CharacterClasses.Single(c => c.Number == 4);
        dk.AttributeCombinations.First(r => r.TargetAttribute == Stats.MinimumPhysBaseDmg && r.InputAttribute == Stats.TotalStrength).InputOperand = 1f / 6;
    }

    private static double ComputeTtkDK(double level, double postHp, double postDef)
    {
        double bm = Interp(
            new double[] { 1, 5, 10, 15, 20, 30, 40, 50, 60, 80, 100, 120, 140, 150 },
            new double[] { 39, 65, 100, 145, 200, 320, 470, 650, 860, 1350, 1950, 2700, 3600, 4200 },
            level);
        const double kDk = 37.0 / 38.4;
        double clsBase = bm * kDk;
        double hitChance = level <= 20 ? 0.80 : 0.90;
        double effective = Math.Max(clsBase - postDef, Math.Max(4.0, level / 10.0));
        return postHp / (effective * hitChance * 1.0);
    }

    private static double Interp(double[] xs, double[] ys, double x)
    {
        if (x <= xs[0]) return ys[0];
        if (x >= xs[^1]) return ys[^1];
        for (int i = 0; i < xs.Length - 1; i++)
        {
            if (x <= xs[i + 1])
            {
                double t = (x - xs[i]) / (xs[i + 1] - xs[i]);
                return ys[i] + t * (ys[i + 1] - ys[i]);
            }
        }

        return ys[^1];
    }

    private static string Snapshot(GameConfiguration configuration) => string.Join("|", new[]
    {
        MonsterAttr(configuration, 3, MaximumHealthId),
        MonsterAttr(configuration, 12, MaximumHealthId),
        BaseValueOrNaN(configuration.CharacterClasses.Single(c => c.Number == 4), Stats.MinimumPhysBaseDmg),
        RelOperand(configuration.CharacterClasses.Single(c => c.Number == 4), Stats.MinimumPhysBaseDmg, Stats.TotalStrength),
    });
}
