using System.Xml.Linq;

namespace MuMain.Tools.Localization.Tests;

internal sealed class ResourceFixture : IDisposable
{
    private readonly string _temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);

    public ResourceFixture()
    {
        this.DirectoryPath = Path.Combine(this._temporaryRoot, $"MuMain-localization-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.DirectoryPath);
    }

    public string DirectoryPath { get; }

    public void Write(string group, string locale, params (string Key, string Value)[] entries)
    {
        var document = new XDocument(new XElement("root", entries.Select(entry =>
            new XElement("data", new XAttribute("name", entry.Key), new XElement("value", entry.Value)))));
        document.Save(Path.Combine(this.DirectoryPath, $"{group}.{locale}.resx"));
    }

    public LocalizationCoverageReport Report(string? locale = null)
    {
        return LocalizationCoverageReporter.Create(this.DirectoryPath, locale);
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(this.DirectoryPath);
        if (!string.Equals(Path.GetDirectoryName(resolved), this._temporaryRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Test fixture cleanup escaped its temporary directory.");
        }

        Directory.Delete(resolved, recursive: true);
    }
}
