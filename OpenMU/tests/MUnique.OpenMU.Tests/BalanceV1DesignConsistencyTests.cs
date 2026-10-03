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
            ("MoneyChance", Constant("MoneyChance"), DesignNumber(design, "loot", "moneyChance")),
            ("JewelChance", Constant("JewelChance"), DesignNumber(design, "loot", "jewelChance")),
            ("ReferenceKillCycleSeconds", Constant("ReferenceKillCycleSeconds"), DesignNumber(design, "combat", "referenceKillCycleSeconds")),
            ("HitMinimum", Constant("HitMinimum"), DesignNumber(design, "combat", "hitMin")),
            ("HitMaximum", Constant("HitMaximum"), DesignNumber(design, "combat", "hitMax")),
            ("HitBase", Constant("HitBase"), DesignNumber(design, "combat", "hitBase")),
            ("ArmorConstant", Constant("ArmorConstant"), DesignNumber(design, "combat", "armorConstant")),
            ("OverlevelGraceRanks", Constant("OverlevelGraceRanks"), DesignNumber(design, "progression", "overlevelGraceRanks")),
            ("OverlevelDecayRanks", Constant("OverlevelDecayRanks"), DesignNumber(design, "progression", "overlevelDecayRanks")),
            ("OverlevelExperienceFloor", Constant("OverlevelExperienceFloor"), DesignNumber(design, "progression", "overlevelFloor")),
        };

        foreach (var (field, actual, expected) in expectations)
        {
            Assert.That(actual, Is.EqualTo(expected).Within(1e-9), $"BalanceV1.{field} must match {DesignFileName}.");
        }
    }
}
