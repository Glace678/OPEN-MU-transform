namespace MuMain.Tools.Localization;

internal sealed class BannedTermList
{
    private readonly IReadOnlyList<(string Banned, string Preferred)> _terms;

    private BannedTermList(IReadOnlyList<(string Banned, string Preferred)> terms)
    {
        this._terms = terms;
    }

    public static BannedTermList Load(string path)
    {
        var terms = new List<(string Banned, string Preferred)>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var columns = line.Split('\t');
            if (columns.Length != 2 || columns.Any(string.IsNullOrWhiteSpace))
            {
                throw new LocalizationToolException($"Invalid banned-term row: {line}");
            }

            terms.Add((columns[0], columns[1]));
        }

        return new BannedTermList(terms);
    }

    public IEnumerable<(string Banned, string Preferred)> FindIn(string value)
    {
        return this._terms.Where(term => value.Contains(term.Banned, StringComparison.Ordinal));
    }

    public string Apply(string value)
    {
        return this._terms.Aggregate(
            value,
            (current, term) => current.Replace(term.Banned, term.Preferred, StringComparison.Ordinal));
    }
}
