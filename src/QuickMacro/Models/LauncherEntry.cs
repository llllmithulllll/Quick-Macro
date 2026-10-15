namespace QuickMacro.Models;

// Keep the first two numeric values stable for existing JSON configuration files.
public enum LauncherTargetType { FilePath, StoreApp, WebsiteUrl, BrowserAppShortcut }

public static class LauncherTargetTypeLabels
{
    public static string Get(LauncherTargetType type) => type switch
    {
        LauncherTargetType.FilePath => "Desktop app",
        LauncherTargetType.StoreApp => "Microsoft Store app",
        LauncherTargetType.WebsiteUrl => "Website",
        LauncherTargetType.BrowserAppShortcut => "Browser-installed app",
        _ => "App"
    };
}
public enum ShortcutAction { Launch, Minimize, Close }

public sealed class LauncherEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public LauncherTargetType TargetType { get; set; } = LauncherTargetType.FilePath;
    public ShortcutAction ShortcutAction { get; set; } = ShortcutAction.Launch;
    public bool MinimizeToSystemTray { get; set; }
    public ShortcutBinding? Shortcut { get; set; }
    public bool Enabled { get; set; } = true;
}

public sealed class ShortcutBinding : IEquatable<ShortcutBinding>
{
    public bool Control { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Windows { get; set; }
    public Keys Key { get; set; }

    public bool Equals(ShortcutBinding? other) => other is not null && Control == other.Control && Alt == other.Alt && Shift == other.Shift && Windows == other.Windows && Key == other.Key;
    public override bool Equals(object? obj) => Equals(obj as ShortcutBinding);
    public override int GetHashCode() => HashCode.Combine(Control, Alt, Shift, Windows, Key);
    public override string ToString() => string.Join(" + ", new[] { Control ? "Ctrl" : null, Alt ? "Alt" : null, Shift ? "Shift" : null, Windows ? "Win" : null, Key.ToString() }.Where(x => x is not null));
}

public sealed class AppConfiguration
{
    public int SchemaVersion { get; set; } = 1;
    public List<LauncherEntry> Entries { get; set; } = [];
    public List<Workspace> Workspaces { get; set; } = [];
    public AppSettings Settings { get; set; } = new();
}

public sealed class Workspace
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool OpenOnNewDesktop { get; set; }
    public WorkspaceRepeatAction RepeatAction { get; set; } = WorkspaceRepeatAction.Launch;
    public ShortcutBinding? Shortcut { get; set; }
    public List<WorkspaceItem> Items { get; set; } = [];
}

public enum WorkspaceRepeatAction { Launch, MinimizeRestore, CloseApps, CloseAppsAndDesktop }

// Coordinates are percentages of the primary display's working area (0–100).
public sealed class WorkspaceItem
{
    // Empty means this app belongs only to the workspace; otherwise it refers to an app entry.
    public Guid EntryId { get; set; }
    public LauncherEntry? Target { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 50;
    public int Height { get; set; } = 100;
}

public sealed class AppSettings
{
    public string ThemeMode { get; set; } = "System";
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool EnableAllShortcuts { get; set; } = true;
    public bool ShowLaunchNotifications { get; set; }
    public bool PreventDuplicateLaunches { get; set; }
}
