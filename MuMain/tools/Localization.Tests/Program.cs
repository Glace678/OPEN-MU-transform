using System.Text.Json;

namespace MuMain.Tools.Localization.Tests;

internal static class Program
{
    private static readonly string[] TutorialLocales = ["de", "es", "id", "pl", "pt", "ru", "tl", "uk", "zh-TW", "ja"];

    private static int Main(string[] args)
    {
        var resources = Path.GetFullPath(args.Length == 0 ? "src/Localization" : args[0]);
        (string Name, Action Run)[] tests = [
            ("complete coverage and English-equal text", CompleteCoverage),
            ("missing key is an English fallback error", MissingKey),
            ("empty value is not counted as fallback", EmptyValue),
            ("missing group is included in the locale matrix", MissingGroup),
            ("empty locale marker is zero coverage", EmptyLocaleMarker),
            ("printf type mismatch is an error", PrintfTypeMismatch),
            ("printf argument count mismatch is an error", PrintfCountMismatch),
            ("verified plain-text label is reported separately", PlainTextLabel),
            ("plain-text classification is restricted to its group", PlainTextGroupScope),
            ("plain-text classification is restricted to its key", PlainTextKeyScope),
            ("plain-text classification does not hide an empty value", PlainTextEmptyValue),
            ("plain-text classification does not hide a missing key", PlainTextMissingKey),
            ("strict audit shares the verified plain-text classification", () => Equal(0, StrictAuditPlainTextLabel(resources, "Game"))),
            ("strict audit still checks other resource groups", () => Equal(1, StrictAuditPlainTextLabel(resources, "Dialog"))),
            ("escaped percent is not a placeholder", EscapedPercent),
            ("indexed placeholders can be reordered", IndexedPlaceholders),
            ("unknown keys do not inflate coverage", UnknownKey),
            ("duplicate entries use the last value", DuplicateKey),
            ("inherited half-width kana only warns", LegacyTextWarning),
            ("accented European text is not mojibake", AccentedText),
            ("line break differences are review warnings", LineBreakWarning),
            ("logical line break counter is preserved", LogicalLineBreaks),
            ("unknown locale is rejected", UnknownLocale),
            ("missing English baseline is rejected", MissingBaseline),
            ("report command writes JSON and exits by errors", ReportCommand),
            ("all ten locales contain eight bounded tutorials", () => TutorialResources(resources)),
            ("German Very Fast has the corrected value", () => GermanResource(resources)),
            ("repository report includes the four-group matrix", () => RepositoryMatrix(resources)),
        ];
        return RunTests(tests.Concat(RepairedPrintfResourceTests.Create(resources)).ToArray());
    }

    private static int RunTests(IReadOnlyList<(string Name, Action Run)> tests)
    {
        var failures = 0;
        foreach (var (name, run) in tests)
        {
            try
            {
                run();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
            }
        }

        Console.WriteLine($"Localization regressions: {tests.Count - failures} passed, {failures} failed, {tests.Count} total.");
        return failures == 0 ? 0 : 1;
    }

