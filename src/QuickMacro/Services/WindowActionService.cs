using System.Diagnostics;
using System.Runtime.InteropServices;
using QuickMacro.Models;

namespace QuickMacro.Services;

public static class WindowActionService
{
    public sealed record HiddenWindow(Guid EntryId, string EntryName, string WindowTitle, IntPtr Handle);
    public sealed record WindowSnapshot(IntPtr Handle, Rectangle Bounds, string Title, bool IsMinimized);

    public static IReadOnlyList<IntPtr> FindMatchingWindows(LauncherEntry entry)
        => FindMatchingWindowSnapshots(entry).Select(snapshot => snapshot.Handle).ToArray();

    public static IReadOnlyList<WindowSnapshot> FindMatchingWindowSnapshots(LauncherEntry entry)
    {
        if (entry.TargetType == LauncherTargetType.WebsiteUrl) return [];
        var executablePath = entry.TargetType is LauncherTargetType.FilePath or LauncherTargetType.BrowserAppShortcut ? ResolveExecutablePath(entry.TargetPath) : null;
        var shortcutAppId = entry.TargetType == LauncherTargetType.BrowserAppShortcut ? GetShortcutAppUserModelId(entry.TargetPath) : null;
        var windows = new List<WindowSnapshot>();
        EnumWindows((handle, _) =>
        {
            if (IsWindowVisible(handle) && (MatchesTarget(handle, entry, executablePath, shortcutAppId)
                || entry.TargetType == LauncherTargetType.StoreApp && MatchesStoreAppChild(handle, entry.TargetPath)
                || entry.TargetType == LauncherTargetType.BrowserAppShortcut && !string.IsNullOrWhiteSpace(shortcutAppId) && MatchesStoreAppChild(handle, shortcutAppId))
                && GetWindowRect(handle, out var bounds))
                windows.Add(new WindowSnapshot(handle, GetVisibleFrameBounds(handle, bounds), GetWindowCaption(handle), IsIconic(handle)));
            return true;
        }, IntPtr.Zero);
        return windows.GroupBy(window => window.Handle).Select(group => group.First())
            .OrderByDescending(window => !string.IsNullOrWhiteSpace(window.Title))
            .ThenByDescending(window => window.Bounds.Width * (long)window.Bounds.Height)
            .ToArray();
    }

    public static string? PlaceWindow(IntPtr handle, int xPercent, int yPercent, int widthPercent, int heightPercent)
    {
        if (!IsWindow(handle)) return "The app window closed before it could be arranged.";
        var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromHandle(handle).WorkingArea;
        var width = Math.Max(240, (int)Math.Round(area.Width * Math.Clamp(widthPercent, 1, 100) / 100d));
        var height = Math.Max(180, (int)Math.Round(area.Height * Math.Clamp(heightPercent, 1, 100) / 100d));
        width = Math.Min(width, area.Width); height = Math.Min(height, area.Height);
        var x = area.Left + Math.Min((int)Math.Round(area.Width * Math.Clamp(xPercent, 0, 99) / 100d), area.Width - width);
        var y = area.Top + Math.Min((int)Math.Round(area.Height * Math.Clamp(yPercent, 0, 99) / 100d), area.Height - height);
        ShowWindow(handle, SwRestore);
        GetWindowRect(handle, out var outerBefore);
        var visibleBefore = GetVisibleFrameBounds(handle, outerBefore);
        // GetWindowRect includes invisible resize borders on modern Windows.
        // Compensate so the visible frame aligns to the canvas cell and screen edge.
        var leftInset = Math.Max(0, visibleBefore.Left - outerBefore.Left);
        var topInset = Math.Max(0, visibleBefore.Top - outerBefore.Top);
        var rightInset = Math.Max(0, outerBefore.Right - visibleBefore.Right);
        var bottomInset = Math.Max(0, outerBefore.Bottom - visibleBefore.Bottom);
        var setX = x - leftInset; var setY = y - topInset;
        var setWidth = width + leftInset + rightInset; var setHeight = height + topInset + bottomInset;
        if (!SetWindowPos(handle, IntPtr.Zero, setX, setY, setWidth, setHeight, 0x0014))
        {
            var error = Marshal.GetLastWin32Error();
            return $"Windows could not move or resize this app (error {error}). Try running QuickMacro with the same permission level as the app.";
        }
        if (GetWindowRect(handle, out var actual))
        {
            var visibleActual = GetVisibleFrameBounds(handle, actual);
            // Many apps (Discord included) enforce a minimum or fixed window size.
            // Keep a window in its workspace when it accepts the position, even if
            // its own sizing rules reject our requested dimensions.
            if (Math.Abs(visibleActual.Left - x) > 12 || Math.Abs(visibleActual.Top - y) > 12)
                return $"The app did not accept the requested window position. Windows reports its visible frame at {visibleActual.Left}, {visibleActual.Top} instead of {x}, {y}. Try running QuickMacro with the same permission level as the app.";
        }
        return null;
    }

