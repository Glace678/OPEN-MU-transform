// <copyright file="AdministratorPasswordDialog.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.LocalLauncher;

/// <summary>
/// Collects the first-run administrator password without persisting it as plain text.
/// </summary>
public sealed class AdministratorPasswordDialog : Form
{
    private readonly TextBox _password = new() { UseSystemPasswordChar = true, Width = 280 };
    private readonly TextBox _confirmation = new() { UseSystemPasswordChar = true, Width = 280 };

    /// <summary>Initializes a new instance of the <see cref="AdministratorPasswordDialog"/> class.</summary>
    public AdministratorPasswordDialog()
    {
        this.Text = "OpenMU 本地后台首次设置";
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.AutoSize = true;
        this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        this.Padding = new Padding(16);

        var layout = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "后台管理员：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(new Label { Text = "localadmin", AutoSize = true, Anchor = AnchorStyles.Left }, 1, 0);
        layout.Controls.Add(new Label { Text = "密码：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(this._password, 1, 1);
        layout.Controls.Add(new Label { Text = "确认密码：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(this._confirmation, 1, 2);
        var passwordDescription = new Label
        {
            Text = "密码至少 12 个字符。数据库随机密码将仅供当前 Windows 用户加密保存。",
            AutoSize = true,
        };
        layout.Controls.Add(passwordDescription, 0, 3);
        layout.SetColumnSpan(layout.GetControlFromPosition(0, 3)!, 2);

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
        var confirm = new Button { Text = "确定", AutoSize = true };
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        confirm.Click += this.OnConfirm;
        buttons.Controls.Add(confirm);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 0, 4);
        layout.SetColumnSpan(buttons, 2);
        this.Controls.Add(layout);
        this.AcceptButton = confirm;
        this.CancelButton = cancel;
    }

    /// <summary>Gets the validated administrator password.</summary>
    public string AdministratorPassword { get; private set; } = string.Empty;

    private void OnConfirm(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(this._password.Text) || this._password.Text.Length < 12)
        {
            MessageBox.Show(this, "密码必须至少包含 12 个字符。", "密码太短", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!string.Equals(this._password.Text, this._confirmation.Text, StringComparison.Ordinal))
        {
            MessageBox.Show(this, "两次输入的密码不一致。", "无法确认密码", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        this.AdministratorPassword = this._password.Text;
        this.DialogResult = DialogResult.OK;
        this.Close();
    }
}
