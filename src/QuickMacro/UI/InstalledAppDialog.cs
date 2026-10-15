using QuickMacro.Models;
using QuickMacro.Services;

namespace QuickMacro.UI;

public sealed class InstalledAppDialog : Form
{
    private static readonly Color Canvas = Color.FromArgb(246, 248, 252);
    private static readonly Color Surface = Color.FromArgb(255, 255, 255);
    private static readonly Color Raised = Color.FromArgb(237, 241, 247);
    private readonly TextBox _search = new();
    private readonly ListBox _apps = new() { Dock = DockStyle.Fill, IntegralHeight = false, DisplayMember = nameof(InstalledAppChoice.DisplayName), BackColor = Surface, ForeColor = Color.FromArgb(25, 35, 52), BorderStyle = BorderStyle.None, ItemHeight = 30 };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.FromArgb(96, 111, 132), Anchor = AnchorStyles.Left };
    private IReadOnlyList<InstalledAppChoice> _choices = [];
    public LauncherEntry? SelectedEntry { get; private set; }

    public InstalledAppDialog()
    {
        Text = "Add installed app"; DialogLayout.FitToWorkingArea(this, new Size(600, 620), new Size(460, 420)); StartPosition = FormStartPosition.CenterParent; BackColor = Canvas; ForeColor = Color.FromArgb(25, 35, 52); Font = new Font("Segoe UI", 9F);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4, BackColor = Canvas };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.Controls.Add(new Label { Text = "Choose an app to add to this workspace", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI Semibold", 10F) }, 0, 0);
        _search.PlaceholderText = "Search installed apps…"; _search.Dock = DockStyle.Fill; _search.BackColor = Raised; _search.ForeColor = Color.FromArgb(25, 35, 52); _search.BorderStyle = BorderStyle.FixedSingle; layout.Controls.Add(_search, 0, 1);
        layout.Controls.Add(_apps, 0, 2);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Canvas };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var choose = Button("Add to workspace", true); choose.Click += (_, _) => SelectApp();
        var cancel = Button("Cancel"); cancel.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(choose); buttons.Controls.Add(cancel); footer.Controls.Add(_status, 0, 0); footer.Controls.Add(buttons, 1, 0); layout.Controls.Add(footer, 0, 3);
        Controls.Add(layout); AcceptButton = choose; CancelButton = cancel;
        _search.TextChanged += (_, _) => RefreshChoices(); _apps.DoubleClick += (_, _) => SelectApp(); Shown += (_, _) => LoadApps();
    }

    private static Button Button(string text, bool primary = false)
    {
        var button = new Button { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = primary ? Color.FromArgb(48, 105, 220) : Raised, ForeColor = Color.FromArgb(25, 35, 52), Padding = new Padding(9, 5, 9, 5), Margin = new Padding(4), Cursor = Cursors.Hand };
        button.FlatAppearance.BorderSize = 0; button.ForeColor = primary ? Color.White : Color.FromArgb(25, 35, 52); return button;
    }

    private void LoadApps()
    {
        try { _choices = InstalledAppCatalog.GetInstalledApps(); RefreshChoices(); }
        catch (Exception ex) { _status.Text = "Could not list installed apps: " + ex.Message; }
    }
    private void RefreshChoices()
    {
        var filter = _search.Text.Trim();
        var matches = _choices.Where(choice => filter.Length == 0 || choice.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase) || choice.TargetPath.Contains(filter, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        _apps.BeginUpdate(); _apps.Items.Clear(); _apps.Items.AddRange(matches.Cast<object>().ToArray()); _apps.EndUpdate();
        _status.Text = matches.Length == 0 ? "No matches. Try Browse for app in the workspace editor." : matches.Length == 1 ? "1 matching app" : $"{matches.Length} matching apps · type shown beside each name";
        if (matches.Length > 0) _apps.SelectedIndex = 0;
    }
    private void SelectApp()
    {
        if (_apps.SelectedItem is not InstalledAppChoice choice) return;
        SelectedEntry = new LauncherEntry { Name = choice.Name, TargetPath = choice.TargetPath, TargetType = choice.TargetType, Arguments = choice.Arguments, WorkingDirectory = choice.WorkingDirectory, ShortcutAction = ShortcutAction.Launch, Enabled = true };
        DialogResult = DialogResult.OK; Close();
    }
}
