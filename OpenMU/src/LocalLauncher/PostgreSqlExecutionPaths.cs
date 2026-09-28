// <copyright file="PostgreSqlExecutionPaths.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

/// <summary>
/// Provides ASCII-only junction paths for PostgreSQL's Windows command-line tools.
/// </summary>
internal sealed class PostgreSqlExecutionPaths
{
    private const uint IoReparseTagMountPoint = 0xA0000003;
    private const uint FsctlSetReparsePoint = 0x000900A4;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagBackupSemantics = 0x02000000;

    private readonly IReadOnlyDictionary<string, string> _junctionTargets;
    private readonly LocalPaths? _nativePaths;

    private PostgreSqlExecutionPaths(string rootDirectory, IReadOnlyDictionary<string, string> junctionTargets, LocalPaths? nativePaths = null)
    {
        this.RootDirectory = rootDirectory;
        this._junctionTargets = junctionTargets;
        this._nativePaths = nativePaths;
    }

    /// <summary>Gets the protected ASCII alias root.</summary>
    public string RootDirectory { get; }

    /// <summary>Gets the ASCII PostgreSQL runtime directory.</summary>
    public string RuntimeDirectory => this._nativePaths?.PostgreSqlRuntimeDirectory ?? Path.Combine(this.RootDirectory, "runtime");

    /// <summary>Gets the ASCII PostgreSQL data directory.</summary>
    public string DataDirectory => this._nativePaths?.PostgreSqlDataDirectory ?? Path.Combine(this.RootDirectory, "data");

    /// <summary>Gets the ASCII protected key directory.</summary>
    public string KeysDirectory => this._nativePaths?.KeysDirectory ?? Path.Combine(this.RootDirectory, "keys");

    /// <summary>Gets the ASCII log directory.</summary>
    public string LogsDirectory => this._nativePaths?.LogsDirectory ?? Path.Combine(this.RootDirectory, "logs");

    /// <summary>Gets the ASCII backup workspace directory.</summary>
    public string BackupsDirectory => this._nativePaths?.BackupsDirectory ?? Path.Combine(this.RootDirectory, "backups");

    /// <summary>Gets the ASCII PostgreSQL control executable.</summary>
    public string PgCtlExecutable => Path.Combine(this.RuntimeDirectory, "bin", LocalPlatform.ExecutableName("pg_ctl"));

    /// <summary>Gets the ASCII PostgreSQL initialization executable.</summary>
    public string InitDbExecutable => Path.Combine(this.RuntimeDirectory, "bin", LocalPlatform.ExecutableName("initdb"));

    /// <summary>Gets the ASCII PostgreSQL readiness executable.</summary>
    public string PgIsReadyExecutable => Path.Combine(this.RuntimeDirectory, "bin", LocalPlatform.ExecutableName("pg_isready"));

    /// <summary>Gets the ASCII PostgreSQL backup executable.</summary>
    public string PgDumpExecutable => Path.Combine(this.RuntimeDirectory, "bin", LocalPlatform.ExecutableName("pg_dump"));

    /// <summary>Gets the ASCII PostgreSQL restore executable.</summary>
    public string PgRestoreExecutable => Path.Combine(this.RuntimeDirectory, "bin", LocalPlatform.ExecutableName("pg_restore"));

    /// <summary>
    /// Creates or validates the current user's protected PostgreSQL execution aliases.
    /// </summary>
    /// <param name="paths">The portable package paths.</param>
    /// <returns>The verified execution paths.</returns>
    public static PostgreSqlExecutionPaths Create(LocalPaths paths)
    {
        if (!OperatingSystem.IsWindows())
        {
            LocalPlatform.RequireUnixDesktop();
            return new PostgreSqlExecutionPaths(paths.DataDirectory, new Dictionary<string, string>(), paths);
        }

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var publicDocuments = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
        var candidates = new[]
        {
            Path.Combine(programData, "OpenMU-Local", "Aliases"),
            Path.Combine(publicDocuments, "OpenMU-Local", "Aliases"),
        };
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (TryPrepareAliasBase(candidate))
            {
                return Create(paths, candidate);
            }
        }

