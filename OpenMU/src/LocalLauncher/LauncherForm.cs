// <copyright file="LauncherForm.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

/// <summary>
/// Provides the local stack and notification-area controls.
/// </summary>
public sealed class LauncherForm : Form
{
    private readonly LocalStackManager _manager;
    private readonly Label _status = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _start = new() { Text = "启动", AutoSize = true };
    private readonly Button _stop = new() { Text = "停止", AutoSize = true };
    private readonly Button _game = new() { Text = "进入游戏", AutoSize = true };
    private readonly Button _admin = new() { Text = "管理后台", AutoSize = true };
    private readonly Button _backup = new() { Text = "备份", AutoSize = true };
    private readonly Button _restore = new() { Text = "还原", AutoSize = true };
    private readonly CheckBox _startWithWindows = new() { Text = "随 Windows 登录启动", AutoSize = true };
    private readonly CheckBox _startGameWhenReady = new() { Text = "服务器就绪后自动进入游戏", AutoSize = true };
    private readonly CheckBox _mobileAccess = new() { Text = "允许同一局域网手机访问", AutoSize = true };
    private readonly ComboBox _gameplayProfile = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly Label _networkDescription = new()
    {
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
    };
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _healthTimer = new() { Interval = 5000 };
    private readonly bool _startInBackground;
    private bool _allowClose;

