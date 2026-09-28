namespace MuMain.Tools.Localization;

internal static class LocalizationAuditor
{
    private const string EnglishLocale = "en";

    public static int Audit(LocalizationCommand command)
    {
        var inputDirectory = Path.GetFullPath(command.Require("input"));
        var locale = command.Require("locale");
        var bannedTerms = BannedTermList.Load(command.Require("banned-terms"));
        var contextualTerms = ContextualReplacementList.Load(command.Require("context-terms"));
        var failures = new List<string>();

        foreach (var englishPath in Directory.EnumerateFiles(inputDirectory, $"*.{EnglishLocale}.resx"))
        {
            var group = Path.GetFileName(englishPath)[..^($".{EnglishLocale}.resx".Length)];
            AuditGroup(inputDirectory, group, locale, bannedTerms, contextualTerms, failures);
        }

        foreach (var failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        if (failures.Count > 0)
        {
            Console.Error.WriteLine($"Localization audit failed with {failures.Count} issue(s).");
            return 1;
        }

        Console.WriteLine($"Localization audit passed for {locale}.");
        return 0;
    }

    private static void AuditGroup(
        string inputDirectory,
        string group,
        string locale,
        BannedTermList bannedTerms,
        ContextualReplacementList contextualTerms,
        ICollection<string> failures)
    {
        var english = ResxDocument.Load(ResourcePath(inputDirectory, group, EnglishLocale));
        var localized = ResxDocument.Load(ResourcePath(inputDirectory, group, locale));

        foreach (var missing in english.Keys.Except(localized.Keys, StringComparer.Ordinal))
        {
            failures.Add($"{group}.{locale}: missing key '{missing}'.");
        }

        foreach (var extra in localized.Keys.Except(english.Keys, StringComparer.Ordinal))
        {
            failures.Add($"{group}.{locale}: unknown key '{extra}'.");
        }

        foreach (var key in english.Keys.Intersect(localized.Keys, StringComparer.Ordinal))
        {
            AuditValue(
                group,
                locale,
                key,
                english.GetValue(key),
                localized.GetValue(key),
                bannedTerms,
                contextualTerms,
                failures);
        }
    }

    private static void AuditValue(
        string group,
        string locale,
        string key,
        string english,
        string localized,
        BannedTermList bannedTerms,
        ContextualReplacementList contextualTerms,
        ICollection<string> failures)
    {
        var prefix = $"{group}.{locale} '{key}'";
        if (string.IsNullOrWhiteSpace(localized))
        {
            failures.Add($"{prefix}: empty translation.");
            return;
        }

        AuditPlaceholders(group, key, prefix, english, localized, failures);

        if (LocalizationTextChecks.CountLogicalLineBreaks(english) != LocalizationTextChecks.CountLogicalLineBreaks(localized))
        {
            failures.Add($"{prefix}: line break count differs from English source.");
        }

        if (LocalizationTextChecks.LooksCorruptedZhCn(localized))
        {
            failures.Add($"{prefix}: possible mojibake or replacement text.");
        }

        foreach (var (banned, preferred) in bannedTerms.FindIn(localized))
        {
            failures.Add($"{prefix}: contains Taiwan term '{banned}', prefer '{preferred}'.");
        }

        foreach (var (found, preferred) in contextualTerms.FindRemaining(group, key, localized))
        {
            failures.Add($"{prefix}: contains contextual term '{found}', prefer '{preferred}'.");
        }
    }

    private static string ResourcePath(string directory, string group, string locale)
    {
        return Path.Combine(directory, $"{group}.{locale}.resx");
    }

    private static void AuditPlaceholders(
        string group,
        string key,
        string prefix,
        string english,
        string localized,
        ICollection<string> failures)
    {
        if (!LocalizationFormatClassification.IsPlainText(group, key)
            && !PlaceholderScanner.Scan(english).SequenceEqual(PlaceholderScanner.Scan(localized)))
        {
            failures.Add($"{prefix}: placeholders differ from English source.");
        }
    }

}
