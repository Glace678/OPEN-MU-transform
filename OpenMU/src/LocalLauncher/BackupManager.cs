// <copyright file="BackupManager.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using System.IO.Compression;
using System.Text.Json;

/// <summary>
/// Creates and restores self-contained local OpenMU backups.
/// </summary>
public sealed class BackupManager
{
    private readonly LocalPaths _paths;
    private readonly PostgreSqlExecutionPaths _executionPaths;
    private readonly LocalStackSettings _settings;
    private readonly ProcessRunner _processRunner;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackupManager"/> class.
    /// </summary>
    /// <param name="paths">The package paths.</param>
    /// <param name="executionPaths">The verified ASCII PostgreSQL execution paths.</param>
    /// <param name="settings">The local settings.</param>
    /// <param name="processRunner">The process runner.</param>
    internal BackupManager(LocalPaths paths, PostgreSqlExecutionPaths executionPaths, LocalStackSettings settings, ProcessRunner processRunner)
    {
        this._paths = paths;
        this._executionPaths = executionPaths;
        this._settings = settings;
        this._processRunner = processRunner;
    }

    /// <summary>
    /// Creates a compressed database and launcher-state backup.
    /// </summary>
    /// <param name="secrets">The local secrets.</param>
    /// <param name="reason">A filesystem-safe reason suffix.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The backup archive path.</returns>
    public async Task<string> CreateAsync(LocalSecrets secrets, string reason, CancellationToken cancellationToken)
    {
        ValidateReason(reason);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture);
        var archivePath = Path.Combine(this._paths.BackupsDirectory, $"OpenMU-{timestamp}-{reason}.zip");
        var workDirectory = Path.Combine(this._paths.BackupsDirectory, $".backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDirectory);
        try
        {
            var dumpPath = Path.Combine(workDirectory, "openmu.dump");
            var executionDumpPath = this.GetExecutionBackupPath(dumpPath);
            this._executionPaths.Validate();
            var result = await this._processRunner.RunAsync(
                this._executionPaths.PgDumpExecutable,
                new[]
                {
                    "--host", "127.0.0.1",
                    "--port", this._settings.DatabasePort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "--username", "postgres",
                    "--format", "custom",
                    "--create",
                    "--file", executionDumpPath,
                    "openmu",
                },
                this._executionPaths.RuntimeDirectory,
                new Dictionary<string, string?> { ["PGPASSWORD"] = secrets.DatabaseAdminPassword },
                cancellationToken).ConfigureAwait(false);
            result.EnsureSuccess("备份 OpenMU 数据库");

            CopyIfPresent(this._paths.SecretsFile, Path.Combine(workDirectory, Path.GetFileName(this._paths.SecretsFile)));
            CopyIfPresent(this._paths.SettingsFile, Path.Combine(workDirectory, "local-settings.json"));
            CopyIfPresent(this._paths.ManifestFile, Path.Combine(workDirectory, "manifest.json"));
            if (Directory.Exists(this._paths.DataProtectionKeysDirectory))
            {
                CopyDirectory(this._paths.DataProtectionKeysDirectory, Path.Combine(workDirectory, "data-protection-keys"));
            }

            ZipFile.CreateFromDirectory(workDirectory, archivePath, CompressionLevel.Optimal, includeBaseDirectory: false);
        }
        catch
        {
            File.Delete(archivePath);
            throw;
        }
        finally
        {
            Directory.Delete(workDirectory, recursive: true);
        }

        if (reason.Equals("auto-stop", StringComparison.OrdinalIgnoreCase))
        {
            ApplyAutomaticRetention(this._paths.BackupsDirectory, this._settings.BackupRetentionCount);
        }

        return archivePath;
    }

