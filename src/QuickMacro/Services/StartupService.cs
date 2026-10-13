using Microsoft.Win32;

namespace QuickMacro.Services;

public sealed class StartupService : IDisposable
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QuickMacro";
    public bool IsEnabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue(ValueName) is string; }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true) ?? throw new InvalidOperationException("Windows startup settings are unavailable.");
        if (enabled)
        {
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Could not determine the QuickMacro executable path.");
            key.SetValue(ValueName, $"\"{exe}\" --minimized", RegistryValueKind.String);
        }
        else key.DeleteValue(ValueName, false);
    }

    public void Dispose() { }
}
