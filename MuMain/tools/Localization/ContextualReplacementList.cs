using System.Text.Json;
using System.Text.RegularExpressions;

namespace MuMain.Tools.Localization;

internal sealed class ContextualReplacementList
{
    private readonly IReadOnlyList<ReplacementRule> _rules;

    private ContextualReplacementList(IReadOnlyList<ReplacementRule> rules)
    {
        this._rules = rules;
    }

    public static ContextualReplacementList Load(string path)
    {
        using var stream = File.OpenRead(path);
        var document = JsonSerializer.Deserialize<ReplacementDocument>(stream)
            ?? throw new LocalizationToolException($"Invalid contextual replacement file: {path}");
        var rules = document.Rules.Select(CreateRule).ToArray();
        return new ContextualReplacementList(rules);
    }

    public string Apply(string group, string key, string value)
    {
        return this.MatchingRules(group, key).Aggregate(
            value,
            (current, rule) => rule.Replacements.Aggregate(
                current,
                (text, replacement) => text.Replace(
                    replacement.Key,
                    replacement.Value,
                    StringComparison.Ordinal)));
    }

    public IEnumerable<(string Found, string Preferred)> FindRemaining(string group, string key, string value)
    {
        return this.MatchingRules(group, key)
            .SelectMany(rule => rule.Replacements)
            .Where(replacement => ContainsUnreplacedTerm(value, replacement))
            .Select(replacement => (replacement.Key, replacement.Value));
    }

    private static bool ContainsUnreplacedTerm(string value, KeyValuePair<string, string> replacement)
    {
        var withoutPreferredTerm = value.Replace(replacement.Value, string.Empty, StringComparison.Ordinal);
        return withoutPreferredTerm.Contains(replacement.Key, StringComparison.Ordinal);
    }

    private static ReplacementRule CreateRule(ReplacementRuleDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Group) || string.IsNullOrWhiteSpace(document.KeyPattern))
        {
            throw new LocalizationToolException("Contextual replacement rules require group and keyPattern.");
        }

        if (document.Replacements.Count == 0 || document.Replacements.Any(pair => string.IsNullOrEmpty(pair.Key)))
        {
            throw new LocalizationToolException($"Contextual replacement rule '{document.KeyPattern}' has no usable replacements.");
        }

        return new ReplacementRule(
            document.Group,
            new Regex(document.KeyPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant),
            document.Replacements);
    }

    private IEnumerable<ReplacementRule> MatchingRules(string group, string key)
    {
        return this._rules.Where(rule =>
            string.Equals(rule.Group, group, StringComparison.Ordinal)
            && rule.KeyPattern.IsMatch(key));
    }

    private sealed record ReplacementRule(
        string Group,
        Regex KeyPattern,
        IReadOnlyDictionary<string, string> Replacements);

    private sealed class ReplacementDocument
    {
        public List<ReplacementRuleDocument> Rules { get; init; } = [];
    }

    private sealed class ReplacementRuleDocument
    {
        public string Group { get; init; } = string.Empty;

        public string KeyPattern { get; init; } = string.Empty;

        public Dictionary<string, string> Replacements { get; init; } = new(StringComparer.Ordinal);
    }
}