    /// <summary>
    /// Initializes a new instance of the <see cref="LauncherForm"/> class.
    /// </summary>
    /// <param name="manager">The local stack manager.</param>
    /// <param name="startInBackground">Whether the initial window should stay hidden.</param>
    public LauncherForm(LocalStackManager manager, bool startInBackground)
    {
        this._manager = manager;
        this._startInBackground = startInBackground;
        this.Text = "OpenMU 本地版";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(640, 300);
        this.Size = new Size(720, 340);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(16) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        this._status.Font = new Font(this.Font, FontStyle.Bold);
        root.Controls.Add(this._status, 0, 0);

        var primary = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        primary.Controls.AddRange(new Control[] { this._start, this._stop, this._game, this._admin, this._backup, this._restore });
        root.Controls.Add(primary, 0, 1);

        var secondary = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var logs = new Button { Text = "日志", AutoSize = true };
        var data = new Button { Text = "数据目录", AutoSize = true };
        secondary.Controls.AddRange(new Control[] { logs, data, this._startWithWindows, this._startGameWhenReady, this._mobileAccess });
        root.Controls.Add(secondary, 0, 2);
        var profile = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        profile.Controls.Add(new Label { Text = "数值规则", AutoSize = true, Margin = new Padding(3, 7, 6, 0) });
        this._gameplayProfile.Items.AddRange(new object[]
        {
            "手机版平衡（标准）",
            "手机版平衡（轻松）",
            "手机版平衡（旅程）",
            "原本地单机规则",
        });
        this._gameplayProfile.SelectedIndex = manager.Settings.GameplayProfile switch
        {
            "balance-v1-standard" => 0,
            "balance-v1-relaxed" => 1,
            "balance-v1-journey" => 2,
            _ => 3,
        };
        profile.Controls.Add(this._gameplayProfile);
        profile.Controls.Add(new Label { Text = "更改后重启服务；已有数据库不会自动转换。", AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(8, 7, 3, 0) });
        root.Controls.Add(profile, 0, 3);
        root.Controls.Add(this._networkDescription, 0, 4);
        this.Controls.Add(root);

        this._start.Click += this.OnStartClicked;
        this._stop.Click += this.OnStopClicked;
        this._game.Click += this.OnGameClicked;
        this._admin.Click += (_, _) => this.RunSynchronous(this._manager.OpenAdminPanel);
        this._backup.Click += this.OnBackupClicked;
        this._restore.Click += this.OnRestoreClicked;
        logs.Click += (_, _) => this.RunSynchronous(this._manager.OpenLogsDirectory);
        data.Click += (_, _) => this.RunSynchronous(this._manager.OpenDataDirectory);
        this._startWithWindows.CheckedChanged += this.OnStartWithWindowsChanged;
        this._startGameWhenReady.CheckedChanged += this.OnStartGameWhenReadyChanged;
        this._mobileAccess.CheckedChanged += this.OnMobileAccessChanged;
        this._gameplayProfile.SelectedIndexChanged += this.OnGameplayProfileChanged;
        this._startWithWindows.Checked = manager.Settings.StartWithWindows;
        this._startGameWhenReady.Checked = manager.Settings.StartGameWhenReady;
        this._mobileAccess.Checked = manager.Settings.MobileAccessEnabled;
        this.UpdateNetworkDescription();

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("显示", null, (_, _) => this.ShowFromTray());
        trayMenu.Items.Add("启动", null, this.OnTrayStartClicked);
        trayMenu.Items.Add("停止", null, this.OnStopClicked);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("退出启动器", null, this.OnExitClicked);
        this._trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "OpenMU 本地版",
            ContextMenuStrip = trayMenu,
            Visible = true,
        };
        this._trayIcon.DoubleClick += (_, _) => this.ShowFromTray();
        this._manager.StatusChanged += this.OnStatusChanged;
        this._healthTimer.Tick += this.OnHealthTimerTick;
        this._healthTimer.Start();
        this.FormClosing += this.OnFormClosing;
        this.FormClosed += (_, _) =>
        {
            this._healthTimer.Dispose();
            this._trayIcon.Dispose();
        };
        this.ApplyStatus(this._manager.Status);
        this.Shown += this.OnShown;
    }

    private void OnStartClicked(object? sender, EventArgs e)
        => _ = this.RunOperationAsync(() => this._manager.StartAsync(this._manager.Settings.StartGameWhenReady, CancellationToken.None));

    private void OnTrayStartClicked(object? sender, EventArgs e)
        => _ = this.RunOperationAsync(() => this._manager.StartAsync(false, CancellationToken.None));

    private void OnStopClicked(object? sender, EventArgs e) => _ = this.StopWithConfirmationAsync();

    private void OnGameClicked(object? sender, EventArgs e)
        => _ = this.RunOperationAsync(() => this._manager.StartGameAsync(CancellationToken.None));

    private void OnBackupClicked(object? sender, EventArgs e) => _ = this.CreateBackupAsync();

    private void OnRestoreClicked(object? sender, EventArgs e) => _ = this.RestoreAsync();

    private void OnExitClicked(object? sender, EventArgs e) => _ = this.ExitAsync();

    private void OnHealthTimerTick(object? sender, EventArgs e)
        => _ = this._manager.CheckHealthAsync(CancellationToken.None);

    private void OnMobileAccessChanged(object? sender, EventArgs e)
    {
        this._manager.Settings.MobileAccessEnabled = this._mobileAccess.Checked;
        this._manager.SaveSettings();
        this.UpdateNetworkDescription();
    }

    private void OnGameplayProfileChanged(object? sender, EventArgs e)
    {
        this._manager.Settings.GameplayProfile = this._gameplayProfile.SelectedIndex switch
        {
            0 => "balance-v1-standard",
            1 => "balance-v1-relaxed",
            2 => "balance-v1-journey",
            _ => "solo",
        };
        this._manager.SaveSettings();
    }

    private void UpdateNetworkDescription()
    {
        if (!this._manager.Settings.MobileAccessEnabled)
        {
            this._networkDescription.Text = "关闭窗口后仍会在通知区域运行。游戏、后台和数据库仅监听 127.0.0.1。";
            return;
        }

        try
        {
            this._networkDescription.Text = $"手机地址：{this._manager.MobileAccessAddress}。游戏和后台对局域网开放，数据库仍仅监听 127.0.0.1；更改后需重启服务。";
        }
        catch (InvalidOperationException error)
        {
            this._networkDescription.Text = error.Message;
        }
    }

    private void OnShown(object? sender, EventArgs e)
    {
        if (this._startInBackground)
        {
            this.Hide();
        }

        var startGame = !this._startInBackground && this._manager.Settings.StartGameWhenReady;
        _ = this.RunOperationAsync(() => this._manager.StartAsync(startGame, CancellationToken.None));
    }

    private async Task CreateBackupAsync()
    {
        await this.RunOperationAsync(async () =>
        {
            var backupPath = await this._manager.CreateBackupAsync(CancellationToken.None).ConfigureAwait(true);
            MessageBox.Show(this, $"备份已创建：\n{backupPath}", "备份完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }).ConfigureAwait(true);
    }

    private async Task RestoreAsync()
    {
        using var dialog = new OpenFileDialog { Filter = "OpenMU 备份 (*.zip)|*.zip", Title = "选择要还原的备份" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (MessageBox.Show(this, "还原会先创建保护性备份，并暂时停止游戏服务器。确定继续吗？", "确认还原", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        await this.RunOperationAsync(() => this._manager.RestoreAsync(dialog.FileName, CancellationToken.None)).ConfigureAwait(true);
    }

    private async Task StopWithConfirmationAsync()
    {
        try
        {
            await this.RunOperationAsync(() => this._manager.StopAsync(force: false, CancellationToken.None), showErrors: false).ConfigureAwait(true);
        }
        catch (TimeoutException ex)
        {
            if (MessageBox.Show(this, $"{ex.Message}\n\n强制停止可能丢失尚未保存的数据。确定强制停止吗？", "正常停止超时", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                await this.RunOperationAsync(() => this._manager.StopAsync(force: true, CancellationToken.None)).ConfigureAwait(true);
            }
        }
        catch (AutomaticBackupFailedException ex)
        {
            if (MessageBox.Show(
                    this,
                    $"{ex.Message}\n\n正常停止已中止，数据库仍在运行。是否重试，并在备份仍失败时强制停止？",
                    "自动备份失败",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                await this.RunOperationAsync(() => this._manager.StopAsync(force: true, CancellationToken.None)).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "停止失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ExitAsync()
    {
        var result = MessageBox.Show(
            this,
            "是否先正常停止游戏服务器和数据库？\n选择“否”会退出启动器，但后台继续运行。",
            "退出 OpenMU 本地版",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);
        if (result == DialogResult.Cancel)
        {
            return;
        }

        if (result == DialogResult.Yes)
        {
            await this.StopWithConfirmationAsync().ConfigureAwait(true);
            if (this._manager.Status.State != LocalStackState.Stopped)
            {
                return;
            }
        }

        this._allowClose = true;
        this.Close();
    }

    private async Task RunOperationAsync(Func<Task> operation, bool showErrors = true)
    {
        this.SetControlsEnabled(false);
        try
        {
            await operation().ConfigureAwait(true);
        }
        catch (Exception ex) when (showErrors)
        {
            MessageBox.Show(this, ex.Message, "操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            this.SetControlsEnabled(true);
        }
    }

    private void RunSynchronous(Action operation)
    {
        try
        {
            operation();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnStatusChanged(LocalStackStatus status)
    {
        if (this.InvokeRequired)
        {
            this.BeginInvoke((Action)(() => this.ApplyStatus(status)));
        }
        else
        {
            this.ApplyStatus(status);
        }
    }

    private void ApplyStatus(LocalStackStatus status)
    {
        var stateText = status.State switch
        {
            LocalStackState.Stopped => "已停止",
            LocalStackState.Initializing => "正在检查本地文件和数据...",
            LocalStackState.StartingDatabase => "正在启动数据库...",
            LocalStackState.StartingServer => "正在启动 OpenMU...",
            LocalStackState.Running => "运行中",
            LocalStackState.Stopping => "正在正常停止...",
            LocalStackState.Faulted => "运行故障",
            _ => status.State.ToString(),
        };
        this._status.Text = string.IsNullOrWhiteSpace(status.Message) ? stateText : $"{stateText}  {status.Message}";
        this._trayIcon.Text = status.State == LocalStackState.Running ? "OpenMU 本地版 - 运行中" : "OpenMU 本地版";
        this._start.Enabled = status.State is LocalStackState.Stopped or LocalStackState.Faulted;
        this._stop.Enabled = status.State is LocalStackState.Running or LocalStackState.Faulted;
        this._game.Enabled = status.State == LocalStackState.Running;
        this._admin.Enabled = status.State == LocalStackState.Running;
        this._backup.Enabled = status.State == LocalStackState.Running;
    }

    private void SetControlsEnabled(bool enabled)
    {
        if (enabled)
        {
            this.ApplyStatus(this._manager.Status);
            this._restore.Enabled = true;
            return;
        }

        this._start.Enabled = enabled;
        this._stop.Enabled = enabled;
        this._game.Enabled = enabled;
        this._admin.Enabled = enabled;
        this._backup.Enabled = enabled;
        this._restore.Enabled = enabled;
    }

    private void OnStartWithWindowsChanged(object? sender, EventArgs e)
    {
        this._manager.Settings.StartWithWindows = this._startWithWindows.Checked;
        this._manager.SaveSettings();
        StartupRegistration.Apply(this._startWithWindows.Checked, Application.ExecutablePath);
    }

    private void OnStartGameWhenReadyChanged(object? sender, EventArgs e)
    {
        this._manager.Settings.StartGameWhenReady = this._startGameWhenReady.Checked;
        this._manager.SaveSettings();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (this._allowClose || e.CloseReason == CloseReason.WindowsShutDown || !this._manager.Settings.CloseToTray)
        {
            return;
        }

        e.Cancel = true;
        this.Hide();
        this._trayIcon.ShowBalloonTip(1500, "OpenMU 本地版", "服务器仍在后台运行。", ToolTipIcon.Info);
    }

    private void ShowFromTray()
    {
        this.Show();
        this.WindowState = FormWindowState.Normal;
        this.Activate();
    }
}
