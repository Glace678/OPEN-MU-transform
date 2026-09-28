// <copyright file="PackageManifestValidator.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using System.Security.Cryptography;
using System.Text.Json;

/// <summary>
/// Validates required portable-package files before a stack is started.
/// </summary>
public sealed class PackageManifestValidator
{
    private const string MutableGameConfigurationPath = "App/Game/config.ini";
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly string[] RequiredFiles =
    {
        "OpenMU-Local.exe",
        "README-简体中文.txt",
        "App/Server/MUnique.OpenMU.Startup.exe",
        "App/Game/Main.exe",
        "App/Game/MUnique.Client.Library.dll",
        "App/Game/config.ini.template",
        "Runtime/PostgreSQL/bin/initdb.exe",
        "Runtime/PostgreSQL/bin/pg_ctl.exe",
        "Runtime/PostgreSQL/bin/pg_isready.exe",
        "Runtime/PostgreSQL/bin/pg_dump.exe",
        "Runtime/PostgreSQL/bin/pg_restore.exe",
        "Runtime/PostgreSQL/bin/vcruntime140.dll",
        "App/Game/msvcp140.dll",
        "App/Game/vcruntime140.dll",
        "App/Game/vcruntime140_1.dll",
        "Licenses/OpenMU-MIT.txt",
        "Licenses/Microsoft-Visual-Cpp-Redistributables.txt",
        "Licenses/PostgreSQL.txt",
        "Licenses/PostgreSQL-ThirdParty.txt",
    };

    private static readonly string[] UnixRequiredFiles =
    {
        "App/GameHost/OpenMU-Game",
        "App/GMHost/OpenMU-GM",
        "App/Server/MUnique.OpenMU.Startup",
        "App/Game/Main",
        "App/Game/config.ini.template",
        "Runtime/PostgreSQL/bin/initdb",
        "Runtime/PostgreSQL/bin/pg_ctl",
        "Runtime/PostgreSQL/bin/pg_isready",
        "Runtime/PostgreSQL/bin/pg_dump",
        "Runtime/PostgreSQL/bin/pg_restore",
        "Runtime/PostgreSQL/bin/psql",
        "Licenses/OpenMU-MIT.txt",
        "Licenses/PostgreSQL.txt",
        "README.txt",
    };

    /// <summary>
    /// Validates all immutable manifest entries, including size and SHA-256.
    /// </summary>
    /// <param name="paths">The package paths.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task ValidateAsync(LocalPaths paths, CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.ManifestFile))
        {
            throw new FileNotFoundException("缺少便携程序包清单。", paths.ManifestFile);
        }

        await using var manifestStream = File.OpenRead(paths.ManifestFile);
        var manifest = await JsonSerializer.DeserializeAsync<PackageManifest>(manifestStream, SerializerOptions, cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidDataException("便携程序包清单为空。");
        if (manifest.FormatVersion != 1
            || !PackageVersion.TryParse(manifest.Version, out _)
            || manifest.Files is not { Count: > 0 } entries)
        {
            throw new InvalidDataException("便携程序包清单格式不受支持、版本无效或未列出任何文件。");
        }

        var manifestPaths = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry is null || string.IsNullOrWhiteSpace(entry.Path) || string.IsNullOrWhiteSpace(entry.Sha256))
            {
                throw new InvalidDataException("便携程序包清单中包含空路径或空 SHA-256 值。");
            }

            var normalizedPath = entry.Path.Replace('\\', '/');
            if (!manifestPaths.Add(normalizedPath))
            {
                throw new InvalidDataException($"清单路径“{entry.Path}”被重复列出。");
            }

            if (entry.Size < 0 || entry.Sha256.Length != 64 || !entry.Sha256.All(Uri.IsHexDigit))
            {
                throw new InvalidDataException($"清单项“{entry.Path}”的大小或 SHA-256 值无效。");
            }

            if (IsMutablePackageFile(normalizedPath))
            {
                continue;
            }

            var fullPath = ResolveManifestPath(paths.RootDirectory, entry.Path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"缺少必需的程序包文件“{entry.Path}”。", fullPath);
            }

            var fileInfo = new FileInfo(fullPath);
            if (fileInfo.Length != entry.Size)
            {
                throw new InvalidDataException($"程序包文件“{entry.Path}”的大小为 {fileInfo.Length}，预期为 {entry.Size}。");
            }

            await using var input = fileInfo.OpenRead();
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken).ConfigureAwait(false));
            if (!hash.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"程序包文件“{entry.Path}”未通过 SHA-256 校验。");
            }
        }

        var requiredFiles = OperatingSystem.IsWindows() ? RequiredFiles : UnixRequiredFiles;
        var networkLibrary = OperatingSystem.IsMacOS() ? "App/Game/MUnique.Client.Library.dylib" : "App/Game/MUnique.Client.Library.so";
        var missingRequiredFile = requiredFiles.Concat(OperatingSystem.IsWindows() ? [] : new[] { networkLibrary })
            .FirstOrDefault(required => !manifestPaths.Contains(required));
        if (missingRequiredFile is not null)
        {
            throw new InvalidDataException($"便携程序包清单未包含必需文件“{missingRequiredFile}”。");
        }
    }

    private static bool IsMutablePackageFile(string normalizedPath)
        => normalizedPath.Equals(
            MutableGameConfigurationPath,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string ResolveManifestPath(string rootDirectory, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException($"清单路径“{relativePath}”必须是相对路径。");
        }

        var fullPath = Path.GetFullPath(Path.Combine(rootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(rootDirectory, fullPath);
        if (relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || relative == "..")
        {
            throw new InvalidDataException($"清单路径“{relativePath}”超出了程序包根目录。");
        }

        return fullPath;
    }

    private sealed class PackageManifest
    {
        public int FormatVersion { get; set; }

        public string? Version { get; set; }

        public List<PackageFile>? Files { get; set; }
    }

    private sealed class PackageFile
    {
        public string? Path { get; set; }

        public long Size { get; set; }

        public string? Sha256 { get; set; }
    }
}
