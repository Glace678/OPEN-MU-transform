namespace MuMain.Tools.Localization;

internal static class LocalizationFormatClassification
{
    public static IReadOnlyList<LocalizationCoverageReport.PlainTextClassification> PlainTextKeys { get; } = [
        new("Game", "OnePercentLow", "Performance label is passed directly to RenderText; the diagnostic number uses a separate format."),
    ];

    public static bool IsPlainText(string group, string key)
    {
        return PlainTextKeys.Any(entry => entry.Group == group && entry.Key == key);
    }
}
