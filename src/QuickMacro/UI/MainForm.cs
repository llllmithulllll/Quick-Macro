using QuickMacro.Models;
using QuickMacro.Services;

namespace QuickMacro.UI;

public sealed class MainForm : Form
{
    private static readonly Color Canvas = Color.FromArgb(246, 248, 252);
    private static readonly Color Surface = Color.FromArgb(255, 255, 255);
    private static readonly Color SurfaceRaised = Color.FromArgb(237, 241, 247);
    private static readonly Color TextPrimary = Color.FromArgb(25, 35, 52);
    private static readonly Color TextMuted = Color.FromArgb(96, 111, 132);
    private static readonly Color Accent = Color.FromArgb(48, 105, 220);

    private readonly ConfigurationRepository _repository;
    private readonly AppConfiguration _config;
    private readonly StartupService _startup;
    private readonly LauncherService _launcher = new();
    private readonly WorkspaceService _workspaceService;
    private readonly GlobalHotkeyService _hotkeys;
    private readonly NotifyIcon _tray;
    private readonly FlowLayoutPanel _entriesPanel = new();
    private readonly Label _status = new();
    private readonly Label _count = new();
    private bool _allowExit;

    public MainForm(ConfigurationRepository repository, AppConfiguration config, StartupService startup, string? loadWarning)
    {
        _repository = repository; _config = config; _startup = startup;
        _workspaceService = new WorkspaceService(_launcher);
        Text = "QuickMacro";
        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        Width = Math.Max(740, Math.Min(1060, workArea.Width - 32));
        Height = Math.Max(500, Math.Min(690, workArea.Height - 48));
        MinimumSize = new Size(Math.Min(820, Width), Math.Min(520, Height)); StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        BackColor = Canvas; ForeColor = TextPrimary; Font = new Font("Segoe UI", 9F);
        _hotkeys = new GlobalHotkeyService(this);
        _hotkeys.Triggered += id =>
        {
            var entry = _config.Entries.FirstOrDefault(e => e.Id == id);
            if (entry is not null) { _ = RunEntry(entry); return; }
            var workspace = _config.Workspaces.FirstOrDefault(w => w.Id == id);
            if (workspace is not null) _ = RunWorkspace(workspace);
        };
        _hotkeys.RegistrationFailed += message => BeginInvoke(new Action(() => { _tray!.ShowBalloonTip(4000, "Shortcut unavailable", message, ToolTipIcon.Warning); SetStatus("Some shortcuts need attention", false); }));

        BuildUi();
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open QuickMacro", null, (_, _) => ShowWindow());
        menu.Items.Add("Workspaces", null, (_, _) => OpenWorkspaces());
        var hiddenWindows = new ToolStripMenuItem("Hidden apps");
        hiddenWindows.DropDownOpening += (_, _) => RefreshHiddenWindowsMenu(hiddenWindows);
        menu.Items.Add(hiddenWindows);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Enable/disable all shortcuts", null, (_, _) => ToggleAll());
        menu.Items.Add("Reload configuration", null, (_, _) => Reload());
        menu.Items.Add("Settings", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());
        _tray = new NotifyIcon { Icon = Icon, Text = "QuickMacro", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => ShowWindow();

        ReRegister();
        RefreshEntries();
        ThemeManager.Apply(this);
        if (loadWarning is not null) Shown += (_, _) => MessageBox.Show(this, loadWarning, "QuickMacro configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        if (_config.Settings.StartMinimized || Environment.GetCommandLineArgs().Contains("--minimized", StringComparer.OrdinalIgnoreCase)) BeginInvoke(new Action(Hide));
    }

    private void BuildUi()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Canvas, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 102));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(26, 18, 26, 12) };
        var mark = new Label { Text = "Q", TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 17), ForeColor = Color.White, BackColor = Accent, Size = new Size(42, 42), Location = new Point(26, 25) };
        var title = new Label { Text = "QuickMacro", AutoSize = true, Font = new Font("Segoe UI Semibold", 20), ForeColor = TextPrimary, Location = new Point(82, 20) };
        var subtitle = new Label { Text = "Your apps, one shortcut away.", AutoSize = true, Font = new Font("Segoe UI", 9.5F), ForeColor = TextMuted, Location = new Point(84, 59) };
        _status.AutoSize = true; _status.Anchor = AnchorStyles.Top | AnchorStyles.Right; _status.Location = new Point(Width - 245, 38); _status.Padding = new Padding(12, 7, 12, 7); _status.Font = new Font("Segoe UI Semibold", 9F);
        header.Controls.Add(mark); header.Controls.Add(title); header.Controls.Add(subtitle); header.Controls.Add(_status);
        header.Resize += (_, _) => _status.Left = header.ClientSize.Width - _status.Width - 28;

        var toolbar = new Panel { Dock = DockStyle.Fill, BackColor = Canvas, Padding = new Padding(26, 10, 26, 8) };
        _count.AutoSize = true; _count.Font = new Font("Segoe UI Semibold", 10F); _count.ForeColor = TextMuted; _count.Location = new Point(28, 25);
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(400, 12) };
        actions.Controls.Add(Button("Backup", (_, _) => BackupRestore()));
        actions.Controls.Add(Button("Workspaces", (_, _) => OpenWorkspaces()));
        actions.Controls.Add(Button("Settings", (_, _) => OpenSettings()));
        actions.Controls.Add(Button("＋  Add application", (_, _) => EditEntry(null), primary: true));
        toolbar.Controls.Add(_count); toolbar.Controls.Add(actions);
        toolbar.Resize += (_, _) => actions.Left = toolbar.ClientSize.Width - actions.Width - 26;

        _entriesPanel.Dock = DockStyle.Fill; _entriesPanel.AutoScroll = true; _entriesPanel.WrapContents = false; _entriesPanel.FlowDirection = FlowDirection.TopDown; _entriesPanel.Padding = new Padding(26, 4, 26, 20); _entriesPanel.BackColor = Canvas;
        _entriesPanel.SizeChanged += (_, _) => ResizeEntryCards();

        layout.Controls.Add(header, 0, 0); layout.Controls.Add(toolbar, 0, 1); layout.Controls.Add(_entriesPanel, 0, 2);
        Controls.Add(layout);
    }

    private Button Button(string text, EventHandler click, bool primary = false)
    {
        var button = new Button
        {
            Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = primary ? Color.White : TextPrimary,
            BackColor = primary ? Accent : SurfaceRaised, Font = new Font("Segoe UI Semibold", 9F),
            Padding = new Padding(primary ? 12 : 10, 7, primary ? 12 : 10, 7), Margin = new Padding(5, 0, 5, 0), Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(32, 86, 200) : Color.FromArgb(222, 229, 239);
        button.Click += click;
        return button;
    }

    private void RefreshEntries()
    {
        _entriesPanel.SuspendLayout();
        _entriesPanel.Controls.Clear();
        _count.Text = _config.Entries.Count == 1 ? "1 shortcut" : $"{_config.Entries.Count} shortcuts";
        SetStatus(_config.Settings.EnableAllShortcuts ? "Shortcuts on" : "Shortcuts paused", _config.Settings.EnableAllShortcuts);

        if (_config.Entries.Count == 0)
        {
            var empty = new Panel { Height = 230, Width = Math.Max(400, _entriesPanel.ClientSize.Width - 58), BackColor = Surface, Margin = new Padding(0, 12, 0, 12) };
            var heading = new Label { Text = "Your shortcuts will appear here", AutoSize = true, Font = new Font("Segoe UI Semibold", 15F), ForeColor = TextPrimary };
            var copy = new Label { Text = "Add an app or website, then open it from anywhere with a hotkey.", AutoSize = true, Font = new Font("Segoe UI", 9.5F), ForeColor = TextMuted };
            var add = Button("Add app", (_, _) => EditEntry(null), primary: true);
            empty.Controls.Add(heading); empty.Controls.Add(copy); empty.Controls.Add(add);
            empty.Resize += (_, _) => { heading.Left = (empty.ClientSize.Width - heading.Width) / 2; heading.Top = 54; copy.Left = (empty.ClientSize.Width - copy.Width) / 2; copy.Top = 91; add.Left = (empty.ClientSize.Width - add.Width) / 2; add.Top = 135; };
            _entriesPanel.Controls.Add(empty);
        }
        else
        {
            foreach (var entry in _config.Entries) _entriesPanel.Controls.Add(CreateEntryCard(entry));
        }
        ResizeEntryCards();
        _entriesPanel.ResumeLayout();
    }

    private Control CreateEntryCard(LauncherEntry entry)
    {
        var card = new Panel { Height = 146, Width = Math.Max(500, _entriesPanel.ClientSize.Width - 58), BackColor = Surface, Margin = new Padding(0, 0, 0, 16), Padding = new Padding(18, 12, 14, 12), Tag = entry.Id };
        card.Paint += (_, e) => { using var pen = new Pen(Color.FromArgb(222, 228, 237)); e.Graphics.DrawRectangle(pen, 0, 0, card.ClientSize.Width - 1, card.ClientSize.Height - 1); };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Surface, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var appInfo = new Panel { Dock = DockStyle.Fill, BackColor = Surface };
        var appName = new Label { Text = entry.Name, AutoEllipsis = true, Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI Semibold", 12F), ForeColor = entry.Enabled ? TextPrimary : TextMuted };
        var target = new Label { Text = LauncherTargetTypeLabels.Get(entry.TargetType) + (entry.Enabled ? "" : " · Paused"), AutoEllipsis = true, Dock = DockStyle.Top, Height = 22, Font = new Font("Segoe UI", 9F), ForeColor = TextMuted };
        appInfo.Controls.Add(target); appInfo.Controls.Add(appName);

        var shortcut = new Label { Text = entry.Shortcut?.ToString() ?? "Shortcut not set", AutoEllipsis = true, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.None, Anchor = AnchorStyles.Right | AnchorStyles.Top, Width = 196, Height = 34, Margin = new Padding(4), Padding = new Padding(6, 0, 6, 0), BackColor = Color.FromArgb(232, 240, 253), ForeColor = Color.FromArgb(38, 82, 154), Font = new Font("Segoe UI Semibold", 9F) };
        appInfo.Dock = DockStyle.Fill;
        var shortcutPanel = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(0, 7, 0, 0) };
        shortcutPanel.Controls.Add(shortcut);
        shortcutPanel.Resize += (_, _) => shortcut.Left = Math.Max(0, shortcutPanel.ClientSize.Width - shortcut.Width - 6);
        var actionText = entry.ShortcutAction switch { ShortcutAction.Minimize when entry.MinimizeToSystemTray => "Minimize to tray", ShortcutAction.Minimize => "Minimize / restore", ShortcutAction.Close => "Close if open", _ => "Launch app" };
        var action = new Label { Text = actionText, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill, ForeColor = TextMuted, Font = new Font("Segoe UI", 9F), Margin = new Padding(2, 0, 2, 0) };

        var commands = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Surface, Padding = new Padding(0, 5, 0, 0), Margin = Padding.Empty };
        var run = CompactButton("Run", Accent);
        var edit = CompactButton("Edit", SurfaceRaised);
        var toggle = CompactButton(entry.Enabled ? "Enabled" : "Paused", entry.Enabled ? Color.FromArgb(229, 246, 236) : Color.FromArgb(250, 241, 224));
        var delete = CompactButton("Delete", Color.FromArgb(252, 235, 237));
        delete.Click += (_, _) => DeleteEntry(entry);
        run.Click += async (_, _) => await RunEntry(entry);
        edit.Click += (_, _) => EditEntry(entry);
        toggle.Click += (_, _) => { entry.Enabled = !entry.Enabled; SaveAndRefresh(); };
        commands.Controls.Add(run); commands.Controls.Add(edit); commands.Controls.Add(toggle); commands.Controls.Add(delete);

        layout.Controls.Add(appInfo, 0, 0); layout.Controls.Add(shortcutPanel, 1, 0);
        layout.Controls.Add(action, 0, 1); layout.Controls.Add(commands, 1, 1);
        card.Controls.Add(layout);
        return card;
    }

    private Button CompactButton(string text, Color background)
    {
        var fore = text switch { "Delete" => Color.FromArgb(173, 54, 67), "Run" => Color.White, "Enabled" => Color.FromArgb(42, 119, 76), "Paused" => Color.FromArgb(145, 103, 35), _ => TextPrimary };
        var button = new Button { Text = text, Width = text switch { "Delete" => 62, "Enabled" => 75, "Paused" => 75, _ => 54 }, Height = 31, FlatStyle = FlatStyle.Flat, BackColor = background, ForeColor = fore, Font = new Font("Segoe UI Semibold", 8.5F), Margin = new Padding(3, 2, 3, 2), Cursor = Cursors.Hand };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void ResizeEntryCards()
    {
        var width = Math.Max(500, _entriesPanel.ClientSize.Width - 72 - SystemInformation.VerticalScrollBarWidth);
        foreach (Control control in _entriesPanel.Controls) control.Width = width;
    }

    private void SetStatus(string message, bool active)
    {
        _status.Text = message;
        _status.BackColor = active ? Color.FromArgb(25, 64, 57) : Color.FromArgb(70, 57, 34);
        _status.ForeColor = active ? Color.FromArgb(139, 231, 199) : Color.FromArgb(242, 202, 126);
        if (_status.Parent is not null) _status.Left = _status.Parent.ClientSize.Width - _status.Width - 28;
    }

    private void EditEntry(LauncherEntry? existing)
    {
        using var dialog = new EntryDialog(existing);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Entry is null) return;
        if (dialog.Entry.Shortcut is not null && _config.Entries.Any(e => e.Id != dialog.Entry.Id && e.Enabled && dialog.Entry.Enabled && dialog.Entry.Shortcut.Equals(e.Shortcut))
            || dialog.Entry.Shortcut is not null && dialog.Entry.Enabled && _config.Workspaces.Any(w => w.Enabled && dialog.Entry.Shortcut.Equals(w.Shortcut)))
        {
            MessageBox.Show(this, "Another enabled app or workspace already uses this shortcut.", "Duplicate shortcut", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        var index = _config.Entries.FindIndex(e => e.Id == dialog.Entry.Id);
        if (index >= 0) _config.Entries[index] = dialog.Entry; else _config.Entries.Add(dialog.Entry);
        SaveAndRefresh();
    }

    private void DeleteEntry(LauncherEntry entry)
    {
        if (MessageBox.Show(this, $"Remove {entry.Name} from QuickMacro?", "Remove app", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _config.Entries.Remove(entry);
        foreach (var workspace in _config.Workspaces) workspace.Items.RemoveAll(item => item.EntryId == entry.Id);
        _config.Workspaces.RemoveAll(workspace => workspace.Items.Count == 0);
        SaveAndRefresh();
    }

    private async Task RunEntry(LauncherEntry entry)
    {
        try
        {
            await _launcher.LaunchAsync(entry, _config.Settings.PreventDuplicateLaunches);
            if (_config.Settings.ShowLaunchNotifications) _tray.ShowBalloonTip(1500, "QuickMacro", $"Shortcut action completed for {entry.Name}", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "Could not run shortcut action", MessageBoxButtons.OK, MessageBoxIcon.Error)));
        }
    }

    private async Task RunWorkspace(Workspace workspace)
    {
        try
        {
            SetStatus($"●  ARRANGING {workspace.Name.ToUpperInvariant()}", true);
            var errors = await _workspaceService.LaunchAsync(workspace, _config.Entries);
            if (errors.Count > 0)
                BeginInvoke(new Action(() => MessageBox.Show(this, string.Join(Environment.NewLine, errors), $"Workspace “{workspace.Name}”", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
            SetStatus(_config.Settings.EnableAllShortcuts ? "●  SHORTCUTS ACTIVE" : "●  SHORTCUTS PAUSED", _config.Settings.EnableAllShortcuts);
        }
        catch (Exception ex) { BeginInvoke(new Action(() => MessageBox.Show(this, ex.Message, "Could not launch workspace", MessageBoxButtons.OK, MessageBoxIcon.Error))); }
    }

    private void SaveAndRefresh()
    {
        try { _repository.Save(_config); ReRegister(); RefreshEntries(); ThemeManager.CurrentMode = string.IsNullOrWhiteSpace(_config.Settings.ThemeMode) ? "System" : _config.Settings.ThemeMode; ThemeManager.Apply(this); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save configuration", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ReRegister() => _hotkeys.Replace(_config.Entries, _config.Workspaces, _config.Settings.EnableAllShortcuts);
    private void ToggleAll() { _config.Settings.EnableAllShortcuts = !_config.Settings.EnableAllShortcuts; SaveAndRefresh(); }

    private void Reload()
    {
        var result = _repository.Load();
        _config.Entries = result.Configuration.Entries; _config.Workspaces = result.Configuration.Workspaces; _config.Settings = result.Configuration.Settings;
        SaveAndRefresh();
        if (result.Warning is not null) MessageBox.Show(this, result.Warning);
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsDialog(_config.Settings, _startup.IsEnabled);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _config.Settings = dialog.Settings;
        try { _startup.SetEnabled(dialog.StartupEnabled); SaveAndRefresh(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Startup setting", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void OpenWorkspaces()
    {
        using var dialog = new WorkspaceManagerDialog(_config.Workspaces, _config.Entries);
        dialog.WorkspacesChanged += workspaces => { _config.Workspaces = workspaces.ToList(); SaveAndRefresh(); };
        dialog.ShowDialog(this);
    }

    private void BackupRestore()
    {
        using var menu = new ContextMenuStrip();
        menu.Items.Add("Export backup…", null, (_, _) =>
        {
            using var dialog = new SaveFileDialog { Filter = "QuickMacro configuration (*.json)|*.json", FileName = "QuickMacro-backup.json" };
            if (dialog.ShowDialog(this) == DialogResult.OK) try { _repository.Export(dialog.FileName, _config); } catch (Exception ex) { MessageBox.Show(this, ex.Message); }
        });
        menu.Items.Add("Restore backup…", null, (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "QuickMacro configuration (*.json)|*.json" };
            if (dialog.ShowDialog(this) == DialogResult.OK) try { var config = _repository.Import(dialog.FileName)!; _config.Entries = config.Entries; _config.Workspaces = config.Workspaces; _config.Settings = config.Settings; SaveAndRefresh(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Invalid backup", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        });
        menu.Show(Cursor.Position);
    }

    private void ShowWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    private void RefreshHiddenWindowsMenu(ToolStripMenuItem menu)
    {
        menu.DropDownItems.Clear();
        var hiddenWindows = WindowActionService.GetHiddenWindows();
        if (hiddenWindows.Count == 0)
        {
            menu.DropDownItems.Add("No hidden apps").Enabled = false;
            return;
        }

        foreach (var hidden in hiddenWindows)
        {
            var label = string.IsNullOrWhiteSpace(hidden.WindowTitle)
                || string.Equals(hidden.WindowTitle, hidden.EntryName, StringComparison.CurrentCultureIgnoreCase)
                ? hidden.EntryName
                : $"{hidden.EntryName} — {hidden.WindowTitle}";
            var item = new ToolStripMenuItem(label);
            item.Click += (_, _) => WindowActionService.RestoreHiddenWindow(hidden.Handle);
            menu.DropDownItems.Add(item);
        }
    }

    private void ExitApp() { _allowExit = true; Close(); }
    protected override void WndProc(ref Message message) { if (_hotkeys.HandleMessage(ref message)) return; base.WndProc(ref message); }
    protected override void OnFormClosing(FormClosingEventArgs e) { if (!_allowExit && _config.Settings.MinimizeToTray && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } base.OnFormClosing(e); }
    protected override void OnFormClosed(FormClosedEventArgs e) { WindowActionService.RestoreAllHiddenWindows(); _hotkeys.Dispose(); _tray.Visible = false; _tray.Dispose(); base.OnFormClosed(e); }
}