    /// <summary>Raises a placed workspace window without changing its layout rectangle.</summary>
    public static void RaiseWindow(IntPtr handle)
    {
        if (!IsWindow(handle)) return;
        ShowWindowAsync(handle, SwRestore);
        SetWindowPos(handle, HwndTop, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow | SwpNoActivate);
        BringWindowToTop(handle);
    }

    /// <summary>Creates and switches to a Windows virtual desktop using the built-in Win+Ctrl+D shortcut.</summary>
    public static void CreateAndSwitchToNewVirtualDesktop()
    {
        const byte vkLWin = 0x5B, vkControl = 0x11, vkD = 0x44;
        keybd_event(vkLWin, 0, 0, UIntPtr.Zero);
        keybd_event(vkControl, 0, 0, UIntPtr.Zero);
        keybd_event(vkD, 0, 0, UIntPtr.Zero);
        keybd_event(vkD, 0, KeyEventKeyUp, UIntPtr.Zero);
        keybd_event(vkControl, 0, KeyEventKeyUp, UIntPtr.Zero);
        keybd_event(vkLWin, 0, KeyEventKeyUp, UIntPtr.Zero);
    }

    public static void ActivateWindow(IntPtr handle)
    {
        if (!IsWindow(handle)) return;
        ShowWindowAsync(handle, SwRestore);
        BringWindowToTop(handle);
        SetForegroundWindow(handle);
    }

    public static bool IsMinimized(IntPtr handle) => IsWindow(handle) && IsIconic(handle);

    public static void MinimizeWindow(IntPtr handle)
    {
        if (IsWindow(handle) && !ShowWindowAsync(handle, SwMinimize))
            PostMessage(handle, WmSysCommand, ScMinimize, IntPtr.Zero);
    }

    public static void CloseWindow(IntPtr handle)
    {
        if (IsWindow(handle)) PostMessage(handle, WmClose, IntPtr.Zero, IntPtr.Zero);
    }

    public static void CloseCurrentVirtualDesktop()
    {
        const byte vkLWin = 0x5B, vkControl = 0x11, vkF4 = 0x73;
        keybd_event(vkLWin, 0, 0, UIntPtr.Zero);
        keybd_event(vkControl, 0, 0, UIntPtr.Zero);
        keybd_event(vkF4, 0, 0, UIntPtr.Zero);
        keybd_event(vkF4, 0, KeyEventKeyUp, UIntPtr.Zero);
        keybd_event(vkControl, 0, KeyEventKeyUp, UIntPtr.Zero);
        keybd_event(vkLWin, 0, KeyEventKeyUp, UIntPtr.Zero);
    }

    public static bool IsOnCurrentVirtualDesktop(IntPtr handle)
    {
        IVirtualDesktopManager? manager = null;
        try
        {
            manager = (IVirtualDesktopManager)new VirtualDesktopManager();
            return manager.IsWindowOnCurrentVirtualDesktop(handle, out var onCurrentDesktop) >= 0 && onCurrentDesktop;
        }
        catch { return true; }
        finally { if (manager is not null) Marshal.ReleaseComObject(manager); }
    }

    private static Rectangle GetVisibleFrameBounds(IntPtr handle, NativeRect fallback)
    {
        var result = DwmGetWindowAttribute(handle, 9, out var visible, Marshal.SizeOf<NativeRect>());
        return result == 0 && visible.Right > visible.Left && visible.Bottom > visible.Top
            ? Rectangle.FromLTRB(visible.Left, visible.Top, visible.Right, visible.Bottom)
            : Rectangle.FromLTRB(fallback.Left, fallback.Top, fallback.Right, fallback.Bottom);
    }

