namespace MuMain.Tools.Localization;

internal sealed record LocalizationCoverageReport(
    string BaselineLocale,
    IReadOnlyList<string> Locales,
    IReadOnlyList<LocalizationCoverageReport.GroupCoverage> Groups,
    IReadOnlyList<LocalizationCoverageReport.Issue> Issues,
    IReadOnlyList<LocalizationCoverageReport.PlainTextClassification> PlainTextClassifications)
{
    public const string ErrorSeverity = "error";
    public const string WarningSeverity = "warning";

    public int SchemaVersion => 1;

    public int ErrorCount => this.Issues.Count(issue => issue.Severity == ErrorSeverity);

    public int WarningCount => this.Issues.Count(issue => issue.Severity == WarningSeverity);

    internal sealed record GroupCoverage(
        string Group,
        string Locale,
        bool ResourceFileExists,
        int SourceKeys,
        int Entries,
        int PresentKeys,
        int FallbackKeys,
        int EmptyKeys,
        int SameAsEnglish,
        int PlainTextKeys,
        int PlaceholderMismatches,
        int Warnings);

    internal sealed record PlainTextClassification(string Group, string Key, string Reason);

    internal sealed record Issue(
        string Group,
        string Locale,
        string Severity,
        string Code,
        string? Key,
        string Message);
}
