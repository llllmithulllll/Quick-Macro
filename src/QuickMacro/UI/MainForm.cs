using QuickMacro.Models;
using QuickMacro.Services;

namespace QuickMacro.UI;

public sealed class MainForm : Form
{
    private static readonly Color Canvas = Color.FromArgb(13, 18, 30);
    private static readonly Color Surface = Color.FromArgb(19, 28, 44);
    private static readonly Color SurfaceRaised = Color.FromArgb(26, 38, 58);
    private static readonly Color TextPrimary = Color.FromArgb(237, 242, 250);
    private static readonly Color TextMuted = Color.FromArgb(149, 164, 185);
    private static readonly Color Accent = Color.FromArgb(79, 153, 255);

    private readonly ConfigurationRepository _repository;
    private readonly AppConfiguration _config;
    private readonly StartupService _startup;
    private readonly LauncherService _launcher = new();
    private readonly GlobalHotkeyService _hotkeys;
    private readonly NotifyIcon _tray;
    private readonly FlowLayoutPanel _entriesPanel = new();
    private readonly Label _status = new();
    private readonly Label _count = new();
    private bool _allowExit;

    public MainForm(ConfigurationRepository repository, AppConfiguration config, StartupService startup, string? loadWarning)
    {
        _repository = repository; _config = config; _startup = startup;
        Text = "QuickMacro"; Width = 1060; Height = 690; MinimumSize = new Size(820, 520); StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        BackColor = Canvas; ForeColor = TextPrimary; Font = new Font("Segoe UI", 9F);
        _hotkeys = new GlobalHotkeyService(this);
        _hotkeys.Triggered += id => { var entry = _config.Entries.FirstOrDefault(e => e.Id == id); if (entry is not null) _ = RunEntry(entry); };
        _hotkeys.RegistrationFailed += message => BeginInvoke(new Action(() => { _tray.ShowBalloonTip(4000, "Shortcut unavailable", message, ToolTipIcon.Warning); SetStatus("Some shortcuts need attention", false); }));

        BuildUi();
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open QuickMacro", null, (_, _) => ShowWindow());
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
        if (loadWarning is not null) Shown += (_, _) => MessageBox.Show(this, loadWarning, "QuickMacro configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        if (_config.Settings.StartMinimized || Environment.GetCommandLineArgs().Contains("--minimized", StringComparer.OrdinalIgnoreCase)) BeginInvoke(new Action(Hide));
    }

    private void BuildUi()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Canvas, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
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
        actions.Controls.Add(Button("Backup / restore", (_, _) => BackupRestore()));
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
            Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = TextPrimary,
            BackColor = primary ? Accent : SurfaceRaised, Font = new Font("Segoe UI Semibold", 9F),
            Padding = new Padding(primary ? 12 : 10, 7, primary ? 12 : 10, 7), Margin = new Padding(5, 0, 5, 0), Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(101, 168, 255) : Color.FromArgb(38, 55, 80);
        button.Click += click;
        return button;
    }

