// <copyright file="Program.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

/// <summary>
/// The local launcher entry point.
/// </summary>
internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\OpenMU-Local-Launcher";

    /// <summary>The application entry point.</summary>
    /// <param name="args">The command line arguments.</param>
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            Run(args);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"OpenMU 本地版无法启动：\n\n{ex.Message}",
                "启动失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void Run(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var ownsMutex);
        if (!ownsMutex)
        {
            MessageBox.Show("OpenMU 本地版已经在运行，请查看通知区域。", "OpenMU 本地版", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var rootDirectory = AppContext.BaseDirectory;
        var manager = new LocalStackManager(rootDirectory);
        try
        {
            if (manager.RequiresProvisioning)
            {
                using var passwordDialog = new AdministratorPasswordDialog();
                if (passwordDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                manager.Provision(passwordDialog.AdministratorPassword);
            }

            var startInBackground = args.Contains("--background", StringComparer.OrdinalIgnoreCase);
            using var launcher = new LauncherForm(manager, startInBackground);
            Application.Run(launcher);
        }
        finally
        {
            manager.Dispose();
        }

        GC.KeepAlive(mutex);
    }
}
