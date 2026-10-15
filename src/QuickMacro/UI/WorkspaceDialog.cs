using System.Drawing.Drawing2D;
using QuickMacro.Models;
using QuickMacro.Services;

namespace QuickMacro.UI;

public sealed class WorkspaceDialog : Form
{
    private static readonly Color Canvas = Color.FromArgb(246, 248, 252);
    private static readonly Color Surface = Color.FromArgb(255, 255, 255);
    private static readonly Color Raised = Color.FromArgb(237, 241, 247);
    private static readonly Color Accent = Color.FromArgb(48, 105, 220);
    private readonly TextBox _name = new() { Dock = DockStyle.Fill };
    private readonly Label _shortcutLabel = new() { AutoSize = true, ForeColor = Color.FromArgb(25, 35, 52) };
    private readonly CheckBox _windowsModifier = new() { Text = "Windows key", AutoSize = true };
    private readonly CheckBox _enabled = new() { Text = "Workspace enabled", Checked = true, AutoSize = true };
    private readonly CheckBox _openOnNewDesktop = new() { Text = "Open on a new Windows desktop", AutoSize = true };
    private readonly ComboBox _repeatAction = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
    private readonly ListBox _availableApps = new() { Dock = DockStyle.Fill, IntegralHeight = false, BackColor = Surface, ForeColor = Color.FromArgb(25, 35, 52), BorderStyle = BorderStyle.None, DisplayMember = nameof(LauncherEntry.Name) };
    private readonly List<WorkspaceItem> _workspaceItems = [];
    private WorkspaceCanvas _canvas = null!;
    private Label _selectionInfo = null!;
    private Button _arrangeButton = null!;
    private ShortcutBinding? _shortcut;
    private bool _recording;
    private readonly Guid _id;
    private readonly IReadOnlyList<LauncherEntry> _entries;
    public Workspace? Workspace { get; private set; }

