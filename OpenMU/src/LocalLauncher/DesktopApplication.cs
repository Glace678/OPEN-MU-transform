// <copyright file="DesktopApplication.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using System.Runtime.InteropServices;
using System.Text.Json;

/// <summary>Shared command entry for the separate desktop game and GM applications.</summary>
public static class DesktopApplication
{
    /// <summary>Executes one desktop lifecycle command without modifying any unrelated service.</summary>
    public static async Task<int> RunAsync(string[] args, bool startGame)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                LocalPlatform.RequireUnixDesktop();
            }

            var options = ParseArguments(args);
            var paths = ResolvePaths(args);
            var root = paths.RootDirectory;
            var data = paths.DataDirectory;
            var command = options.Keys.FirstOrDefault(k => k != "--root" && k != "--data") ?? "--start";
            if (command == "--help")
            {
                Console.WriteLine("OpenMU desktop: [--root PATH] [--data PATH] [--start|--setup|--stop|--backup|--verify|--probe]");
                return 0;
            }

            if (command == "--probe")
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    application = startGame ? "OpenMU-Game" : "OpenMU-GM",
                    runtime = RuntimeInformation.RuntimeIdentifier,
                    paths.RootDirectory,
                    paths.DataDirectory,
                    paths.GameExecutable,
                    paths.ServerExecutable,
                    paths.PgCtlExecutable,
                    payloadPresent = File.Exists(paths.GameExecutable) && File.Exists(paths.ServerExecutable) && File.Exists(paths.PgCtlExecutable),
                }));
                return 0;
            }

            if (command == "--verify")
            {
                await new PackageManifestValidator().ValidateAsync(paths, CancellationToken.None).ConfigureAwait(false);
                Console.WriteLine("Package hashes verified. This is not a gameplay or hardware acceptance result.");
                return 0;
            }

            paths.EnsureDataDirectories();
            var lockPath = Path.Combine(paths.KeysDirectory, "desktop-lifecycle.lock");
            LocalPlatform.RejectLink(lockPath);
            using var lifecycleLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            using var manager = new LocalStackManager(root, data);
            manager.StatusChanged += status => Console.WriteLine($"{status.State}: {status.Message}");
            await ExecuteAsync(manager, command, startGame).ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            return 1;
        }
    }

    /// <summary>Resolves the common package and save paths for graphical and console launchers.</summary>
    public static LocalPaths ResolvePaths(string[] args)
    {
        var options = ParseArguments(args);
        var root = options.GetValueOrDefault("--root") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".."));
        return new LocalPaths(root, options.GetValueOrDefault("--data") ?? DefaultDataDirectory(root));
    }

    internal static Dictionary<string, string?> ParseArguments(string[] args)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        var commands = new[] { "--start", "--setup", "--stop", "--backup", "--verify", "--probe", "--help" };
        for (var index = 0; index < args.Length; index++)
        {
            var key = args[index];
            string? value = null;
            if (key is "--root" or "--data")
            {
                if (++index == args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Missing path after {key}.");
                }

                value = Path.GetFullPath(args[index]);
            }
            else if (!commands.Contains(key, StringComparer.Ordinal))
            {
                throw new ArgumentException($"Unknown option: {key}");
            }

            if (!result.TryAdd(key, value))
            {
                throw new ArgumentException($"Duplicate option: {key}");
            }
        }

        if (result.Keys.Count(k => commands.Contains(k, StringComparer.Ordinal)) > 1)
        {
            throw new ArgumentException("Specify only one lifecycle command.");
        }

        return result;
    }

    private static string DefaultDataDirectory(string root)
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(root, "Data");
        }

        var basePath = OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support")
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(basePath) || !Path.IsPathFullyQualified(basePath))
        {
            throw new InvalidOperationException("Cannot determine a writable per-user data directory.");
        }

        return Path.Combine(basePath, "OpenMU-Solo");
    }

    private static async Task ExecuteAsync(LocalStackManager manager, string command, bool startGame)
    {
        if (command == "--stop")
        {
            await manager.StopAsync(force: false, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        if (command == "--backup")
        {
            Console.WriteLine(await manager.CreateBackupAsync(CancellationToken.None).ConfigureAwait(false));
            return;
        }

        if (manager.RequiresProvisioning)
        {
            if (command != "--setup")
            {
                throw new InvalidOperationException("First-run setup is required. Run OpenMU-GM --setup in a terminal and choose the local administrator password.");
            }

            var password = ReadPassword("Local administrator password (12 or more characters): ");
            if (!string.Equals(password, ReadPassword("Repeat password: "), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Passwords do not match.");
            }

            manager.Provision(password);
            Console.WriteLine("Local administrator: localadmin. First-run setup saved.");
        }

        if (command != "--setup")
        {
            await manager.StartAsync(startGame, CancellationToken.None).ConfigureAwait(false);
            if (!startGame)
            {
                manager.OpenAdminPanel();
            }
        }
    }

    private static string ReadPassword(string prompt)
    {
        if (Console.IsInputRedirected)
        {
            throw new InvalidOperationException("Administrator setup requires an interactive terminal; passwords are not accepted as command-line arguments.");
        }

        Console.Write(prompt);
        var value = new StringBuilder();
        for (;;)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return value.ToString();
            }

            if (key.Key == ConsoleKey.Escape)
            {
                throw new OperationCanceledException("Setup cancelled.");
            }

            if (key.Key == ConsoleKey.Backspace && value.Length > 0)
            {
                value.Length--;
            }
            else if (!char.IsControl(key.KeyChar) && value.Length < 1024)
            {
                value.Append(key.KeyChar);
            }
        }
    }
}