    private const int SwMinimize = 6;
    private const int SwHide = 0;
    private const int SwRestore = 9;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTop = IntPtr.Zero;
    private const uint WmSysCommand = 0x0112;
    private static readonly IntPtr ScMinimize = new(0xF020);
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint WmClose = 0x0010;
    private static readonly object HiddenWindowsLock = new();
    private static readonly Dictionary<Guid, List<HiddenWindow>> HiddenWindows = [];
    private static readonly Guid PropertyStoreId = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    private static readonly PropertyKey AppUserModelIdKey = new(new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

    /// <summary>Applies a configured action to visible top-level windows. Returns true if a matching window was found.</summary>
    public static bool TryApply(LauncherEntry entry)
    {
        var previouslyHidden = TakeHiddenWindows(entry.Id);
        previouslyHidden.RemoveAll(window => !IsWindow(window.Handle));
        if (entry.ShortcutAction == ShortcutAction.Minimize && previouslyHidden.Count > 0)
        {
            RestoreWindows(previouslyHidden, activateFirst: true);
            return true;
        }
        if (entry.ShortcutAction == ShortcutAction.Launch || entry.TargetType == LauncherTargetType.WebsiteUrl)
        {
            RestoreWindows(previouslyHidden, activateFirst: false);
            return false;
        }

        var executablePath = entry.TargetType is LauncherTargetType.FilePath or LauncherTargetType.BrowserAppShortcut ? ResolveExecutablePath(entry.TargetPath) : null;
        var shortcutAppId = entry.TargetType == LauncherTargetType.BrowserAppShortcut ? GetShortcutAppUserModelId(entry.TargetPath) : null;
        var windows = previouslyHidden.Where(window => IsWindow(window.Handle)).Select(window => window.Handle).ToList();
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle)) return true;
            if (MatchesTarget(handle, entry, executablePath, shortcutAppId)) windows.Add(handle);
            else if (entry.TargetType == LauncherTargetType.StoreApp && MatchesStoreAppChild(handle, entry.TargetPath)) windows.Add(handle);
            else if (entry.TargetType == LauncherTargetType.BrowserAppShortcut && !string.IsNullOrWhiteSpace(shortcutAppId) && MatchesStoreAppChild(handle, shortcutAppId)) windows.Add(handle);
            return true;
        }, IntPtr.Zero);

        // Enumerable.All returns true for an empty collection. With no running
        // window, the caller must continue to launch the app instead of trying
        // to restore windows[0].
        if (windows.Count == 0) return false;

        if (entry.ShortcutAction == ShortcutAction.Minimize)
        {
            // Treat the same shortcut as a toggle: minimize visible instances, and
            // restore/foreground one when every matching instance is already minimized.
            if (windows.All(IsIconic))
            {
                foreach (var window in windows) ShowWindowAsync(window, SwRestore);
                SetForegroundWindow(windows[0]);
            }
            else
            {
                if (entry.MinimizeToSystemTray)
                {
                    HideWindowsInTray(entry, windows);
                }
                else foreach (var window in windows)
                {
                    // Some applications ignore an asynchronous show-state request.
                    // Queue the standard system minimize command as a fallback too.
                    if (!ShowWindowAsync(window, SwMinimize)) PostMessage(window, WmSysCommand, ScMinimize, IntPtr.Zero);
                }
            }
        }
        else if (entry.ShortcutAction == ShortcutAction.Close)
            foreach (var window in windows) PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
        // A matching window means the configured action was handled. Do not launch a
        // second copy just because ShowWindowAsync reports its prior visibility state.
        return windows.Count > 0;
    }

    public static IReadOnlyList<HiddenWindow> GetHiddenWindows()
    {
        lock (HiddenWindowsLock)
        {
            foreach (var entry in HiddenWindows.ToArray())
            {
                entry.Value.RemoveAll(window => !IsWindow(window.Handle) || IsWindowVisible(window.Handle));
                if (entry.Value.Count == 0) HiddenWindows.Remove(entry.Key);
            }
            return HiddenWindows.Values.SelectMany(windows => windows).ToArray();
        }
    }

    public static void RestoreHiddenWindow(IntPtr handle)
    {
        HiddenWindow? hidden = null;
        lock (HiddenWindowsLock)
        {
            foreach (var entry in HiddenWindows.ToArray())
            {
                var index = entry.Value.FindIndex(window => window.Handle == handle);
                if (index < 0) continue;
                hidden = entry.Value[index];
                entry.Value.RemoveAt(index);
                if (entry.Value.Count == 0) HiddenWindows.Remove(entry.Key);
                break;
            }
        }

        if (hidden is not null && IsWindow(hidden.Handle))
        {
            ShowWindowAsync(hidden.Handle, SwRestore);
            SetForegroundWindow(hidden.Handle);
        }
    }

    public static void RestoreAllHiddenWindows()
    {
        List<HiddenWindow> hidden;
        lock (HiddenWindowsLock)
        {
            hidden = HiddenWindows.Values.SelectMany(windows => windows).ToList();
            HiddenWindows.Clear();
        }
        RestoreWindows(hidden, activateFirst: true);
    }

    public static void RestoreHiddenWindowsForEntry(Guid entryId)
    {
        var hidden = TakeHiddenWindows(entryId);
        RestoreWindows(hidden, activateFirst: false);
    }

    private static List<HiddenWindow> TakeHiddenWindows(Guid entryId)
    {
        lock (HiddenWindowsLock)
        {
            if (!HiddenWindows.Remove(entryId, out var windows)) return [];
            return windows;
        }
    }

    private static void HideWindowsInTray(LauncherEntry entry, IEnumerable<IntPtr> windows)
    {
        var hidden = windows.Where(IsWindow).Distinct()
            .Select(handle => new HiddenWindow(entry.Id, entry.Name, GetWindowCaption(handle), handle))
            .ToList();
        foreach (var window in hidden) ShowWindowAsync(window.Handle, SwHide);
        if (hidden.Count == 0) return;

        lock (HiddenWindowsLock)
        {
            if (!HiddenWindows.TryGetValue(entry.Id, out var current)) HiddenWindows[entry.Id] = current = [];
            foreach (var window in hidden)
                if (current.All(existing => existing.Handle != window.Handle)) current.Add(window);
        }
    }

    private static void RestoreWindows(IEnumerable<HiddenWindow> windows, bool activateFirst)
    {
        var ordered = windows.Where(window => IsWindow(window.Handle)).ToArray();
        foreach (var window in ordered) ShowWindowAsync(window.Handle, SwRestore);
        if (activateFirst && ordered.Length > 0) SetForegroundWindow(ordered[0].Handle);
    }

    private static bool MatchesStoreAppChild(IntPtr topLevelWindow, string appUserModelId)
    {
        var found = false;
        EnumChildWindows(topLevelWindow, (child, _) =>
        {
            if (!string.Equals(GetWindowAppUserModelId(child), appUserModelId, StringComparison.OrdinalIgnoreCase)) return true;
            found = true;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static bool MatchesTarget(IntPtr window, LauncherEntry entry, string? executablePath, string? shortcutAppId)
    {
        if (entry.TargetType == LauncherTargetType.StoreApp)
        {
            var appId = GetWindowAppUserModelId(window);
            // Store/UWP apps can expose the app ID on a child HWND instead of the
            // ApplicationFrameWindow. Some builds expose neither, but retain the
            // configured app's top-level caption, which is also a useful fallback.
            return string.Equals(appId, entry.TargetPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetWindowCaption(window), entry.Name, StringComparison.CurrentCultureIgnoreCase);
        }
        if (entry.TargetType == LauncherTargetType.BrowserAppShortcut)
        {
            var windowAppId = GetWindowAppUserModelId(window);
            if (!string.IsNullOrWhiteSpace(shortcutAppId) && string.Equals(windowAppId, shortcutAppId, StringComparison.OrdinalIgnoreCase)) return true;
            GetWindowThreadProcessId(window, out var browserProcessId);
            if (browserProcessId == 0 || !IsBrowserProcessForShortcut(browserProcessId, executablePath)) return false;
            var caption = GetWindowCaption(window);
            return string.Equals(caption, entry.Name, StringComparison.CurrentCultureIgnoreCase)
                || caption.Contains(entry.Name, StringComparison.CurrentCultureIgnoreCase);
        }

        GetWindowThreadProcessId(window, out var processId);
        if (processId == 0) return false;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            var runningPath = GetProcessImagePath(processId) ?? process.MainModule?.FileName;
            if (runningPath is not null && executablePath is not null && string.Equals(Path.GetFullPath(runningPath), executablePath, StringComparison.OrdinalIgnoreCase)) return true;

            // Shell launches, aliases, and protected processes can make Windows hide
            // the full image path. The executable name remains available for many of
            // those processes, so use it as a fallback for standard file targets.
            var expectedName = Path.GetFileNameWithoutExtension(executablePath);
            return !string.IsNullOrWhiteSpace(expectedName) && string.Equals(process.ProcessName, expectedName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsBrowserProcessForShortcut(uint processId, string? shortcutExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(shortcutExecutablePath)) return false;
        try
        {
            var processPath = GetProcessImagePath(processId);
            if (processPath is null) return false;
            if (string.Equals(Path.GetFullPath(processPath), shortcutExecutablePath, StringComparison.OrdinalIgnoreCase)) return true;
            var targetName = Path.GetFileName(shortcutExecutablePath);
            var processName = Path.GetFileName(processPath);
            var proxyTarget = targetName.Equals("chrome_proxy.exe", StringComparison.OrdinalIgnoreCase)
                || targetName.Equals("msedge_proxy.exe", StringComparison.OrdinalIgnoreCase);
            var browserExecutable = processName.Equals("brave.exe", StringComparison.OrdinalIgnoreCase)
                || processName.Equals("chrome.exe", StringComparison.OrdinalIgnoreCase)
                || processName.Equals("msedge.exe", StringComparison.OrdinalIgnoreCase);
            return proxyTarget && browserExecutable && string.Equals(Path.GetDirectoryName(processPath), Path.GetDirectoryName(shortcutExecutablePath), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static string? GetProcessImagePath(uint processId)
    {
        using var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process.IsInvalid) return null;
        var buffer = new System.Text.StringBuilder(32768);
        var size = (uint)buffer.Capacity;
        return QueryFullProcessImageName(process, 0, buffer, ref size) ? buffer.ToString() : null;
    }

    private static string? ResolveExecutablePath(string targetPath)
    {
        try
        {
            if (!string.Equals(Path.GetExtension(targetPath), ".lnk", StringComparison.OrdinalIgnoreCase)) return Path.GetFullPath(Environment.ExpandEnvironmentVariables(targetPath));
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return null;
            object? shell = null; object? shortcut = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                if (shell is null) return null;
                dynamic shellObject = shell;
                shortcut = shellObject.CreateShortcut(targetPath);
                dynamic shortcutObject = shortcut;
                string path = shortcutObject.TargetPath;
                return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
            }
            finally
            {
                if (shortcut is not null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
                if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
            }
        }
        catch { return null; }
    }

    private static string? GetWindowAppUserModelId(IntPtr window)
    {
        IPropertyStore? store = null;
        PropVariant value = default;
        try
        {
            var propertyStoreId = PropertyStoreId;
            if (SHGetPropertyStoreForWindow(window, ref propertyStoreId, out store) < 0 || store is null) return null;
            var key = AppUserModelIdKey;
            if (store.GetValue(ref key, out value) < 0 || value.Type != 31) return null; // VT_LPWSTR
            return Marshal.PtrToStringUni(value.Pointer);
        }
        catch { return null; }
        finally
        {
            PropVariantClear(ref value);
            if (store is not null) Marshal.ReleaseComObject(store);
        }
    }

    private static string? GetShortcutAppUserModelId(string shortcutPath)
    {
        IPropertyStore? store = null;
        PropVariant value = default;
        try
        {
            var iid = PropertyStoreId;
            if (SHGetPropertyStoreFromParsingName(shortcutPath, IntPtr.Zero, 0, ref iid, out store) < 0 || store is null) return null;
            var key = AppUserModelIdKey;
            if (store.GetValue(ref key, out value) < 0 || value.Type != 31) return null;
            return Marshal.PtrToStringUni(value.Pointer);
        }
        catch { return null; }
        finally
        {
            PropVariantClear(ref value);
            if (store is not null) Marshal.ReleaseComObject(store);
        }
    }

    private static string GetWindowCaption(IntPtr window)
    {
        var length = GetWindowTextLength(window);
        if (length <= 0) return string.Empty;
        var caption = new System.Text.StringBuilder(length + 1);
        return GetWindowText(window, caption, caption.Capacity) > 0 ? caption.ToString() : string.Empty;
    }

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsCallback callback, IntPtr lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int maxCount);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out NativeRect value, int size);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, System.Text.StringBuilder imageName, ref uint size);
    [DllImport("shell32.dll", PreserveSig = true)] private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid, out IPropertyStore propertyStore);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)] private static extern int SHGetPropertyStoreFromParsingName(string path, IntPtr bindContext, uint flags, ref Guid iid, out IPropertyStore propertyStore);
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant propVariant);

    [ComImport, Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManager
    {
        [PreserveSig] int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);
    }

    [ComImport, Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A"), ClassInterface(ClassInterfaceType.None)]
    private class VirtualDesktopManager { }

    private delegate bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    private sealed class SafeProcessHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeProcessHandle() : base(true) { }
        protected override bool ReleaseHandle() => CloseHandle(handle);
    }

    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint propertyCount);
        [PreserveSig] int GetAt(uint propertyIndex, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;
        public PropertyKey(Guid formatId, uint propertyId) { FormatId = formatId; PropertyId = propertyId; }
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public IntPtr Pointer;
    }
}
