using System.Runtime.InteropServices;
using QuickMacro.Models;

namespace QuickMacro.Services;

public sealed record InstalledAppChoice(string Name, string TargetPath, LauncherTargetType TargetType, string Arguments = "", string WorkingDirectory = "")
{
    public string DisplayName => $"{Name}   ·   {LauncherTargetTypeLabels.Get(TargetType)}";
    public override string ToString() => DisplayName;
}

public static class InstalledAppCatalog
{
    public static IReadOnlyList<InstalledAppChoice> GetInstalledApps()
    {
        var choices = new Dictionary<string, InstalledAppChoice>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<BrowserAppChoice> browserApps;
        try { browserApps = BrowserAppCatalog.GetInstalledApps(); }
        catch { browserApps = []; }
        var browserShortcuts = browserApps.GroupBy(app => app.ShortcutPath, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
        var links = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            try { foreach (var path in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories)) links.Add(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        }

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is not null)
        {
            object? shell = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                if (shell is not null)
                {
                    dynamic shellObject = shell;
                    foreach (var path in links)
                    {
                        object? shortcut = null;
                        try
                        {
                            shortcut = shellObject.CreateShortcut(path);
                            dynamic link = shortcut;
                            string target = link.TargetPath;
                            string arguments = link.Arguments;
                            if (string.IsNullOrWhiteSpace(target)) continue;
                            if (browserShortcuts.TryGetValue(path, out var browser))
                                choices[path] = new InstalledAppChoice(browser.Name, path, LauncherTargetType.BrowserAppShortcut);
                            else if (TryReadAppUserModelId(arguments, out var appUserModelId))
                                choices["store:" + appUserModelId] = new InstalledAppChoice(Path.GetFileNameWithoutExtension(path), appUserModelId, LauncherTargetType.StoreApp);
                            else
                            {
                                var name = Path.GetFileNameWithoutExtension(path);
                                choices[path] = new InstalledAppChoice(name, path, LauncherTargetType.FilePath);
                            }
                        }
                        catch (Exception ex) when (ex is COMException or IOException or InvalidOperationException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
                        finally { if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut); }
                    }
                }
            }
            finally { if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell); }
        }

        try
        {
            foreach (var app in StoreAppCatalog.GetInstalledApps())
                choices["store:" + app.AppUserModelId] = new InstalledAppChoice(app.Name, app.AppUserModelId, LauncherTargetType.StoreApp);
        }
        catch { /* Keep the Start Menu/Desktop results if the Store catalog is unavailable. */ }
        return choices.Values.OrderBy(choice => choice.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static bool TryReadAppUserModelId(string arguments, out string appUserModelId)
    {
        const string prefix = "shell:AppsFolder\\";
        appUserModelId = "";
        var start = arguments.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return false;
        start += prefix.Length;
        var end = arguments.IndexOfAny(['"', '\'', ' ', '\t'], start);
        appUserModelId = (end < 0 ? arguments[start..] : arguments[start..end]).TrimEnd('"', '\'');
        return appUserModelId.Contains('!') && appUserModelId.Length < 300;
    }
}
