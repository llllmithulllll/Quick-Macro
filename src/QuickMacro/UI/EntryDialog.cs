using QuickMacro.Models;
using QuickMacro.Services;

namespace QuickMacro.UI;

public sealed class EntryDialog : Form
{
    private static readonly Color Raised = Color.FromArgb(237, 241, 247);
    private static readonly Color Accent = Color.FromArgb(48, 105, 220);
    private readonly TextBox _name = new(), _path = new(), _args = new(), _working = new();
    private readonly ComboBox _targetKind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly Label _typeDescription = new() { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(96, 111, 132), TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _chooseTarget = new() { Text = "Browse…", AutoSize = true };
    private readonly CheckBox _enabled = new() { Text = "Enabled", Checked = true, AutoSize = true };
    private readonly Label _shortcutLabel = new() { AutoSize = true, ForeColor = Color.FromArgb(25, 35, 52) };
    private readonly CheckBox _windowsModifier = new() { Text = "Windows key", AutoSize = true };
    private readonly ComboBox _shortcutAction = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly CheckBox _minimizeToTray = new() { Text = "Hide app in QuickMacro’s tray when minimized", AutoSize = true };
    private ShortcutBinding? _shortcut;
    private bool _recording;
    private readonly Guid _id;
    public LauncherEntry? Entry { get; private set; }

    public EntryDialog(LauncherEntry? entry)
    {
        _id = entry?.Id ?? Guid.NewGuid(); _shortcut = entry?.Shortcut;
        Text = entry is null ? "Add a shortcut" : "Edit shortcut"; DialogLayout.FitToWorkingArea(this, new Size(760, 650), new Size(700, 600)); FormBorderStyle = FormBorderStyle.Sizable; StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false; BackColor = Color.FromArgb(246, 248, 252); ForeColor = Color.FromArgb(25, 35, 52); Font = new Font("Segoe UI", 9F); KeyPreview = true;
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 4, RowCount = 12, BackColor = this.BackColor };
        foreach (var height in new[] { 32, 42, 38, 28, 40, 38, 38, 54, 44, 36, 34, 50 }) table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));

        var intro = new Label { Text = "Choose what the shortcut should open, then set how the hotkey behaves.", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(96, 111, 132), TextAlign = ContentAlignment.MiddleLeft };
        table.Controls.Add(intro, 0, 0); table.SetColumnSpan(intro, 4);
        AddRow(table, 1, "Name", _name);
        table.Controls.Add(new Label { Text = "Opens", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        _targetKind.Items.AddRange(Enum.GetValues<LauncherTargetType>().Select(LauncherTargetTypeLabels.Get).Cast<object>().ToArray());
        _targetKind.BackColor = Raised; _targetKind.ForeColor = Color.FromArgb(25, 35, 52); _targetKind.FlatStyle = FlatStyle.Flat;
        _targetKind.SelectedIndex = (int)(entry?.TargetType ?? LauncherTargetType.FilePath);
        table.Controls.Add(_targetKind, 1, 2); table.SetColumnSpan(_targetKind, 3);
        table.Controls.Add(_typeDescription, 1, 3); table.SetColumnSpan(_typeDescription, 3);
        AddRow(table, 4, "Target", _path);
        _chooseTarget = StyleButton(_chooseTarget);
        table.Controls.Add(_chooseTarget, 2, 4); table.SetColumnSpan(_chooseTarget, 2);
        AddRow(table, 5, "Arguments", _args);
        AddRow(table, 6, "Working folder", _working, BrowseDirectory);

        table.Controls.Add(new Label { Text = "Global shortcut", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
        var shortcutPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, BackColor = this.BackColor };
        var record = StyleButton(new Button { Text = "Set shortcut", AutoSize = true }); record.Click += (_, _) => { _recording = true; _shortcutLabel.Text = "Press a key combination"; Focus(); };
        var clear = StyleButton(new Button { Text = "Clear", AutoSize = true }); clear.Click += (_, _) => { _shortcut = null; _shortcutLabel.Text = "Not set"; };
        shortcutPanel.Controls.Add(record); shortcutPanel.Controls.Add(clear); shortcutPanel.Controls.Add(_shortcutLabel); shortcutPanel.Controls.Add(_windowsModifier); table.Controls.Add(shortcutPanel, 1, 7); table.SetColumnSpan(shortcutPanel, 3);

        _shortcutAction.Items.AddRange(["Always launch a new instance", "Launch / minimize / restore", "Launch, or close if open"]);
        _shortcutAction.BackColor = Raised; _shortcutAction.ForeColor = Color.FromArgb(25, 35, 52); _shortcutAction.FlatStyle = FlatStyle.Flat;
        table.Controls.Add(new Label { Text = "Hotkey action", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 8);
        table.Controls.Add(_shortcutAction, 1, 8); table.SetColumnSpan(_shortcutAction, 3);
        _minimizeToTray.ForeColor = Color.FromArgb(25, 35, 52);
        table.Controls.Add(_minimizeToTray, 1, 9); table.SetColumnSpan(_minimizeToTray, 3);
        table.Controls.Add(_enabled, 1, 10);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = this.BackColor };
        var save = StyleButton(new Button { Text = "Save shortcut", AutoSize = true }, true); save.Click += (_, _) => SaveEntry(); var cancel = StyleButton(new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel }); actions.Controls.Add(save); actions.Controls.Add(cancel); table.Controls.Add(actions, 0, 11); table.SetColumnSpan(actions, 4);
        Controls.Add(table); AcceptButton = save; CancelButton = cancel;

        foreach (var textBox in new[] { _name, _path, _args, _working }) { textBox.BackColor = Raised; textBox.ForeColor = Color.FromArgb(25, 35, 52); textBox.BorderStyle = BorderStyle.FixedSingle; }
        _enabled.ForeColor = Color.FromArgb(25, 35, 52); _windowsModifier.ForeColor = Color.FromArgb(25, 35, 52);
        var actionIndex = (int)(entry?.ShortcutAction ?? ShortcutAction.Launch);
        _shortcutAction.SelectedIndex = actionIndex >= 0 && actionIndex < _shortcutAction.Items.Count ? actionIndex : 0;
        if (entry is not null) { _name.Text = entry.Name; _path.Text = entry.TargetPath; _args.Text = entry.Arguments; _working.Text = entry.WorkingDirectory; _enabled.Checked = entry.Enabled; _windowsModifier.Checked = entry.Shortcut?.Windows ?? false; _minimizeToTray.Checked = entry.MinimizeToSystemTray; }
        _targetKind.SelectedIndexChanged += (_, _) => UpdateTargetControls();
        _shortcutAction.SelectedIndexChanged += (_, _) => UpdateTrayOption();
        _chooseTarget.Click += (_, _) => ChooseTarget();
        UpdateShortcutLabel(); UpdateTargetControls();
        KeyDown += CaptureKey;
    }

    private static Button StyleButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0; button.BackColor = primary ? Accent : Raised;
        button.ForeColor = primary ? Color.White : Color.FromArgb(25, 35, 52); button.Font = new Font("Segoe UI Semibold", 9F); button.Padding = new Padding(8, 4, 8, 4); button.Cursor = Cursors.Hand;
        return button;
    }

    private void AddRow(TableLayoutPanel table, int row, string label, Control input, Action? browse = null)
    {
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        input.Dock = DockStyle.Fill; table.Controls.Add(input, 1, row);
        if (browse is not null) { var button = StyleButton(new Button { Text = "Browse…", AutoSize = true }); button.Click += (_, _) => browse(); table.Controls.Add(button, 2, row); table.SetColumnSpan(button, 2); }
    }

    private LauncherTargetType SelectedTargetType => _targetKind.SelectedIndex >= 0 ? (LauncherTargetType)_targetKind.SelectedIndex : LauncherTargetType.FilePath;

    private void UpdateTargetControls()
    {
        var type = SelectedTargetType;
        _typeDescription.Text = type switch
        {
            LauncherTargetType.FilePath => "Choose a Windows program or shortcut file on this PC.",
            LauncherTargetType.StoreApp => "Select an installed Microsoft Store app by its Windows app identity.",
            LauncherTargetType.WebsiteUrl => "Enter a web address; QuickMacro opens it in your default browser.",
            LauncherTargetType.BrowserAppShortcut => "Choose an app shortcut installed by Chrome, Edge, or Brave.",
            _ => string.Empty
        };
        _chooseTarget.Enabled = type != LauncherTargetType.WebsiteUrl;
        _chooseTarget.Text = type switch
        {
            LauncherTargetType.StoreApp => "Choose Store app…",
            LauncherTargetType.BrowserAppShortcut => "Choose browser app shortcut…",
            LauncherTargetType.WebsiteUrl => "Enter a website address",
            _ => "Browse for app…"
        };
        _path.PlaceholderText = type switch
        {
            LauncherTargetType.WebsiteUrl => "https://example.com",
            LauncherTargetType.StoreApp => "Choose a Microsoft Store app",
            LauncherTargetType.BrowserAppShortcut => "Choose a browser-installed app shortcut (.lnk)",
            _ => "Select an .exe or .lnk file"
        };
        _shortcutAction.Enabled = type != LauncherTargetType.WebsiteUrl;
        if (type == LauncherTargetType.WebsiteUrl) _shortcutAction.SelectedIndex = (int)ShortcutAction.Launch;
        UpdateTrayOption();
    }

    private void UpdateTrayOption()
    {
        _minimizeToTray.Visible = SelectedTargetType != LauncherTargetType.WebsiteUrl
            && _shortcutAction.SelectedIndex == (int)ShortcutAction.Minimize;
    }

    private void ChooseTarget()
    {
        switch (SelectedTargetType)
        {
            case LauncherTargetType.StoreApp: BrowseStoreApp(); break;
            case LauncherTargetType.BrowserAppShortcut: BrowseBrowserApp(); break;
            case LauncherTargetType.FilePath: BrowseDesktopApp(); break;
        }
    }

    private void BrowseDesktopApp()
    {
        using var dialog = new OpenFileDialog { Filter = "Applications and shortcuts (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|All files (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _path.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(_name.Text)) _name.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
    }

    private void BrowseBrowserApp()
    {
        using var dialog = new BrowserAppDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedApp is null) return;
        _path.Text = dialog.SelectedApp.ShortcutPath;
        if (string.IsNullOrWhiteSpace(_name.Text)) _name.Text = dialog.SelectedApp.Name;
    }

    private void BrowseStoreApp()
    {
        try
        {
            using var dialog = new StoreAppDialog();
            if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedApp is null) return;
            _path.Text = dialog.SelectedApp.AppUserModelId;
            if (string.IsNullOrWhiteSpace(_name.Text)) _name.Text = dialog.SelectedApp.Name;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not list Store apps", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void BrowseDirectory() { using var d = new FolderBrowserDialog(); if (d.ShowDialog(this) == DialogResult.OK) _working.Text = d.SelectedPath; }

    private void CaptureKey(object? sender, KeyEventArgs e)
    {
        if (!_recording) return;
        e.SuppressKeyPress = true; e.Handled = true;
        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin) return;
        var binding = new ShortcutBinding { Control = e.Control, Alt = e.Alt, Shift = e.Shift, Windows = _windowsModifier.Checked, Key = e.KeyCode };
        var error = ShortcutRules.Validate(binding);
        if (error is not null) { MessageBox.Show(this, error, "Invalid shortcut", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        _shortcut = binding; _recording = false; UpdateShortcutLabel();
    }

    private void UpdateShortcutLabel() => _shortcutLabel.Text = _shortcut?.ToString() ?? "Not set";

    private void SaveEntry()
    {
        var action = (ShortcutAction)Math.Max(0, _shortcutAction.SelectedIndex);
        var candidate = new LauncherEntry { Id = _id, Name = _name.Text.Trim(), TargetPath = _path.Text.Trim(), TargetType = SelectedTargetType, ShortcutAction = action, MinimizeToSystemTray = action == ShortcutAction.Minimize && _minimizeToTray.Checked, Arguments = _args.Text, WorkingDirectory = _working.Text.Trim(), Shortcut = _shortcut, Enabled = _enabled.Checked };
        var error = ShortcutRules.ValidateEntry(candidate);
        if (error is not null) { MessageBox.Show(this, error, "Check shortcut details", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        Entry = candidate; DialogResult = DialogResult.OK; Close();
    }
}