    private static void CompleteCoverage()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("One", "One"), ("Two", "Two"));
        fixture.Write("Game", "es", ("One", "Uno"), ("Two", "Two"));
        var report = fixture.Report("es");
        var row = report.Groups.Single();
        Equal(2, row.PresentKeys);
        Equal(0, row.FallbackKeys);
        Equal(1, row.SameAsEnglish);
        Equal(0, report.ErrorCount);
    }

    private static void MissingKey()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("One", "One"), ("Two", "Two"));
        fixture.Write("Game", "es", ("One", "Uno"));
        var report = fixture.Report("es");
        Equal(1, report.Groups.Single().FallbackKeys);
        Equal(1, report.ErrorCount);
        Equal("missing_key", report.Issues.Single().Code);
    }

    private static void EmptyValue()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("One", "One"));
        fixture.Write("Game", "es", ("One", " "));
        var report = fixture.Report("es");
        var row = report.Groups.Single();
        Equal(0, row.PresentKeys);
        Equal(0, row.FallbackKeys);
        Equal(1, row.EmptyKeys);
        Equal("empty_value", report.Issues.Single().Code);
    }

    private static void MissingGroup()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("One", "One"));
        fixture.Write("Editor", "en", ("Editor", "Editor"));
        fixture.Write("Game", "es", ("One", "Uno"));
        var report = fixture.Report("es");
        var editor = report.Groups.Single(row => row.Group == "Editor");
        Equal(false, editor.ResourceFileExists);
        Equal(1, editor.FallbackKeys);
        Equal(2, report.ErrorCount);
        Equal(true, report.Issues.Any(issue => issue.Code == "missing_file"));
    }

    private static void EmptyLocaleMarker()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("One", "One"), ("Two", "Two"));
        fixture.Write("Game", "fr");
        var report = fixture.Report("fr");
        Equal(0, report.Groups.Single().PresentKeys);
        Equal(2, report.Groups.Single().FallbackKeys);
        Equal(true, report.Groups.Single().ResourceFileExists);
        Equal(2, report.ErrorCount);
    }

    private static void PrintfTypeMismatch()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("Value", "%d"));
        fixture.Write("Game", "es", ("Value", "%s"));
        var report = fixture.Report("es");
        Equal(1, report.Groups.Single().PlaceholderMismatches);
        Equal("placeholder_mismatch", report.Issues.Single().Code);
        Equal(LocalizationCoverageReport.ErrorSeverity, report.Issues.Single().Severity);
    }

    private static void PrintfCountMismatch()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("Value", "%d/%d"));
        fixture.Write("Game", "es", ("Value", "%d"));
        Equal(1, fixture.Report("es").Groups.Single().PlaceholderMismatches);
    }

    private static void EscapedPercent()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("Value", "50%%: %d"));
        fixture.Write("Game", "es", ("Value", "%d: 50%%"));
        Equal(0, fixture.Report("es").ErrorCount);
    }

    private static void PlainTextLabel()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("OnePercentLow", "1% Low"));
        fixture.Write("Game", "es", ("OnePercentLow", "1 % bajo"));
        var report = fixture.Report("es");
        Equal(0, report.ErrorCount);
        Equal(0, report.Groups.Single().PlaceholderMismatches);
        Equal(1, report.Groups.Single().PlainTextKeys);
        Equal(1, report.PlainTextClassifications.Count);
        Equal("Game", report.PlainTextClassifications.Single().Group);
        Equal("OnePercentLow", report.PlainTextClassifications.Single().Key);
    }

    private static void PlainTextGroupScope()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Dialog", "en", ("OnePercentLow", "1% Low"));
        fixture.Write("Dialog", "es", ("OnePercentLow", "1 % bajo"));
        var report = fixture.Report("es");
        Equal(1, report.Groups.Single().PlaceholderMismatches);
        Equal(0, report.Groups.Single().PlainTextKeys);
        Equal(0, report.PlainTextClassifications.Count);
    }

    private static void PlainTextKeyScope()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("OtherLabel", "1% Low"));
        fixture.Write("Game", "es", ("OtherLabel", "1 % bajo"));
        var report = fixture.Report("es");
        Equal(1, report.Groups.Single().PlaceholderMismatches);
        Equal(0, report.Groups.Single().PlainTextKeys);
        Equal(0, report.PlainTextClassifications.Count);
    }

    private static void PlainTextEmptyValue()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("OnePercentLow", "1% Low"));
        fixture.Write("Game", "es", ("OnePercentLow", " "));
        var report = fixture.Report("es");
        Equal(1, report.ErrorCount);
        Equal("empty_value", report.Issues.Single().Code);
        Equal(0, report.Groups.Single().PlainTextKeys);
    }

    private static void PlainTextMissingKey()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("OnePercentLow", "1% Low"));
        fixture.Write("Game", "es");
        var report = fixture.Report("es");
        Equal(1, report.ErrorCount);
        Equal("missing_key", report.Issues.Single().Code);
        Equal(0, report.Groups.Single().PlainTextKeys);
    }

    private static int StrictAuditPlainTextLabel(string resources, string group)
    {
        using var fixture = new ResourceFixture();
        fixture.Write(group, "en", ("OnePercentLow", "1% Low"));
        fixture.Write(group, "zh-CN", ("OnePercentLow", "1%低点"));
        var policies = Path.GetFullPath(Path.Combine(resources, "..", "..", "tools", "Localization"));
        var command = LocalizationCommandLine.Parse([
            "audit", "--input", fixture.DirectoryPath, "--locale", "zh-CN",
            "--banned-terms", Path.Combine(policies, "mainland-banned-terms.tsv"),
            "--context-terms", Path.Combine(policies, "mainland-context-terms.json"),
        ]);
        return LocalizationAuditor.Audit(command);
    }

    private static void IndexedPlaceholders()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Dialog", "en", ("Value", "{0}: {1}"));
        fixture.Write("Dialog", "es", ("Value", "{1}: {0}"));
        Equal(0, fixture.Report("es").ErrorCount);
    }

    private static void UnknownKey()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("One", "One"));
        fixture.Write("Game", "es", ("One", "Uno"), ("Extra", "Extra"));
        var report = fixture.Report("es");
        Equal(2, report.Groups.Single().Entries);
        Equal(1, report.Groups.Single().PresentKeys);
        Equal(0, report.ErrorCount);
        Equal("unknown_key", report.Issues.Single().Code);
    }

    private static void DuplicateKey()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("One", "One"));
        fixture.Write("Game", "es", ("One", "Wrong %d"), ("One", "Uno"));
        var report = fixture.Report("es");
        Equal(1, report.Groups.Single().PresentKeys);
        Equal(0, report.ErrorCount);
        Equal("duplicate_key", report.Issues.Single().Code);
    }

    private static void LegacyTextWarning()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("Legacy", "\uFF7C\uFF93\uFF7C\uFF7A"));
        fixture.Write("Game", "es", ("Legacy", "\uFF7C\uFF93\uFF7C\uFF7A"));
        var report = fixture.Report("es");
        Equal(0, report.ErrorCount);
        Equal(1, report.WarningCount);
        Equal("suspect_legacy_text", report.Issues.Single().Code);
    }

    private static void AccentedText()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("Value", "Letters"));
        fixture.Write("Game", "de", ("Value", "\u00C4\u00D6\u00DC\u00DF"));
        var report = fixture.Report("de");
        Equal(0, report.ErrorCount);
        Equal(0, report.WarningCount);
    }

    private static void LineBreakWarning()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("Value", "One\nTwo"));
        fixture.Write("Game", "es", ("Value", "Uno Dos"));
        var report = fixture.Report("es");
        Equal(0, report.ErrorCount);
        Equal("line_break_difference", report.Issues.Single().Code);
    }

    private static void LogicalLineBreaks()
    {
        Equal(4, LocalizationTextChecks.CountLogicalLineBreaks("a\r\nb\nc\rd\\ne"));
        Equal(true, LocalizationTextChecks.LooksCorruptedZhCn("\uFFFD"));
        Equal(true, LocalizationTextChecks.LooksCorruptedZhCn("?????"));
    }

    private static void UnknownLocale()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("One", "One"));
        ThrowsToolException(() => fixture.Report("hi"));
    }

    private static void MissingBaseline()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "es", ("One", "Uno"));
        ThrowsToolException(() => fixture.Report());
    }

    private static void ReportCommand()
    {
        using var fixture = new ResourceFixture();
        fixture.Write("Game", "en", ("One", "One"));
        fixture.Write("Game", "es", ("One", "Uno"));
        var output = Path.Combine(fixture.DirectoryPath, "reports", "coverage.json");
        var command = LocalizationCommandLine.Parse(["report", "--input", fixture.DirectoryPath, "--output", output, "--locale", "es"]);
        Equal(0, LocalizationCoverageReporter.Run(command));
        using var json = JsonDocument.Parse(File.ReadAllText(output));
        Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
        fixture.Write("Game", "es");
        Equal(1, LocalizationCoverageReporter.Run(command));
        using var failed = JsonDocument.Parse(File.ReadAllText(output));
        Equal(1, failed.RootElement.GetProperty("groups")[0].GetProperty("fallbackKeys").GetInt32());
    }

    private static void TutorialResources(string resources)
    {
        var english = ResxDocument.Load(Path.Combine(resources, "Game.en.resx"));
        var tutorialKeys = english.Keys.Where(key => key.StartsWith("Tutorial", StringComparison.Ordinal)).ToArray();
        Equal(8, tutorialKeys.Length);
        foreach (var locale in TutorialLocales)
        {
            var localized = ResxDocument.Load(Path.Combine(resources, $"Game.{locale}.resx"));
            foreach (var key in tutorialKeys)
            {
                var value = localized.GetValue(key);
                Equal(false, string.IsNullOrWhiteSpace(value));
                Equal(true, value.Length <= 99);
                Equal(false, string.Equals(value, english.GetValue(key), StringComparison.Ordinal));
                Equal(true, PlaceholderScanner.Scan(value).SequenceEqual(PlaceholderScanner.Scan(english.GetValue(key))));
            }

            foreach (var token in new[] { "F1", "PageUp", "PageDown", "LB/RB", "Esc" })
            {
                Equal(true, localized.GetValue("TutorialPaging").Contains(token, StringComparison.Ordinal));
            }
        }
    }

    private static void GermanResource(string resources)
    {
        var german = ResxDocument.Load(Path.Combine(resources, "Game.de.resx"));
        Equal("Sehr schnell", german.GetValue("Very Fast"));
    }

    private static void RepositoryMatrix(string resources)
    {
        var report = LocalizationCoverageReporter.Create(resources);
        Equal(15, report.Locales.Count);
        Equal(60, report.Groups.Count);
        Equal(0, report.Groups.Sum(row => row.PlaceholderMismatches));
        Equal(1, report.PlainTextClassifications.Count);
        foreach (var locale in TutorialLocales)
        {
            Equal(0, report.Groups.Single(row => row.Locale == locale && row.Group == "Game").FallbackKeys);
        }

        foreach (var locale in new[] { "fr", "ko", "vi" })
        {
            Equal(0, report.Groups.Where(row => row.Locale == locale).Sum(row => row.PresentKeys));
        }
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected {expected}, actual {actual}.");
        }
    }

    private static void ThrowsToolException(Action action)
    {
        try
        {
            action();
        }
        catch (LocalizationToolException)
        {
            return;
        }

        throw new InvalidOperationException("Expected LocalizationToolException.");
    }
}
