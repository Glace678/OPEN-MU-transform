namespace MuMain.Tools.Localization;

internal static class ZhCnResourceGenerator
{
    private const string EnglishLocale = "en";
    private const string TraditionalLocale = "zh-TW";
    private const string SimplifiedLocale = "zh-CN";

    private static readonly string[] ConvertedGroups = ["Game", "Editor", "Metadata"];
    private static readonly string[] OverrideOnlyGroups = ["Dialog"];

    public static async Task<int> GenerateAsync(LocalizationCommand command)
    {
        var inputDirectory = Path.GetFullPath(command.Require("input"));
        var overrides = ZhCnOverrides.Load(command.Require("overrides"));
        var replacements = BannedTermList.Load(command.Require("replacements"));
        var contextualReplacements = ContextualReplacementList.Load(command.Require("context-terms"));
        var converter = new OpenCcConverter(command.Require("opencc-module"));

        foreach (var group in ConvertedGroups)
        {
            await GenerateGroupAsync(
                inputDirectory,
                group,
                overrides.ForGroup(group),
                replacements,
                contextualReplacements,
                converter).ConfigureAwait(false);
        }

        foreach (var group in OverrideOnlyGroups)
        {
            GenerateOverrideOnlyGroup(inputDirectory, group, overrides.ForGroup(group));
        }

        return 0;
    }

    private static async Task GenerateGroupAsync(
        string inputDirectory,
        string group,
        IReadOnlyDictionary<string, string> overrides,
        BannedTermList replacements,
        ContextualReplacementList contextualReplacements,
        OpenCcConverter converter)
    {
        var english = ResxDocument.Load(ResourcePath(inputDirectory, group, EnglishLocale));
        var traditional = ResxDocument.Load(ResourcePath(inputDirectory, group, TraditionalLocale));
        EnsureOverridesExist(group, english.Keys, overrides.Keys);

        var sourceKeys = english.Keys.Where(traditional.Keys.Contains).ToArray();
        var sourceValues = sourceKeys.Select(traditional.GetValue).ToArray();
        var convertedValues = await converter.ConvertAsync(sourceValues).ConfigureAwait(false);
        var convertedByKey = sourceKeys.Zip(convertedValues).ToDictionary(pair => pair.First, pair => pair.Second, StringComparer.Ordinal);

        foreach (var key in english.Keys)
        {
            if (overrides.TryGetValue(key, out var replacement))
            {
                english.SetValue(key, replacement);
                continue;
            }

            if (!convertedByKey.TryGetValue(key, out var converted))
            {
                throw new LocalizationToolException($"{group}.{SimplifiedLocale} requires an override for missing source key '{key}'.");
            }

            var regionalized = replacements.Apply(converted);
            english.SetValue(key, contextualReplacements.Apply(group, key, regionalized));
        }

        var outputPath = ResourcePath(inputDirectory, group, SimplifiedLocale);
        english.Save(outputPath);
        Console.WriteLine($"Generated {Path.GetFileName(outputPath)} ({english.Keys.Count} entries).");
    }

    private static void GenerateOverrideOnlyGroup(
        string inputDirectory,
        string group,
        IReadOnlyDictionary<string, string> overrides)
    {
        var english = ResxDocument.Load(ResourcePath(inputDirectory, group, EnglishLocale));
        EnsureOverridesExist(group, english.Keys, overrides.Keys);
        foreach (var key in english.Keys)
        {
            if (!overrides.TryGetValue(key, out var replacement))
            {
                throw new LocalizationToolException($"{group}.{SimplifiedLocale} requires a translation for '{key}'.");
            }

            english.SetValue(key, replacement);
        }

        var outputPath = ResourcePath(inputDirectory, group, SimplifiedLocale);
        english.Save(outputPath);
        Console.WriteLine($"Generated {Path.GetFileName(outputPath)} ({english.Keys.Count} entries).");
    }

    private static void EnsureOverridesExist(
        string group,
        IReadOnlyCollection<string> resourceKeys,
        IEnumerable<string> overrideKeys)
    {
        var unknownKeys = overrideKeys.Where(key => !resourceKeys.Contains(key)).ToArray();
        if (unknownKeys.Length == 0)
        {
            return;
        }

        throw new LocalizationToolException($"Unknown {group} override keys: {string.Join(", ", unknownKeys)}");
    }

    private static string ResourcePath(string directory, string group, string locale)
    {
        return Path.Combine(directory, $"{group}.{locale}.resx");
    }
}