        throw new UnauthorizedAccessException("ProgramData 和 Public Documents 都不允许创建 PostgreSQL ASCII 别名目录。");
    }

    /// <summary>
    /// Creates aliases below an explicit base directory. This overload is intended for validation tests.
    /// </summary>
    /// <param name="paths">The portable package paths.</param>
    /// <param name="aliasBaseDirectory">The ASCII alias base directory.</param>
    /// <returns>The verified execution paths.</returns>
    public static PostgreSqlExecutionPaths Create(LocalPaths paths, string aliasBaseDirectory)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var fullBaseDirectory = Path.GetFullPath(aliasBaseDirectory);
        EnsureAscii(fullBaseDirectory, "PostgreSQL ASCII 别名根目录");

        var currentUser = WindowsIdentity.GetCurrent().User
                          ?? throw new InvalidOperationException("当前 Windows 用户没有可用的安全标识符。");
        var userSegment = $"u-{Hash(currentUser.Value, 20)}";
        var packageSegment = $"p-{Hash(paths.RootDirectory.ToUpperInvariant(), 24)}";
        var userDirectory = Path.Combine(fullBaseDirectory, userSegment);
        var rootDirectory = Path.Combine(userDirectory, packageSegment);

        EnsurePhysicalDirectory(fullBaseDirectory);
        EnsurePhysicalDirectory(userDirectory);
        WindowsDirectorySecurity.RestrictToCurrentUser(userDirectory);
        EnsurePhysicalDirectory(rootDirectory);
        WindowsDirectorySecurity.RestrictToCurrentUser(rootDirectory);

        var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.Combine(rootDirectory, "runtime")] = paths.PostgreSqlRuntimeDirectory,
            [Path.Combine(rootDirectory, "data")] = paths.PostgreSqlDataDirectory,
            [Path.Combine(rootDirectory, "keys")] = paths.KeysDirectory,
            [Path.Combine(rootDirectory, "logs")] = paths.LogsDirectory,
            [Path.Combine(rootDirectory, "backups")] = paths.BackupsDirectory,
        };

        foreach (var pair in targets)
        {
            EnsureJunction(pair.Key, pair.Value);
        }

        var result = new PostgreSqlExecutionPaths(rootDirectory, targets);
        result.Validate();
        return result;
    }

    /// <summary>Rejects aliases which were replaced or redirected after creation.</summary>
    public void Validate()
    {
        if (this._nativePaths is not null)
        {
            foreach (var path in new[] { this.RootDirectory, this.DataDirectory, this.KeysDirectory, this.LogsDirectory, this.BackupsDirectory })
            {
                LocalPlatform.RejectLink(path);
            }

            return;
        }

        EnsureAscii(this.RootDirectory, "PostgreSQL ASCII 别名目录");
        foreach (var pair in this._junctionTargets)
        {
            ValidateJunction(pair.Key, pair.Value);
        }
    }

    /// <summary>Rejects a resolved junction target which does not exactly match its expected package directory.</summary>
    /// <param name="junctionPath">The alias path used in diagnostics.</param>
    /// <param name="resolvedTargetPath">The resolved immediate junction target.</param>
    /// <param name="expectedTargetPath">The expected package directory.</param>
    internal static void ValidateResolvedTarget(string junctionPath, string resolvedTargetPath, string expectedTargetPath)
    {
        var actual = Path.TrimEndingDirectorySeparator(Path.GetFullPath(resolvedTargetPath));
        var expected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(expectedTargetPath));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"PostgreSQL ASCII 别名目标不匹配。别名：{junctionPath}；期望：{expected}；实际：{actual}");
        }
    }

    private static void EnsurePhysicalDirectory(string path)
    {
        ValidatePhysicalAncestors(path);
        if (Directory.Exists(path))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException($"PostgreSQL ASCII 别名父目录不能是重解析点：{path}");
            }

            return;
        }

        if (File.Exists(path))
        {
            throw new InvalidDataException($"PostgreSQL ASCII 别名目录被同名文件占用：{path}");
        }

        Directory.CreateDirectory(path);
        ValidatePhysicalAncestors(path);
    }

    private static void EnsureJunction(string junctionPath, string targetPath)
    {
        var fullTargetPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetPath));
        if (!Directory.Exists(fullTargetPath))
        {
            throw new DirectoryNotFoundException($"PostgreSQL 别名目标目录不存在：{fullTargetPath}");
        }

        if (Directory.Exists(junctionPath))
        {
            ValidateJunction(junctionPath, fullTargetPath);
            return;
        }

        if (File.Exists(junctionPath))
        {
            throw new InvalidDataException($"PostgreSQL ASCII 别名被同名文件占用：{junctionPath}");
        }

        Directory.CreateDirectory(junctionPath);
        try
        {
            CreateJunction(junctionPath, fullTargetPath);
            ValidateJunction(junctionPath, fullTargetPath);
        }
        catch
        {
            if (Directory.Exists(junctionPath)
                && (File.GetAttributes(junctionPath) & FileAttributes.ReparsePoint) == 0
                && !Directory.EnumerateFileSystemEntries(junctionPath).Any())
            {
                Directory.Delete(junctionPath);
            }

            throw;
        }
    }

    private static void ValidateJunction(string junctionPath, string expectedTargetPath)
    {
        var directory = new DirectoryInfo(junctionPath);
        if (!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) == 0)
        {
            throw new InvalidDataException($"PostgreSQL ASCII 别名不是受信任的目录 junction：{junctionPath}");
        }

        var resolvedTarget = directory.ResolveLinkTarget(returnFinalTarget: false)
                             ?? throw new InvalidDataException($"无法解析 PostgreSQL ASCII 别名：{junctionPath}");
        ValidateResolvedTarget(junctionPath, resolvedTarget.FullName, expectedTargetPath);
    }

    private static bool TryPrepareAliasBase(string aliasBaseDirectory)
    {
        EnsureAscii(Path.GetFullPath(aliasBaseDirectory), "PostgreSQL ASCII 别名根目录");
        try
        {
            EnsurePhysicalDirectory(aliasBaseDirectory);
            var probe = Path.Combine(aliasBaseDirectory, $".access-{Guid.NewGuid():N}");
            Directory.CreateDirectory(probe);
            Directory.Delete(probe);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void ValidatePhysicalAncestors(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath) ?? throw new InvalidDataException($"路径缺少文件系统根目录：{fullPath}");
        var current = Path.TrimEndingDirectorySeparator(root);
        foreach (var segment in Path.GetRelativePath(root, fullPath).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException($"PostgreSQL ASCII 别名路径的祖先不能是重解析点：{current}");
            }
        }
    }

    private static void CreateJunction(string junctionPath, string targetPath)
    {
        var substituteName = $@"\??\{targetPath}";
        var substituteBytes = System.Text.Encoding.Unicode.GetBytes(substituteName);
        var printBytes = System.Text.Encoding.Unicode.GetBytes(targetPath);
        var pathBufferLength = checked(substituteBytes.Length + sizeof(char) + printBytes.Length + sizeof(char));
        var reparseDataLength = checked((ushort)(8 + pathBufferLength));
        var buffer = new byte[8 + reparseDataLength];

        WriteUInt32(buffer, 0, IoReparseTagMountPoint);
        WriteUInt16(buffer, 4, reparseDataLength);
        WriteUInt16(buffer, 8, 0);
        WriteUInt16(buffer, 10, checked((ushort)substituteBytes.Length));
        WriteUInt16(buffer, 12, checked((ushort)(substituteBytes.Length + sizeof(char))));
        WriteUInt16(buffer, 14, checked((ushort)printBytes.Length));
        substituteBytes.CopyTo(buffer, 16);
        printBytes.CopyTo(buffer, 16 + substituteBytes.Length + sizeof(char));

        using var handle = CreateFile(
            junctionPath,
            GenericWrite,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOpenReparsePoint | FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"无法打开 PostgreSQL ASCII 别名目录：{junctionPath}");
        }

        if (!DeviceIoControl(handle, FsctlSetReparsePoint, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"无法创建 PostgreSQL ASCII junction：{junctionPath}");
        }
    }

    private static void EnsureAscii(string value, string description)
    {
        if (value.Any(character => character > 0x7F))
        {
            throw new InvalidOperationException($"{description}必须只包含 ASCII 字符：{value}");
        }
    }

    private static string Hash(string value, int hexadecimalLength)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..hexadecimalLength].ToLowerInvariant();
    }

    private static void WriteUInt16(byte[] buffer, int offset, ushort value)
        => System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), value);

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
        => System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), value);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint ioControlCode,
        byte[] inputBuffer,
        int inputBufferSize,
        IntPtr outputBuffer,
        int outputBufferSize,
        out int bytesReturned,
        IntPtr overlapped);
}
