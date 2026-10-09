using System.Text.Json;

namespace MuMain.Tools.Localization;

internal static class LocalizationCoverageReporter
{
    private const string EnglishLocale = "en";
    private const string EnglishFileSuffix = ".en.resx";
    private const int TutorialMaximumCharacters = 99;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static int Run(LocalizationCommand command)
    {
        command.Options.TryGetValue("locale", out var locale);
        var report = Create(Path.GetFullPath(command.Require("input")), locale);
        var outputPath = Path.GetFullPath(command.Require("output"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, JsonOptions));
        WriteSummary(report);
        Console.WriteLine($"Report: {outputPath}");
        return report.ErrorCount == 0 ? 0 : 1;
    }

    public static LocalizationCoverageReport Create(string inputDirectory, string? selectedLocale = null)
    {
        if (!Directory.Exists(inputDirectory))
        {
            throw new LocalizationToolException($"Resource directory not found: {inputDirectory}");
        }

        var sources = LoadSources(inputDirectory);
        var locales = FindLocales(inputDirectory, sources.Keys, selectedLocale);
        var issues = new List<LocalizationCoverageReport.Issue>();
        var groups = new List<LocalizationCoverageReport.GroupCoverage>();
        foreach (var locale in locales)
        {
            foreach (var (group, english) in sources)
            {
                groups.Add(AuditGroup(inputDirectory, group, locale, english, issues));
            }
        }

        var plainTextKeys = LocalizationFormatClassification.PlainTextKeys
            .Where(entry => sources.TryGetValue(entry.Group, out var source) && source.ContainsKey(entry.Key))
            .ToArray();
        return new LocalizationCoverageReport(EnglishLocale, locales, groups, issues, plainTextKeys);
    }

    private static SortedDictionary<string, ResxDocument> LoadSources(string inputDirectory)
    {
        var sources = new SortedDictionary<string, ResxDocument>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(inputDirectory, $"*{EnglishFileSuffix}"))
        {
            var group = Path.GetFileName(path)[..^EnglishFileSuffix.Length];
            sources.Add(group, ResxDocument.Load(path));
        }

        if (sources.Count == 0)
        {
            throw new LocalizationToolException("No English baseline resource groups were found.");
        }

