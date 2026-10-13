using System.Diagnostics;
using QuickMacro.Models;
using System.Runtime.InteropServices;

namespace QuickMacro.Services;

public sealed class LauncherService
{
    private readonly HashSet<string> _running = new(StringComparer.OrdinalIgnoreCase);

    public async Task LaunchAsync(LauncherEntry entry, bool preventDuplicates, bool forceLaunch = false)
    {
        await Task.Run(() =>
        {
            if (!forceLaunch && WindowActionService.TryApply(entry)) return;
            if (entry.TargetType is LauncherTargetType.FilePath or LauncherTargetType.BrowserAppShortcut && !File.Exists(entry.TargetPath)) throw new FileNotFoundException($"Could not find {entry.Name}. Check its path and edit the entry.", entry.TargetPath);
            // Don't re-enumerate the AppsFolder here. Its COM item collection can
            // change while Windows updates Store apps and may throw an index error.
            // Launch the saved AUMID directly; Windows will report if it is invalid.
            var key = entry.TargetType switch
            {
                LauncherTargetType.StoreApp => "store:" + entry.TargetPath,
                LauncherTargetType.WebsiteUrl => "url:" + entry.TargetPath,
                _ => Path.GetFullPath(entry.TargetPath)
            };
            lock (_running)
            {
                if (preventDuplicates && _running.Contains(key)) return;
                var start = entry.TargetType == LauncherTargetType.StoreApp
                    ? new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), $"shell:AppsFolder\\{entry.TargetPath}") { UseShellExecute = true }
                    : entry.TargetType == LauncherTargetType.WebsiteUrl
                    ? new ProcessStartInfo(entry.TargetPath) { UseShellExecute = true }
                    : new ProcessStartInfo(entry.TargetPath) { Arguments = entry.Arguments, UseShellExecute = true };
                if (!string.IsNullOrWhiteSpace(entry.WorkingDirectory)) start.WorkingDirectory = entry.WorkingDirectory;
                var process = Process.Start(start) ?? throw new InvalidOperationException($"Windows did not start {entry.Name}.");
                _running.Add(key);
                if (preventDuplicates)
                    _ = Task.Run(async () => { try { await process.WaitForExitAsync(); } catch { } finally { lock (_running) _running.Remove(key); process.Dispose(); } });
                else process.Dispose();
            }
        });
    }
}

public sealed record StoreAppChoice(string Name, string AppUserModelId)
{
    public override string ToString() => Name;
}

public static class StoreAppCatalog
{
    private const string AppsFolder = "shell:::{4234d49b-0245-4df3-b780-3893943456e1}";

    public static IReadOnlyList<StoreAppChoice> GetInstalledApps()
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application") ?? throw new InvalidOperationException("Windows Shell application listing is unavailable.");
        object? shell = null; object? folder = null; object? items = null;
        try
        {
            shell = Activator.CreateInstance(shellType) ?? throw new InvalidOperationException("Could not open Windows app list.");
            dynamic shellObject = shell;
            folder = shellObject.Namespace(AppsFolder);
            if (folder is null) throw new InvalidOperationException("Windows could not open the installed apps folder.");
            dynamic folderObject = folder;
            items = folderObject.Items();
            dynamic itemCollection = items;
            var count = (int)itemCollection.Count;
            var choices = new List<StoreAppChoice>();
            for (var i = 0; i < count; i++)
            {
                object? rawItem = null;
                try
                {
                    rawItem = itemCollection.Item(i);
                    dynamic item = rawItem;
                    string name = item.Name;
                    string appId = item.Path;
                    if (appId.Contains('!') && !string.IsNullOrWhiteSpace(name)) choices.Add(new StoreAppChoice(name, appId));
                }
                catch (Exception ex) when (ex is COMException or ArgumentOutOfRangeException or IndexOutOfRangeException or InvalidOperationException) { }
                finally { if (rawItem is not null && Marshal.IsComObject(rawItem)) Marshal.FinalReleaseComObject(rawItem); }
            }
            return choices.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
        finally
        {
            if (items is not null && Marshal.IsComObject(items)) Marshal.FinalReleaseComObject(items);
            if (folder is not null && Marshal.IsComObject(folder)) Marshal.FinalReleaseComObject(folder);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }

    public static bool IsInstalled(string appUserModelId) => GetInstalledApps().Any(a => string.Equals(a.AppUserModelId, appUserModelId, StringComparison.OrdinalIgnoreCase));
}
