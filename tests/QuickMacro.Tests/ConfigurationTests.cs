using QuickMacro.Models;
using QuickMacro.Services;

namespace QuickMacro.Tests;

public class ConfigurationTests
{
    [Fact]
    public void Configuration_round_trips_through_json()
    {
        var config = new AppConfiguration { Entries = [new LauncherEntry { Name = "Notepad", TargetPath = @"C:\Windows\notepad.exe", ShortcutAction = ShortcutAction.Close, MinimizeToSystemTray = true, Shortcut = new ShortcutBinding { Control = true, Alt = true, Key = Keys.N } }] };
        var json = System.Text.Json.JsonSerializer.Serialize(config);
        var copy = System.Text.Json.JsonSerializer.Deserialize<AppConfiguration>(json)!;
        Assert.Equal("Notepad", copy.Entries.Single().Name);
        Assert.Equal(config.Entries.Single().Shortcut, copy.Entries.Single().Shortcut);
        Assert.Equal(ShortcutAction.Close, copy.Entries.Single().ShortcutAction);
        Assert.True(copy.Entries.Single().MinimizeToSystemTray);
    }

    [Fact]
    public void Workspaces_round_trip_with_shortcut_and_window_layout()
    {
        var workspace = new Workspace
        {
            Name = "Writing", Shortcut = new ShortcutBinding { Control = true, Alt = true, Key = Keys.W },
            Items = [new WorkspaceItem { EntryId = Guid.NewGuid(), X = 0, Y = 0, Width = 60, Height = 100 }]
        };
        var config = new AppConfiguration { Workspaces = [workspace] };
        var copy = System.Text.Json.JsonSerializer.Deserialize<AppConfiguration>(System.Text.Json.JsonSerializer.Serialize(config))!;
        Assert.Equal(workspace.Name, copy.Workspaces.Single().Name);
        Assert.Equal(workspace.Shortcut, copy.Workspaces.Single().Shortcut);
        Assert.Equal((0, 0, 60, 100), (copy.Workspaces.Single().Items.Single().X, copy.Workspaces.Single().Items.Single().Y, copy.Workspaces.Single().Items.Single().Width, copy.Workspaces.Single().Items.Single().Height));
    }

