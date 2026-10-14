using QuickMacro.Services;

namespace QuickMacro.UI;

public sealed class StoreAppDialog : Form
{
    private readonly ListBox _apps = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    public StoreAppChoice? SelectedApp => _apps.SelectedItem as StoreAppChoice;

    public StoreAppDialog()
    {
        Text = "Choose a Microsoft Store app"; DialogLayout.FitToWorkingArea(this, new Size(460, 500), new Size(360, 300)); StartPosition = FormStartPosition.CenterParent; BackColor = Color.FromArgb(246, 248, 252); ForeColor = Color.FromArgb(25, 35, 52);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "Installed Store apps", AutoSize = true, Padding = new Padding(0, 0, 0, 8) }, 0, 0);
        _apps.DisplayMember = nameof(StoreAppChoice.Name); layout.Controls.Add(_apps, 0, 1);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var add = new Button { Text = "Choose", AutoSize = true }; add.Click += (_, _) => SelectApp();
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        StyleButton(add, primary: true); StyleButton(cancel);
        buttons.Controls.Add(add); buttons.Controls.Add(cancel); layout.Controls.Add(buttons, 0, 2); Controls.Add(layout);
        AcceptButton = add; CancelButton = cancel;
        Shown += (_, _) => LoadApps();
        _apps.DoubleClick += (_, _) => SelectApp();
    }

    private void LoadApps()
    {
        try
        {
            var installedApps = StoreAppCatalog.GetInstalledApps();
            _apps.Items.AddRange(installedApps.Cast<object>().ToArray());
            if (_apps.Items.Count == 0)
            {
                MessageBox.Show(this, "No installed Microsoft Store apps were found. You can still add a desktop app or shortcut.", "No Store apps found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DialogResult = DialogResult.Cancel; Close(); return;
            }
            _apps.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Windows could not list installed apps. " + ex.Message, "Store app list unavailable", MessageBoxButtons.OK, MessageBoxIcon.Error);
            DialogResult = DialogResult.Cancel; Close();
        }
    }

    private void SelectApp()
    {
        if (SelectedApp is null) return;
        DialogResult = DialogResult.OK; Close();
    }

    private static void StyleButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0;
        button.BackColor = primary ? Color.FromArgb(48, 105, 220) : Color.FromArgb(237, 241, 247);
        button.ForeColor = primary ? Color.White : Color.FromArgb(25, 35, 52);
        button.Font = new Font("Segoe UI Semibold", 9F); button.Padding = new Padding(10, 5, 10, 5); button.Cursor = Cursors.Hand;
    }
}
