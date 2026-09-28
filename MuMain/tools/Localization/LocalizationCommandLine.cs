namespace MuMain.Tools.Localization;

internal static class LocalizationCommandLine
{
    public const string Usage =
        "Usage:\n" +
        "  LocalizationTool generate-zh-cn --input <LocalizationDir> --overrides <json> --replacements <tsv> --context-terms <json> --opencc-module <dir>\n" +
        "  LocalizationTool audit --input <LocalizationDir> --locale <BCP47> --banned-terms <tsv> --context-terms <json>\n" +
        "  LocalizationTool report --input <LocalizationDir> --output <report.json> [--locale <BCP47>]";

    public static LocalizationCommand Parse(string[] args)
    {
        if (args.Length == 0)
        {
            throw new LocalizationToolException("Missing command.");
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new LocalizationToolException($"Invalid option near argument {index + 1}.");
            }

            options.Add(args[index][2..], args[index + 1]);
        }

        return new LocalizationCommand(args[0], options);
    }
}
