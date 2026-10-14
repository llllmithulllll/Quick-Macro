using QuickMacro.Models;

namespace QuickMacro.UI;

public sealed class WorkspaceManagerDialog : Form
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false, BackColor = Color.FromArgb(255, 255, 255), ForeColor = Color.FromArgb(25, 35, 52), BorderStyle = BorderStyle.None, DisplayMember = nameof(Workspace.Name) };
    private readonly List<Workspace> _workspaces;
    private readonly IReadOnlyList<LauncherEntry> _entries;
    public IReadOnlyList<Workspace> Workspaces => _workspaces;
    public event Action<IReadOnlyList<Workspace>>? WorkspacesChanged;

    public WorkspaceManagerDialog(IEnumerable<Workspace> workspaces, IReadOnlyList<LauncherEntry> entries)
    {
        _workspaces = workspaces.ToList(); _entries = entries;
        Text = "Workspaces"; DialogLayout.FitToWorkingArea(this, new Size(610, 440), new Size(520, 360)); StartPosition = FormStartPosition.CenterParent; BackColor = Color.FromArgb(246, 248, 252); ForeColor = Color.FromArgb(25, 35, 52); Font = new Font("Segoe UI", 9F);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 3, BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.Controls.Add(new Label { Text = "Create layouts for the apps you use together.", AutoSize = true, Font = new Font("Segoe UI Semibold", 12F), Anchor = AnchorStyles.Left }, 0, 0);
        root.Controls.Add(_list, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.Add(Button("New workspace", (_, _) => Edit(null), true)); buttons.Controls.Add(Button("Edit", (_, _) => Edit(_list.SelectedItem as Workspace))); buttons.Controls.Add(Button("Delete", (_, _) => Delete()));
        var done = Button("Done", (_, _) => { DialogResult = DialogResult.OK; Close(); }); done.Anchor = AnchorStyles.Right; buttons.Controls.Add(done);
        root.Controls.Add(buttons, 0, 2); Controls.Add(root); RefreshList();
        _list.DoubleClick += (_, _) => Edit(_list.SelectedItem as Workspace);
    }

    private static Button Button(string text, EventHandler click, bool primary = false)
    {
        var button = new Button { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = primary ? Color.FromArgb(48, 105, 220) : Color.FromArgb(237, 241, 247), ForeColor = Color.FromArgb(25, 35, 52), Padding = new Padding(9, 4, 9, 4), Margin = new Padding(4) };
        button.FlatAppearance.BorderSize = 0; button.ForeColor = primary ? Color.White : Color.FromArgb(25, 35, 52); button.Click += click; return button;
    }
    private void RefreshList() { _list.Items.Clear(); foreach (var workspace in _workspaces) _list.Items.Add(workspace); }
    private void Edit(Workspace? selected)
    {
        using var dialog = new WorkspaceDialog(selected, _entries);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Workspace is null) return;
        var edited = dialog.Workspace;
        if (edited.Enabled && edited.Shortcut is not null && _entries.Any(entry => entry.Enabled && entry.Shortcut?.Equals(edited.Shortcut) == true)) { MessageBox.Show(this, "An enabled app already uses this shortcut.", "Duplicate shortcut", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (edited.Enabled && _workspaces.Any(workspace => workspace.Id != edited.Id && workspace.Enabled && workspace.Shortcut?.Equals(edited.Shortcut) == true)) { MessageBox.Show(this, "Another enabled workspace already uses this shortcut.", "Duplicate shortcut", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        var index = _workspaces.FindIndex(workspace => workspace.Id == edited.Id);
        if (index >= 0) _workspaces[index] = edited; else _workspaces.Add(edited);
        RefreshList(); _list.SelectedItem = edited; WorkspacesChanged?.Invoke(_workspaces);
    }
    private void Delete()
    {
        if (_list.SelectedItem is not Workspace selected) return;
        if (MessageBox.Show(this, $"Delete workspace ‘{selected.Name}’?", "Delete workspace", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) { _workspaces.Remove(selected); RefreshList(); WorkspacesChanged?.Invoke(_workspaces); }
    }
}
