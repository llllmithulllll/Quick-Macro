using QuickMacro.Services;

namespace QuickMacro.UI;

public sealed class BrowserAppDialog : Form
{
    private static readonly Color Surface = Color.FromArgb(255, 255, 255);
    private static readonly Color Raised = Color.FromArgb(237, 241, 247);
    private readonly TextBox _search = new();
    private readonly ListBox _apps = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label _status = new() { AutoSize = true, ForeColor = Color.FromArgb(96, 111, 132), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private List<BrowserAppChoice> _choices = [];
    public BrowserAppChoice? SelectedApp => _apps.SelectedItem as BrowserAppChoice;

    public BrowserAppDialog()
    {
        Text = "Choose a browser-installed app"; DialogLayout.FitToWorkingArea(this, new Size(550, 540), new Size(420, 360)); StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(246, 248, 252); ForeColor = Color.FromArgb(25, 35, 52); Font = new Font("Segoe UI", 9F);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), RowCount = 4, ColumnCount = 1, BackColor = BackColor };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.Controls.Add(new Label { Text = "Web apps found in your Start Menu and Desktop", AutoSize = true, Padding = new Padding(0, 0, 0, 10), ForeColor = Color.FromArgb(25, 35, 52) }, 0, 0);
        _search.PlaceholderText = "Search web apps…"; _search.Dock = DockStyle.Fill; _search.BackColor = Raised; _search.ForeColor = Color.FromArgb(25, 35, 52); _search.BorderStyle = BorderStyle.FixedSingle;
        layout.Controls.Add(_search, 0, 1);
        _apps.BackColor = Surface; _apps.ForeColor = Color.FromArgb(25, 35, 52); _apps.BorderStyle = BorderStyle.FixedSingle;
        layout.Controls.Add(_apps, 0, 2);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = BackColor };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = BackColor };
        var choose = StyleButton(new Button { Text = "Choose", AutoSize = true }, true); choose.Click += (_, _) => SelectApp();
        var cancel = StyleButton(new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel });
        buttons.Controls.Add(choose); buttons.Controls.Add(cancel);
        footer.Controls.Add(_status, 0, 0); footer.Controls.Add(buttons, 1, 0);
        layout.Controls.Add(footer, 0, 3); Controls.Add(layout);
        AcceptButton = choose; CancelButton = cancel;
        _search.TextChanged += (_, _) => RefreshChoices();
        _apps.DoubleClick += (_, _) => SelectApp();
        Shown += (_, _) => LoadApps();
    }

    private static Button StyleButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0; button.BackColor = primary ? Color.FromArgb(48, 105, 220) : Raised;
        button.ForeColor = primary ? Color.White : Color.FromArgb(25, 35, 52); button.Font = new Font("Segoe UI Semibold", 9F); button.Padding = new Padding(9, 5, 9, 5); button.Cursor = Cursors.Hand;
        return button;
    }

    private void LoadApps()
    {
        try
        {
            _choices = BrowserAppCatalog.GetInstalledApps().ToList();
            RefreshChoices();
            if (_choices.Count == 0) _status.Text = "No browser app shortcuts found. Install the site as an app in Brave, Chrome, or Edge, then try again.";
        }
        catch (Exception ex) { _status.Text = "Could not search shortcuts: " + ex.Message; }
    }

    private void RefreshChoices()
    {
        var filter = _search.Text.Trim();
        var matches = _choices.Where(app => filter.Length == 0 || app.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase) || app.BrowserName.Contains(filter, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        _apps.BeginUpdate(); _apps.Items.Clear(); _apps.Items.AddRange(matches.Cast<object>().ToArray()); _apps.EndUpdate();
        _status.Text = matches.Length == 1 ? "1 web app" : $"{matches.Length} web apps";
        if (matches.Length > 0) _apps.SelectedIndex = 0;
    }

    private void SelectApp()
    {
        if (SelectedApp is null) return;
        DialogResult = DialogResult.OK; Close();
    }
}
