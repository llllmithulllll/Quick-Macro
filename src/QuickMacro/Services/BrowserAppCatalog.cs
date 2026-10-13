using System.Runtime.InteropServices;

namespace QuickMacro.Services;

public sealed record BrowserAppChoice(string Name, string ShortcutPath, string BrowserName)
{
    public override string ToString() => $"{Name}  ·  {BrowserName}";
}

public static class BrowserAppCatalog
{
    public static IReadOnlyList<BrowserAppChoice> GetInstalledApps()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);

        var shortcuts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories)) shortcuts.Add(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        }

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null) return [];
        object? shell = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell is null) return [];
            dynamic shellObject = shell;
            var choices = new List<BrowserAppChoice>();
            foreach (var shortcutPath in shortcuts)
            {
                object? shortcut = null;
                try
                {
                    shortcut = shellObject.CreateShortcut(shortcutPath);
                    dynamic link = shortcut;
                    string target = link.TargetPath;
                    string arguments = link.Arguments;
                    var browserName = FindBrowserName(target);
                    if (browserName is null) continue;

                    var parent = Path.GetFileName(Path.GetDirectoryName(shortcutPath)) ?? string.Empty;
                    var isAppsFolder = parent.Contains("Apps", StringComparison.OrdinalIgnoreCase);
                    var hasAppArgument = arguments.Contains("--app", StringComparison.OrdinalIgnoreCase)
                        || arguments.Contains("--app-id", StringComparison.OrdinalIgnoreCase)
                        || arguments.Contains("--app-url", StringComparison.OrdinalIgnoreCase);
                    if (!isAppsFolder && !hasAppArgument) continue;
                    choices.Add(new BrowserAppChoice(Path.GetFileNameWithoutExtension(shortcutPath), shortcutPath, browserName));
                }
                catch (Exception ex) when (ex is COMException or IOException or InvalidOperationException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
                finally { if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut); }
            }
            return choices.OrderBy(choice => choice.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }
        finally { if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell); }
    }

    private static string? FindBrowserName(string targetPath)
    {
        var executable = Path.GetFileName(targetPath);
        if (executable.Equals("brave.exe", StringComparison.OrdinalIgnoreCase) || executable.Equals("brave_proxy.exe", StringComparison.OrdinalIgnoreCase)) return "Brave";
        if (executable.Equals("chrome.exe", StringComparison.OrdinalIgnoreCase) || executable.Equals("chrome_proxy.exe", StringComparison.OrdinalIgnoreCase)) return "Chrome";
        if (executable.Equals("msedge.exe", StringComparison.OrdinalIgnoreCase) || executable.Equals("msedge_proxy.exe", StringComparison.OrdinalIgnoreCase)) return "Microsoft Edge";
        return null;
    }
}
