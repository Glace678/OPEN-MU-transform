namespace MuMain.Tools.Localization;

internal sealed record LocalizationCommand(string Name, IReadOnlyDictionary<string, string> Options)
{
    public string Require(string option)
    {
        if (this.Options.TryGetValue(option, out var value))
        {
            return value;
        }

        throw new LocalizationToolException($"Missing required option: --{option}");
    }
}
