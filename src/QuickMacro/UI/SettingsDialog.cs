using QuickMacro.Models;

namespace QuickMacro.UI;

public sealed class SettingsDialog : Form
{
    private static readonly Color Raised = Color.FromArgb(237, 241, 247);
    private static readonly Color Accent = Color.FromArgb(48, 105, 220);
    private readonly CheckBox _startup = new() { Text = "Launch QuickMacro when I sign in to Windows", AutoSize = true };
    private readonly ComboBox _theme = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    public bool StartupEnabled => _startup.Checked;
    public AppSettings Settings { get; private set; }

    public SettingsDialog(AppSettings settings, bool startupEnabled)
    {
        Settings = new AppSettings { ThemeMode = string.IsNullOrWhiteSpace(settings.ThemeMode) ? "System" : settings.ThemeMode, StartWithWindows = settings.StartWithWindows, StartMinimized = settings.StartMinimized, MinimizeToTray = settings.MinimizeToTray, EnableAllShortcuts = settings.EnableAllShortcuts, ShowLaunchNotifications = settings.ShowLaunchNotifications, PreventDuplicateLaunches = settings.PreventDuplicateLaunches };
        Text = "QuickMacro settings"; DialogLayout.FitToWorkingArea(this, new Size(500, 420), new Size(420, 360)); FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false;
        BackColor = Color.FromArgb(246, 248, 252); ForeColor = Color.FromArgb(25, 35, 52); Font = new Font("Segoe UI", 9F);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(22), BackColor = this.BackColor };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        var heading = new Panel { Dock = DockStyle.Fill, BackColor = this.BackColor };
        heading.Controls.Add(new Label { Text = "Preferences", AutoSize = true, Font = new Font("Segoe UI Semibold", 16F), ForeColor = Color.FromArgb(25, 35, 52), Location = new Point(0, 0) });
        heading.Controls.Add(new Label { Text = "Choose how QuickMacro behaves on your PC.", AutoSize = true, ForeColor = Color.FromArgb(96, 111, 132), Location = new Point(2, 34) });

        var scrollArea = new Panel { Dock = DockStyle.Fill, AutoScroll = true, AutoScrollMinSize = new Size(0, 330), BackColor = Color.FromArgb(255, 255, 255) };
        var options = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.FromArgb(255, 255, 255), Padding = new Padding(16), Location = Point.Empty };
        _startup.Checked = startupEnabled; options.Controls.Add(StyleCheck(_startup));
        var startMin = AddCheck(options, "Start minimized", settings.StartMinimized);
        var tray = AddCheck(options, "Hide in system tray when closed", settings.MinimizeToTray);
        var enabled = AddCheck(options, "Enable global shortcuts", settings.EnableAllShortcuts);
        var notification = AddCheck(options, "Show a notification after an action", settings.ShowLaunchNotifications);
        var single = AddCheck(options, "Prevent duplicate launches", settings.PreventDuplicateLaunches);
        _theme.Items.AddRange(ThemeManager.Options);
        _theme.SelectedItem = ThemeManager.Options.Contains(Settings.ThemeMode, StringComparer.OrdinalIgnoreCase) ? ThemeManager.Options.First(item => item.Equals(Settings.ThemeMode, StringComparison.OrdinalIgnoreCase)) : "System";
        var themeRow = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 1, Margin = new Padding(3, 12, 3, 4), BackColor = Color.White };
        themeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145)); themeRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        themeRow.Controls.Add(new Label { Text = "Appearance", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.FromArgb(25, 35, 52), Margin = new Padding(0, 6, 0, 0) }, 0, 0);
        themeRow.Controls.Add(_theme, 1, 0); options.Controls.Add(themeRow);
        scrollArea.Controls.Add(options);
        scrollArea.SizeChanged += (_, _) => options.MaximumSize = new Size(Math.Max(200, scrollArea.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2), 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 10, 0, 0), BackColor = this.BackColor };
        var ok = StyleButton(new Button { Text = "Save", AutoSize = true }, true);
        ok.Click += (_, _) => { Settings.ThemeMode = _theme.SelectedItem?.ToString() ?? "System"; Settings.StartWithWindows = _startup.Checked; Settings.StartMinimized = startMin.Checked; Settings.MinimizeToTray = tray.Checked; Settings.EnableAllShortcuts = enabled.Checked; Settings.ShowLaunchNotifications = notification.Checked; Settings.PreventDuplicateLaunches = single.Checked; DialogResult = DialogResult.OK; Close(); };
        var cancel = StyleButton(new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel });
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
        layout.Controls.Add(heading, 0, 0); layout.Controls.Add(scrollArea, 0, 1); layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout); AcceptButton = ok; CancelButton = cancel;
    }

    private static CheckBox AddCheck(Control parent, string text, bool value)
    {
        var check = new CheckBox { Text = text, Checked = value, AutoSize = true, Margin = new Padding(3, 11, 3, 0) };
        parent.Controls.Add(StyleCheck(check));
        return check;
    }

    private static CheckBox StyleCheck(CheckBox check) { check.ForeColor = Color.FromArgb(25, 35, 52); check.Font = new Font("Segoe UI", 9F); return check; }
    private static Button StyleButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0; button.BackColor = primary ? Accent : Raised;
        button.ForeColor = primary ? Color.White : Color.FromArgb(25, 35, 52); button.Font = new Font("Segoe UI Semibold", 9F); button.Padding = new Padding(10, 5, 10, 5); button.Cursor = Cursors.Hand;
        return button;
    }
}
