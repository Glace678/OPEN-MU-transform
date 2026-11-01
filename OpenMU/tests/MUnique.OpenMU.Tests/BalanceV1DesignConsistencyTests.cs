#nullable enable

namespace MUnique.OpenMU.Tests;

using System.IO;
using System.Reflection;
using System.Text.Json;
using MUnique.OpenMU.GameLogic;
using NUnit.Framework;

/// <summary>
/// Verifies that the hardcoded runtime constants in <see cref="BalanceV1"/>
/// stay in lockstep with the authored balance.v2.json design file, so the two
/// cannot silently drift apart.
/// </summary>
[TestFixture]
public class BalanceV1DesignConsistencyTests
{
    private const string DesignFileName = "balance.v2.json";

    private static JsonDocument LoadDesign()
    {
        var path = FindDesignFile(new DirectoryInfo(AppContext.BaseDirectory));
        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream);
    }

    private static string FindDesignFile(DirectoryInfo start)
    {
        var current = start;
        for (var depth = 0; current is not null && depth < 12; depth++, current = current.Parent)
        {
            foreach (var candidate in current.EnumerateFiles(DesignFileName, SearchOption.AllDirectories))
            {
                // Prefer the canonical design folder and ignore build output copies.
                if (candidate.FullName.Contains($"design{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || candidate.Directory?.Name == "design")
                {
                    return candidate.FullName;
                }
            }
        }

        throw new FileNotFoundException($"Could not locate {DesignFileName} starting from {start.FullName}.");
    }

    private static double Constant(string name)
    {
        var field = typeof(BalanceV1).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingFieldException(typeof(BalanceV1).Name, name);
        return Convert.ToDouble(field.GetValue(null), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static double DesignNumber(JsonDocument design, params string[] path)
    {
        var current = design.RootElement;
        foreach (var property in path)
        {
            current = current.GetProperty(property);
        }

        return current.GetDouble();
    }

    [Test]
    public void ConstantsMatchDesignFile()
    {
        using var design = LoadDesign();

        var expectations = new (string Field, double Actual, double Expected)[]
        {
            ("NormalLevelCap", BalanceV1.NormalLevelCap, DesignNumber(design, "normalCap")),
            ("MasterLevelCap", BalanceV1.MasterLevelCap, DesignNumber(design, "masterCap")),
            ("PointsPerLevel", BalanceV1.PointsPerLevel, DesignNumber(design, "pointsPerLevel")),
            ("PartyExperiencePerExtraMember", BalanceV1.PartyExperiencePerExtraMember, DesignNumber(design, "progression", "partyXpPerExtra")),
            ("MoneyChance", Constant("MoneyChance"), DesignNumber(design, "loot", "moneyChance")),
            ("JewelChance", Constant("JewelChance"), DesignNumber(design, "loot", "jewelChance")),
            ("RareEligibilityRank", Constant("RareEligibilityRank"), DesignNumber(design, "loot", "rareEligibilityRank")),
            ("MasterRankPerLevel", Constant("MasterRankPerLevel"), DesignNumber(design, "masterRankPerLevel")),
            ("ReferenceKillCycleSeconds", Constant("ReferenceKillCycleSeconds"), DesignNumber(design, "combat", "referenceKillCycleSeconds")),
            ("HitMinimum", Constant("HitMinimum"), DesignNumber(design, "combat", "hitMin")),
            ("HitMaximum", Constant("HitMaximum"), DesignNumber(design, "combat", "hitMax")),
            ("HitBase", Constant("HitBase"), DesignNumber(design, "combat", "hitBase")),
            ("HitSlope", Constant("HitSlope"), DesignNumber(design, "combat", "hitSlope")),
            ("ArmorConstant", Constant("ArmorConstant"), DesignNumber(design, "combat", "armorConstant")),
            ("ArmorPerRank", Constant("ArmorPerRank"), DesignNumber(design, "combat", "armorPerRank")),
            ("ArmorReductionCap", Constant("ArmorReductionCap"), DesignNumber(design, "combat", "armorCap")),
            ("TotalReductionCap", Constant("TotalReductionCap"), DesignNumber(design, "combat", "totalReductionCap")),
            ("OverlevelGraceRanks", Constant("OverlevelGraceRanks"), DesignNumber(design, "progression", "overlevelGraceRanks")),
            ("OverlevelDecayRanks", Constant("OverlevelDecayRanks"), DesignNumber(design, "progression", "overlevelDecayRanks")),
            ("OverlevelExperienceFloor", Constant("OverlevelExperienceFloor"), DesignNumber(design, "progression", "overlevelFloor")),
            ("UnderlevelExperienceCap", Constant("UnderlevelExperienceCap"), DesignNumber(design, "progression", "underlevelRewardCap")),
        };

        foreach (var (field, actual, expected) in expectations)
        {
            Assert.That(actual, Is.EqualTo(expected).Within(1e-9), $"BalanceV1.{field} must match {DesignFileName}.");
        }

        var normalAnchors = (ValueTuple<double, double>[])typeof(BalanceV1)
            .GetField("NormalExperienceAnchors", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var designNormalAnchors = design.RootElement.GetProperty("progression").GetProperty("normalSeconds")
            .EnumerateArray()
            .Select(e => (e[0].GetDouble(), e[1].GetDouble()))
            .ToArray();
        Assert.That(normalAnchors, Is.EqualTo(designNormalAnchors), $"BalanceV1.NormalExperienceAnchors must match {DesignFileName}.");

        var masterAnchors = (ValueTuple<double, double>[])typeof(BalanceV1)
            .GetField("MasterExperienceAnchors", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;
        var designMasterAnchors = design.RootElement.GetProperty("progression").GetProperty("masterSeconds")
            .EnumerateArray()
            .Select(e => (e[0].GetDouble(), e[1].GetDouble()))
            .ToArray();
        Assert.That(masterAnchors, Is.EqualTo(designMasterAnchors), $"BalanceV1.MasterExperienceAnchors must match {DesignFileName}.");

        var bossIds = ((System.Collections.Generic.HashSet<short>)typeof(BalanceV1)
            .GetField("BossMonsterNumbers", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!).OrderBy(x => x).ToArray();
        var designBossIds = design.RootElement.GetProperty("monsters").GetProperty("bossIds")
            .EnumerateArray()
            .Select(e => (short)e.GetInt32())
            .OrderBy(x => x)
            .ToArray();
        Assert.That(bossIds, Is.EqualTo(designBossIds), $"BalanceV1.BossMonsterNumbers must match {DesignFileName}.");

        var eliteIds = ((System.Collections.Generic.HashSet<short>)typeof(BalanceV1)
            .GetField("EliteMonsterNumbers", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!).OrderBy(x => x).ToArray();
        var designEliteIds = design.RootElement.GetProperty("monsters").GetProperty("eliteIds")
            .EnumerateArray()
            .Select(e => (short)e.GetInt32())
            .OrderBy(x => x)
            .ToArray();
        Assert.That(eliteIds, Is.EqualTo(designEliteIds), $"BalanceV1.EliteMonsterNumbers must match {DesignFileName}.");

        foreach (var potion in design.RootElement.GetProperty("economy").GetProperty("potions").EnumerateArray())
        {
            var group = (byte)potion.GetProperty("group").GetInt32();
            var number = (short)potion.GetProperty("number").GetInt32();
            Assert.That(BalanceV1.TryGetPotionRule(group, number, out var rule), Is.True, $"Potion {group}:{number} must exist in BalanceV1.");
            Assert.That(rule.UnlockLevel, Is.EqualTo(potion.GetProperty("unlock").GetInt32()), $"Potion {group}:{number} UnlockLevel");
            Assert.That(rule.RecoveryFraction, Is.EqualTo(potion.GetProperty("fraction").GetDouble()).Within(1e-9), $"Potion {group}:{number} RecoveryFraction");
            Assert.That(rule.RecoveryCap, Is.EqualTo(potion.GetProperty("cap").GetDouble()).Within(1e-9), $"Potion {group}:{number} RecoveryCap");
        }

        var designSteps = design.RootElement.GetProperty("enhancement").GetProperty("steps").EnumerateArray().ToArray();
        Assert.That(designSteps.Length, Is.EqualTo(15));
        for (var level = 1; level <= designSteps.Length; level++)
        {
            var step = BalanceV1.GetUpgradeStep(level);
            var expectedStep = designSteps[level - 1];
            Assert.That(step.TargetLevel, Is.EqualTo(expectedStep.GetProperty("level").GetInt32()), $"UpgradeStep[{level}].TargetLevel");
            Assert.That(step.Chance, Is.EqualTo(expectedStep.GetProperty("chance").GetDouble()).Within(1e-9), $"UpgradeStep[{level}].Chance");
            Assert.That(step.PityAttempts, Is.EqualTo(expectedStep.GetProperty("pity").GetInt32()), $"UpgradeStep[{level}].PityAttempts");
            Assert.That(step.Jewels, Is.EqualTo(expectedStep.GetProperty("jewels").GetInt32()), $"UpgradeStep[{level}].Jewels");
        }
    }
}
