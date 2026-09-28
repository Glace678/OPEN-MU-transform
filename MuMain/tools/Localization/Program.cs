namespace MuMain.Tools.Localization;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var command = LocalizationCommandLine.Parse(args);
            return command.Name switch
            {
                "generate-zh-cn" => await ZhCnResourceGenerator.GenerateAsync(command).ConfigureAwait(false),
                "audit" => LocalizationAuditor.Audit(command),
                "report" => LocalizationCoverageReporter.Run(command),
                _ => throw new LocalizationToolException($"Unknown command: {command.Name}"),
            };
        }
        catch (LocalizationToolException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(LocalizationCommandLine.Usage);
            return 1;
        }
    }
}