    private void RefreshEntries()
    {
        _entriesPanel.SuspendLayout();
        _entriesPanel.Controls.Clear();
        _count.Text = _config.Entries.Count == 1 ? "1 APPLICATION" : $"{_config.Entries.Count} APPLICATIONS";
        SetStatus(_config.Settings.EnableAllShortcuts ? "●  SHORTCUTS ACTIVE" : "●  SHORTCUTS PAUSED", _config.Settings.EnableAllShortcuts);

        if (_config.Entries.Count == 0)
        {
            var empty = new Panel { Height = 230, Width = Math.Max(400, _entriesPanel.ClientSize.Width - 58), BackColor = Surface, Margin = new Padding(0, 12, 0, 12) };
            var heading = new Label { Text = "Start with your first app", AutoSize = true, Font = new Font("Segoe UI Semibold", 15F), ForeColor = TextPrimary };
            var copy = new Label { Text = "Add an app, choose a shortcut, and launch it from anywhere.", AutoSize = true, Font = new Font("Segoe UI", 9.5F), ForeColor = TextMuted };
            var add = Button("＋  Add application", (_, _) => EditEntry(null), primary: true);
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
        var card = new Panel { Height = 104, Width = Math.Max(500, _entriesPanel.ClientSize.Width - 58), BackColor = Surface, Margin = new Padding(0, 0, 0, 10), Padding = new Padding(16, 12, 12, 12), Tag = entry.Id };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Surface, Margin = Padding.Empty };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));

        var appInfo = new Panel { Dock = DockStyle.Fill, BackColor = Surface };
        var appName = new Label { Text = entry.Name + (entry.Enabled ? "" : "  ·  PAUSED"), AutoEllipsis = true, Dock = DockStyle.Top, Height = 29, Font = new Font("Segoe UI Semibold", 11F), ForeColor = entry.Enabled ? TextPrimary : TextMuted };
        var targetText = entry.TargetType switch
        {
            LauncherTargetType.StoreApp => "Microsoft Store app · " + entry.TargetPath,
            LauncherTargetType.WebsiteUrl => "Website · " + entry.TargetPath,
            LauncherTargetType.BrowserAppShortcut => "Browser-installed app · " + entry.TargetPath,
            _ => entry.TargetPath
        };
        var target = new Label { Text = targetText, AutoEllipsis = true, Dock = DockStyle.Top, Height = 25, Font = new Font("Segoe UI", 8.5F), ForeColor = TextMuted };
        appInfo.Controls.Add(target); appInfo.Controls.Add(appName);

        var shortcut = new Label { Text = entry.Shortcut?.ToString() ?? "NO SHORTCUT", AutoEllipsis = true, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill, Margin = new Padding(7, 19, 7, 19), Padding = new Padding(6, 0, 6, 0), BackColor = Color.FromArgb(25, 44, 69), ForeColor = entry.Shortcut is null ? TextMuted : Color.FromArgb(143, 199, 255), Font = new Font("Segoe UI Semibold", 9F) };
        var actionText = entry.ShortcutAction switch { ShortcutAction.Minimize when entry.MinimizeToSystemTray => "MINIMIZE TO TRAY", ShortcutAction.Minimize => "MINIMIZE", ShortcutAction.Close => "CLOSE", _ => "LAUNCH" };
        var action = new Label { Text = actionText, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill, ForeColor = TextMuted, Font = new Font("Segoe UI Semibold", 8F), Margin = new Padding(4) };

        var commands = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Surface, Padding = new Padding(0, 22, 0, 0) };
        var delete = CompactButton("Delete", Color.FromArgb(68, 38, 47));
        var run = CompactButton("Run", Color.FromArgb(30, 65, 105));
        var edit = CompactButton("Edit", SurfaceRaised);
        var toggle = CompactButton(entry.Enabled ? "On" : "Off", entry.Enabled ? Color.FromArgb(28, 67, 62) : Color.FromArgb(57, 61, 72));
        delete.Click += (_, _) => DeleteEntry(entry);
        run.Click += async (_, _) => await RunEntry(entry);
        edit.Click += (_, _) => EditEntry(entry);
        toggle.Click += (_, _) => { entry.Enabled = !entry.Enabled; SaveAndRefresh(); };
        commands.Controls.Add(delete); commands.Controls.Add(run); commands.Controls.Add(edit); commands.Controls.Add(toggle);

        layout.Controls.Add(appInfo, 0, 0); layout.Controls.Add(shortcut, 1, 0); layout.Controls.Add(action, 2, 0); layout.Controls.Add(commands, 3, 0);
        card.Controls.Add(layout);
        return card;
    }

    private Button CompactButton(string text, Color background)
    {
        var button = new Button { Text = text, Width = text == "Delete" ? 62 : 52, Height = 31, FlatStyle = FlatStyle.Flat, BackColor = background, ForeColor = TextPrimary, Font = new Font("Segoe UI Semibold", 8.5F), Margin = new Padding(3, 0, 3, 0), Cursor = Cursors.Hand };
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
        if (dialog.Entry.Shortcut is not null && _config.Entries.Any(e => e.Id != dialog.Entry.Id && e.Enabled && dialog.Entry.Enabled && dialog.Entry.Shortcut.Equals(e.Shortcut)))
        {
            MessageBox.Show(this, "Another enabled app already uses this shortcut.", "Duplicate shortcut", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

    private void SaveAndRefresh()
    {
        try { _repository.Save(_config); ReRegister(); RefreshEntries(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Could not save configuration", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ReRegister() => _hotkeys.Replace(_config.Entries, _config.Settings.EnableAllShortcuts);
    private void ToggleAll() { _config.Settings.EnableAllShortcuts = !_config.Settings.EnableAllShortcuts; SaveAndRefresh(); }

    private void Reload()
    {
        var result = _repository.Load();
        _config.Entries = result.Configuration.Entries; _config.Settings = result.Configuration.Settings;
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
            if (dialog.ShowDialog(this) == DialogResult.OK) try { var config = _repository.Import(dialog.FileName)!; _config.Entries = config.Entries; _config.Settings = config.Settings; SaveAndRefresh(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Invalid backup", MessageBoxButtons.OK, MessageBoxIcon.Error); }
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
