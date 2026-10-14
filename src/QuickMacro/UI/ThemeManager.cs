using Microsoft.Win32;

namespace QuickMacro.UI;

internal static class ThemeManager
{
    public static string CurrentMode { get; set; } = "System";
    public static string[] Options { get; } = ["System", "Light", "Dark"];

    public static void Apply(Control root) => Apply(root, CurrentMode);

    public static void Apply(Control root, string mode)
    {
        var dark = mode.Equals("Dark", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("System", StringComparison.OrdinalIgnoreCase) && !WindowsUsesLightTheme();
        ApplyToTree(root, dark);
    }

    private static bool WindowsUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return Convert.ToInt32(key?.GetValue("AppsUseLightTheme") ?? 1) != 0;
        }
        catch { return true; }
    }

    private static void ApplyToTree(Control control, bool dark)
    {
        control.BackColor = ConvertColor(control.BackColor, dark);
        control.ForeColor = ConvertColor(control.ForeColor, dark);
        if (control is Button button)
        {
            button.FlatAppearance.MouseOverBackColor = ConvertColor(button.FlatAppearance.MouseOverBackColor, dark);
            button.FlatAppearance.BorderColor = Color.FromArgb(57, 70, 91);
        }
        foreach (Control child in control.Controls) ApplyToTree(child, dark);
    }

    private static Color ConvertColor(Color color, bool dark)
    {
        if (!color.IsEmpty && color.A == 255)
        {
            var rgb = Color.FromArgb(color.R, color.G, color.B);
            if (dark)
            {
                if (rgb == Color.FromArgb(246, 248, 252)) return Color.FromArgb(16, 21, 32);
                if (rgb == Color.White) return Color.FromArgb(25, 33, 48);
                if (rgb == Color.FromArgb(237, 241, 247)) return Color.FromArgb(36, 46, 63);
                if (rgb == Color.FromArgb(25, 35, 52)) return Color.FromArgb(235, 240, 248);
                if (rgb == Color.FromArgb(75, 89, 109)) return Color.FromArgb(194, 204, 219);
                if (rgb == Color.FromArgb(96, 111, 132)) return Color.FromArgb(155, 169, 191);
                if (rgb == Color.FromArgb(224, 236, 255)) return Color.FromArgb(42, 64, 94);
                if (rgb == Color.FromArgb(222, 229, 239)) return Color.FromArgb(52, 64, 84);
                if (rgb == Color.FromArgb(232, 240, 253)) return Color.FromArgb(37, 54, 80);
            }
            else
            {
                if (rgb == Color.FromArgb(16, 21, 32)) return Color.FromArgb(246, 248, 252);
                if (rgb == Color.FromArgb(25, 33, 48)) return Color.White;
                if (rgb == Color.FromArgb(36, 46, 63)) return Color.FromArgb(237, 241, 247);
                if (rgb == Color.FromArgb(235, 240, 248)) return Color.FromArgb(25, 35, 52);
                if (rgb == Color.FromArgb(194, 204, 219)) return Color.FromArgb(75, 89, 109);
                if (rgb == Color.FromArgb(155, 169, 191)) return Color.FromArgb(96, 111, 132);
                if (rgb == Color.FromArgb(42, 64, 94)) return Color.FromArgb(224, 236, 255);
                if (rgb == Color.FromArgb(52, 64, 84)) return Color.FromArgb(222, 229, 239);
                if (rgb == Color.FromArgb(37, 54, 80)) return Color.FromArgb(232, 240, 253);
            }
        }
        return color;
    }
}
