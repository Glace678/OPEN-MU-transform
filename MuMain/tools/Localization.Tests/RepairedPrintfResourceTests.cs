namespace MuMain.Tools.Localization.Tests;

internal static class RepairedPrintfResourceTests
{
    private static readonly (string Locale, string[] Keys)[] RepairCases = [
        ("de", [
            "5%% decrease in castle and arena invitation combine rate",
            "5%% increase in castle and arena invitation combine rate.",
            "Attacking skill will increase +20%%",
            "Increase 150%% attack in Dark Spirit class",
            "Increase 30%% of attacking & Wizardry Dmg",
            "PleaseCheckOutTheAnnouncementOn",
            "Register the lucky number on the 100%% winning card.",
            "Restores HP by 100%% immediately.",
            "Restores HP by 65%% immediately.",
            "Restores Mana by 100%% immediately.",
            "Restores SD by 65%% immediately.",
            "Sell (S)",
            "written on the 100%% winning card.",
        ]),
        ("es", [
            "Absorb 30%% of damage",
            "Increase 30%% of attacking & Wizardry Dmg",
            "PleaseCheckOutTheAnnouncementOn",
            "Restores HP by 100%% immediately.",
            "Restores HP by 65%% immediately.",
            "Restores Mana by 100%% immediately.",
            "Restores SD by 65%% immediately.",
        ]),
        ("id", ["Absorb 30%% of damage"]),
        ("ja", [
            "%d sent you a gift.",
            "%s +%d?",
            "Are you sure you want to dissolve",
            "Entrance is allowed for %d times",
            "Up to %d%% EXP gain increase, depending on the number of members in your party.",
            "You may enter only %d times per day.",
        ]),
        ("pt", [
            "Absorb 30%% of damage",
            "Increase 150%% attack in Dark Spirit class",
            "Increase 30%% of attacking & Wizardry Dmg",
            "Restores HP by 100%% immediately.",
            "Restores HP by 65%% immediately.",
            "Restores SD by 65%% immediately.",
            "You need more %%s to purchase this item.",
        ]),
        ("ru", ["Increase 30%% of attacking & Wizardry Dmg"]),
    ];

    private static readonly (string Locale, string Value)[] LiteralLabels = [
        ("es", "1 % bajo"), ("id", "1% Rendah"), ("pl", "Min. 1%"), ("pt", "1% baixo"),
        ("ru", "1% минимум"), ("tl", "1% Mababa"), ("uk", "1% мінімум"), ("zh-TW", "1% 低點"),
    ];

    public static IReadOnlyList<(string Name, Action Run)> Create(string resources)
    {
        var english = ResxDocument.Load(Path.Combine(resources, "Game.en.resx"));
        var tests = new List<(string Name, Action Run)>();
        foreach (var (locale, keys) in RepairCases)
        {
            var localized = ResxDocument.Load(Path.Combine(resources, $"Game.{locale}.resx"));
            foreach (var key in keys)
            {
                tests.Add(($"{locale} printf contract: {key}", () => VerifyRepair(english, localized, key)));
            }
        }

        foreach (var (locale, value) in LiteralLabels)
        {
            var localized = ResxDocument.Load(Path.Combine(resources, $"Game.{locale}.resx"));
            tests.Add(($"{locale} telemetry label remains literal", () =>
                Require(localized.GetValue("OnePercentLow") == value, "A directly rendered label must not gain printf arguments or escaped percent signs.")));
        }

        tests.Add(("bounded printf contract retains argument order", VerifyArgumentOrder));
        tests.Add(("bounded printf contract distinguishes literal percent-s", VerifyLiteralPercent));
        tests.Add(("bounded printf contract rejects bare percent signs", VerifyBarePercent));
        return tests;
    }

    private static void VerifyRepair(ResxDocument english, ResxDocument localized, string key)
    {
        var source = english.GetValue(key);
        var value = localized.GetValue(key);
        Require(!string.IsNullOrWhiteSpace(value), "Translation must not be empty.");
        Require(value != source, "The repair must preserve translated text rather than replace it with English.");
        Require(ScanFormatTokens(source).SequenceEqual(ScanFormatTokens(value)),
            "The ordered printf arguments and literal percent escapes must match English exactly.");
        Require(PlaceholderScanner.Scan(source).SequenceEqual(PlaceholderScanner.Scan(value)),
            "The localization audit must accept the repaired placeholder signature.");
    }

    private static void VerifyArgumentOrder()
    {
        Require(ScanFormatTokens("%s +%d?").SequenceEqual(new[] { "%s", "%d" }), "Argument order was lost.");
        Require(!ScanFormatTokens("%s +%d?").SequenceEqual(ScanFormatTokens("%d +%s?")), "Reordered varargs must differ.");
    }

    private static void VerifyLiteralPercent()
    {
        Require(ScanFormatTokens("%%s").SequenceEqual(new[] { "%%" }), "Literal percent-s is not a string argument.");
        Require(ScanFormatTokens("%d%%").SequenceEqual(new[] { "%d", "%%" }), "Numeric percent must retain both tokens.");
    }

    private static void VerifyBarePercent()
    {
        try
        {
            ScanFormatTokens("30% translated text");
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("A bare percent sign must not be accepted in the repaired printf resources.");
    }

    private static IReadOnlyList<string> ScanFormatTokens(string value)
    {
        var tokens = new List<string>();
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            // These bounded repairs use only %d, %s and escaped percent; retain their exact order.
            Require(index + 1 < value.Length && value[index + 1] is '%' or 'd' or 's',
                $"Invalid or unexpected percent token at position {index}.");
            tokens.Add(value.Substring(index, 2));
            index++;
        }

        return tokens;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
