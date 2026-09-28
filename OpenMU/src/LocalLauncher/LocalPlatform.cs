// <copyright file="LocalPlatform.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("MUnique.OpenMU.LocalLauncher.Tests")]

namespace MUnique.OpenMU.LocalLauncher;

/// <summary>Desktop platform policies shared by the game and GM launchers.</summary>
public static class LocalPlatform
{
    /// <summary>Owner-only read and write permissions.</summary>
    public const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>Gets the host's filesystem path comparison.</summary>
    public static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Resolves a native executable without assuming a Windows suffix.</summary>
    public static string ExecutableName(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

    /// <summary>Resolves Unix parent-directory aliases before comparing a live executable to its package.</summary>
    internal static string ResolveExecutablePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows())
        {
            return fullPath;
        }

        const int maximumLinks = 40;
        for (var followedLinks = 0; followedLinks < maximumLinks; followedLinks++)
        {
            var root = Path.GetPathRoot(fullPath)!;
            var segments = fullPath[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            var redirected = false;
            for (var index = 0; index < segments.Length; index++)
            {
                current = Path.Combine(current, segments[index]);
                FileSystemInfo entry = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
                if (entry.ResolveLinkTarget(returnFinalTarget: false) is not { } target)
                {
                    continue;
                }

                fullPath = Path.GetFullPath(Path.Combine(new[] { target.FullName }.Concat(segments.Skip(index + 1)).ToArray()));
                redirected = true;
                break;
            }

            if (!redirected)
            {
                return fullPath;
            }
        }

        throw new IOException("Too many symbolic links in the packaged executable path.");
    }

    /// <summary>Rejects mobile platforms until the embedded server port exists.</summary>
    public static void RequireUnixDesktop()
    {
        if ((!OperatingSystem.IsLinux() || OperatingSystem.IsAndroid()) && !OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("The local desktop server supports Linux and macOS, not mobile app sandboxes.");
        }
    }

    /// <summary>Creates a private application-owned directory without following a leaf link.</summary>
    public static void CreatePrivateDirectory(string path)
    {
        RejectLink(path);
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            WindowsDirectorySecurity.RestrictToCurrentUser(path);
            return;
        }

        RequireUnixDesktop();
        var mode = PrivateFileMode | UnixFileMode.UserExecute;
        Directory.CreateDirectory(path, mode);
        File.SetUnixFileMode(path, mode);
    }

    /// <summary>Rejects a redirected application-owned file or directory.</summary>
    public static void RejectLink(string path)
    {
        if (new FileInfo(path).LinkTarget is not null || new DirectoryInfo(path).LinkTarget is not null)
        {
            throw new InvalidDataException($"Application data path must not be a symbolic link: {path}");
        }
    }
}
