using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace MuMain.Tools.Localization;

internal sealed class ResxDocument
{
    private readonly XDocument _document;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<XElement>> _entries;
    private readonly string[] _keys;

    private ResxDocument(XDocument document, IReadOnlyDictionary<string, IReadOnlyList<XElement>> entries)
    {
        this._document = document;
        this._entries = entries;
        this._keys = entries.Keys.ToArray();
    }

    public IReadOnlyCollection<string> Keys => this._keys;

    public IEnumerable<string> DuplicateKeys => this._entries
        .Where(pair => pair.Value.Count > 1)
        .Select(pair => pair.Key)
        .Order(StringComparer.Ordinal);

    public bool ContainsKey(string key) => this._entries.ContainsKey(key);

    public static ResxDocument Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new LocalizationToolException($"Resource file not found: {path}");
        }

        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        var entries = new Dictionary<string, List<XElement>>(StringComparer.Ordinal);
        foreach (var element in document.Root?.Elements("data") ?? [])
        {
            var key = element.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(key) || key.StartsWith(">>", StringComparison.Ordinal))
            {
                continue;
            }

            if (!entries.TryGetValue(key, out var elements))
            {
                elements = [];
                entries.Add(key, elements);
            }

            elements.Add(element);
        }

        return new ResxDocument(
            document,
            entries.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<XElement>)pair.Value,
                StringComparer.Ordinal));
    }

    public string GetValue(string key)
    {
        var entry = this.GetEntries(key)[^1];
        return entry.Element("value")?.Value ?? string.Empty;
    }

    public void SetValue(string key, string value)
    {
        foreach (var entry in this.GetEntries(key))
        {
            var valueElement = entry.Element("value");
            if (valueElement is null)
            {
                throw new LocalizationToolException($"Resource key '{key}' has no value element.");
            }

            valueElement.Value = value;
        }
    }

    public void Save(string path)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
        };
        using var writer = XmlWriter.Create(path, settings);
        this._document.Save(writer);
    }

    private IReadOnlyList<XElement> GetEntries(string key)
    {
        if (this._entries.TryGetValue(key, out var entry))
        {
            return entry;
        }

        throw new LocalizationToolException($"Resource key not found: {key}");
    }
}