    public WorkspaceDialog(Workspace? workspace, IReadOnlyList<LauncherEntry> entries)
    {
        _id = workspace?.Id ?? Guid.NewGuid(); _entries = entries;
        Text = workspace is null ? "Create workspace" : "Edit workspace";
        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        AutoScaleMode = AutoScaleMode.Dpi;
        var scale = DialogLayout.GetScaleFactor();
        Width = Math.Min(1100, Math.Max(480, (int)(workArea.Width / scale) - 32));
        Height = Math.Min(730, Math.Max(420, (int)(workArea.Height / scale) - 32));
        MinimumSize = new Size(Math.Min(760, Width), Math.Min(500, Height));
        StartPosition = FormStartPosition.CenterParent; BackColor = Canvas; ForeColor = Color.FromArgb(25, 35, 52); Font = new Font("Segoe UI", 9F); KeyPreview = true;
        _repeatAction.Items.AddRange(["Launch / arrange again", "Minimize / restore apps", "Close workspace apps", "Close apps and this desktop"]);
        _repeatAction.SelectedIndex = (int)(workspace?.RepeatAction ?? WorkspaceRepeatAction.Launch);
        if (workspace is not null) { _name.Text = workspace.Name; _shortcut = workspace.Shortcut; _enabled.Checked = workspace.Enabled; _openOnNewDesktop.Checked = workspace.OpenOnNewDesktop; _workspaceItems.AddRange(workspace.Items.Select(Clone)); }

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 3, BackColor = Canvas };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildEditor(), 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        Controls.Add(root);
        Shown += (_, _) => ThemeManager.Apply(this);
        AcceptButton = _saveButton; CancelButton = _cancelButton;
        RefreshAvailableApps(); RefreshCanvas(); UpdateShortcutLabel(); KeyDown += CaptureKey;
    }

    private Button _saveButton = null!;
    private Button _cancelButton = null!;

    private Control BuildHeader()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Canvas, Margin = Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(new Label { Text = "WORKSPACE DETAILS", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 9F), ForeColor = Color.FromArgb(96, 111, 132), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);

        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Canvas, Margin = Padding.Empty };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        var namePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Canvas, Margin = new Padding(0, 0, 18, 0) };
        namePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); namePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); namePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); namePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        namePanel.Controls.Add(new Label { Text = "Workspace name", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(75, 89, 109), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _name.Dock = DockStyle.Fill; _name.Margin = Padding.Empty; _name.BackColor = Raised; _name.ForeColor = Color.FromArgb(25, 35, 52); _name.BorderStyle = BorderStyle.FixedSingle;
        namePanel.Controls.Add(_name, 0, 1);
        namePanel.Controls.Add(_enabled, 0, 2); _enabled.ForeColor = Color.FromArgb(75, 89, 109); _enabled.Margin = new Padding(0, 2, 0, 0);
        namePanel.Controls.Add(_openOnNewDesktop, 0, 3); _openOnNewDesktop.ForeColor = Color.FromArgb(75, 89, 109); _openOnNewDesktop.Margin = new Padding(0, 2, 0, 0);

        var shortcutPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Canvas, Margin = Padding.Empty };
        shortcutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); shortcutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); shortcutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); shortcutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shortcutPanel.Controls.Add(new Label { Text = "Global shortcut", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(75, 89, 109), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        var shortcutButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, Padding = Padding.Empty };
        var record = Button("Set shortcut"); record.Click += (_, _) => { _recording = true; _shortcutLabel.Text = "Press a key combination"; Focus(); };
        var clear = Button("Clear"); clear.Click += (_, _) => { _shortcut = null; UpdateShortcutLabel(); };
        shortcutButtons.Controls.Add(record); shortcutButtons.Controls.Add(clear); shortcutButtons.Controls.Add(_shortcutLabel);
        _shortcutLabel.Margin = new Padding(9, 8, 8, 0);
        shortcutPanel.Controls.Add(shortcutButtons, 0, 1);
        _windowsModifier.ForeColor = Color.FromArgb(75, 89, 109); _windowsModifier.Margin = new Padding(2, 2, 0, 0);
        shortcutPanel.Controls.Add(_windowsModifier, 0, 2);
        var repeatPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, Padding = Padding.Empty };
        repeatPanel.Controls.Add(new Label { Text = "Shortcut again", AutoSize = true, ForeColor = Color.FromArgb(75, 89, 109), Margin = new Padding(2, 5, 8, 0) });
        repeatPanel.Controls.Add(_repeatAction);
        _repeatAction.SelectedIndexChanged += (_, _) => _repeatAction.Enabled = _openOnNewDesktop.Checked || _repeatAction.SelectedIndex != 3;
        _openOnNewDesktop.CheckedChanged += (_, _) => _repeatAction.Enabled = _openOnNewDesktop.Checked || _repeatAction.SelectedIndex != 3;
        shortcutPanel.Controls.Add(repeatPanel, 0, 3);

        fields.Controls.Add(namePanel, 0, 0); fields.Controls.Add(shortcutPanel, 1, 0);
        root.Controls.Add(fields, 0, 1);
        return root;
    }

    private Control BuildEditor()
    {
        var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Canvas, Padding = new Padding(0, 8, 0, 8) };
        editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250)); editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var library = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1, BackColor = Surface, Padding = new Padding(12) };
        library.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); library.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (var i = 0; i < 4; i++) library.RowStyles.Add(new RowStyle(SizeType.Absolute, 41));
        library.Controls.Add(new Label { Text = "Add apps", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 10F), ForeColor = Color.FromArgb(75, 89, 109), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        library.Controls.Add(_availableApps, 0, 1);
        var add = Button("＋  Add selected", true); add.Dock = DockStyle.Fill; add.Click += (_, _) => AddSelectedApp(); library.Controls.Add(add, 0, 2);
        _availableApps.DoubleClick += (_, _) => AddSelectedApp();
        _availableApps.DrawMode = DrawMode.OwnerDrawFixed; _availableApps.ItemHeight = 56;
        _availableApps.DrawItem += (_, e) =>
        {
            e.DrawBackground();
            if (e.Index >= 0 && _availableApps.Items[e.Index] is LauncherEntry entry)
            {
                var selected = (e.State & DrawItemState.Selected) != 0;
                using var background = new SolidBrush(selected ? Color.FromArgb(224, 236, 255) : Color.White);
                e.Graphics.FillRectangle(background, e.Bounds);
                TextRenderer.DrawText(e.Graphics, entry.Name, Font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 3, e.Bounds.Width - 16, 23), Color.FromArgb(25, 35, 52), TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                TextRenderer.DrawText(e.Graphics, LauncherTargetTypeLabels.Get(entry.TargetType), Font, new Rectangle(e.Bounds.X + 8, e.Bounds.Y + 27, e.Bounds.Width - 16, 23), Color.FromArgb(96, 111, 132), TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            e.DrawFocusRectangle();
        };
        var installed = Button("Installed apps…"); installed.Dock = DockStyle.Fill; installed.Click += (_, _) => AddInstalledApp(); library.Controls.Add(installed, 0, 3);
        var browse = Button("Browse for app…"); browse.Dock = DockStyle.Fill; browse.Click += (_, _) => BrowseForApp(); library.Controls.Add(browse, 0, 4);
        var remove = Button("Remove selected window"); remove.Dock = DockStyle.Fill; remove.Click += (_, _) => RemoveSelectedApp(); library.Controls.Add(remove, 0, 5);
        editor.Controls.Add(library, 0, 0);

        var preview = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Canvas, Padding = new Padding(12, 0, 0, 0) };
        preview.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); preview.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var caption = new Panel { Dock = DockStyle.Fill, BackColor = Canvas };
        caption.Controls.Add(new Label { Text = "Desktop layout", AutoSize = true, Font = new Font("Segoe UI Semibold", 10F), ForeColor = Color.FromArgb(75, 89, 109), Location = new Point(0, 10) });
        _selectionInfo = new Label { Text = "Drag to move · Grab any edge or corner to resize", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, ForeColor = Color.FromArgb(96, 111, 132), Location = new Point(310, 10) };
        _arrangeButton = Button("Auto arrange"); _arrangeButton.Location = new Point(0, 2); _arrangeButton.Click += (_, _) => AutoArrange();
        caption.Controls.Add(_selectionInfo); caption.Controls.Add(_arrangeButton); caption.Resize += (_, _) => PositionCaptionControls(caption);
        preview.Controls.Add(caption, 0, 0);
        _canvas = new WorkspaceCanvas(_workspaceItems, ResolveEntry) { Dock = DockStyle.Fill };
        _canvas.SelectionChanged += UpdateSelectionInfo;
        preview.Controls.Add(_canvas, 0, 1);
        editor.Controls.Add(preview, 1, 0);
        return editor;
    }

    private Control BuildFooter()
    {
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Canvas, Padding = new Padding(0, 4, 0, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
        _saveButton = Button("Save workspace", true); _saveButton.Dock = DockStyle.Fill; _saveButton.Margin = new Padding(4, 3, 0, 3); _saveButton.Click += (_, _) => SaveWorkspace();
        _cancelButton = Button("Cancel"); _cancelButton.Dock = DockStyle.Fill; _cancelButton.Margin = new Padding(4, 3, 0, 3); _cancelButton.DialogResult = DialogResult.Cancel;
        var help = new Label { Text = "Drag to move · Resize from any edge", Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Color.FromArgb(96, 111, 132), TextAlign = ContentAlignment.MiddleCenter, Margin = Padding.Empty, Padding = new Padding(0, 2, 0, 2) };
        footer.Controls.Add(help, 0, 0); footer.Controls.Add(_cancelButton, 1, 0); footer.Controls.Add(_saveButton, 2, 0);
        return footer;
    }

    private static WorkspaceItem Clone(WorkspaceItem item)
    {
        var x = Math.Clamp(item.X, 0, 92); var y = Math.Clamp(item.Y, 0, 92);
        var target = item.Target is null ? null : new LauncherEntry { Id = item.Target.Id, Name = item.Target.Name, TargetPath = item.Target.TargetPath, Arguments = item.Target.Arguments, WorkingDirectory = item.Target.WorkingDirectory, TargetType = item.Target.TargetType, ShortcutAction = ShortcutAction.Launch, Enabled = true };
        return new WorkspaceItem { EntryId = item.EntryId, Target = target, X = x, Y = y, Width = Math.Clamp(item.Width, 8, 100 - x), Height = Math.Clamp(item.Height, 8, 100 - y) };
    }
    private static Control Labeled(string label, Control input)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Canvas };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); row.Controls.Add(input, 1, 0);
        input.BackColor = Raised; input.ForeColor = Color.FromArgb(25, 35, 52); return row;
    }
    private static Button Button(string text, bool primary = false)
    {
        var button = new Button { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Raised, ForeColor = primary ? Color.White : Color.FromArgb(25, 35, 52), Padding = new Padding(9, 4, 9, 4), Margin = new Padding(4), Cursor = Cursors.Hand };
        button.FlatAppearance.BorderSize = 0; button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(32, 86, 200) : Color.FromArgb(222, 229, 239); return button;
    }

    private void AddSelectedApp()
    {
        if (_availableApps.SelectedItem is not LauncherEntry entry) return;
        AddEntryToWorkspace(entry, configuredEntry: true);
    }

    private void AddInstalledApp()
    {
        using var dialog = new InstalledAppDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedEntry is not null) AddEntryToWorkspace(dialog.SelectedEntry, configuredEntry: false);
    }

    private void BrowseForApp()
    {
        using var dialog = new OpenFileDialog { Title = "Choose an app for this workspace", Filter = "Apps and shortcuts (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|All files (*.*)|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var targetType = LauncherTargetType.FilePath;
        if (string.Equals(Path.GetExtension(dialog.FileName), ".lnk", StringComparison.OrdinalIgnoreCase))
        {
            try { if (InstalledAppCatalog.GetInstalledApps().FirstOrDefault(choice => string.Equals(choice.TargetPath, dialog.FileName, StringComparison.OrdinalIgnoreCase)) is { } choice) targetType = choice.TargetType; }
            catch { }
        }
        AddEntryToWorkspace(new LauncherEntry { Name = Path.GetFileNameWithoutExtension(dialog.FileName), TargetPath = dialog.FileName, TargetType = targetType }, configuredEntry: false);
    }

    private void AddEntryToWorkspace(LauncherEntry entry, bool configuredEntry)
    {
        var existing = configuredEntry ? entry : _entries.FirstOrDefault(candidate => candidate.TargetType == entry.TargetType && string.Equals(candidate.TargetPath, entry.TargetPath, StringComparison.OrdinalIgnoreCase));
        var duplicated = _workspaceItems.Any(item => existing is not null
            ? item.Target is null && item.EntryId == existing.Id
            : ResolveEntry(item) is { } current && current.TargetType == entry.TargetType && string.Equals(current.TargetPath, entry.TargetPath, StringComparison.OrdinalIgnoreCase));
        if (duplicated) { MessageBox.Show(this, "This app is already in the workspace."); return; }
        var item = new WorkspaceItem { EntryId = existing?.Id ?? Guid.Empty, Target = existing is null ? CloneEntry(entry) : null, Width = 100, Height = 100 };
        _workspaceItems.Add(item); RefreshAvailableApps(); AutoArrange(); _canvas.SelectItem(item);
    }

    private static LauncherEntry CloneEntry(LauncherEntry entry) => new() { Name = entry.Name, TargetPath = entry.TargetPath, Arguments = entry.Arguments, WorkingDirectory = entry.WorkingDirectory, TargetType = entry.TargetType, ShortcutAction = ShortcutAction.Launch, Enabled = true };

    private void RemoveSelectedApp()
    {
        var selected = _canvas.SelectedItem;
        if (selected is null) return;
        _workspaceItems.Remove(selected); _canvas.ClearSelection(); RefreshAvailableApps(); RefreshCanvas();
    }

    private void RefreshAvailableApps()
    {
        _availableApps.Items.Clear();
        foreach (var entry in _entries.Where(entry => entry.TargetType != LauncherTargetType.WebsiteUrl && !_workspaceItems.Any(item => item.EntryId == entry.Id)))
            _availableApps.Items.Add(entry);
        _availableApps.SelectedIndex = _availableApps.Items.Count > 0 ? 0 : -1;
    }

    private LauncherEntry? ResolveEntry(WorkspaceItem item) => item.Target ?? _entries.FirstOrDefault(entry => entry.Id == item.EntryId);

    private void RefreshCanvas() => _canvas?.Invalidate();
    private void AutoArrange()
    {
        if (_workspaceItems.Count == 0) return;
        var columns = (int)Math.Ceiling(Math.Sqrt(_workspaceItems.Count));
        var rows = (int)Math.Ceiling(_workspaceItems.Count / (double)columns);
        for (var i = 0; i < _workspaceItems.Count; i++)
        {
            var column = i % columns; var row = i / columns;
            var left = column * 100 / columns; var right = (column + 1) * 100 / columns;
            var top = row * 100 / rows; var bottom = (row + 1) * 100 / rows;
            _workspaceItems[i].X = left; _workspaceItems[i].Y = top;
            _workspaceItems[i].Width = right - left; _workspaceItems[i].Height = bottom - top;
        }
        _canvas.Invalidate(); UpdateSelectionInfo();
    }

    private void PositionCaptionControls(Control caption)
    {
        _arrangeButton.Left = Math.Max(0, caption.ClientSize.Width - _arrangeButton.Width);
        _selectionInfo.Left = Math.Max(210, _arrangeButton.Left - _selectionInfo.Width - 12);
    }
    private void UpdateSelectionInfo()
    {
        var item = _canvas.SelectedItem;
        var entry = item is null ? null : ResolveEntry(item);
        _selectionInfo.Text = item is null ? "Drag to move · Grab any edge or corner to resize" : $"{entry?.Name ?? "App"}  ·  {item.Width}% × {item.Height}%";
        if (_selectionInfo.Parent is not null) PositionCaptionControls(_selectionInfo.Parent);
    }

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
    private void SaveWorkspace()
    {
        if (string.IsNullOrWhiteSpace(_name.Text)) { MessageBox.Show(this, "Enter a workspace name."); return; }
        if (_shortcut is null) { MessageBox.Show(this, "Set a shortcut for this workspace."); return; }
        if (_workspaceItems.Count == 0) { MessageBox.Show(this, "Add at least one app to this workspace."); return; }
        var missing = _workspaceItems.FirstOrDefault(item => ResolveEntry(item) is null || item.Target is not null && ShortcutRules.ValidateEntry(item.Target) is not null);
        if (missing is not null) { MessageBox.Show(this, "Remove any missing apps from this workspace."); return; }
        Workspace = new Workspace { Id = _id, Name = _name.Text.Trim(), Shortcut = _shortcut, Enabled = _enabled.Checked, OpenOnNewDesktop = _openOnNewDesktop.Checked, RepeatAction = (WorkspaceRepeatAction)Math.Clamp(_repeatAction.SelectedIndex, 0, 3), Items = _workspaceItems.Select(Clone).ToList() };
        DialogResult = DialogResult.OK; Close();
    }

    private sealed class WorkspaceCanvas : Control
    {
        private static readonly Color[] TileColors = [Color.FromArgb(36, 77, 122), Color.FromArgb(57, 72, 111), Color.FromArgb(43, 91, 87), Color.FromArgb(104, 72, 94), Color.FromArgb(96, 81, 54)];
        private readonly IReadOnlyList<WorkspaceItem> _items;
        private readonly Func<WorkspaceItem, LauncherEntry?> _resolveEntry;
        private WorkspaceItem? _selected;
        private WorkspaceItem? _dragItem;
        private Point _dragStartMouse;
        private Rectangle _dragStartBounds;
        private ResizeEdges _resizeEdges;
        private Rectangle _desktop;
        public event Action? SelectionChanged;
        public WorkspaceItem? SelectedItem => _selected;

        public WorkspaceCanvas(IReadOnlyList<WorkspaceItem> items, Func<WorkspaceItem, LauncherEntry?> resolveEntry)
        {
            _items = items; _resolveEntry = resolveEntry;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Canvas; Cursor = Cursors.Default; TabStop = true;
        }

        public void SelectItem(WorkspaceItem item) { _selected = item; SelectionChanged?.Invoke(); Invalidate(); }
        public void ClearSelection() { _selected = null; SelectionChanged?.Invoke(); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var graphics = e.Graphics; graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var frame = new SolidBrush(Color.FromArgb(22, 31, 47)); graphics.FillRectangle(frame, ClientRectangle);
            var display = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            var aspect = display.Width / (double)display.Height;
            var maxWidth = Math.Max(100, ClientSize.Width - 62); var maxHeight = Math.Max(100, ClientSize.Height - 78);
            var width = Math.Min(maxWidth, (int)(maxHeight * aspect)); var height = (int)(width / aspect);
            if (height > maxHeight) { height = maxHeight; width = (int)(height * aspect); }
            _desktop = new Rectangle((ClientSize.Width - width) / 2, (ClientSize.Height - height) / 2 - 2, width, height);
            using (var shadow = new SolidBrush(Color.FromArgb(12, 15, 22))) graphics.FillRectangle(shadow, _desktop.X - 7, _desktop.Y - 7, _desktop.Width + 14, _desktop.Height + 20);
            using (var screen = new LinearGradientBrush(_desktop, Color.FromArgb(14, 24, 38), Color.FromArgb(20, 34, 51), LinearGradientMode.ForwardDiagonal)) graphics.FillRectangle(screen, _desktop);
            using (var border = new Pen(Color.FromArgb(71, 91, 117), 2)) graphics.DrawRectangle(border, _desktop);
            using (var stand = new Pen(Color.FromArgb(46, 59, 77), 5)) { var cx = _desktop.Left + _desktop.Width / 2; graphics.DrawLine(stand, cx, _desktop.Bottom + 2, cx, _desktop.Bottom + 12); graphics.DrawLine(stand, cx - 31, _desktop.Bottom + 13, cx + 31, _desktop.Bottom + 13); }
            using var gridPen = new Pen(Color.FromArgb(18, 117, 147, 180), 1);
            for (var i = 1; i < 10; i++) graphics.DrawLine(gridPen, _desktop.Left + _desktop.Width * i / 10, _desktop.Top, _desktop.Left + _desktop.Width * i / 10, _desktop.Bottom);
            for (var i = 1; i < 10; i++) graphics.DrawLine(gridPen, _desktop.Left, _desktop.Top + _desktop.Height * i / 10, _desktop.Right, _desktop.Top + _desktop.Height * i / 10);

            for (var i = 0; i < _items.Count; i++) DrawItem(graphics, _items[i], i);
            // Draw handles after all cards so every window can be resized directly,
            // without selecting it first.
            foreach (var item in _items) DrawResizeHandles(graphics, item);
            if (_items.Count == 0)
            {
                using var hint = new SolidBrush(Color.FromArgb(160, 190, 205, 220)); using var font = new Font("Segoe UI", 11F);
                const string text = "Add an app, then drag it into place"; var size = graphics.MeasureString(text, font);
                graphics.DrawString(text, font, hint, _desktop.Left + (_desktop.Width - size.Width) / 2, _desktop.Top + (_desktop.Height - size.Height) / 2);
            }
        }

        private void DrawItem(Graphics graphics, WorkspaceItem item, int index)
        {
            var rect = ItemBounds(item); var selected = ReferenceEquals(item, _selected);
            var color = selected ? Color.FromArgb(53, 119, 190) : TileColors[index % TileColors.Length];
            using (var fill = new SolidBrush(color)) FillRounded(graphics, fill, rect, 7);
            using (var outline = new Pen(selected ? Color.FromArgb(147, 203, 255) : Color.FromArgb(118, 164, 210), selected ? 2.5f : 1.2f)) DrawRounded(graphics, outline, rect, 7);

            var entry = _resolveEntry(item);
            var name = entry?.Name ?? "Missing app";
            var inset = Math.Min(12, Math.Max(5, rect.Width / 18));
            var iconSize = Math.Clamp(rect.Height / 5, 24, 38);
            var iconRect = new Rectangle(rect.X + inset, rect.Y + inset, iconSize, iconSize);
            using (var icon = new SolidBrush(Color.FromArgb(45, 197, 225, 255))) FillRounded(graphics, icon, iconRect, 8);
            using (var initialFont = new Font("Segoe UI Semibold", Math.Clamp(iconSize * .42f, 10, 15))) using (var white = new SolidBrush(Color.White))
                TextRenderer.DrawText(graphics, name.Length == 0 ? "?" : name[..1].ToUpperInvariant(), initialFont, iconRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            var textX = iconRect.Right + Math.Min(10, inset); var textWidth = Math.Max(20, rect.Right - inset - textX - 3);
            var titleRect = new Rectangle(textX, rect.Y + inset + 1, textWidth, Math.Max(22, iconSize / 2 + 4));
            var titleFont = new Font("Segoe UI Semibold", Math.Clamp(rect.Height / 12f, 9, 13));
            TextRenderer.DrawText(graphics, name, titleFont, titleRect, Color.White, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            titleFont.Dispose();
            if (rect.Height > iconSize + inset * 2 + 12)
            {
                var kind = entry is null ? "Missing app" : LauncherTargetTypeLabels.Get(entry.TargetType);
                var detailRect = new Rectangle(textX, titleRect.Bottom, textWidth, 19);
                TextRenderer.DrawText(graphics, kind, Font, detailRect, Color.FromArgb(197, 220, 244), TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
            var sizeText = $"{item.Width}% × {item.Height}%";
            TextRenderer.DrawText(graphics, sizeText, Font, new Rectangle(rect.Left + inset, rect.Bottom - 24, rect.Width - inset - 13, 18), Color.FromArgb(205, 225, 246), TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        private void DrawResizeHandles(Graphics graphics, WorkspaceItem item)
        {
            var rect = ItemBounds(item);
            var midX = rect.Left + rect.Width / 2; var midY = rect.Top + rect.Height / 2;
            var points = new[]
            {
                new Point(rect.Left, rect.Top), new Point(midX, rect.Top), new Point(rect.Right, rect.Top),
                new Point(rect.Right, midY), new Point(rect.Right, rect.Bottom), new Point(midX, rect.Bottom),
                new Point(rect.Left, rect.Bottom), new Point(rect.Left, midY)
            };
            const int handleSize = 9;
            using var outline = new Pen(Color.FromArgb(35, 73, 124), 1.25f);
            using var fill = new SolidBrush(Color.White);
            foreach (var point in points)
            {
                var handle = new Rectangle(point.X - handleSize / 2, point.Y - handleSize / 2, handleSize, handleSize);
                graphics.FillEllipse(fill, handle);
                graphics.DrawEllipse(outline, handle);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); Focus();
            if (e.Button != MouseButtons.Left) return;
            var item = _items.Reverse().FirstOrDefault(candidate => ItemBounds(candidate).Contains(e.Location));
            _selected = item; _dragItem = item; _resizeEdges = item is null ? ResizeEdges.None : HitResizeEdges(ItemBounds(item), e.Location);
            _dragStartMouse = e.Location;
            if (item is not null) _dragStartBounds = new Rectangle(item.X, item.Y, item.Width, item.Height);
            Capture = item is not null;
            Cursor = item is null ? Cursors.Default : CursorFor(_resizeEdges);
            SelectionChanged?.Invoke(); Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragItem is null)
            {
                var hovered = _items.Reverse().FirstOrDefault(item => ItemBounds(item).Contains(e.Location));
                Cursor = hovered is null ? Cursors.Default : CursorFor(HitResizeEdges(ItemBounds(hovered), e.Location));
                return;
            }
            if (_desktop.Width <= 0 || _desktop.Height <= 0) return;
            var dx = (int)Math.Round((e.X - _dragStartMouse.X) * 100d / _desktop.Width);
            var dy = (int)Math.Round((e.Y - _dragStartMouse.Y) * 100d / _desktop.Height);
            if (_resizeEdges != ResizeEdges.None)
            {
                var left = _dragStartBounds.Left; var top = _dragStartBounds.Top;
                var right = _dragStartBounds.Right; var bottom = _dragStartBounds.Bottom;
                if (_resizeEdges.HasFlag(ResizeEdges.Left)) left = SnapResizeEdge(_dragItem, _dragStartBounds.Left + dx, right, horizontal: true);
                if (_resizeEdges.HasFlag(ResizeEdges.Right)) right = SnapResizeEdge(_dragItem, _dragStartBounds.Right + dx, left, horizontal: true);
                if (_resizeEdges.HasFlag(ResizeEdges.Top)) top = SnapResizeEdge(_dragItem, _dragStartBounds.Top + dy, bottom, horizontal: false);
                if (_resizeEdges.HasFlag(ResizeEdges.Bottom)) bottom = SnapResizeEdge(_dragItem, _dragStartBounds.Bottom + dy, top, horizontal: false);
                _dragItem.X = left; _dragItem.Y = top;
                _dragItem.Width = right - left; _dragItem.Height = bottom - top;
            }
            else
            {
                var x = Math.Clamp(_dragStartBounds.X + dx, 0, 100 - _dragStartBounds.Width);
                var y = Math.Clamp(_dragStartBounds.Y + dy, 0, 100 - _dragStartBounds.Height);
                _dragItem.X = SnapMove(_dragItem, x, horizontal: true);
                _dragItem.Y = SnapMove(_dragItem, y, horizontal: false);
            }
            SelectionChanged?.Invoke(); Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture = false; _dragItem = null; _resizeEdges = ResizeEdges.None; Cursor = Cursors.Default; }

        private int SnapMove(WorkspaceItem moving, int proposed, bool horizontal)
        {
            var size = horizontal ? moving.Width : moving.Height;
            var candidates = new List<int> { 0, 100 - size };
            foreach (var other in _items.Where(item => !ReferenceEquals(item, moving)))
            {
                var start = horizontal ? other.X : other.Y;
                var end = start + (horizontal ? other.Width : other.Height);
                candidates.Add(start);
                candidates.Add(end);
                candidates.Add(start - size);
                candidates.Add(end - size);
            }
            return SnapToNearest(proposed, candidates, 0, 100 - size);
        }

        private int SnapResizeEdge(WorkspaceItem moving, int proposedEdge, int fixedEdge, bool horizontal)
        {
            var isLowEdge = horizontal ? _resizeEdges.HasFlag(ResizeEdges.Left) : _resizeEdges.HasFlag(ResizeEdges.Top);
            var min = isLowEdge ? 0 : fixedEdge + 8;
            var max = isLowEdge ? fixedEdge - 8 : 100;
            var candidates = new List<int> { 0, 100 };
            foreach (var other in _items.Where(item => !ReferenceEquals(item, moving)))
            {
                var start = horizontal ? other.X : other.Y;
                var end = start + (horizontal ? other.Width : other.Height);
                candidates.Add(start); candidates.Add(end);
            }
            return SnapToNearest(proposedEdge, candidates, min, max);
        }

        private static int SnapToNearest(int value, IEnumerable<int> candidates, int min, int max)
        {
            const int snapDistance = 2;
            var nearest = candidates.Where(candidate => candidate >= min && candidate <= max)
                .Select(candidate => (Value: candidate, Distance: Math.Abs(candidate - value)))
                .Where(candidate => candidate.Distance <= snapDistance)
                .OrderBy(candidate => candidate.Distance)
                .ToArray();
            return nearest.Length > 0 ? nearest[0].Value : Math.Clamp(value, min, max);
        }

        private Rectangle ItemBounds(WorkspaceItem item) => new(
            _desktop.Left + _desktop.Width * item.X / 100,
            _desktop.Top + _desktop.Height * item.Y / 100,
            Math.Max(42, _desktop.Width * item.Width / 100),
            Math.Max(38, _desktop.Height * item.Height / 100));
        [Flags]
        private enum ResizeEdges { None = 0, Left = 1, Right = 2, Top = 4, Bottom = 8 }

        private static ResizeEdges HitResizeEdges(Rectangle rect, Point point)
        {
            const int edge = 9;
            var result = ResizeEdges.None;
            if (point.X < rect.Left + edge) result |= ResizeEdges.Left;
            else if (point.X >= rect.Right - edge) result |= ResizeEdges.Right;
            if (point.Y < rect.Top + edge) result |= ResizeEdges.Top;
            else if (point.Y >= rect.Bottom - edge) result |= ResizeEdges.Bottom;
            return result;
        }

        private static Cursor CursorFor(ResizeEdges edges)
        {
            if (edges == ResizeEdges.Left || edges == ResizeEdges.Right) return Cursors.SizeWE;
            if (edges == ResizeEdges.Top || edges == ResizeEdges.Bottom) return Cursors.SizeNS;
            if ((edges & (ResizeEdges.Left | ResizeEdges.Top)) == (ResizeEdges.Left | ResizeEdges.Top)
                || (edges & (ResizeEdges.Right | ResizeEdges.Bottom)) == (ResizeEdges.Right | ResizeEdges.Bottom)) return Cursors.SizeNWSE;
            if (edges != ResizeEdges.None) return Cursors.SizeNESW;
            return Cursors.SizeAll;
        }
        private static void FillRounded(Graphics graphics, Brush brush, Rectangle rect, int radius)
        {
            using var path = RoundedPath(rect, radius); graphics.FillPath(brush, path);
        }
        private static void DrawRounded(Graphics graphics, Pen pen, Rectangle rect, int radius)
        {
            using var path = RoundedPath(rect, radius); graphics.DrawPath(pen, path);
        }
        private static GraphicsPath RoundedPath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath(); var diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90); path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90); path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure(); return path;
        }
    }
}
