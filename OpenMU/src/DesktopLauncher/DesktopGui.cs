// <copyright file="DesktopGui.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DesktopLauncher;

using Avalonia;
using MUnique.OpenMU.LocalLauncher;

/// <summary>Starts the graphical desktop launcher, retaining explicit command-line operations.</summary>
public static class DesktopGui
{
    /// <summary>Runs on the application's main thread.</summary>
    public static int Run(string[] args, bool startGame)
    {
        if (args.Any(arg => arg is "--start" or "--setup" or "--stop" or "--backup" or "--verify" or "--probe" or "--help"))
        {
            return DesktopApplication.RunAsync(args, startGame).GetAwaiter().GetResult();
        }

        try
        {
            var paths = DesktopApplication.ResolvePaths(args);
            return AppBuilder.Configure(() => new LauncherApplication(paths, startGame))
                .UsePlatformDetect()
                .StartWithClassicDesktopLifetime(Array.Empty<string>());
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