    /// <summary>
    /// Restores a database dump from a launcher backup. The stack must be stopped while PostgreSQL remains running.
    /// </summary>
    /// <param name="archivePath">The backup archive.</param>
    /// <param name="secrets">The currently active local secrets.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task RestoreDatabaseAsync(string archivePath, LocalSecrets secrets, CancellationToken cancellationToken)
    {
        var fullArchivePath = Path.GetFullPath(archivePath);
        if (!File.Exists(fullArchivePath))
        {
            throw new FileNotFoundException("选定的备份文件不存在。", fullArchivePath);
        }

        var workDirectory = Path.Combine(this._paths.BackupsDirectory, $".restore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDirectory);
        try
        {
            ZipFile.ExtractToDirectory(fullArchivePath, workDirectory);
            var dumpPath = Path.Combine(workDirectory, "openmu.dump");
            if (!File.Exists(dumpPath) || new FileInfo(dumpPath).Length == 0)
            {
                throw new InvalidDataException("选定的备份中没有有效的 openmu.dump 文件。");
            }

            this.ValidateArchiveManifest(Path.Combine(workDirectory, "manifest.json"));

            var executionDumpPath = this.GetExecutionBackupPath(dumpPath);
            this._executionPaths.Validate();
            var result = await this._processRunner.RunAsync(
                this._executionPaths.PgRestoreExecutable,
                new[]
                {
                    "--host", "127.0.0.1",
                    "--port", this._settings.DatabasePort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "--username", "postgres",
                    "--dbname", "postgres",
                    "--clean",
                    "--if-exists",
                    "--create",
                    executionDumpPath,
                },
                this._executionPaths.RuntimeDirectory,
                new Dictionary<string, string?> { ["PGPASSWORD"] = secrets.DatabaseAdminPassword },
                cancellationToken).ConfigureAwait(false);
            result.EnsureSuccess("还原 OpenMU 数据库");
            var archivedKeyDirectory = Path.Combine(workDirectory, "data-protection-keys");
            if (Directory.Exists(archivedKeyDirectory))
            {
                CopyDirectory(archivedKeyDirectory, this._paths.DataProtectionKeysDirectory);
            }
        }
        finally
        {
            Directory.Delete(workDirectory, recursive: true);
        }
    }

    /// <summary>Rejects backups created by a newer package version.</summary>
    /// <param name="archivedVersion">The package version stored in the backup.</param>
    /// <param name="currentVersion">The currently installed package version.</param>
    internal static void EnsureRestoreVersionSupported(string? archivedVersion, string? currentVersion)
    {
        if (!PackageVersion.TryParse(archivedVersion, out var archivedComparableVersion)
            || !PackageVersion.TryParse(currentVersion, out var currentComparableVersion))
        {
            throw new InvalidDataException("备份或当前程序包的版本清单无效。");
        }

        if (archivedComparableVersion.CompareTo(currentComparableVersion) > 0)
        {
            throw new InvalidDataException("此备份由更新版本的 OpenMU 本地版创建，当前版本无法还原。");
        }
    }

    /// <summary>Applies retention only to automatic stop backups.</summary>
    /// <param name="backupsDirectory">The managed backup directory.</param>
    /// <param name="retentionCount">The number of automatic backups to retain.</param>
    internal static void ApplyAutomaticRetention(string backupsDirectory, int retentionCount)
    {
        var retention = Math.Max(1, retentionCount);
        foreach (var obsolete in new DirectoryInfo(backupsDirectory)
                     .EnumerateFiles("OpenMU-*-auto-stop.zip")
                     .OrderByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
                     .Skip(retention))
        {
            obsolete.Delete();
        }
    }

    private static void CopyIfPresent(string source, string destination)
    {
        if (File.Exists(source))
        {
            File.Copy(source, destination, overwrite: true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("备份标识不能为空，且不能包含文件名禁用字符。", nameof(reason));
        }
    }

    private static bool TryGetManifestIdentity(JsonElement root, out string? version)
    {
        version = null;
        return root.ValueKind == JsonValueKind.Object
               && root.TryGetProperty("formatVersion", out var formatVersion)
               && formatVersion.ValueKind == JsonValueKind.Number
               && formatVersion.TryGetInt32(out var format)
               && format == 1
               && root.TryGetProperty("version", out var versionElement)
               && versionElement.ValueKind == JsonValueKind.String
               && !string.IsNullOrWhiteSpace(version = versionElement.GetString());
    }

    private void ValidateArchiveManifest(string archivedManifestPath)
    {
        if (!File.Exists(archivedManifestPath) || !File.Exists(this._paths.ManifestFile))
        {
            throw new InvalidDataException("备份或当前程序包缺少版本清单。");
        }

        using var archived = JsonDocument.Parse(File.ReadAllBytes(archivedManifestPath));
        using var current = JsonDocument.Parse(File.ReadAllBytes(this._paths.ManifestFile));
        if (!TryGetManifestIdentity(archived.RootElement, out var archivedVersion)
            || !TryGetManifestIdentity(current.RootElement, out var currentVersion))
        {
            throw new InvalidDataException("备份或当前程序包的版本清单无效。");
        }

        EnsureRestoreVersionSupported(archivedVersion, currentVersion);
    }

    private string GetExecutionBackupPath(string packageBackupPath)
    {
        var relativePath = Path.GetRelativePath(this._paths.BackupsDirectory, packageBackupPath);
        if (Path.IsPathRooted(relativePath)
            || relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidDataException("PostgreSQL 备份工作文件必须位于受管理的备份目录中。");
        }

        return Path.Combine(this._executionPaths.BackupsDirectory, relativePath);
    }
}
