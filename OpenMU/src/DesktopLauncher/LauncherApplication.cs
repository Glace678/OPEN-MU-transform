// <copyright file="LauncherApplication.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DesktopLauncher;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using MUnique.OpenMU.LocalLauncher;

/// <summary>Hosts the shared game and GM desktop window.</summary>
public sealed class LauncherApplication(LocalPaths paths, bool startGame) : Application
{
    /// <inheritdoc/>
    public override void Initialize()
    {
        this.RequestedThemeVariant = ThemeVariant.Light;
        this.Styles.Add(new FluentTheme());
    }

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new LauncherWindow(paths, startGame);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
