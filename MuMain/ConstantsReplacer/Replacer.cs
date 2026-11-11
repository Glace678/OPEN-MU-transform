namespace ConstantsReplacer;

using System.Text;
using System.Text.RegularExpressions;

internal class Replacer
{
    private readonly Regex _constantsRegex = new(@"(\b.*\b)\s*=\s*(\b.*\b)", RegexOptions.Compiled);

    public async Task SearchAndReplaceAsync(string folder, string fileNamePattern, string searchPattern, string constantsString, CancellationToken cancellationToken = default, IProgress<(int Total, int Current)>? progress = null)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        cancellationToken.ThrowIfCancellationRequested();

        var constLookupTable = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in this._constantsRegex.Matches(constantsString))
        {
            constLookupTable[match.Groups[2].Value.Replace(" ", string.Empty)] = match.Groups[1].Value;
        }

        var searchRegex = new Regex(searchPattern, RegexOptions.Compiled);
        var files = System.IO.Directory.GetFiles(folder, fileNamePattern, System.IO.SearchOption.AllDirectories);

        var koreanEncoding = Encoding.GetEncoding(51949);
        for (var index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = files[index];
            progress?.Report((files.Length, index));
            var content = FileEncoding.TryLoadFile(file, koreanEncoding);
            var replaced = false;
            var lastMatch = 0;
            while (searchRegex.Match(content, lastMatch) is { Success: true } match)
            {
                if (match.Length == 0)
                {
                    lastMatch = match.Index + 1;
                    if (lastMatch > content.Length)
                    {
                        break;
                    }

                    continue;
                }

                lastMatch = match.Index + match.Length;
                if (match.Groups.Count > 1 && constLookupTable.TryGetValue(match.Groups[1].Value.Replace(" ", string.Empty), out var constant))
                {
                    content = content.Remove(match.Index, match.Length).Insert(match.Index, constant);
                    lastMatch = match.Index + constant.Length;
                    replaced = true;
                }
            }

            if (replaced)
            {
                var tempFile = $"{file}.{Guid.NewGuid():N}.tmp";
                try
                {
                    await File.WriteAllTextAsync(tempFile, content, Encoding.UTF8, cancellationToken); // let's always write in UTF8 with BOM.
                    File.Move(tempFile, file, overwrite: true);
                }
                finally
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
            }
        }
    }
}