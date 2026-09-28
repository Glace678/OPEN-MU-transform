using System.Text.Json;

namespace MuMain.Tools.Localization;

internal sealed class ZhCnOverrides
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _groups;

    private ZhCnOverrides(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> groups)
    {
        this._groups = groups;
    }

    public static ZhCnOverrides Load(string path)
    {
        using var stream = File.OpenRead(path);
        var groups = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)
            ?? throw new LocalizationToolException($"Invalid override file: {path}");
        return new ZhCnOverrides(groups.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<string, string>)pair.Value,
            StringComparer.Ordinal));
    }

    public IReadOnlyDictionary<string, string> ForGroup(string group)
    {
        return this._groups.TryGetValue(group, out var values)
            ? values
            : new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
