using QuickMacro.Models;

namespace QuickMacro.Services;

public static class ShortcutRules
{
    public static string? Validate(ShortcutBinding? binding)
    {
        if (binding is null) return null;
        if (binding.Key is Keys.None or Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
            return "Choose a regular letter, number, or function key.";
        if (!(binding.Control || binding.Alt || binding.Shift || binding.Windows))
            return "Add Ctrl, Alt, Shift, or the Windows key to make the shortcut distinctive.";
        if (binding.Key == Keys.Delete && binding.Control && binding.Alt)
            return "Ctrl + Alt + Delete is reserved by Windows.";
        if ((binding.Key is Keys.Tab or Keys.Escape) && (binding.Alt || binding.Windows))
            return "That key combination is reserved by Windows.";
        return null;
    }

    public static IReadOnlyList<string> FindDuplicateAssignments(IEnumerable<LauncherEntry> entries)
    {
        return entries.Where(e => e.Enabled && e.Shortcut is not null)
            .GroupBy(e => e.Shortcut!).Where(g => g.Count() > 1)
            .Select(g => string.Join(", ", g.Select(e => e.Name))).ToArray();
    }

    public static string? ValidateEntry(LauncherEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Name)) return "Enter an application name.";
        if (string.IsNullOrWhiteSpace(entry.TargetPath)) return "Choose a target for this shortcut.";
        if (!Enum.IsDefined(entry.TargetType)) return "The selected application type is invalid.";
        if (!Enum.IsDefined(entry.ShortcutAction)) return "Choose a valid action for this shortcut.";
        if (entry.TargetType == LauncherTargetType.StoreApp && !entry.TargetPath.Contains('!')) return "Choose an installed app with a valid Windows application ID.";
        if (entry.TargetType == LauncherTargetType.WebsiteUrl && (!Uri.TryCreate(entry.TargetPath, UriKind.Absolute, out var uri) || !(string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))) return "Enter a website address that starts with http:// or https://.";
        if (entry.TargetType == LauncherTargetType.WebsiteUrl && entry.ShortcutAction != ShortcutAction.Launch) return "Website links can only use the launch action.";
        if (entry.TargetType == LauncherTargetType.BrowserAppShortcut && !string.Equals(Path.GetExtension(entry.TargetPath), ".lnk", StringComparison.OrdinalIgnoreCase)) return "Choose a browser-installed app shortcut (.lnk file).";
        if (entry.Shortcut is not null) return Validate(entry.Shortcut);
        return null;
    }
}
