// <copyright file="LauncherWindow.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.DesktopLauncher;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using MUnique.OpenMU.LocalLauncher;

/// <summary>First-run setup and local stack controls without a terminal.</summary>
public sealed class LauncherWindow : Window
{
    private readonly LocalPaths _paths;
    private readonly LocalStackManager _manager;
    private readonly bool _startGame;
    private readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal, Spacing = 12 };
    private readonly StackPanel _setup = new() { Spacing = 10 };
    private readonly TextBox _password = new() { PasswordChar = '*', MaxLength = 1024 };
    private readonly TextBox _confirmation = new() { PasswordChar = '*', MaxLength = 1024 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _progress = new() { IsIndeterminate = true, IsVisible = false, Height = 3 };
    private bool _busy;

    /// <summary>Initializes a launcher for one of the two desktop applications.</summary>
    public LauncherWindow(LocalPaths paths, bool startGame)
    {
        this._paths = paths;
        this._startGame = startGame;
        this._manager = new LocalStackManager(paths.RootDirectory, paths.DataDirectory);
        this.Title = startGame ? "OpenMU Game" : "OpenMU GM";
        this.Width = 600;
        this.Height = 480;
        this.MinWidth = 520;
        this.MinHeight = 440;
        this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        this.Content = this.CreateContent();
        this._manager.StatusChanged += status => Dispatcher.UIThread.Post(() => this._status.Text = status.Message ?? StateText(status.State));
        this.Closing += (_, args) => args.Cancel = this._busy;
        this.Closed += (_, _) => this._manager.Dispose();
    }

    private static string StateText(LocalStackState state) => state switch
    {
        LocalStackState.Stopped => "服务已停止",
        LocalStackState.Initializing => "正在检查程序包",
        LocalStackState.StartingDatabase => "正在启动本地数据库",
        LocalStackState.StartingServer => "正在启动游戏服务",
        LocalStackState.Running => "本地服务已就绪",
        LocalStackState.Stopping => "正在保存并停止服务",
        LocalStackState.Faulted => "操作未完成",
        _ => state.ToString(),
    };

    private Control CreateContent()
    {
        var content = new StackPanel { Margin = new Thickness(28), Spacing = 18 };
        content.Children.Add(new TextBlock { Text = this.Title, FontSize = 26, FontWeight = FontWeight.SemiBold });
        this._setup.Children.Add(new TextBlock { Text = "首次设置 · 管理员 localadmin" });
        this._setup.Children.Add(new TextBlock { Text = "管理员密码（至少 12 个字符）" });
        this._setup.Children.Add(this._password);
        this._setup.Children.Add(new TextBlock { Text = "再次输入密码" });
        this._setup.Children.Add(this._confirmation);
        this._setup.IsVisible = this._manager.RequiresProvisioning;
        content.Children.Add(this._setup);
        this.AddAction(this._startGame ? "启动游戏" : "打开 GM", this.StartAsync);
        this.AddAction("备份", async () =>
        {
            var path = await this._manager.CreateBackupAsync(CancellationToken.None);
            this._status.Text = $"备份完成：{path}";
        });
        this.AddAction("停止服务", () => this._manager.StopAsync(force: false, CancellationToken.None));
        content.Children.Add(this._actions);
        content.Children.Add(this._progress);
        this._status.Text = this._manager.RequiresProvisioning ? "等待首次设置" : "尚未连接本地服务";
        content.Children.Add(this._status);
        return new ScrollViewer { Content = content, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }

    private void AddAction(string label, Func<Task> action)
    {
        var button = new Button { Content = label, MinHeight = 36 };
        button.Click += async (_, _) => await this.ExecuteAsync(action);
        this._actions.Children.Add(button);
    }

    private async Task StartAsync()
    {
        if (this._manager.RequiresProvisioning)
        {
            if (!string.Equals(this._password.Text, this._confirmation.Text, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("两次密码输入不一致。");
            }

            this._manager.Provision(this._password.Text ?? string.Empty);
            this._password.Text = string.Empty;
            this._confirmation.Text = string.Empty;
            this._setup.IsVisible = false;
        }

        await this._manager.StartAsync(this._startGame, CancellationToken.None);
        if (!this._startGame)
        {
            this._manager.OpenAdminPanel();
        }
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        if (this._busy)
        {
            return;
        }

        this._busy = true;
        this._actions.IsEnabled = false;
        this._setup.IsEnabled = false;
        this._progress.IsVisible = true;
        try
        {
            var lockPath = Path.Combine(this._paths.KeysDirectory, "desktop-lifecycle.lock");
            LocalPlatform.RejectLink(lockPath);
            using var lifecycleLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            await action();
        }
        catch (Exception exception)
        {
            this._status.Text = exception is IOException
                ? $"操作未完成，请确认另一个启动器没有正在执行操作。{exception.Message}"
                : exception.Message;
        }
        finally
        {
            this._busy = false;
            this._actions.IsEnabled = true;
            this._setup.IsEnabled = true;
            this._progress.IsVisible = false;
        }
    }
}
