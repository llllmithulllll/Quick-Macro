using System.Runtime.InteropServices;
using QuickMacro.Models;

namespace QuickMacro.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly Form _owner;
    private readonly Dictionary<int, Guid> _registrations = [];
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001, ModControl = 0x0002, ModShift = 0x0004, ModWin = 0x0008, ModNoRepeat = 0x4000;

    public GlobalHotkeyService(Form owner) => _owner = owner;
    public event Action<Guid>? Triggered;
    public event Action<string>? RegistrationFailed;

    public void Replace(IEnumerable<LauncherEntry> entries, bool globallyEnabled)
    {
        Clear();
        if (!globallyEnabled) return;
        var id = 0x5100;
        foreach (var entry in entries.Where(e => e.Enabled && e.Shortcut is not null))
        {
            var error = ShortcutRules.Validate(entry.Shortcut);
            if (error is not null) { RegistrationFailed?.Invoke($"{entry.Name}: {error}"); continue; }
            var shortcut = entry.Shortcut!;
            uint mods = ModNoRepeat;
            if (shortcut.Alt) mods |= ModAlt;
            if (shortcut.Control) mods |= ModControl;
            if (shortcut.Shift) mods |= ModShift;
            if (shortcut.Windows) mods |= ModWin;
            if (!RegisterHotKey(_owner.Handle, id, mods, (uint)shortcut.Key))
                RegistrationFailed?.Invoke($"{entry.Name}: {shortcut} is already in use or unavailable. Choose a different shortcut.");
            else _registrations[id] = entry.Id;
            id++;
        }
    }

    public bool HandleMessage(ref Message message)
    {
        if (message.Msg != WmHotkey || !_registrations.TryGetValue(message.WParam.ToInt32(), out var entryId)) return false;
        Triggered?.Invoke(entryId);
        return true;
    }

    public void Clear()
    {
        foreach (var id in _registrations.Keys) UnregisterHotKey(_owner.Handle, id);
        _registrations.Clear();
    }
    public void Dispose() => Clear();

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
