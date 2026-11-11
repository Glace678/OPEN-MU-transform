using System.Text.RegularExpressions;

namespace MuMain.Tools.Localization;

internal static partial class PlaceholderScanner
{
    public static IReadOnlyList<string> Scan(string value)
    {
        var formatText = value.Replace("%%", string.Empty, StringComparison.Ordinal);
        var printfPlaceholders = PrintfPlaceholder()
            .Matches(formatText)
            .Select(match => Normalize(match.Value));
        var indexedPlaceholders = IndexedPlaceholder()
            .Matches(formatText)
            .Select(match => match.Value)
            .Order(StringComparer.Ordinal);
        return printfPlaceholders
            .Concat(indexedPlaceholders)
            .ToArray();
    }

    private static string Normalize(string placeholder)
    {
        var type = placeholder[^1];
        return char.ToLowerInvariant(type).ToString();
    }

    [GeneratedRegex(@"%(?!%)(?:\d+\$)?[-+ #0']*(?:\d+|\*)?(?:\.(?:\d+|\*))?(?:hh|h|ll|l|j|z|t|L|I32|I64)?[diuoxXfFeEgGaAcCsSpn]", RegexOptions.CultureInvariant)]
    private static partial Regex PrintfPlaceholder();

    [GeneratedRegex(@"\{\d+(?:,[^}:]+)?(?:\:[^}]+)?\}", RegexOptions.CultureInvariant)]
    private static partial Regex IndexedPlaceholder();
}