        return sources;
    }

    private static string[] FindLocales(string directory, IEnumerable<string> groups, string? selectedLocale)
    {
        var groupNames = groups.ToHashSet(StringComparer.Ordinal);
        var locales = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(directory, "*.resx"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var separator = name.LastIndexOf('.');
            if (separator > 0 && separator < name.Length - 1 && groupNames.Contains(name[..separator]))
            {
                locales.Add(name[(separator + 1)..]);
            }
        }

        if (selectedLocale is not null)
        {
            if (!locales.Contains(selectedLocale))
            {
                throw new LocalizationToolException($"No resource files exist for locale '{selectedLocale}'.");
            }

            return [selectedLocale];
        }

        return locales.OrderBy(locale => locale == EnglishLocale ? string.Empty : locale, StringComparer.Ordinal).ToArray();
    }

    private static LocalizationCoverageReport.GroupCoverage AuditGroup(
        string directory,
        string group,
        string locale,
        ResxDocument english,
        List<LocalizationCoverageReport.Issue> issues)
    {
        var path = Path.Combine(directory, $"{group}.{locale}.resx");
        var localized = File.Exists(path) ? ResxDocument.Load(path) : null;
        var firstIssue = issues.Count;
        if (localized is null)
        {
            AddIssue(issues, group, locale, "missing_file", null, $"Missing {Path.GetFileName(path)}; the entire group falls back to English.");
        }

        foreach (var key in english.Keys.Order(StringComparer.Ordinal))
        {
            AuditKey(group, locale, key, english.GetValue(key), localized, issues);
        }

        AuditAdditionalKeys(group, locale, english, localized, issues);
        return SummarizeGroup(group, locale, english, localized, issues.Skip(firstIssue));
    }

    private static void AuditKey(
        string group,
        string locale,
        string key,
        string source,
        ResxDocument? localized,
        ICollection<LocalizationCoverageReport.Issue> issues)
    {
        if (localized is null || !localized.ContainsKey(key))
        {
            AddIssue(issues, group, locale, "missing_key", key, "Missing key; the generated catalog falls back to English.");
            return;
        }

        var value = localized.GetValue(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            AddIssue(issues, group, locale, "empty_value", key, "Empty translation; the generator preserves it rather than falling back.");
            return;
        }

        AuditPlaceholders(group, locale, key, source, value, issues);
        AuditTextWarnings(group, locale, key, source, value, issues);
        if (group == "Game" && key.StartsWith("Tutorial", StringComparison.Ordinal) && value.Length > TutorialMaximumCharacters)
        {
            AddIssue(issues, group, locale, "tutorial_too_long", key, $"Tutorial text has {value.Length} characters; the current buffer allows {TutorialMaximumCharacters}.");
        }
    }

    private static void AuditPlaceholders(
        string group,
        string locale,
        string key,
        string source,
        string value,
        ICollection<LocalizationCoverageReport.Issue> issues)
    {
        if (LocalizationFormatClassification.IsPlainText(group, key))
        {
            return;
        }

        var expected = PlaceholderScanner.Scan(source);
        var actual = PlaceholderScanner.Scan(value);
        if (!expected.SequenceEqual(actual))
        {
            AddIssue(issues, group, locale, "placeholder_mismatch", key,
                $"Placeholder signature differs: expected [{string.Join(", ", expected)}], found [{string.Join(", ", actual)}].");
        }
    }

    private static void AuditTextWarnings(
        string group,
        string locale,
        string key,
        string source,
        string value,
        ICollection<LocalizationCoverageReport.Issue> issues)
    {
        if (LocalizationTextChecks.IsMojibakeHardFail(value))
        {
            AddIssue(issues, group, locale, "mojibake_residue", key,
                "Value contains U+FFFD, four or more consecutive question marks, or a Latin-1 Supplement mojibake run (e.g. Korean cp949/cp1252 residue).",
                LocalizationCoverageReport.ErrorSeverity);
        }

        if (LocalizationTextChecks.ContainsSuspiciousLegacyText(key) || LocalizationTextChecks.ContainsSuspiciousLegacyText(value))
        {
            AddIssue(issues, group, locale, "suspect_legacy_text", key,
                "Review half-width kana; inherited legacy text is a review warning, not a failure.",
                LocalizationCoverageReport.WarningSeverity);
        }

        if (LocalizationTextChecks.CountLogicalLineBreaks(source) != LocalizationTextChecks.CountLogicalLineBreaks(value))
        {
            AddIssue(issues, group, locale, "line_break_difference", key,
                "Logical line break count differs from English; review the text layout.", LocalizationCoverageReport.WarningSeverity);
        }
    }

    private static void AuditAdditionalKeys(
        string group,
        string locale,
        ResxDocument english,
        ResxDocument? localized,
        ICollection<LocalizationCoverageReport.Issue> issues)
    {
        if (localized is null)
        {
            return;
        }

        foreach (var key in localized.Keys.Except(english.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            AddIssue(issues, group, locale, "unknown_key", key, "Key is absent from English and is not emitted by ResxGen.", LocalizationCoverageReport.WarningSeverity);
        }

        foreach (var key in localized.DuplicateKeys)
        {
            AddIssue(issues, group, locale, "duplicate_key", key, "Duplicate entries; ResxGen uses the last value.", LocalizationCoverageReport.WarningSeverity);
        }
    }

    private static LocalizationCoverageReport.GroupCoverage SummarizeGroup(
        string group,
        string locale,
        ResxDocument english,
        ResxDocument? localized,
        IEnumerable<LocalizationCoverageReport.Issue> issues)
    {
        var storedKeys = localized is null ? [] : english.Keys.Intersect(localized.Keys, StringComparer.Ordinal).ToArray();
        var presentKeys = storedKeys.Where(key => !string.IsNullOrWhiteSpace(localized!.GetValue(key))).ToArray();
        var groupIssues = issues.ToArray();
        return new LocalizationCoverageReport.GroupCoverage(
            group, locale, localized is not null, english.Keys.Count, localized?.Keys.Count ?? 0,
            presentKeys.Length, english.Keys.Count - storedKeys.Length, storedKeys.Length - presentKeys.Length,
            presentKeys.Count(key => string.Equals(english.GetValue(key), localized!.GetValue(key), StringComparison.Ordinal)),
            presentKeys.Count(key => LocalizationFormatClassification.IsPlainText(group, key)),
            groupIssues.Count(issue => issue.Code == "placeholder_mismatch"),
            groupIssues.Count(issue => issue.Severity == LocalizationCoverageReport.WarningSeverity));
    }

    private static void AddIssue(
        ICollection<LocalizationCoverageReport.Issue> issues,
        string group,
        string locale,
        string code,
        string? key,
        string message,
        string severity = LocalizationCoverageReport.ErrorSeverity)
    {
        issues.Add(new LocalizationCoverageReport.Issue(group, locale, severity, code, key, message));
    }

    private static void WriteSummary(LocalizationCoverageReport report)
    {
        Console.WriteLine("Locale | Group | Present/Source | Fallback | Empty | Same as English | Plain-text keys | Placeholder errors | Warnings");
        foreach (var group in report.Groups)
        {
            Console.WriteLine($"{group.Locale} | {group.Group} | {group.PresentKeys}/{group.SourceKeys} | {group.FallbackKeys} | {group.EmptyKeys} | {group.SameAsEnglish} | {group.PlainTextKeys} | {group.PlaceholderMismatches} | {group.Warnings}");
        }

        foreach (var entry in report.PlainTextClassifications)
        {
            Console.WriteLine($"Plain-text classification: {entry.Group}.{entry.Key}. {entry.Reason}");
        }

        Console.WriteLine($"Resource audit: {report.Locales.Count} locale(s), {report.Groups.Count} group/locale pair(s), {report.ErrorCount} error(s), {report.WarningCount} warning(s).");
        Console.WriteLine("Presence is not translation quality; identical English text is counted separately, and warning-only legacy text does not fail the audit.");
    }
}
