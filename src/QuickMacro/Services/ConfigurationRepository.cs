using System.Text.Json;
using QuickMacro.Models;

namespace QuickMacro.Services;

public sealed record ConfigurationLoadResult(AppConfiguration Configuration, string? Warning);

public class ConfigurationRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public virtual string DirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickMacro");
    public virtual string FilePath => Path.Combine(DirectoryPath, "config.json");

    public ConfigurationLoadResult Load()
    {
        if (!File.Exists(FilePath)) return new(new AppConfiguration(), null);
        try
        {
            var config = JsonSerializer.Deserialize<AppConfiguration>(File.ReadAllText(FilePath), JsonOptions) ?? new AppConfiguration();
            config.Entries ??= [];
            config.Workspaces ??= [];
            config.Settings ??= new AppSettings();
            config.SchemaVersion = 1;
            config.Entries = config.Entries.Where(e => e is not null && e.Id != Guid.Empty && !string.IsNullOrWhiteSpace(e.Name) && !string.IsNullOrWhiteSpace(e.TargetPath)).ToList();
            foreach (var entry in config.Entries)
            {
                if (!Enum.IsDefined(entry.TargetType)) entry.TargetType = LauncherTargetType.FilePath;
                if (!Enum.IsDefined(entry.ShortcutAction)) entry.ShortcutAction = ShortcutAction.Launch;
            }
            config.Workspaces = config.Workspaces.Where(workspace => workspace is not null && workspace.Id != Guid.Empty && !string.IsNullOrWhiteSpace(workspace.Name) && workspace.Shortcut is not null && ShortcutRules.Validate(workspace.Shortcut) is null && workspace.Items is not null && workspace.Items.Count > 0).ToList();
            foreach (var workspace in config.Workspaces) workspace.Items = workspace.Items.Where(item => IsValidWorkspaceItem(item, config.Entries)).ToList();
            config.Workspaces.RemoveAll(workspace => workspace.Items.Count == 0);
            return new(config, null);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            try { File.Copy(FilePath, FilePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); } catch { }
            return new(new AppConfiguration(), "The configuration file could not be read. A fresh configuration was loaded; the original file was preserved when possible. " + ex.Message);
        }
    }

    public void Save(AppConfiguration configuration)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(configuration, JsonOptions));
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true);
        else File.Move(temp, FilePath);
    }

    public AppConfiguration? Import(string path)
    {
        var imported = JsonSerializer.Deserialize<AppConfiguration>(File.ReadAllText(path), JsonOptions);
        if (imported is null || imported.Entries is null || imported.Settings is null) throw new InvalidDataException("The selected file is not a valid QuickMacro configuration.");
        imported.Workspaces ??= [];
        if (imported.Entries.Any(e => e is null || ShortcutRules.ValidateEntry(e) is not null)) throw new InvalidDataException("One or more entries are invalid.");
        if (ShortcutRules.FindDuplicateAssignments(imported.Entries).Count > 0) throw new InvalidDataException("Two or more enabled entries use the same shortcut.");
        if (imported.Workspaces.Any(workspace => ValidateWorkspace(workspace, imported.Entries) is not null)) throw new InvalidDataException("One or more workspaces are invalid.");
        var allIds = imported.Entries.Select(entry => entry.Id).Concat(imported.Workspaces.Select(workspace => workspace.Id))
            .Concat(imported.Workspaces.SelectMany(workspace => workspace.Items).Where(item => item.Target is not null).Select(item => item.Target!.Id));
        if (allIds.Distinct().Count() != imported.Entries.Count + imported.Workspaces.Count + imported.Workspaces.Sum(workspace => workspace.Items.Count(item => item.Target is not null))) throw new InvalidDataException("Each app and workspace target must have a unique ID.");
        if (imported.Workspaces.Where(workspace => workspace.Enabled && workspace.Shortcut is not null).GroupBy(workspace => workspace.Shortcut!).Any(group => group.Count() > 1)
            || imported.Workspaces.Any(workspace => workspace.Enabled && imported.Entries.Any(entry => entry.Enabled && entry.Shortcut?.Equals(workspace.Shortcut) == true))) throw new InvalidDataException("Two or more enabled apps or workspaces use the same shortcut.");
        imported.SchemaVersion = 1;
        return imported;
    }

    public void Export(string path, AppConfiguration configuration) => File.WriteAllText(path, JsonSerializer.Serialize(configuration, JsonOptions));

    private static string? ValidateWorkspace(Workspace? workspace, IReadOnlyList<LauncherEntry> entries)
    {
        if (workspace is null || workspace.Id == Guid.Empty || string.IsNullOrWhiteSpace(workspace.Name) || workspace.Shortcut is null || ShortcutRules.Validate(workspace.Shortcut) is not null || workspace.Items is null || workspace.Items.Count == 0) return "Invalid workspace fields.";
        if (workspace.Items.Any(item => !IsValidWorkspaceItem(item, entries))) return "Invalid workspace app or position.";
        var ids = workspace.Items.Select(item => item.EntryId != Guid.Empty ? item.EntryId : item.Target!.Id);
        return ids.Distinct().Count() == workspace.Items.Count ? null : "An app appears more than once.";
    }

    private static bool IsValidWorkspaceItem(WorkspaceItem? item, IReadOnlyList<LauncherEntry> entries)
    {
        if (item is null || item.X is < 0 or >= 100 || item.Y is < 0 or >= 100 || item.Width is <= 0 or > 100 || item.Height is <= 0 or > 100) return false;
        if (item.EntryId != Guid.Empty)
            return item.Target is null && entries.Any(entry => entry.Id == item.EntryId && entry.TargetType != LauncherTargetType.WebsiteUrl);
        return item.Target is { Id: var id, Shortcut: null } target && id != Guid.Empty && target.TargetType != LauncherTargetType.WebsiteUrl && ShortcutRules.ValidateEntry(target) is null;
    }
}
