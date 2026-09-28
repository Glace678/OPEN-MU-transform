// <copyright file="StartupRegistration.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

using Microsoft.Win32;

/// <summary>
/// Manages the optional per-user Windows startup entry.
/// </summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "OpenMU-Local";

    /// <summary>
    /// Enables or disables startup for the current Windows user.
    /// </summary>
    /// <param name="enabled">Whether startup should be enabled.</param>
    /// <param name="executablePath">The launcher executable path.</param>
    public static void Apply(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                        ?? throw new InvalidOperationException("无法打开当前 Windows 用户的开机启动注册表项。");
        if (enabled)
        {
            key.SetValue(ValueName, $"\"{Path.GetFullPath(executablePath)}\" --background", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
