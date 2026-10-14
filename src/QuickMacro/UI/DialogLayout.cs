namespace QuickMacro.UI;

internal static class DialogLayout
{
    public static float GetScaleFactor()
    {
        try { using var graphics = Graphics.FromHwnd(IntPtr.Zero); return Math.Max(1f, graphics.DpiX / 96f); }
        catch { return 1f; }
    }

    public static void FitToWorkingArea(Form form, Size preferred, Size minimum)
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        form.AutoScaleMode = AutoScaleMode.Dpi;
        var scale = GetScaleFactor();
        var width = Math.Min(preferred.Width, Math.Max(320, (int)(area.Width / scale) - 32));
        var height = Math.Min(preferred.Height, Math.Max(300, (int)(area.Height / scale) - 48));
        form.MinimumSize = new Size(Math.Min(minimum.Width, width), Math.Min(minimum.Height, height));
        form.Size = new Size(width, height);
        form.Shown += (_, _) => ThemeManager.Apply(form);
    }
}
