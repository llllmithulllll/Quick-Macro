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
            config.Settings ??= new AppSettings();
            config.SchemaVersion = 1;
            config.Entries = config.Entries.Where(e => e is not null && e.Id != Guid.Empty && !string.IsNullOrWhiteSpace(e.Name) && !string.IsNullOrWhiteSpace(e.TargetPath)).ToList();
            foreach (var entry in config.Entries)
            {
                if (!Enum.IsDefined(entry.TargetType)) entry.TargetType = LauncherTargetType.FilePath;
                if (!Enum.IsDefined(entry.ShortcutAction)) entry.ShortcutAction = ShortcutAction.Launch;
            }
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
        if (imported.Entries.Any(e => e is null || ShortcutRules.ValidateEntry(e) is not null)) throw new InvalidDataException("One or more entries are invalid.");
        if (ShortcutRules.FindDuplicateAssignments(imported.Entries).Count > 0) throw new InvalidDataException("Two or more enabled entries use the same shortcut.");
        imported.SchemaVersion = 1;
        return imported;
    }

    public void Export(string path, AppConfiguration configuration) => File.WriteAllText(path, JsonSerializer.Serialize(configuration, JsonOptions));
}
