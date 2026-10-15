using QuickMacro.Models;

namespace QuickMacro.Services;

public sealed class WorkspaceService(LauncherService launcher)
{
    public Task<IReadOnlyList<string>> LaunchAsync(Workspace workspace, IReadOnlyCollection<LauncherEntry> entries)
        => LaunchAsync(workspace, entries, createNewDesktop: true);

    public async Task<IReadOnlyList<string>> HandleShortcutAsync(Workspace workspace, IReadOnlyCollection<LauncherEntry> entries)
    {
        if (workspace.RepeatAction == WorkspaceRepeatAction.Launch)
            return await LaunchAsync(workspace, entries);

        var windows = new HashSet<IntPtr>();
        foreach (var item in workspace.Items)
        {
            var entry = item.Target ?? entries.FirstOrDefault(candidate => candidate.Id == item.EntryId);
            if (entry is null) continue;
            WindowActionService.RestoreHiddenWindowsForEntry(entry.Id);
            foreach (var handle in WindowActionService.FindMatchingWindows(entry)) windows.Add(handle);
        }

        if (windows.Count == 0)
            return await LaunchAsync(workspace, entries);

        if (workspace.RepeatAction == WorkspaceRepeatAction.MinimizeRestore)
        {
            if (windows.All(WindowActionService.IsMinimized))
                return await LaunchAsync(workspace, entries, createNewDesktop: false);
            foreach (var handle in windows) WindowActionService.MinimizeWindow(handle);
            return [];
        }

        if (workspace.RepeatAction == WorkspaceRepeatAction.CloseApps)
        {
            foreach (var handle in windows) WindowActionService.CloseWindow(handle);
            return [];
        }

        var workspaceDesktopIsActive = workspace.OpenOnNewDesktop && windows.Any(WindowActionService.IsOnCurrentVirtualDesktop);
        if (!workspaceDesktopIsActive)
            return ["Switch to this workspace’s desktop before pressing its shortcut to close that desktop."];

        foreach (var handle in windows) WindowActionService.CloseWindow(handle);
        await Task.Delay(700);
        WindowActionService.CloseCurrentVirtualDesktop();
        return [];
    }

    private async Task<IReadOnlyList<string>> LaunchAsync(Workspace workspace, IReadOnlyCollection<LauncherEntry> entries, bool createNewDesktop)
    {
        var errors = new List<string>();
        var usedWindows = new HashSet<IntPtr>();
        var windowsToPlace = new List<(IntPtr Handle, WorkspaceItem Item, string Name)>();

        if (workspace.OpenOnNewDesktop && createNewDesktop)
        {
            WindowActionService.CreateAndSwitchToNewVirtualDesktop();
            // Give Explorer time to create the desktop and finish switching before
            // app matching or launching starts.
            await Task.Delay(900);
        }

        foreach (var item in workspace.Items)
        {
            var entry = item.Target ?? entries.FirstOrDefault(candidate => candidate.Id == item.EntryId);
            if (entry is null) { errors.Add($"{workspace.Name}: an app in this workspace no longer exists."); continue; }
            try
            {
                WindowActionService.RestoreHiddenWindowsForEntry(entry.Id);
                var window = WindowActionService.FindMatchingWindows(entry).FirstOrDefault(handle =>
                    !usedWindows.Contains(handle) && (!workspace.OpenOnNewDesktop || WindowActionService.IsOnCurrentVirtualDesktop(handle)));
                if (window == IntPtr.Zero)
                {
                    await launcher.LaunchAsync(entry, preventDuplicates: false, forceLaunch: true);
                    window = await WaitForWindow(entry, usedWindows, TimeSpan.FromSeconds(25), workspace.OpenOnNewDesktop);
                }
                if (window == IntPtr.Zero) { errors.Add($"Could not find a window for {entry.Name} after launching it."); continue; }
                usedWindows.Add(window);
                windowsToPlace.Add((window, item, entry.Name));
            }
            catch (Exception ex) { errors.Add($"{entry.Name}: {ex.Message}"); }
        }

        // Launch all workspace members first. Some apps reposition or resize
        // themselves during startup, so arrange every visible frame in two passes
        // after all of them have finished opening.
        var placementErrors = new Dictionary<IntPtr, string>();
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var (handle, item, name) in windowsToPlace)
            {
                var error = WindowActionService.PlaceWindow(handle, item.X, item.Y, item.Width, item.Height);
                if (error is null) placementErrors.Remove(handle);
                else placementErrors[handle] = $"{name}: {error}";
            }
            if (pass == 0) await Task.Delay(450);
        }
        errors.AddRange(placementErrors.Values);

        // Put workspace windows above unrelated windows after apps have completed
        // their own startup and layout changes. Activate the first configured app
        // last so the workspace opens with a predictable foreground window.
        foreach (var (handle, _, _) in windowsToPlace.AsEnumerable().Reverse())
            WindowActionService.RaiseWindow(handle);
        if (windowsToPlace.Count > 0)
            WindowActionService.ActivateWindow(windowsToPlace[0].Handle);

        return errors;
    }

    private static async Task<IntPtr> WaitForWindow(LauncherEntry entry, HashSet<IntPtr> used, TimeSpan timeout, bool currentDesktopOnly)
    {
        var until = DateTime.UtcNow + timeout;
        WindowActionService.WindowSnapshot? previous = null;
        var stableSince = DateTime.MinValue;
        while (DateTime.UtcNow < until)
        {
            var current = WindowActionService.FindMatchingWindowSnapshots(entry).FirstOrDefault(snapshot => !used.Contains(snapshot.Handle) && !snapshot.IsMinimized && snapshot.Bounds.Width >= 120 && snapshot.Bounds.Height >= 90 && (!currentDesktopOnly || WindowActionService.IsOnCurrentVirtualDesktop(snapshot.Handle)));
            if (current is not null)
            {
                if (previous is not null && current.Handle == previous.Handle && current.Bounds == previous.Bounds && current.Title == previous.Title)
                {
                    if (DateTime.UtcNow - stableSince >= TimeSpan.FromMilliseconds(1000)) return current.Handle;
                }
                else { previous = current; stableSince = DateTime.UtcNow; }
            }
            else { previous = null; stableSince = DateTime.MinValue; }
            await Task.Delay(300);
        }
        return IntPtr.Zero;
    }
}