    [Fact]
    public void Workspace_can_own_an_app_without_a_global_hotkey_entry()
    {
        var workspace = new Workspace
        {
            Name = "Workspace-only app", Shortcut = new ShortcutBinding { Control = true, Alt = true, Key = Keys.B },
            Items = [new WorkspaceItem { Target = new LauncherEntry { Name = "Standalone", TargetPath = @"C:\Tools\standalone.exe" }, X = 0, Y = 0, Width = 100, Height = 100 }]
        };
        var path = Path.Combine(Path.GetTempPath(), "QuickMacro.Tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path);
        var backup = Path.Combine(path, "workspace.json");
        try
        {
            File.WriteAllText(backup, System.Text.Json.JsonSerializer.Serialize(new AppConfiguration { Workspaces = [workspace] }));
            var imported = new ConfigurationRepository().Import(backup)!;
            Assert.Empty(imported.Entries);
            Assert.Equal("Standalone", imported.Workspaces.Single().Items.Single().Target!.Name);
        }
        finally { Directory.Delete(path, true); }
    }

    [Fact]
    public void Legacy_configuration_is_migrated_to_current_schema()
    {
        var dir = Path.Combine(Path.GetTempPath(), "QuickMacro.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "config.json"), "{\"schemaVersion\":0,\"entries\":[],\"settings\":{}}");
            var loaded = new TestRepository(dir).Load();
            Assert.Equal(1, loaded.Configuration.SchemaVersion);
            Assert.Null(loaded.Warning);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Startup_and_launch_settings_round_trip()
    {
        var settings = new AppSettings { StartWithWindows = true, StartMinimized = true, MinimizeToTray = false, EnableAllShortcuts = false, ShowLaunchNotifications = true, PreventDuplicateLaunches = true };
        var copy = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(settings))!;
        Assert.True(copy.StartWithWindows); Assert.True(copy.StartMinimized); Assert.False(copy.MinimizeToTray);
        Assert.False(copy.EnableAllShortcuts); Assert.True(copy.ShowLaunchNotifications); Assert.True(copy.PreventDuplicateLaunches);
    }

    [Fact]
    public void Import_rejects_duplicate_hotkeys()
    {
        var dir = Path.Combine(Path.GetTempPath(), "QuickMacro.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "backup.json");
        try
        {
            var shortcut = new ShortcutBinding { Control = true, Alt = true, Key = Keys.K };
            File.WriteAllText(file, System.Text.Json.JsonSerializer.Serialize(new AppConfiguration { Entries = [new LauncherEntry { Name = "A", TargetPath = "a.exe", Shortcut = shortcut }, new LauncherEntry { Name = "B", TargetPath = "b.exe", Shortcut = shortcut }] }));
            Assert.Throws<InvalidDataException>(() => new ConfigurationRepository().Import(file));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Import_rejects_shortcut_shared_by_app_and_workspace()
    {
        var dir = Path.Combine(Path.GetTempPath(), "QuickMacro.Tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "backup.json");
        try
        {
            var binding = new ShortcutBinding { Control = true, Alt = true, Key = Keys.P };
            var entry = new LauncherEntry { Name = "Editor", TargetPath = @"C:\Apps\editor.exe", Shortcut = binding };
            var workspace = new Workspace { Name = "Work", Shortcut = new ShortcutBinding { Control = true, Alt = true, Key = Keys.P }, Items = [new WorkspaceItem { EntryId = entry.Id }] };
            File.WriteAllText(file, System.Text.Json.JsonSerializer.Serialize(new AppConfiguration { Entries = [entry], Workspaces = [workspace] }));
            Assert.Throws<InvalidDataException>(() => new ConfigurationRepository().Import(file));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData(Keys.None, false, false, false, false, false)]
    [InlineData(Keys.F8, false, false, false, false, false)]
    [InlineData(Keys.N, true, false, false, false, true)]
    [InlineData(Keys.Delete, true, true, false, false, false)]
    public void Shortcut_validation_checks_key_and_modifiers(Keys key, bool ctrl, bool alt, bool shift, bool win, bool valid)
    {
        var result = ShortcutRules.Validate(new ShortcutBinding { Key = key, Control = ctrl, Alt = alt, Shift = shift, Windows = win });
        Assert.Equal(valid, result is null);
    }

    [Fact]
    public void Duplicate_shortcuts_are_reported_only_for_enabled_entries()
    {
        var binding = new ShortcutBinding { Control = true, Alt = true, Key = Keys.C };
        var entries = new[] { new LauncherEntry { Name = "One", Shortcut = binding }, new LauncherEntry { Name = "Two", Shortcut = new ShortcutBinding { Control = true, Alt = true, Key = Keys.C } }, new LauncherEntry { Name = "Off", Enabled = false, Shortcut = binding } };
        Assert.Single(ShortcutRules.FindDuplicateAssignments(entries));
    }

    [Fact]
    public void Entry_validation_requires_name_and_path()
    {
        Assert.NotNull(ShortcutRules.ValidateEntry(new LauncherEntry()));
        Assert.Null(ShortcutRules.ValidateEntry(new LauncherEntry { Name = "Editor", TargetPath = @"C:\Apps\editor.exe" }));
    }

    [Fact]
    public void Store_app_entry_accepts_an_application_user_model_id()
    {
        var entry = new LauncherEntry { Name = "Store app", TargetPath = "Example.Package_123abc!App", TargetType = LauncherTargetType.StoreApp };
        Assert.Null(ShortcutRules.ValidateEntry(entry));
        entry.TargetPath = "not-an-app-id";
        Assert.NotNull(ShortcutRules.ValidateEntry(entry));
    }

    [Fact]
    public void Website_entry_accepts_only_http_and_https_urls()
    {
        var entry = new LauncherEntry { Name = "Docs", TargetPath = "https://example.com", TargetType = LauncherTargetType.WebsiteUrl };
        Assert.Null(ShortcutRules.ValidateEntry(entry));
        entry.TargetPath = "javascript:alert(1)";
        Assert.NotNull(ShortcutRules.ValidateEntry(entry));
    }

    [Fact]
    public void Browser_installed_app_requires_a_link_shortcut()
    {
        var entry = new LauncherEntry { Name = "Mail", TargetPath = @"C:\Apps\Mail.lnk", TargetType = LauncherTargetType.BrowserAppShortcut };
        Assert.Null(ShortcutRules.ValidateEntry(entry));
        entry.TargetPath = @"C:\Apps\Mail.exe";
        Assert.NotNull(ShortcutRules.ValidateEntry(entry));
    }

    [Fact]
    public void Malformed_configuration_recovers_to_defaults_and_preserves_bad_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), "QuickMacro.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "config.json"); File.WriteAllText(path, "{");
            var repo = new TestRepository(dir);
            var loaded = repo.Load();
            Assert.Empty(loaded.Configuration.Entries); Assert.NotNull(loaded.Warning); Assert.Single(Directory.GetFiles(dir, "config.json.corrupt-*"));
        }
        finally { Directory.Delete(dir, true); }
    }

    private sealed class TestRepository(string directory) : ConfigurationRepository
    {
        public override string DirectoryPath { get; } = directory;
        public override string FilePath => Path.Combine(DirectoryPath, "config.json");
    }
}
