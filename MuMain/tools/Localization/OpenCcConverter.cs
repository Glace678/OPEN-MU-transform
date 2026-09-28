using System.Diagnostics;
using System.Text.Json;

namespace MuMain.Tools.Localization;

internal sealed class OpenCcConverter
{
    private const string ConfigurationName = "tw2sp.json";

    private readonly string _modulePath;
    private readonly string _bridgePath;

    public OpenCcConverter(string modulePath)
    {
        this._modulePath = Path.GetFullPath(modulePath);
        this._bridgePath = Path.Combine(AppContext.BaseDirectory, "OpenCcBridge.mjs");
        if (!Directory.Exists(this._modulePath))
        {
            throw new LocalizationToolException($"OpenCC module directory not found: {this._modulePath}");
        }
    }

    public async Task<IReadOnlyList<string>> ConvertAsync(IReadOnlyList<string> values)
    {
        var startInfo = new ProcessStartInfo("node")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(this._bridgePath);
        startInfo.ArgumentList.Add(this._modulePath);
        startInfo.ArgumentList.Add(ConfigurationName);

        using var process = Process.Start(startInfo)
            ?? throw new LocalizationToolException("Failed to start Node.js for OpenCC conversion.");
        await JsonSerializer.SerializeAsync(process.StandardInput.BaseStream, values).ConfigureAwait(false);
        process.StandardInput.Close();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new LocalizationToolException($"OpenCC conversion failed: {error.Trim()}");
        }

        return JsonSerializer.Deserialize<string[]>(output)
            ?? throw new LocalizationToolException("OpenCC returned invalid JSON.");
    }
}
